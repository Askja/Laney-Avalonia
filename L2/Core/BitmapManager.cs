using Avalonia;
using Avalonia.Media.Imaging;
using ELOR.Laney.Core.Network;
using ELOR.Laney.Helpers;
using Serilog;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ELOR.Laney.Core {
    public enum BitmapCacheKind {
        Default = 0,
        Avatar = 1,
        Attachment = 2,
        E2E = 3,
        Background = 4
    }

    public readonly struct BitmapCacheSnapshot {
        public int EntryCount { get; }
        public int LoadingCount { get; }
        public int DefaultCount { get; }
        public int AvatarCount { get; }
        public int AttachmentCount { get; }
        public int E2ECount { get; }
        public int BackgroundCount { get; }
        public long SizeBytes { get; }
        public long LimitBytes { get; }

        public BitmapCacheSnapshot(int entryCount, int loadingCount, int defaultCount, int avatarCount, int attachmentCount, int e2ECount, int backgroundCount, long sizeBytes, long limitBytes) {
            EntryCount = entryCount;
            LoadingCount = loadingCount;
            DefaultCount = defaultCount;
            AvatarCount = avatarCount;
            AttachmentCount = attachmentCount;
            E2ECount = e2ECount;
            BackgroundCount = backgroundCount;
            SizeBytes = sizeBytes;
            LimitBytes = limitBytes;
        }
    }

    public static class BitmapManager {
        private static readonly TimeSpan ExpirationScanInterval = TimeSpan.FromMinutes(1);

        private static readonly object cacheLock = new object();
        private static readonly Dictionary<string, CacheEntry> cachedImages = new Dictionary<string, CacheEntry>();
        private static readonly LinkedList<string> lruKeys = new LinkedList<string>();
        private static readonly ConcurrentDictionary<string, Lazy<Task<Bitmap>>> nowLoading = new ConcurrentDictionary<string, Lazy<Task<Bitmap>>>();

        private static long cachedBytes = 0;
        private static DateTime lastExpirationScanUtc = DateTime.MinValue;
        private static DateTime lastPressureTrimUtc = DateTime.MinValue;

        public static void ClearCachedImages() {
            long ramBefore = System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64;
            List<Bitmap> bitmapsToDispose = new List<Bitmap>();

            lock (cacheLock) {
                foreach (var cacheEntry in cachedImages.Values) {
                    bitmapsToDispose.Add(cacheEntry.Bitmap);
                }

                cachedImages.Clear();
                lruKeys.Clear();
                cachedBytes = 0;
            }

            foreach (var bitmap in bitmapsToDispose) {
                bitmap.Dispose();
            }

            long ramAfter = System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64;

#if !MAC
            Log.Information(
                "ClearCachedImages: RAM usage before cleaning is {Before} Mb, after cleaning is {After} Mb.",
                Math.Round((double)ramBefore / 1048576, 1),
                Math.Round((double)ramAfter / 1048576, 1));
#endif
        }

        public static BitmapCacheSnapshot GetCacheSnapshot() {
            lock (cacheLock) {
                int defaultCount = 0;
                int avatarCount = 0;
                int attachmentCount = 0;
                int e2ECount = 0;
                int backgroundCount = 0;

                foreach (var cacheEntry in cachedImages.Values) {
                    switch (cacheEntry.Kind) {
                        case BitmapCacheKind.Avatar:
                            avatarCount++;
                            break;
                        case BitmapCacheKind.Attachment:
                            attachmentCount++;
                            break;
                        case BitmapCacheKind.E2E:
                            e2ECount++;
                            break;
                        case BitmapCacheKind.Background:
                            backgroundCount++;
                            break;
                        default:
                            defaultCount++;
                            break;
                    }
                }

                return new BitmapCacheSnapshot(
                    cachedImages.Count,
                    nowLoading.Count,
                    defaultCount,
                    avatarCount,
                    attachmentCount,
                    e2ECount,
                    backgroundCount,
                    cachedBytes,
                    GetCacheLimitBytes());
            }
        }

        public static bool TrimForMemoryPressure(TimeSpan? minInterval = null) {
            if (!MediaMemoryGovernor.IsProcessMemoryPressureHigh()) return false;

            TimeSpan throttle = minInterval ?? TimeSpan.FromSeconds(5);
            DateTime utcNow = DateTime.UtcNow;
            List<Bitmap> bitmapsToDispose = new List<Bitmap>();

            lock (cacheLock) {
                if (utcNow - lastPressureTrimUtc < throttle) return false;

                lastPressureTrimUtc = utcNow;
                TrimExpiredEntries(bitmapsToDispose, utcNow);
                TrimCache(bitmapsToDispose, GetCacheLimitBytes());
                TrimForProcessPressure(bitmapsToDispose);
            }

            foreach (var bitmap in bitmapsToDispose) {
                bitmap.Dispose();
            }

            if (bitmapsToDispose.Count > 0 || MediaMemoryGovernor.IsProcessMemoryPressureHigh()) {
                GC.Collect(2, GCCollectionMode.Optimized, false, false);
            }

            return bitmapsToDispose.Count > 0;
        }

        public static Task<Bitmap> GetBitmapAsync(Uri source, double decodeWidth, double decodeHeight, BitmapCacheKind cacheKind, CancellationToken cancellationToken = default) {
            return GetBitmapAsync(source, decodeWidth, decodeHeight, cancellationToken, cacheKind);
        }

        public static async Task<Bitmap> GetBitmapAsync(Uri source, double decodeWidth = 0, double decodeHeight = 0, CancellationToken cancellationToken = default, BitmapCacheKind cacheKind = BitmapCacheKind.Default) {
            if (source == null) return null;

            ImageRequest request = ImageRequest.Create(source, decodeWidth, decodeHeight, cacheKind);
            cancellationToken.ThrowIfCancellationRequested();

            if (source.Scheme == "avares") {
                return await LoadAssetBitmapAsync(request, cancellationToken);
            }

            if (TryGetCachedBitmap(request.Key, out Bitmap cachedBitmap)) {
                return cachedBitmap;
            }

            Lazy<Task<Bitmap>> lazyLoad = nowLoading.GetOrAdd(
                request.Key,
                _ => new Lazy<Task<Bitmap>>(() => LoadAndCacheBitmapAsync(request), LazyThreadSafetyMode.ExecutionAndPublication));

            Task<Bitmap> loadTask = lazyLoad.Value;
            _ = loadTask.ContinueWith(_ => nowLoading.TryRemove(request.Key, out Lazy<Task<Bitmap>> ignored), TaskScheduler.Default);

            try {
                return await loadTask.WaitAsync(cancellationToken);
            } catch (OperationCanceledException) {
                if (Settings.BitmapManagerLogs) {
                    Log.Information("GetBitmapAsync canceled. Source: {Source}, size: {Width}x{Height}", source.AbsoluteUri, request.DecodeWidth, request.DecodeHeight);
                }

                throw;
            } catch (Exception ex) {
                Log.Error(ex, "GetBitmapAsync error! Source: {Source}, size: {Width}x{Height}", source.AbsoluteUri, request.DecodeWidth, request.DecodeHeight);
                return null;
            }
        }

        private static bool TryGetCachedBitmap(string key, out Bitmap bitmap) {
            Bitmap expiredBitmap = null;
            string expiredKey = null;
            BitmapCacheKind expiredKind = BitmapCacheKind.Default;
            bool isHit = false;

            lock (cacheLock) {
                if (!cachedImages.TryGetValue(key, out CacheEntry cacheEntry)) {
                    bitmap = null;
                } else {
                    DateTime utcNow = DateTime.UtcNow;
                    if (IsExpired(cacheEntry, utcNow)) {
                        if (RemoveCacheEntry(cacheEntry)) {
                            expiredBitmap = cacheEntry.Bitmap;
                            expiredKey = cacheEntry.Key;
                            expiredKind = cacheEntry.Kind;
                        }

                        bitmap = null;
                    } else {
                        cacheEntry.Touch(utcNow);
                        bitmap = cacheEntry.Bitmap;
                        isHit = true;
                    }
                }
            }

            if (expiredBitmap != null) {
                expiredBitmap.Dispose();

                if (Settings.BitmapManagerLogs) {
                    Log.Information("Bitmap cache expired {Key} ({Kind}). Cache size: {CacheMb} Mb.", expiredKey, expiredKind, Math.Round((double)cachedBytes / 1048576, 1));
                }
            }

            return isHit;
        }

        private static async Task<Bitmap> LoadAndCacheBitmapAsync(ImageRequest request) {
            Bitmap bitmap = request.Uri.IsFile
                ? await LoadFileBitmapAsync(request)
                : await LoadNetworkBitmapAsync(request);
            AddToCache(request.Key, bitmap, request.CacheKind);
            return bitmap;
        }

        private static async Task<Bitmap> LoadFileBitmapAsync(ImageRequest request) {
            string path = request.Uri.LocalPath;
            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path)) throw new FileNotFoundException("Bitmap file not found.", path);

            if (request.CacheKind == BitmapCacheKind.Background) {
                Bitmap backgroundBitmap = DecodeLocalBackgroundBitmap(path, request.DecodeWidth, request.DecodeHeight);

                if (Settings.BitmapManagerLogs) {
                    Log.Information(
                        "Loaded local background {Source}, requested {Width}x{Height}, decoded {DecodedWidth}x{DecodedHeight}, cache {CacheMb} Mb.",
                        request.Uri.AbsoluteUri,
                        request.DecodeWidth,
                        request.DecodeHeight,
                        backgroundBitmap.PixelSize.Width,
                        backgroundBitmap.PixelSize.Height,
                        Math.Round((double)cachedBytes / 1048576, 1));
                }

                return backgroundBitmap;
            }

            await using FileStream stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            Bitmap bitmap = DecodeBitmap(stream, request.DecodeWidth, request.DecodeHeight);

            if (Settings.BitmapManagerLogs) {
                Log.Information(
                    "Loaded local bitmap {Source}, requested {Width}x{Height}, decoded {DecodedWidth}x{DecodedHeight}, cache {CacheMb} Mb.",
                    request.Uri.AbsoluteUri,
                    request.DecodeWidth,
                    request.DecodeHeight,
                    bitmap.PixelSize.Width,
                    bitmap.PixelSize.Height,
                    Math.Round((double)cachedBytes / 1048576, 1));
            }

            return bitmap;
        }

        private static async Task<Bitmap> LoadNetworkBitmapAsync(ImageRequest request) {
            using var response = Settings.LoadImagesSequential
                ? await LNet.GetSequentialAsync(request.Uri)
                : await LNet.GetAsync(request.Uri);

            response.EnsureSuccessStatusCode();

            byte[] bytes = await response.Content.ReadAsByteArrayAsync();
            if (bytes.Length == 0) throw new Exception("Image length is 0!");

            using Stream stream = new MemoryStream(bytes, false);
            Bitmap bitmap = DecodeBitmap(stream, request.DecodeWidth, request.DecodeHeight);

            if (Settings.BitmapManagerLogs) {
                Log.Information(
                    "Loaded bitmap {Source}, requested {Width}x{Height}, decoded {DecodedWidth}x{DecodedHeight}, cache {CacheMb} Mb.",
                    request.Uri.AbsoluteUri,
                    request.DecodeWidth,
                    request.DecodeHeight,
                    bitmap.PixelSize.Width,
                    bitmap.PixelSize.Height,
                    Math.Round((double)cachedBytes / 1048576, 1));
            }

            return bitmap;
        }

        private static async Task<Bitmap> LoadAssetBitmapAsync(ImageRequest request, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();

            Bitmap bitmap = AssetsManager.GetBitmapFromUri(request.Uri);
            if (request.DecodeWidth <= 0 && request.DecodeHeight <= 0) return bitmap;

            return await CreateScaledBitmapAsync(bitmap, request.DecodeWidth, request.DecodeHeight, cancellationToken);
        }

        private static Bitmap DecodeBitmap(Stream stream, int decodeWidth, int decodeHeight) {
            if (decodeWidth <= 0 && decodeHeight <= 0) {
                return new Bitmap(stream);
            }

            if (decodeWidth > 0 && (decodeHeight <= 0 || decodeWidth <= decodeHeight)) {
                return Bitmap.DecodeToWidth(stream, decodeWidth, BitmapInterpolationMode.MediumQuality);
            }

            return Bitmap.DecodeToHeight(stream, decodeHeight, BitmapInterpolationMode.MediumQuality);
        }

        private static Bitmap DecodeLocalBackgroundBitmap(string path, int decodeWidth, int decodeHeight) {
            if (decodeWidth <= 0 && decodeHeight <= 0) {
                using FileStream stream = File.OpenRead(path);
                return DecodeBitmap(stream, decodeWidth, decodeHeight);
            }

            try {
                using SKCodec codec = SKCodec.Create(path);
                if (codec == null) {
                    using FileStream stream = File.OpenRead(path);
                    return DecodeBitmap(stream, decodeWidth, decodeHeight);
                }

                SKImageInfo sourceInfo = codec.Info;
                int sampleSize = GetSkiaSampleSize(sourceInfo.Width, sourceInfo.Height, decodeWidth, decodeHeight);
                SKImageInfo sampledInfo = new SKImageInfo(
                    Math.Max(1, sourceInfo.Width / sampleSize),
                    Math.Max(1, sourceInfo.Height / sampleSize),
                    SKColorType.Bgra8888,
                    SKAlphaType.Premul);

                using SKBitmap sampledBitmap = new SKBitmap(sampledInfo);
                SKCodecResult result = codec.GetPixels(sampledInfo, sampledBitmap.GetPixels(), new SKCodecOptions(0, sampleSize));
                if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput) {
                    using FileStream stream = File.OpenRead(path);
                    return DecodeBitmap(stream, decodeWidth, decodeHeight);
                }

                using SKImage image = SKImage.FromBitmap(sampledBitmap);
                using SKData encoded = image.Encode(SKEncodedImageFormat.Png, 90);
                using Stream bitmapStream = encoded.AsStream();
                return new Bitmap(bitmapStream);
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot sampled-decode local background {Path}; falling back to Avalonia decoder.", path);
                using FileStream stream = File.OpenRead(path);
                return DecodeBitmap(stream, decodeWidth, decodeHeight);
            }
        }

        private static int GetSkiaSampleSize(int sourceWidth, int sourceHeight, int decodeWidth, int decodeHeight) {
            if (sourceWidth <= 0 || sourceHeight <= 0) return 1;

            double widthRatio = decodeWidth > 0 ? (double)sourceWidth / decodeWidth : 1;
            double heightRatio = decodeHeight > 0 ? (double)sourceHeight / decodeHeight : 1;
            double ratio = Math.Max(widthRatio, heightRatio);

            int sampleSize = 1;
            while (sampleSize < 64 && sampleSize * 2 <= ratio) {
                sampleSize *= 2;
            }

            return Math.Max(1, sampleSize);
        }

        private static async Task<Bitmap> CreateScaledBitmapAsync(Bitmap bitmap, int decodeWidth, int decodeHeight, CancellationToken cancellationToken) {
            if (bitmap == null) return null;

            double imageWidth = bitmap.Size.Width;
            double imageHeight = bitmap.Size.Height;

            if (decodeWidth <= 0 || decodeHeight <= 0 || imageWidth <= 0 || imageHeight <= 0) {
                if (Settings.BitmapManagerLogs) {
                    Log.Information(
                        "CreateScaledBitmapAsync: bitmap size is {ImageWidth}x{ImageHeight}, container size is {Width}x{Height}. Returning original bitmap.",
                        imageWidth,
                        imageHeight,
                        decodeWidth,
                        decodeHeight);
                }

                return bitmap;
            }

            ElorMath.Resize(imageWidth, imageHeight, decodeWidth, decodeHeight, out double resizedWidth, out double resizedHeight);

            if (Settings.BitmapManagerLogs) {
                Log.Information(
                    "CreateScaledBitmapAsync: bitmap size is {ImageWidth}x{ImageHeight}, container size is {Width}x{Height}, resized to {ResizedWidth}x{ResizedHeight}.",
                    imageWidth,
                    imageHeight,
                    decodeWidth,
                    decodeHeight,
                    resizedWidth,
                    resizedHeight);
            }

            return await Task.Run(
                () => bitmap.CreateScaledBitmap(
                    new PixelSize((int)resizedWidth, (int)resizedHeight),
                    BitmapInterpolationMode.MediumQuality),
                cancellationToken);
        }

        private static void AddToCache(string key, Bitmap bitmap, BitmapCacheKind cacheKind) {
            if (bitmap == null) return;

            List<Bitmap> bitmapsToDispose = new List<Bitmap>();

            lock (cacheLock) {
                if (cachedImages.TryGetValue(key, out CacheEntry existingEntry)) {
                    existingEntry.Touch(DateTime.UtcNow);
                    bitmapsToDispose.Add(bitmap);
                } else {
                    long size = EstimateBitmapSize(bitmap);
                    CacheEntry cacheEntry = new CacheEntry(key, bitmap, size, cacheKind, DateTime.UtcNow);
                    cachedImages.Add(key, cacheEntry);
                    cachedBytes += size;
                }

                TrimExpiredEntries(bitmapsToDispose, DateTime.UtcNow);
                TrimCache(bitmapsToDispose, GetCacheLimitBytes());
                TrimForProcessPressure(bitmapsToDispose);
            }

            foreach (var bitmapToDispose in bitmapsToDispose) {
                bitmapToDispose.Dispose();
            }

            if (bitmapsToDispose.Count > 0 && MediaMemoryGovernor.IsProcessMemoryPressureHigh()) {
                GC.Collect(2, GCCollectionMode.Optimized, false, false);
            }
        }

        private static void TrimExpiredEntries(List<Bitmap> bitmapsToDispose, DateTime utcNow) {
            if (utcNow - lastExpirationScanUtc < ExpirationScanInterval) return;

            lastExpirationScanUtc = utcNow;
            List<CacheEntry> expiredEntries = new List<CacheEntry>();

            foreach (var cacheEntry in cachedImages.Values) {
                if (IsExpired(cacheEntry, utcNow)) expiredEntries.Add(cacheEntry);
            }

            foreach (var cacheEntry in expiredEntries) {
                if (!RemoveCacheEntry(cacheEntry)) continue;

                bitmapsToDispose.Add(cacheEntry.Bitmap);

                if (Settings.BitmapManagerLogs) {
                    Log.Information("Bitmap cache expired {Key} ({Kind}). Cache size: {CacheMb} Mb.", cacheEntry.Key, cacheEntry.Kind, Math.Round((double)cachedBytes / 1048576, 1));
                }
            }
        }

        private static void TrimCache(List<Bitmap> bitmapsToDispose, long cacheLimitBytes) {
            while (cachedBytes > cacheLimitBytes && lruKeys.Count > 1) {
                string key = lruKeys.First.Value;
                if (!cachedImages.TryGetValue(key, out CacheEntry cacheEntry)) {
                    lruKeys.RemoveFirst();
                    continue;
                }

                RemoveCacheEntry(cacheEntry);
                bitmapsToDispose.Add(cacheEntry.Bitmap);

                if (Settings.BitmapManagerLogs) {
                    Log.Information("Bitmap cache evicted {Key}. Cache size: {CacheMb} Mb.", key, Math.Round((double)cachedBytes / 1048576, 1));
                }
            }
        }

        private static void TrimForProcessPressure(List<Bitmap> bitmapsToDispose) {
            if (!MediaMemoryGovernor.IsProcessMemoryPressureHigh()) return;

            long emergencyLimitBytes = MediaMemoryGovernor.GetEmergencyBitmapCacheBudgetBytes();
            long beforeBytes = cachedBytes;
            TrimCache(bitmapsToDispose, emergencyLimitBytes);

            if (Settings.BitmapManagerLogs && cachedBytes < beforeBytes) {
                Log.Information(
                    "Bitmap cache emergency trim. Before: {BeforeMb} Mb, after: {AfterMb} Mb, limit: {LimitMb} Mb.",
                    Math.Round((double)beforeBytes / 1048576, 1),
                    Math.Round((double)cachedBytes / 1048576, 1),
                    Math.Round((double)emergencyLimitBytes / 1048576, 1));
            }
        }

        private static long EstimateBitmapSize(Bitmap bitmap) {
            long width = Math.Max(1, bitmap.PixelSize.Width);
            long height = Math.Max(1, bitmap.PixelSize.Height);
            return width * height * 4;
        }

        private static long GetCacheLimitBytes() {
            return MediaMemoryGovernor.GetBitmapCacheBudgetBytes();
        }

        private static bool RemoveCacheEntry(CacheEntry cacheEntry) {
            if (!cachedImages.Remove(cacheEntry.Key)) return false;

            cacheEntry.Detach();
            cachedBytes = Math.Max(0, cachedBytes - cacheEntry.SizeBytes);
            return true;
        }

        private static bool IsExpired(CacheEntry cacheEntry, DateTime utcNow) {
            TimeSpan ttl = GetCacheTtl(cacheEntry.Kind);
            return ttl > TimeSpan.Zero && utcNow - cacheEntry.LastAccessUtc >= ttl;
        }

        private static TimeSpan GetCacheTtl(BitmapCacheKind cacheKind) {
            int minutes = cacheKind switch {
                BitmapCacheKind.Avatar => Settings.ImageCacheAvatarTtlMinutes,
                BitmapCacheKind.Attachment => Settings.ImageCacheAttachmentTtlMinutes,
                BitmapCacheKind.Background => Math.Min(Settings.ImageCacheAttachmentTtlMinutes, 30),
                BitmapCacheKind.E2E => Settings.ImageCacheE2ETtlMinutes,
                _ => Settings.ImageCacheDefaultTtlMinutes
            };

            return minutes <= 0 ? TimeSpan.Zero : TimeSpan.FromMinutes(minutes);
        }

        private sealed class CacheEntry {
            public string Key { get; }
            public Bitmap Bitmap { get; }
            public long SizeBytes { get; }
            public BitmapCacheKind Kind { get; }
            public DateTime LastAccessUtc { get; private set; }
            public LinkedListNode<string> Node { get; private set; }

            public CacheEntry(string key, Bitmap bitmap, long sizeBytes, BitmapCacheKind kind, DateTime utcNow) {
                Key = key;
                Bitmap = bitmap;
                SizeBytes = sizeBytes;
                Kind = kind;
                LastAccessUtc = utcNow;
                Node = lruKeys.AddLast(key);
            }

            public void Touch(DateTime utcNow) {
                LastAccessUtc = utcNow;
                if (Node == null) return;
                lruKeys.Remove(Node);
                Node = lruKeys.AddLast(Key);
            }

            public void Detach() {
                if (Node == null) return;
                lruKeys.Remove(Node);
                Node = null;
            }
        }

        private readonly struct ImageRequest {
            public Uri Uri { get; }
            public string Key { get; }
            public int DecodeWidth { get; }
            public int DecodeHeight { get; }
            public BitmapCacheKind CacheKind { get; }

            private ImageRequest(Uri uri, string key, int decodeWidth, int decodeHeight, BitmapCacheKind cacheKind) {
                Uri = uri;
                Key = key;
                DecodeWidth = decodeWidth;
                DecodeHeight = decodeHeight;
                CacheKind = cacheKind;
            }

            public static ImageRequest Create(Uri uri, double decodeWidth, double decodeHeight, BitmapCacheKind cacheKind) {
                int width = NormalizeDecodeSize(decodeWidth);
                int height = NormalizeDecodeSize(decodeHeight);
                string key = $"{cacheKind}:{uri.AbsoluteUri}|{width}x{height}";
                return new ImageRequest(uri, key, width, height, cacheKind);
            }

            private static int NormalizeDecodeSize(double value) {
                if (value <= 0) return 0;
                return Math.Max(1, Convert.ToInt32(Math.Ceiling(value * App.Current.DPI)));
            }
        }
    }
}
