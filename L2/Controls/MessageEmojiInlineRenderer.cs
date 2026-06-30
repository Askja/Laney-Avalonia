using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ELOR.Laney.Core;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ELOR.Laney.Controls {
    internal static class MessageEmojiInlineRenderer {
        private const int MaxRichTextLength = 720;
        private const int MaxCustomInlineImages = 1;
        private const int MaxPackInlineImages = 4;
        private const int MaxCustomEmojiLetters = 32;
        private const int MaxPackEmojiLetters = 48;
        private const int MaxPackImageTextLength = 220;
        private const int MaxSystemEmojiLetters = 180;
        private const double EmojiSize = 20;
        private static readonly SemaphoreSlim InlineEmojiLoadGate = new SemaphoreSlim(1, 1);
        private static long inlineEmojiLoadSequence;
        private static readonly FontFamily SystemEmojiFontFamily = new FontFamily("Segoe UI Emoji, Apple Color Emoji, Noto Color Emoji, Twemoji Mozilla, EmojiOne Color");
        private static readonly FontFamily TelegramEmojiFontFamily = new FontFamily("Apple Color Emoji, Segoe UI Emoji, Noto Color Emoji, Twemoji Mozilla, EmojiOne Color");

        public static FontFamily EmojiTextFontFamily => SystemEmojiFontFamily;

        public static FontFamily GetEmojiTextFontFamily(string packId) {
            string pack = EmojiPackIds.Normalize(packId);
            return pack == EmojiPackIds.TelegramLike ? TelegramEmojiFontFamily : SystemEmojiFontFamily;
        }

        public static void Prewarm() { }

        public static bool ShouldUseEmojiFont(string text, long peerId) {
            if (String.IsNullOrEmpty(text) || text.Length > MaxRichTextLength) return false;

            string pack = Settings.ResolvePeerEmojiPack(peerId);
            if (pack == EmojiPackIds.Fallback || pack == EmojiPackIds.Custom) return false;

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
                    || TryApplyEmojiFontRuns(target, text, GetEmojiTextFontFamily(pack));
            }

            return pack != EmojiPackIds.Fallback && TryApplyEmojiFontRuns(target, text, GetEmojiTextFontFamily(pack));
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
                        inlines.Add(CreateInlineEmojiImage(imageUri));
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
                    IReadOnlyList<Uri> imageUris = EmojiAssetResolver.ResolveImageUris(textElement, pack, peerId);
                    if (imageUris.Count > 0) {
                        FlushText(inlines, ref buffer);
                        inlines.Add(CreateInlineEmojiImage(imageUris));
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

        private static bool TryApplyEmojiFontRuns(TextBlock target, string text, FontFamily fontFamily) {
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
                        FontFamily = fontFamily
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
            return pack == EmojiPackIds.Vk
                || pack == EmojiPackIds.TelegramLike
                || pack == EmojiPackIds.Noto
                || pack == EmojiPackIds.Twemoji;
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

        private static Image CreateInlineEmojiImage(Uri imageUri) {
            return CreateInlineEmojiImage(new[] { imageUri });
        }

        private static Image CreateInlineEmojiImage(IReadOnlyList<Uri> imageUris) {
            Image image = new Image {
                Width = EmojiSize,
                Height = EmojiSize,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Avalonia.Thickness(1, 0),
                Source = EmojiSpriteStore.PlaceholderImage
            };

            long sequence = Interlocked.Increment(ref inlineEmojiLoadSequence);
            _ = LoadInlineEmojiImageAsync(new WeakReference<Image>(image), imageUris, sequence);
            return image;
        }

        private static async Task LoadInlineEmojiImageAsync(WeakReference<Image> imageReference, IReadOnlyList<Uri> imageUris, long sequence) {
            if (imageReference == null || imageUris == null || imageUris.Count == 0) return;

            try {
                int delayMs = (int)(Math.Min(sequence % 16, 15) * 12);
                if (delayMs > 0) await Task.Delay(delayMs).ConfigureAwait(false);
                if (!imageReference.TryGetTarget(out Image image)) return;

                await InlineEmojiLoadGate.WaitAsync().ConfigureAwait(false);
                try {
                    using IDisposable lease = await MediaMemoryGovernor.EnterMediaLoadAsync(CancellationToken.None).ConfigureAwait(false);
                    var bitmap = await LoadFirstAvailableEmojiBitmapAsync(imageUris).ConfigureAwait(false);
                    if (bitmap == null) return;

                    await Dispatcher.UIThread.InvokeAsync(() => {
                        if (imageReference.TryGetTarget(out Image aliveImage)) aliveImage.Source = bitmap;
                    }, DispatcherPriority.Background);
                } finally {
                    InlineEmojiLoadGate.Release();
                }
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot load inline emoji image candidates: {ImageUris}", String.Join(", ", imageUris));
            }
        }

        private static async Task<Avalonia.Media.Imaging.Bitmap> LoadFirstAvailableEmojiBitmapAsync(IReadOnlyList<Uri> imageUris) {
            foreach (Uri imageUri in imageUris) {
                var bitmap = await BitmapManager.GetBitmapAsync(imageUri, EmojiSize, EmojiSize, CancellationToken.None, BitmapCacheKind.Emoji, false).ConfigureAwait(false);
                if (bitmap != null) return bitmap;
            }

            return null;
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
