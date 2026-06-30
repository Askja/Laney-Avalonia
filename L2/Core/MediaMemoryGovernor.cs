using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ELOR.Laney.Core {
    public readonly struct MediaMemorySnapshot {
        public long TotalBudgetBytes { get; }
        public long EstimatedUsedBytes { get; }
        public long HeadroomBytes { get; }
        public long BitmapCacheBytes { get; }
        public long BitmapCacheBudgetBytes { get; }
        public long WaveformCacheBytes { get; }
        public int BitmapCacheEntries { get; }
        public int WaveformCacheItems { get; }
        public int ActiveLocalStickerAnimations { get; }
        public int LocalStickerAnimationLimit { get; }
        public int ActiveMediaLoads { get; }
        public int MediaLoadConcurrency { get; }
        public bool CanPrefetchMedia { get; }

        public MediaMemorySnapshot(long totalBudgetBytes, long estimatedUsedBytes, long headroomBytes, long bitmapCacheBytes, long bitmapCacheBudgetBytes, long waveformCacheBytes, int bitmapCacheEntries, int waveformCacheItems, int activeLocalStickerAnimations, int localStickerAnimationLimit, int activeMediaLoads, int mediaLoadConcurrency, bool canPrefetchMedia) {
            TotalBudgetBytes = totalBudgetBytes;
            EstimatedUsedBytes = estimatedUsedBytes;
            HeadroomBytes = headroomBytes;
            BitmapCacheBytes = bitmapCacheBytes;
            BitmapCacheBudgetBytes = bitmapCacheBudgetBytes;
            WaveformCacheBytes = waveformCacheBytes;
            BitmapCacheEntries = bitmapCacheEntries;
            WaveformCacheItems = waveformCacheItems;
            ActiveLocalStickerAnimations = activeLocalStickerAnimations;
            LocalStickerAnimationLimit = localStickerAnimationLimit;
            ActiveMediaLoads = activeMediaLoads;
            MediaLoadConcurrency = mediaLoadConcurrency;
            CanPrefetchMedia = canPrefetchMedia;
        }
    }

    public static class MediaMemoryGovernor {
        private const long Mb = 1024L * 1024L;
        private const long ProcessPrivateSoftLimitBytes = 210L * Mb;
        private const long ProcessWorkingSetSoftLimitBytes = 260L * Mb;
        private const long EstimatedLocalStickerAnimationBytes = 8L * Mb;
        private static long waveformCacheBytes;
        private static int waveformCacheItems;
        private static int activeLocalStickerAnimations;
        private static readonly SemaphoreSlim mediaLoadGate = new SemaphoreSlim(4, 4);
        private static int activeMediaLoads;

        public static long TotalBudgetBytes => Settings.MediaMemoryBudgetMb * Mb;

        public static long GetBitmapCacheBudgetBytes() {
            long imageLimitBytes = Settings.ImageCacheRamLimitMb * Mb;
            long reservedBytes = GetWaveformBudgetBytes() + GetAnimationReserveBytes() + GetPrefetchReserveBytes();
            long budgetBytes = Math.Max(16L * Mb, TotalBudgetBytes - reservedBytes);
            long hardCap = Settings.LowMemoryMode ? 48L * Mb : 96L * Mb;
            return Math.Clamp(Math.Min(imageLimitBytes, budgetBytes), 16L * Mb, hardCap);
        }

        public static long GetEmergencyBitmapCacheBudgetBytes() {
            return Settings.LowMemoryMode ? 20L * Mb : 32L * Mb;
        }

        public static bool IsProcessMemoryPressureHigh() {
            Process process = Process.GetCurrentProcess();
            process.Refresh();
            return process.PrivateMemorySize64 >= ProcessPrivateSoftLimitBytes
                || process.WorkingSet64 >= ProcessWorkingSetSoftLimitBytes;
        }

        public static bool TrimProcessWorkingSet() {
#if WIN
            try {
                using Process process = Process.GetCurrentProcess();
                return SetProcessWorkingSetSize(process.Handle, new IntPtr(-1), new IntPtr(-1));
            } catch {
                return false;
            }
#else
            return false;
#endif
        }

        public static int GetWaveformCacheItemLimit() {
            if (Settings.LowMemoryMode) return 128;
            if (Settings.MediaMemoryBudgetMb <= 128) return 160;
            if (Settings.MediaMemoryBudgetMb >= 512) return 256;
            return 256;
        }

        public static int GetLocalStickerAnimationLimit() {
            if (Settings.LowMotionMode || Settings.StickerAnimation == StickerAnimationMode.Never) return 0;
            if (Settings.LowMemoryMode) return 1;
            if (Settings.MediaMemoryBudgetMb <= 128) return 1;
            if (Settings.MediaMemoryBudgetMb >= 512) return 3;
            return 2;
        }

        public static int GetMediaLoadConcurrency() {
            if (Settings.LowMemoryMode || Settings.LoadImagesSequential) return 1;
            return 1;
        }

        public static Task<IDisposable> EnterMediaLoadAsync() {
            return EnterMediaLoadAsync(CancellationToken.None);
        }

        public static async Task<IDisposable> EnterMediaLoadAsync(CancellationToken cancellationToken) {
            while (Volatile.Read(ref activeMediaLoads) >= GetMediaLoadConcurrency()) {
                await Task.Delay(16, cancellationToken);
            }

            await mediaLoadGate.WaitAsync(cancellationToken);
            Interlocked.Increment(ref activeMediaLoads);
            return new MediaLoadLease();
        }

        public static bool CanStartLocalStickerAnimation() {
            int limit = GetLocalStickerAnimationLimit();
            if (limit <= 0) return false;
            if (Volatile.Read(ref activeLocalStickerAnimations) >= limit) return false;
            return GetEstimatedHeadroomBytes() >= EstimatedLocalStickerAnimationBytes;
        }

        public static void ReportWaveformCache(int itemCount, long sizeBytes) {
            Interlocked.Exchange(ref waveformCacheItems, Math.Max(0, itemCount));
            Interlocked.Exchange(ref waveformCacheBytes, Math.Max(0, sizeBytes));
        }

        public static void ReportActiveLocalStickerAnimations(int count) {
            Interlocked.Exchange(ref activeLocalStickerAnimations, Math.Max(0, count));
        }

        public static bool CanPrefetchMedia() {
            return CanPrefetchMedia(GetEstimatedHeadroomBytes());
        }

        public static MediaMemorySnapshot GetSnapshot() {
            BitmapCacheSnapshot bitmapSnapshot = BitmapManager.GetCacheSnapshot();
            long waveformBytes = Volatile.Read(ref waveformCacheBytes);
            int waveformItems = Volatile.Read(ref waveformCacheItems);
            int activeAnimations = Volatile.Read(ref activeLocalStickerAnimations);
            int animationLimit = GetLocalStickerAnimationLimit();
            long totalBudgetBytes = TotalBudgetBytes;
            long estimatedUsedBytes = EstimateUsedBytes(bitmapSnapshot.SizeBytes, waveformBytes, activeAnimations);
            long headroomBytes = Math.Max(0, totalBudgetBytes - estimatedUsedBytes);

            return new MediaMemorySnapshot(
                totalBudgetBytes,
                estimatedUsedBytes,
                headroomBytes,
                bitmapSnapshot.SizeBytes,
                bitmapSnapshot.LimitBytes,
                waveformBytes,
                bitmapSnapshot.EntryCount,
                waveformItems,
                activeAnimations,
                animationLimit,
                Volatile.Read(ref activeMediaLoads),
                GetMediaLoadConcurrency(),
                CanPrefetchMedia(headroomBytes));
        }

        private static long GetEstimatedHeadroomBytes() {
            BitmapCacheSnapshot bitmapSnapshot = BitmapManager.GetCacheSnapshot();
            long usedBytes = EstimateUsedBytes(
                bitmapSnapshot.SizeBytes,
                Volatile.Read(ref waveformCacheBytes),
                Volatile.Read(ref activeLocalStickerAnimations));
            return Math.Max(0, TotalBudgetBytes - usedBytes);
        }

        private static long EstimateUsedBytes(long bitmapBytes, long waveformBytes, int activeAnimations) {
            return Math.Max(0, bitmapBytes)
                + Math.Max(0, waveformBytes)
                + Math.Max(0, activeAnimations) * EstimatedLocalStickerAnimationBytes;
        }

        private static long GetWaveformBudgetBytes() {
            return Math.Clamp(TotalBudgetBytes / 64, 2L * Mb, 12L * Mb);
        }

        private static long GetAnimationReserveBytes() {
            return GetLocalStickerAnimationLimit() * EstimatedLocalStickerAnimationBytes;
        }

        private static long GetPrefetchReserveBytes() {
            return FeatureFlags.IsEnabled(FeatureFlags.PreloadNextChat) && !Settings.LowMemoryMode
                ? Math.Clamp(TotalBudgetBytes / 8, 16L * Mb, 96L * Mb)
                : 0;
        }

        private static bool CanPrefetchMedia(long headroomBytes) {
            return FeatureFlags.IsEnabled(FeatureFlags.PreloadNextChat)
                && !Settings.LowMemoryMode
                && headroomBytes >= Math.Clamp(TotalBudgetBytes / 8, 16L * Mb, 96L * Mb);
        }

        private sealed class MediaLoadLease : IDisposable {
            private bool disposed;

            public void Dispose() {
                if (disposed) return;
                disposed = true;
                Interlocked.Decrement(ref activeMediaLoads);
                mediaLoadGate.Release();
            }
        }

#if WIN
        [DllImport("kernel32.dll")]
        private static extern bool SetProcessWorkingSetSize(IntPtr process, IntPtr minimumWorkingSetSize, IntPtr maximumWorkingSetSize);
#endif
    }
}
