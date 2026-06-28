using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ELOR.Laney.Helpers {
    public sealed class ImageUploadOptimizationResult {
        public IStorageFile File { get; init; }
        public bool Optimized { get; init; }
        public string Report { get; init; }
    }

    public static class ImageUploadOptimizer {
        private const int MaxImageSide = 2560;
        private const long MinSavingsBytes = 128 * 1024;

        public static async Task<List<ImageUploadOptimizationResult>> OptimizeForPhotoUploadAsync(IStorageProvider storageProvider, IEnumerable<IStorageFile> files) {
            List<ImageUploadOptimizationResult> result = new List<ImageUploadOptimizationResult>();
            foreach (IStorageFile file in files) {
                result.Add(await TryOptimizeAsync(storageProvider, file));
            }

            return result;
        }

        private static async Task<ImageUploadOptimizationResult> TryOptimizeAsync(IStorageProvider storageProvider, IStorageFile file) {
            try {
                string extension = Path.GetExtension(file.Name);
                if (!IsSupportedImageExtension(extension)) return KeepOriginal(file);

                await using Stream sourceStream = await file.OpenReadAsync();
                long originalSize = sourceStream.CanSeek ? sourceStream.Length : 0;
                using Bitmap source = new Bitmap(sourceStream);
                PixelSize targetSize = GetTargetSize(source.PixelSize);

                if (targetSize == source.PixelSize && originalSize <= 0) return KeepOriginal(file);

                string directory = Path.Combine(Path.GetTempPath(), "Laney", "ImageUpload");
                Directory.CreateDirectory(directory);

                string path = Path.Combine(directory, $"laney-photo-{Guid.NewGuid():N}.jpg");
                if (targetSize == source.PixelSize) {
                    source.Save(path, 88);
                } else {
                    using Bitmap output = source.CreateScaledBitmap(targetSize, BitmapInterpolationMode.HighQuality);
                    output.Save(path, 88);
                }

                FileInfo optimizedInfo = new FileInfo(path);
                if (originalSize > 0 && optimizedInfo.Length >= originalSize - MinSavingsBytes) {
                    TryDelete(path);
                    return KeepOriginal(file);
                }

                IStorageFile optimizedFile = await storageProvider.TryGetFileFromPathAsync(path);
                if (optimizedFile == null) {
                    TryDelete(path);
                    return KeepOriginal(file);
                }

                return new ImageUploadOptimizationResult {
                    File = optimizedFile,
                    Optimized = true,
                    Report = BuildReport(file.Name, source.PixelSize, targetSize, originalSize, optimizedInfo.Length)
                };
            } catch (Exception ex) {
                Log.Warning(ex, "Unable to optimize image {FileName}. Original file will be uploaded.", file.Name);
                return KeepOriginal(file);
            }
        }

        private static bool IsSupportedImageExtension(string extension) {
            if (String.IsNullOrWhiteSpace(extension)) return false;
            return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase);
        }

        private static PixelSize GetTargetSize(PixelSize sourceSize) {
            if (sourceSize.Width <= 0 || sourceSize.Height <= 0) return sourceSize;

            int maxSide = Math.Max(sourceSize.Width, sourceSize.Height);
            if (maxSide <= MaxImageSide) return sourceSize;

            double scale = (double)MaxImageSide / maxSide;
            return new PixelSize(
                Math.Max(1, (int)Math.Round(sourceSize.Width * scale)),
                Math.Max(1, (int)Math.Round(sourceSize.Height * scale))
            );
        }

        private static ImageUploadOptimizationResult KeepOriginal(IStorageFile file) {
            return new ImageUploadOptimizationResult {
                File = file,
                Optimized = false,
                Report = null
            };
        }

        private static string BuildReport(string name, PixelSize original, PixelSize target, long originalSize, long optimizedSize) {
            string size = originalSize > 0
                ? $"{FormatBytes(originalSize)} -> {FormatBytes(optimizedSize)}"
                : $"итог {FormatBytes(optimizedSize)}";
            return $"{name}: {original.Width}x{original.Height} -> {target.Width}x{target.Height}, {size}";
        }

        private static string FormatBytes(long bytes) {
            if (bytes < 1024) return $"{bytes} Б";
            double kb = bytes / 1024.0;
            if (kb < 1024) return $"{kb:0.#} КБ";
            return $"{kb / 1024.0:0.##} МБ";
        }

        private static void TryDelete(string path) {
            try {
                if (File.Exists(path)) File.Delete(path);
            } catch {
                // temp cleanup is best-effort
            }
        }
    }
}
