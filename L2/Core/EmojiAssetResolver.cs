using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ELOR.Laney.Core {
    public static class EmojiAssetResolver {
        private const int VariationSelector16 = 0xFE0F;
        private const int ZeroWidthJoiner = 0x200D;

        public static Uri ResolvePickerImageUri(string emoji, long peerId = 0) {
            string pack = peerId != 0 ? Settings.ResolvePeerEmojiPack(peerId) : Settings.EmojiPack;
            return ResolveImageUri(emoji, pack, peerId);
        }

        public static Uri ResolveImageUri(string emoji, string packId, long peerId = 0) {
            if (String.IsNullOrWhiteSpace(emoji)) return null;

            string pack = EmojiPackIds.Normalize(packId);
            if (pack == EmojiPackIds.Custom) {
                return EmojiSpriteStore.TryResolveSpriteUri(emoji, peerId, out Uri uri) ? uri : null;
            }

            return pack switch {
                EmojiPackIds.Vk => BuildVkPngUri(emoji),
                EmojiPackIds.Noto => BuildNotoPngUri(emoji),
                EmojiPackIds.Twemoji => BuildTwemojiPngUri(emoji),
                _ => null
            };
        }

        public static bool IsImageBackedPack(string packId) {
            string pack = EmojiPackIds.Normalize(packId);
            return pack == EmojiPackIds.Vk
                || pack == EmojiPackIds.Noto
                || pack == EmojiPackIds.Twemoji
                || pack == EmojiPackIds.Custom;
        }

        private static Uri BuildVkPngUri(string emoji) {
            string code = BuildVkCodepointName(emoji);
            return String.IsNullOrEmpty(code)
                ? null
                : new Uri($"https://vk.com/images/emoji/{code}.png");
        }

        private static Uri BuildNotoPngUri(string emoji) {
            string code = BuildCodepointName(emoji, "_", stripVariationSelectors: true);
            return String.IsNullOrEmpty(code)
                ? null
                : new Uri($"https://cdn.jsdelivr.net/gh/googlefonts/noto-emoji@main/png/128/emoji_u{code}.png");
        }

        private static Uri BuildTwemojiPngUri(string emoji) {
            string code = BuildTwemojiCodepointName(emoji);
            return String.IsNullOrEmpty(code)
                ? null
                : new Uri($"https://cdn.jsdelivr.net/gh/jdecked/twemoji@latest/assets/72x72/{code}.png");
        }

        private static string BuildTwemojiCodepointName(string emoji) {
            List<int> codepoints = GetCodepoints(emoji);
            bool hasJoiner = codepoints.Contains(ZeroWidthJoiner);
            StringBuilder builder = StringBuilderCache.Acquire();

            for (int i = 0; i < codepoints.Count; i++) {
                int codepoint = codepoints[i];
                if (codepoint == VariationSelector16 && !hasJoiner) continue;
                if (builder.Length > 0) builder.Append('-');
                builder.Append(codepoint.ToString("x", CultureInfo.InvariantCulture));
            }

            return StringBuilderCache.GetStringAndRelease(builder);
        }

        private static string BuildVkCodepointName(string emoji) {
            StringBuilder builder = StringBuilderCache.Acquire();

            foreach (Rune rune in emoji.EnumerateRunes()) {
                int value = rune.Value;
                if (value == VariationSelector16) continue;
                if (value == ZeroWidthJoiner) {
                    StringBuilderCache.Release(builder);
                    return String.Empty;
                }

                if (value <= 0xFFFF) {
                    builder.Append(value.ToString("X4", CultureInfo.InvariantCulture));
                    continue;
                }

                int normalized = value - 0x10000;
                int high = 0xD800 + (normalized >> 10);
                int low = 0xDC00 + (normalized & 0x3FF);
                builder.Append(high.ToString("X4", CultureInfo.InvariantCulture));
                builder.Append(low.ToString("X4", CultureInfo.InvariantCulture));
            }

            return StringBuilderCache.GetStringAndRelease(builder);
        }

        private static string BuildCodepointName(string emoji, string separator, bool stripVariationSelectors) {
            StringBuilder builder = StringBuilderCache.Acquire();
            foreach (int codepoint in GetCodepoints(emoji)) {
                if (stripVariationSelectors && codepoint == VariationSelector16) continue;
                if (builder.Length > 0) builder.Append(separator);
                builder.Append(codepoint.ToString("x", CultureInfo.InvariantCulture));
            }

            return StringBuilderCache.GetStringAndRelease(builder);
        }

        private static List<int> GetCodepoints(string text) {
            List<int> result = new List<int>();
            foreach (Rune rune in text.EnumerateRunes()) {
                result.Add(rune.Value);
            }

            return result;
        }
    }
}
