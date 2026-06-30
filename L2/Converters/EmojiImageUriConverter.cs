using Avalonia;
using Avalonia.Data.Converters;
using ELOR.Laney.Core;
using NeoSmart.Unicode;
using System;
using System.Globalization;

namespace ELOR.Laney.Converters {
    public sealed class EmojiImageUriConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            string emoji = value switch {
                SingleEmoji singleEmoji => singleEmoji.ToString(),
                string text => text,
                _ => null
            };

            long peerId = parameter switch {
                long longValue => longValue,
                int intValue => intValue,
                string textValue when Int64.TryParse(textValue, out long parsed) => parsed,
                _ => 0
            };

            Uri uri = EmojiAssetResolver.ResolvePickerImageUri(emoji, peerId);
            return uri ?? AvaloniaProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return AvaloniaProperty.UnsetValue;
        }
    }
}
