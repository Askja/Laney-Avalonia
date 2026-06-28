using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ELOR.Laney.Core {
    public readonly struct EmojiSpriteMatch {
        public string Emoji { get; }
        public string FilePath { get; }
        public int Length { get; }

        public EmojiSpriteMatch(string emoji, string filePath) {
            Emoji = emoji;
            FilePath = filePath;
            Length = emoji?.Length ?? 0;
        }
    }

    public static class EmojiSpriteStore {
        private static readonly HashSet<string> SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            ".png", ".jpg", ".jpeg", ".webp", ".gif"
        };
        private static readonly ConcurrentDictionary<string, SpriteMap> MapCache = new ConcurrentDictionary<string, SpriteMap>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, IImage> ImageCache = new ConcurrentDictionary<string, IImage>(StringComparer.OrdinalIgnoreCase);
        private static readonly IImage EmptyImage = new WriteableBitmap(new PixelSize(1, 1), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Premul);

        public static IImage PlaceholderImage => EmptyImage;

        public static bool HasSprites(long peerId = 0) {
            return GetMap(peerId).ByEmoji.Count > 0;
        }

        public static bool TryMatch(string text, int index, long peerId, out EmojiSpriteMatch match) {
            match = default;
            if (String.IsNullOrEmpty(text) || index < 0 || index >= text.Length) return false;

            SpriteMap map = GetMap(peerId);
            foreach (string emoji in map.EmojisByLength) {
                if (!text.AsSpan(index).StartsWith(emoji.AsSpan(), StringComparison.Ordinal)) continue;

                match = new EmojiSpriteMatch(emoji, map.ByEmoji[emoji]);
                return true;
            }

            return false;
        }

        public static Task<IImage?> LoadImageAsync(string filePath) {
            return Task.Run<IImage?>(() => {
                try {
                    if (String.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return null;
                    return ImageCache.GetOrAdd(filePath, LoadImage);
                } catch (Exception ex) {
                    Log.Warning(ex, "Cannot load emoji sprite {FilePath}", filePath);
                    return null;
                }
            });
        }

        public static void ClearCache() {
            MapCache.Clear();
            ImageCache.Clear();
        }

        private static IImage LoadImage(string filePath) {
            using FileStream stream = File.OpenRead(filePath);
            return new Bitmap(stream);
        }

        private static SpriteMap GetMap(long peerId) {
            string manifestPath = ResolveManifestPath(peerId);
            if (String.IsNullOrWhiteSpace(manifestPath)) return SpriteMap.Empty;

            string cacheKey = BuildCacheKey(manifestPath);
            return MapCache.GetOrAdd(cacheKey, _ => ReadMap(manifestPath));
        }

        private static string ResolveManifestPath(long peerId) {
            string pack = peerId != 0 ? Settings.ResolvePeerEmojiPack(peerId) : Settings.EmojiPack;
            if (pack != EmojiPackIds.Custom) return null;

            string path = Settings.EmojiCustomPackPath;
            if (String.IsNullOrWhiteSpace(path)) return null;

            path = path.Trim();
            if (File.Exists(path)) return path;
            if (!Directory.Exists(path)) return null;

            string[] candidates = [
                Path.Combine(path, "emoji-sprites.txt"),
                Path.Combine(path, "emoji-map.txt"),
                Path.Combine(path, "emoji.txt")
            ];
            return candidates.FirstOrDefault(File.Exists);
        }

        private static string BuildCacheKey(string manifestPath) {
            try {
                return $"{manifestPath}:{File.GetLastWriteTimeUtc(manifestPath).Ticks}";
            } catch {
                return manifestPath;
            }
        }

        private static SpriteMap ReadMap(string manifestPath) {
            Dictionary<string, string> byEmoji = new Dictionary<string, string>(StringComparer.Ordinal);
            string baseDirectory = Path.GetDirectoryName(manifestPath) ?? AppContext.BaseDirectory;

            foreach (string rawLine in File.ReadLines(manifestPath)) {
                if (!TryParseLine(rawLine, baseDirectory, out string emoji, out string filePath)) continue;
                byEmoji[emoji] = filePath;
            }

            return new SpriteMap(byEmoji);
        }

        private static bool TryParseLine(string rawLine, string baseDirectory, out string emoji, out string filePath) {
            emoji = null;
            filePath = null;

            string line = StripComment(rawLine).Trim();
            if (String.IsNullOrWhiteSpace(line)) return false;

            int equalsIndex = line.IndexOf('=');
            if (equalsIndex > 0) {
                emoji = line[..equalsIndex].Trim();
                filePath = line[(equalsIndex + 1)..].Trim();
            } else {
                string[] parts = line.Split([' ', '\t', ',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length < 2) return false;

                emoji = parts[0];
                filePath = parts[1];
            }

            filePath = ResolveSpritePath(filePath.Trim('"'), baseDirectory);
            return !String.IsNullOrWhiteSpace(emoji)
                && !String.IsNullOrWhiteSpace(filePath)
                && SupportedExtensions.Contains(Path.GetExtension(filePath))
                && File.Exists(filePath);
        }

        private static string StripComment(string line) {
            if (String.IsNullOrWhiteSpace(line)) return String.Empty;

            int hash = line.IndexOf('#');
            int slashes = line.IndexOf("//", StringComparison.Ordinal);
            int cut = hash >= 0 && slashes >= 0 ? Math.Min(hash, slashes) : Math.Max(hash, slashes);
            return cut >= 0 ? line[..cut] : line;
        }

        private static string ResolveSpritePath(string filePath, string baseDirectory) {
            if (String.IsNullOrWhiteSpace(filePath)) return null;
            return Path.IsPathRooted(filePath) ? filePath : Path.GetFullPath(Path.Combine(baseDirectory, filePath));
        }

        private sealed class SpriteMap {
            public static readonly SpriteMap Empty = new SpriteMap(new Dictionary<string, string>(StringComparer.Ordinal));

            public IReadOnlyDictionary<string, string> ByEmoji { get; }
            public IReadOnlyList<string> EmojisByLength { get; }

            public SpriteMap(Dictionary<string, string> byEmoji) {
                ByEmoji = byEmoji;
                EmojisByLength = byEmoji.Keys
                    .OrderByDescending(e => e.Length)
                    .ThenBy(e => e, StringComparer.Ordinal)
                    .ToList();
            }
        }
    }
}
