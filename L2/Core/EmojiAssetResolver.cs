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
                EmojiPackIds.TelegramLike => BuildNotoPngUri(emoji),
                EmojiPackIds.Twemoji => BuildTwemojiPngUri(emoji),
                _ => null
            };
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
