using ELOR.Laney.Core;
using System;
using System.IO;

namespace ELOR.Laney.DataModels {
    public sealed class FrequentLocalFile {
        public string FilePath { get; set; }
        public string Name { get; set; }
        public string Extension { get; set; }
        public long Size { get; set; }
        public int UploadType { get; set; }
        public int UseCount { get; set; }
        public DateTimeOffset LastUsedAt { get; set; }

        public string DisplayTitle => String.IsNullOrWhiteSpace(Name) ? Path.GetFileName(FilePath) : Name;
        public string UploadKind => UploadType switch {
            Constants.PhotoUploadCommand => "Фото",
            Constants.VideoUploadCommand => "Видео",
            Constants.FileUploadCommand => "Документ",
            Constants.GraffitiUploadCommand => "Граффити",
            Constants.AudioMessageUploadCommand => "Голосовое",
            _ => "Файл"
        };

        public string DisplaySubtitle {
            get {
                string size = Size > 0 ? FrequentLocalFileStore.FormatBytes(Size) + " · " : String.Empty;
                string used = UseCount == 1 ? "1 раз" : $"{UseCount} раз";
                return $"{UploadKind} · {size}{used} · {LastUsedAt.LocalDateTime:g}";
            }
        }

        public string IconId => UploadType switch {
            Constants.PhotoUploadCommand => VKUI.Controls.VKIconNames.Icon20PictureOutline,
            Constants.VideoUploadCommand => VKUI.Controls.VKIconNames.Icon20VideoOutline,
            Constants.GraffitiUploadCommand => VKUI.Controls.VKIconNames.Icon24BrushOutline,
            Constants.AudioMessageUploadCommand => VKUI.Controls.VKIconNames.Icon24VoiceOutline,
            _ => VKUI.Controls.VKIconNames.Icon20DocumentOutline
        };
    }
}
