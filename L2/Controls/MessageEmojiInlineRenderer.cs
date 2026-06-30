using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using ELOR.Laney.Core;
using System;
using System.Globalization;
using System.Text;

namespace ELOR.Laney.Controls {
    internal static class MessageEmojiInlineRenderer {
        private const int MaxRichTextLength = 720;
        private const int MaxCustomInlineImages = 1;
        private const int MaxPackInlineImages = 4;
        private const int MaxCustomEmojiLetters = 32;
        private const int MaxPackEmojiLetters = 36;
        private const int MaxPackImageTextLength = 160;
        private const int MaxSystemEmojiLetters = 180;
        private const double EmojiSize = 20;
        private static readonly FontFamily EmojiFontFamily = new FontFamily("Segoe UI Emoji");

        public static FontFamily EmojiTextFontFamily => EmojiFontFamily;

        public static void Prewarm() { }

        public static bool ShouldUseEmojiFont(string text, long peerId) {
            if (String.IsNullOrEmpty(text) || text.Length > MaxRichTextLength) return false;

            string pack = Settings.ResolvePeerEmojiPack(peerId);
            if (pack != EmojiPackIds.TelegramLike && pack != EmojiPackIds.Twemoji) return false;

            int index = 0;
            while (index < text.Length) {
                string textElement = StringInfo.GetNextTextElement(text, index);
                if (LooksLikeEmoji(textElement)) return true;
                index += textElement.Length;
            }

            return false;
        }

        public static bool TryApply(TextBlock target, string text, long peerId) {
            if (target == null || String.IsNullOrEmpty(text)) return false;
            if (text.Length > MaxRichTextLength) return false;

            string pack = Settings.ResolvePeerEmojiPack(peerId);
            if (IsCustomSpritePack(pack, peerId)) return TryApplyCustomSpritePack(target, text, pack, peerId);
            if (IsImageBackedPack(pack)) {
                return TryApplyPackImageInlines(target, text, pack, peerId)
                    || TryApplySystemEmojiRuns(target, text);
            }

            return false;
        }

        private static bool TryApplyCustomSpritePack(TextBlock target, string text, string pack, long peerId) {
            if (CountTextLetters(text, MaxCustomEmojiLetters) > MaxCustomEmojiLetters) return false;

            InlineCollection inlines = new InlineCollection();
            StringBuilder buffer = StringBuilderCache.Acquire(Math.Min(text.Length, StringBuilderCache.MaxBuilderSize));
            int richEmojis = 0;
            int index = 0;

            while (index < text.Length) {
                if (TryMatchEmoji(text, index, pack, peerId, out string emoji, out Uri imageUri)) {
                    if (richEmojis < MaxCustomInlineImages) {
                        FlushText(inlines, ref buffer);
                        Image image = new Image {
                            Width = EmojiSize,
                            Height = EmojiSize,
                            Stretch = Stretch.Uniform,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Avalonia.Thickness(1, 0)
                        };
                        ImageLoader.SetSource(image, imageUri);
                        inlines.Add(image);
                        richEmojis++;
                    } else {
                        buffer.Append(emoji);
                    }

                    index += emoji.Length;
                    continue;
                }

                int charCount = GetTextElementCharCount(text, index);
                buffer.Append(text, index, charCount);
                index += charCount;
            }

            if (richEmojis == 0) {
                StringBuilderCache.Release(buffer);
                return false;
            }

            FlushFinalText(inlines, buffer);
            target.Inlines = inlines;
            return true;
        }

        private static bool TryApplyPackImageInlines(TextBlock target, string text, string pack, long peerId) {
            if (text.Length > MaxPackImageTextLength) return false;
            if (CountTextLetters(text, MaxPackEmojiLetters) > MaxPackEmojiLetters) return false;

            InlineCollection inlines = new InlineCollection();
            StringBuilder buffer = StringBuilderCache.Acquire(Math.Min(text.Length, StringBuilderCache.MaxBuilderSize));
            int richEmojis = 0;
            int index = 0;

            while (index < text.Length) {
                string textElement = StringInfo.GetNextTextElement(text, index);
                if (LooksLikeEmoji(textElement) && richEmojis < MaxPackInlineImages) {
                    Uri imageUri = EmojiAssetResolver.ResolveImageUri(textElement, pack, peerId);
                    if (imageUri != null) {
                        FlushText(inlines, ref buffer);
                        Image image = new Image {
                            Width = EmojiSize,
                            Height = EmojiSize,
                            Stretch = Stretch.Uniform,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Avalonia.Thickness(1, 0)
                        };
                        ImageLoader.SetSource(image, imageUri);
                        inlines.Add(image);
                        richEmojis++;
                        index += textElement.Length;
                        continue;
                    }
                }

                buffer.Append(textElement);
                index += textElement.Length;
            }

            if (richEmojis == 0) {
                StringBuilderCache.Release(buffer);
                return false;
            }

            FlushFinalText(inlines, buffer);
            target.Inlines = inlines;
            return true;
        }

        private static bool TryApplySystemEmojiRuns(TextBlock target, string text) {
            if (CountTextLetters(text, MaxSystemEmojiLetters) > MaxSystemEmojiLetters) return false;

            InlineCollection inlines = new InlineCollection();
            StringBuilder buffer = StringBuilderCache.Acquire(Math.Min(text.Length, StringBuilderCache.MaxBuilderSize));
            bool hasEmoji = false;
            int index = 0;

            while (index < text.Length) {
                string textElement = StringInfo.GetNextTextElement(text, index);
                if (LooksLikeEmoji(textElement)) {
                    FlushText(inlines, ref buffer);
                    inlines.Add(new Run {
                        Text = textElement,
                        FontFamily = EmojiFontFamily
                    });
                    hasEmoji = true;
                } else {
                    buffer.Append(textElement);
                }

                index += textElement.Length;
            }

            if (!hasEmoji) {
                StringBuilderCache.Release(buffer);
                return false;
            }

            FlushFinalText(inlines, buffer);
            target.Inlines = inlines;
            return true;
        }

        private static bool IsCustomSpritePack(string pack, long peerId) {
            return pack == EmojiPackIds.Custom && EmojiSpriteStore.HasSprites(peerId);
        }

        private static bool IsImageBackedPack(string pack) {
            return pack == EmojiPackIds.TelegramLike || pack == EmojiPackIds.Twemoji;
        }

        private static bool TryMatchEmoji(string text, int index, string pack, long peerId, out string emoji, out Uri imageUri) {
            emoji = null;
            imageUri = null;

            if (pack == EmojiPackIds.Custom) {
                if (!EmojiSpriteStore.TryMatch(text, index, peerId, out EmojiSpriteMatch match)) return false;

                emoji = match.Emoji;
                return EmojiSpriteStore.TryResolveSpriteUri(emoji, peerId, out imageUri);
            }

            string textElement = StringInfo.GetNextTextElement(text, index);
            if (!LooksLikeEmoji(textElement)) return false;

            emoji = textElement;
            return true;
        }

        private static void FlushText(InlineCollection inlines, ref StringBuilder buffer) {
            if (buffer.Length == 0) return;

            inlines.Add(StringBuilderCache.GetStringAndRelease(buffer));
            buffer = StringBuilderCache.Acquire();
        }

        private static void FlushFinalText(InlineCollection inlines, StringBuilder buffer) {
            if (buffer.Length == 0) {
                StringBuilderCache.Release(buffer);
                return;
            }

            inlines.Add(StringBuilderCache.GetStringAndRelease(buffer));
        }

        private static int GetTextElementCharCount(string text, int index) {
            return StringInfo.GetNextTextElement(text, index).Length;
        }

        private static int CountTextLetters(string text, int limit) {
            int count = 0;
            foreach (char c in text) {
                if (!Char.IsLetterOrDigit(c)) continue;
                count++;
                if (count > limit) return count;
            }

            return count;
        }

        private static bool LooksLikeEmoji(string textElement) {
            bool hasEmojiCodepoint = false;
            bool hasEmojiSelector = false;

            foreach (Rune rune in textElement.EnumerateRunes()) {
                int value = rune.Value;
                if (value == 0xFE0F || value == 0x200D || value == 0x20E3) hasEmojiSelector = true;
                if (IsEmojiCodepoint(value)) hasEmojiCodepoint = true;
            }

            return hasEmojiCodepoint || hasEmojiSelector;
        }

        private static bool IsEmojiCodepoint(int value) {
            return value is >= 0x1F000 and <= 0x1FAFF
                || value is >= 0x2600 and <= 0x27BF
                || value is >= 0x2300 and <= 0x23FF
                || value is >= 0x1F1E6 and <= 0x1F1FF;
        }
    }
}
