using System;
using System.IO;

namespace ELOR.Laney.DataModels {
    public sealed class LocalSticker {
        public string Id { get; set; }
        public string Title { get; set; }
        public string FilePath { get; set; }
        public string Extension { get; set; }
        public string FallbackFilePath { get; set; }
        public string FallbackExtension { get; set; }
        public string Tags { get; set; }
        public string Source { get; set; }
        public string SourcePack { get; set; }
        public string SourceUrl { get; set; }
        public string SourceId { get; set; }
        public bool IsDisabled { get; set; }
        public int SortOrder { get; set; }
        public bool IsFavorite { get; set; }
        public int UseCount { get; set; }
        public DateTimeOffset LastUsedAt { get; set; }

        public bool CanPreview => IsRasterImageExtension(Extension) || HasRasterFallback;
        public bool CanUploadAsImageAttachment => IsPhotoAttachmentExtension(Extension);
        public bool FallbackCanUploadAsImageAttachment => IsPhotoAttachmentExtension(FallbackExtension);
        public bool HasRasterFallback => IsRasterImageExtension(FallbackExtension) && File.Exists(FallbackFilePath);
        public Uri PreviewUri {
            get {
                if (IsRasterImageExtension(Extension) && File.Exists(FilePath)) return new Uri(FilePath);
                return HasRasterFallback ? new Uri(FallbackFilePath) : null;
            }
        }
        public string DisplayKind => String.IsNullOrWhiteSpace(Extension) ? "FILE" : Extension.TrimStart('.').ToUpperInvariant();
        public bool IsTelegram => String.Equals(Source, "telegram", StringComparison.OrdinalIgnoreCase);
        public bool IsAnimated => IsLottieSticker || IsVideoSticker;
        public bool IsLottieSticker => IsExtension(".tgs");
        public bool IsVideoSticker => IsExtension(".webm");
        public bool CanInlineAnimate => IsLottieSticker && File.Exists(FilePath);
        public bool HasAnimationBadge => IsAnimated;
        public string AnimationBadge => IsLottieSticker ? "TGS" : IsVideoSticker ? "WEBM" : null;
        public bool HasSourceBadge => !String.IsNullOrWhiteSpace(SourceBadge);
        public string SourceBadge => IsTelegram ? "TG" : null;

        public bool ShouldUploadAsGraffiti() {
            string ext = Extension?.ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".webp";
        }

        public bool ShouldUploadFallbackAsGraffiti() {
            string ext = FallbackExtension?.ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".webp";
        }

        private static bool IsRasterImageExtension(string extension) {
            string ext = extension?.ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".webp" || ext == ".gif";
        }

        private static bool IsPhotoAttachmentExtension(string extension) {
            string ext = extension?.ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg";
        }

        private bool IsExtension(string extension) {
            return String.Equals(Extension, extension, StringComparison.OrdinalIgnoreCase);
        }
    }
}
