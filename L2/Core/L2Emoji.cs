using ELOR.Laney.DataModels;
using NeoSmart.Unicode;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace ELOR.Laney.Core {

    // Название такое чтобы не конфликтовался с классом NeoSmart.Unicode.Emoji.
    public class L2Emoji {
        private static readonly Dictionary<string, ObservableCollection<EmojiGroup>> Cache = new Dictionary<string, ObservableCollection<EmojiGroup>>();
        private static readonly IReadOnlyDictionary<string, string[]> SuggestionCatalog = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase) {
            ["улыб smile happy радость"] = ["😀", "😄", "😊", "🙂"],
            ["смех laugh lol ахах"] = ["😂", "🤣", "😆"],
            ["любовь love heart сердце"] = ["❤️", "😍", "😘", "🥰"],
            ["ок ok yes да лайк"] = ["👍", "👌", "✅"],
            ["нет no nope"] = ["👎", "❌", "🚫"],
            ["спасибо thanks благодарю"] = ["🙏", "🤝", "💙"],
            ["огонь fire hot пушка"] = ["🔥", "💥", "⚡"],
            ["грусть sad cry плач"] = ["😢", "😭", "☹️"],
            ["злой angry rage"] = ["😡", "🤬", "😤"],
            ["сон sleep tired устал"] = ["😴", "🥱", "💤"],
            ["думать think hmm"] = ["🤔", "🧐"],
            ["шок wow surprise"] = ["😮", "😱", "🤯"],
            ["праздник party birthday др"] = ["🎉", "🥳", "🎂"],
            ["деньги money cash"] = ["💸", "💰", "🤑"],
            ["кофе coffee"] = ["☕"],
            ["работа work briefcase"] = ["💼", "🛠️"],
            ["кот cat"] = ["🐱", "😺"],
            ["собака dog"] = ["🐶"],
            ["поцелуй kiss"] = ["😘", "💋"],
            ["звезда star"] = ["⭐", "✨"],
            ["вопрос question"] = ["❓", "🤔"],
            ["идея idea"] = ["💡"],
            ["музыка music"] = ["🎵", "🎧"],
            ["еда food"] = ["🍔", "🍕", "🍜"],
            ["дом home"] = ["🏠"],
            ["машина car"] = ["🚗"],
            ["самолет plane travel"] = ["✈️", "🧳"]
        };
        public static ObservableCollection<EmojiGroup> All { get => GetEmojis(Settings.EmojiPack, Settings.EmojiCustomPackPath); }

        public static ObservableCollection<EmojiGroup> GetForPeer(long peerId) {
            return GetEmojis(Settings.ResolvePeerEmojiPack(peerId), Settings.EmojiCustomPackPath);
        }

        public static void ClearCache() {
            Cache.Clear();
            EmojiSpriteStore.ClearCache();
        }

        public static IReadOnlyList<SingleEmoji> SearchByWord(string query, long peerId = 0) {
            string normalizedQuery = query?.Trim().ToLowerInvariant();
            if (String.IsNullOrWhiteSpace(normalizedQuery) || normalizedQuery.Length < 2) return Array.Empty<SingleEmoji>();

            Dictionary<string, SingleEmoji> available = GetForPeer(peerId)
                .SelectMany(group => group)
                .GroupBy(e => e.ToString(), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            List<SingleEmoji> result = new List<SingleEmoji>();
            HashSet<string> added = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in SuggestionCatalog) {
                if (!IsAliasMatch(pair.Key, normalizedQuery)) continue;

                foreach (string emojiText in pair.Value) {
                    if (!available.TryGetValue(emojiText, out SingleEmoji emoji) || !added.Add(emojiText)) continue;
                    result.Add(emoji);
                }
            }

            return result;
        }

        private static bool IsAliasMatch(string aliases, string query) {
            return aliases
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(alias => alias.Contains(query, StringComparison.OrdinalIgnoreCase) || query.Contains(alias, StringComparison.OrdinalIgnoreCase));
        }

        private static ObservableCollection<EmojiGroup> GetEmojis(string packId, string customPath) {
            string normalizedPackId = EmojiPackIds.Normalize(packId);
            string normalizedPath = NormalizeCustomPath(customPath);
            string cacheKey = $"{normalizedPackId}:{normalizedPath}";
            if (Cache.TryGetValue(cacheKey, out ObservableCollection<EmojiGroup> cached)) return cached;

            ObservableCollection<EmojiGroup> emojis = normalizedPackId switch {
                EmojiPackIds.Vk => BuildTelegramLikePack(),
                EmojiPackIds.TelegramLike => BuildTelegramLikePack(),
                EmojiPackIds.Noto => BuildTelegramLikePack(),
                EmojiPackIds.Twemoji => BuildTelegramLikePack(),
                EmojiPackIds.OpenMoji => BuildTelegramLikePack(),
                EmojiPackIds.Fallback => BuildFallbackPack(),
                EmojiPackIds.Custom => BuildCustomPack(normalizedPath) ?? BuildSystemPack(),
                _ => BuildSystemPack()
            };

            Cache[cacheKey] = emojis;
            return emojis;
        }

        private static ObservableCollection<EmojiGroup> BuildSystemPack() {
            List<SingleEmoji> emojis = Emoji.Basic
                .Where(e => e.Group != "Flags")
                .Concat(Emoji.All.Where(e => e.Group == "Flags"))
                .ToList();

            return ToEmojiGroups(emojis);
        }

        private static ObservableCollection<EmojiGroup> BuildTelegramLikePack() {
            string[] order = [
                "Smileys & Emotion",
                "People & Body",
                "Animals & Nature",
                "Food & Drink",
                "Activities",
                "Travel & Places",
                "Objects",
                "Symbols",
                "Flags"
            ];

            Dictionary<string, int> orderMap = order
                .Select((group, index) => new { group, index })
                .ToDictionary(x => x.group, x => x.index, StringComparer.Ordinal);

            List<SingleEmoji> emojis = Emoji.All
                .OrderBy(e => orderMap.TryGetValue(e.Group, out int index) ? index : Int32.MaxValue)
                .ThenBy(e => e.ToString(), StringComparer.Ordinal)
                .ToList();

            return ToEmojiGroups(emojis);
        }

        private static ObservableCollection<EmojiGroup> BuildFallbackPack() {
            HashSet<string> allowedGroups = new HashSet<string>(StringComparer.Ordinal) {
                "Smileys & Emotion",
                "People & Body",
                "Symbols"
            };

            List<SingleEmoji> emojis = Emoji.All
                .Where(e => allowedGroups.Contains(e.Group))
                .ToList();

            return ToEmojiGroups(emojis);
        }

        private static ObservableCollection<EmojiGroup> BuildCustomPack(string path) {
            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

            Dictionary<string, SingleEmoji> known = Emoji.All
                .GroupBy(e => e.ToString(), StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            List<SingleEmoji> emojis = new List<SingleEmoji>();
            HashSet<string> added = new HashSet<string>(StringComparer.Ordinal);

            foreach (string line in File.ReadLines(path)) {
                foreach (string token in ReadCustomEmojiTokens(line)) {
                    if (!known.TryGetValue(token, out SingleEmoji emoji) || !added.Add(token)) continue;
                    emojis.Add(emoji);
                }
            }

            return emojis.Count == 0 ? null : ToEmojiGroups(emojis, "Custom");
        }

        private static IEnumerable<string> ReadCustomEmojiTokens(string line) {
            if (String.IsNullOrWhiteSpace(line)) yield break;

            string normalized = StripComment(line).Trim();
            if (String.IsNullOrWhiteSpace(normalized)) yield break;

            int equalsIndex = normalized.IndexOf('=');
            if (equalsIndex > 0) {
                yield return normalized[..equalsIndex].Trim();
                yield break;
            }

            foreach (string token in normalized.Split([' ', '\t', ',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
                yield return token;
            }
        }

        private static string StripComment(string line) {
            int hash = line.IndexOf('#');
            int slashes = line.IndexOf("//", StringComparison.Ordinal);
            int cut = hash >= 0 && slashes >= 0 ? Math.Min(hash, slashes) : Math.Max(hash, slashes);
            return cut >= 0 ? line[..cut] : line;
        }

        private static ObservableCollection<EmojiGroup> ToEmojiGroups(IEnumerable<SingleEmoji> emojis, string groupName = null) {
            IEnumerable<IGrouping<string, SingleEmoji>> grouped = emojis.GroupBy(e => groupName ?? e.Group);
            return new ObservableCollection<EmojiGroup>(grouped.Select(g => new EmojiGroup(g)));
        }

        private static string NormalizeCustomPath(string path) {
            return String.IsNullOrWhiteSpace(path) ? String.Empty : path.Trim();
        }
    }
}
