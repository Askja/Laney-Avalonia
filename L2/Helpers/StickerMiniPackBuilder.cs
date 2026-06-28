using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ELOR.Laney.Helpers {
    public static class StickerMiniPackBuilder {
        private const int TargetSize = 512;
        private const int Padding = 34;
        private const int OutlineRadius = 5;
        private const int BackgroundThreshold = 42;
        private const int AlphaThreshold = 12;
        private const int MaxSourcePixels = 4096 * 4096;

        public static async Task<List<IStorageFile>> BuildAsync(IStorageProvider storageProvider, IEnumerable<IStorageFile> files) {
            List<IStorageFile> result = new List<IStorageFile>();
            if (storageProvider == null || files == null) return result;

            string directory = Path.Combine(Path.GetTempPath(), "Laney", "MiniPacks");
            Directory.CreateDirectory(directory);

            foreach (IStorageFile file in files) {
                try {
                    string path = await BuildOneAsync(storageProvider, file, directory);
                    IStorageFile output = await storageProvider.TryGetFileFromPathAsync(path);
                    if (output != null) result.Add(output);
                } catch (Exception ex) {
                    Log.Warning(ex, "Cannot build mini-pack sticker from {FileName}", file?.Name);
                }
            }

            return result;
        }

        private static async Task<string> BuildOneAsync(IStorageProvider storageProvider, IStorageFile file, string directory) {
            using Stream stream = await file.OpenReadAsync();
            using Bitmap bitmap = new Bitmap(stream);

            PixelSize size = bitmap.PixelSize;
            if (size.Width <= 0 || size.Height <= 0 || size.Width * size.Height > MaxSourcePixels) {
                throw new InvalidOperationException("Image is empty or too large for mini-pack builder.");
            }

            byte[] pixels = CopyPixels(bitmap, size);
            RemoveBackgroundByCorners(pixels, size.Width, size.Height);
            PixelRect crop = FindContentBounds(pixels, size.Width, size.Height);
            byte[] sticker = BuildStickerPixels(pixels, size.Width, size.Height, crop);

            string safeName = GetSafeFileName(Path.GetFileNameWithoutExtension(file.Name));
            string path = Path.Combine(directory, $"{safeName}-{Guid.NewGuid():N}.png");
            SavePixels(path, sticker);
            return path;
        }

        private static byte[] CopyPixels(Bitmap bitmap, PixelSize size) {
            int stride = size.Width * 4;
            byte[] pixels = new byte[stride * size.Height];
            GCHandle handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try {
                bitmap.CopyPixels(new PixelRect(0, 0, size.Width, size.Height), handle.AddrOfPinnedObject(), pixels.Length, stride);
            } finally {
                handle.Free();
            }

            return pixels;
        }

        private static void RemoveBackgroundByCorners(byte[] pixels, int width, int height) {
            List<(int B, int G, int R)> samples = new List<(int B, int G, int R)>();
            AddCornerSample(samples, pixels, width, height, 0, 0);
            AddCornerSample(samples, pixels, width, height, width - 1, 0);
            AddCornerSample(samples, pixels, width, height, 0, height - 1);
            AddCornerSample(samples, pixels, width, height, width - 1, height - 1);
            if (samples.Count == 0) return;

            int bgB = (int)Math.Round(samples.Average(s => s.B));
            int bgG = (int)Math.Round(samples.Average(s => s.G));
            int bgR = (int)Math.Round(samples.Average(s => s.R));

            for (int i = 0; i < pixels.Length; i += 4) {
                int alpha = pixels[i + 3];
                if (alpha < 245) continue;

                int db = pixels[i] - bgB;
                int dg = pixels[i + 1] - bgG;
                int dr = pixels[i + 2] - bgR;
                double distance = Math.Sqrt(db * db + dg * dg + dr * dr);
                if (distance <= BackgroundThreshold) {
                    pixels[i] = 0;
                    pixels[i + 1] = 0;
                    pixels[i + 2] = 0;
                    pixels[i + 3] = 0;
                }
            }
        }

        private static void AddCornerSample(List<(int B, int G, int R)> samples, byte[] pixels, int width, int height, int x, int y) {
            int index = GetIndex(width, x, y);
            if (pixels[index + 3] < 245) return;
            samples.Add((pixels[index], pixels[index + 1], pixels[index + 2]));
        }

        private static PixelRect FindContentBounds(byte[] pixels, int width, int height) {
            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    int alpha = pixels[GetIndex(width, x, y) + 3];
                    if (alpha <= AlphaThreshold) continue;

                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }

            if (maxX < minX || maxY < minY) return new PixelRect(0, 0, width, height);
            return new PixelRect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private static byte[] BuildStickerPixels(byte[] source, int width, int height, PixelRect crop) {
            byte[] layer = new byte[TargetSize * TargetSize * 4];
            int available = TargetSize - Padding * 2;
            double scale = Math.Min((double)available / crop.Width, (double)available / crop.Height);
            int scaledWidth = Math.Max(1, (int)Math.Round(crop.Width * scale));
            int scaledHeight = Math.Max(1, (int)Math.Round(crop.Height * scale));
            int offsetX = (TargetSize - scaledWidth) / 2;
            int offsetY = (TargetSize - scaledHeight) / 2;

            for (int y = 0; y < scaledHeight; y++) {
                double sourceY = crop.Y + (y + 0.5) / scale - 0.5;
                for (int x = 0; x < scaledWidth; x++) {
                    double sourceX = crop.X + (x + 0.5) / scale - 0.5;
                    int targetIndex = GetIndex(TargetSize, offsetX + x, offsetY + y);
                    SampleBilinear(source, width, height, sourceX, sourceY, layer, targetIndex);
                }
            }

            byte[] output = BuildOutline(layer);
            for (int i = 0; i < layer.Length; i += 4) {
                if (layer[i + 3] <= AlphaThreshold) continue;
                output[i] = layer[i];
                output[i + 1] = layer[i + 1];
                output[i + 2] = layer[i + 2];
                output[i + 3] = layer[i + 3];
            }

            return output;
        }

        private static void SampleBilinear(byte[] source, int width, int height, double x, double y, byte[] target, int targetIndex) {
            int x0 = Math.Clamp((int)Math.Floor(x), 0, width - 1);
            int y0 = Math.Clamp((int)Math.Floor(y), 0, height - 1);
            int x1 = Math.Clamp(x0 + 1, 0, width - 1);
            int y1 = Math.Clamp(y0 + 1, 0, height - 1);
            double tx = Math.Clamp(x - x0, 0, 1);
            double ty = Math.Clamp(y - y0, 0, 1);

            for (int c = 0; c < 4; c++) {
                double a = source[GetIndex(width, x0, y0) + c] * (1 - tx) + source[GetIndex(width, x1, y0) + c] * tx;
                double b = source[GetIndex(width, x0, y1) + c] * (1 - tx) + source[GetIndex(width, x1, y1) + c] * tx;
                target[targetIndex + c] = (byte)Math.Clamp((int)Math.Round(a * (1 - ty) + b * ty), 0, 255);
            }
        }

        private static byte[] BuildOutline(byte[] layer) {
            byte[] output = new byte[layer.Length];
            int radiusSq = OutlineRadius * OutlineRadius;

            for (int y = 0; y < TargetSize; y++) {
                for (int x = 0; x < TargetSize; x++) {
                    int index = GetIndex(TargetSize, x, y);
                    if (layer[index + 3] > AlphaThreshold) continue;
                    if (!HasOpaqueNeighbor(layer, x, y, radiusSq)) continue;

                    output[index] = 255;
                    output[index + 1] = 255;
                    output[index + 2] = 255;
                    output[index + 3] = 255;
                }
            }

            return output;
        }

        private static bool HasOpaqueNeighbor(byte[] layer, int x, int y, int radiusSq) {
            for (int dy = -OutlineRadius; dy <= OutlineRadius; dy++) {
                int ny = y + dy;
                if (ny < 0 || ny >= TargetSize) continue;

                for (int dx = -OutlineRadius; dx <= OutlineRadius; dx++) {
                    if (dx * dx + dy * dy > radiusSq) continue;
                    int nx = x + dx;
                    if (nx < 0 || nx >= TargetSize) continue;
                    if (layer[GetIndex(TargetSize, nx, ny) + 3] > AlphaThreshold) return true;
                }
            }

            return false;
        }

        private static void SavePixels(string path, byte[] pixels) {
            using WriteableBitmap bitmap = new WriteableBitmap(new PixelSize(TargetSize, TargetSize), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            using (ILockedFramebuffer framebuffer = bitmap.Lock()) {
                Marshal.Copy(pixels, 0, framebuffer.Address, pixels.Length);
            }

            bitmap.Save(path);
        }

        private static int GetIndex(int width, int x, int y) {
            return (y * width + x) * 4;
        }

        private static string GetSafeFileName(string fileName) {
            if (String.IsNullOrWhiteSpace(fileName)) return "sticker";

            char[] invalid = Path.GetInvalidFileNameChars();
            string safe = new string(fileName.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
            return String.IsNullOrWhiteSpace(safe) ? "sticker" : safe;
        }
    }
}
