using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ELOR.Laney.Core {
    public static class FrequentPhraseStore {
        private const int MaxItems = 240;

        private static string DirectoryPath => LocalDataProfile.GetCurrentAccountDirectory("frequent-phrases");
        private static string LibraryPath => Path.Combine(DirectoryPath, "library.json");

        public static List<string> GetTop(long peerId, IEnumerable<string> knownReplies = null, int limit = 6) {
            HashSet<string> known = new HashSet<string>(
                (knownReplies ?? Array.Empty<string>()).Select(Normalize).Where(s => !String.IsNullOrWhiteSpace(s)),
                StringComparer.OrdinalIgnoreCase);

            return Load()
                .Where(i => i.PeerId == 0 || i.PeerId == peerId)
                .GroupBy(i => i.Text, StringComparer.OrdinalIgnoreCase)
                .Select(group => new {
                    Text = group.Key,
                    PeerSpecific = group.Any(i => i.PeerId == peerId),
                    UseCount = group.Sum(i => i.UseCount),
                    LastUsedAt = group.Max(i => i.LastUsedAt)
                })
                .Where(i => !known.Contains(i.Text))
                .OrderByDescending(i => i.PeerSpecific)
                .ThenByDescending(i => i.UseCount)
                .ThenByDescending(i => i.LastUsedAt)
                .Select(i => i.Text)
                .Take(Math.Clamp(limit, 1, 20))
                .ToList();
        }

        public static void MarkUsed(long peerId, string text) {
            string normalized = Normalize(text);
            if (String.IsNullOrWhiteSpace(normalized)) return;

            List<FrequentPhraseItem> items = Load();
            Upsert(items, 0, normalized);
            if (peerId != 0) Upsert(items, peerId, normalized);
            Save(items);
        }

        public static void Clear() {
            try {
                if (File.Exists(LibraryPath)) File.Delete(LibraryPath);
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot clear frequent phrases.");
            }
        }

        private static void Upsert(List<FrequentPhraseItem> items, long peerId, string text) {
            FrequentPhraseItem existing = items.FirstOrDefault(i => i.PeerId == peerId && String.Equals(i.Text, text, StringComparison.OrdinalIgnoreCase));
            if (existing == null) {
                existing = new FrequentPhraseItem {
                    PeerId = peerId,
                    Text = text
                };
                items.Add(existing);
            }

            existing.UseCount++;
            existing.LastUsedAt = DateTimeOffset.UtcNow;
        }

        private static string Normalize(string text) {
            if (String.IsNullOrWhiteSpace(text)) return String.Empty;

            string normalized = text.Replace("\r", " ").Replace("\n", " ").Trim();
            normalized = String.Join(" ", normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            if (normalized.Length < 6 || normalized.StartsWith("/", StringComparison.Ordinal)) return String.Empty;
            return normalized.Length <= 240 ? normalized : $"{normalized[..240]}...";
        }

        private static List<FrequentPhraseItem> Load() {
            try {
                if (!File.Exists(LibraryPath)) return new List<FrequentPhraseItem>();

                using FileStream input = File.OpenRead(LibraryPath);
                List<FrequentPhraseItem> items = JsonSerializer.Deserialize<List<FrequentPhraseItem>>(input) ?? new List<FrequentPhraseItem>();
                return NormalizeItems(items);
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot read frequent phrases.");
                return new List<FrequentPhraseItem>();
            }
        }

        private static void Save(List<FrequentPhraseItem> items) {
            try {
                Directory.CreateDirectory(DirectoryPath);
                List<FrequentPhraseItem> normalized = NormalizeItems(items)
                    .OrderByDescending(i => i.UseCount)
                    .ThenByDescending(i => i.LastUsedAt)
                    .Take(MaxItems)
                    .ToList();

                using FileStream output = File.Create(LibraryPath);
                JsonSerializer.Serialize(output, normalized, new JsonSerializerOptions { WriteIndented = true });
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot save frequent phrases.");
            }
        }

        private static List<FrequentPhraseItem> NormalizeItems(IEnumerable<FrequentPhraseItem> items) {
            return items?
                .Where(i => i != null)
                .Select(i => new FrequentPhraseItem {
                    PeerId = i.PeerId,
                    Text = Normalize(i.Text),
                    UseCount = Math.Max(1, i.UseCount),
                    LastUsedAt = i.LastUsedAt == default ? DateTimeOffset.UtcNow : i.LastUsedAt
                })
                .Where(i => !String.IsNullOrWhiteSpace(i.Text))
                .GroupBy(i => $"{i.PeerId}:{i.Text}", StringComparer.OrdinalIgnoreCase)
                .Select(group => new FrequentPhraseItem {
                    PeerId = group.First().PeerId,
                    Text = group.First().Text,
                    UseCount = group.Sum(i => i.UseCount),
                    LastUsedAt = group.Max(i => i.LastUsedAt)
                })
                .ToList() ?? new List<FrequentPhraseItem>();
        }

        private sealed class FrequentPhraseItem {
            public long PeerId { get; set; }
            public string Text { get; set; }
            public int UseCount { get; set; }
            public DateTimeOffset LastUsedAt { get; set; }
        }
    }
}
