using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Serilog;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ELOR.Laney.Helpers {
    public sealed class ImagePreSendEditOptions {
        public bool CropSquare { get; set; }
        public bool BlurCenter { get; set; }
        public bool PrivacyMask { get; set; }
        public bool Arrow { get; set; }
        public bool DrawMarker { get; set; }
        public string OverlayStickerPath { get; set; }
        public string Text { get; set; }
    }

    public static class ImagePreSendEditorHelper {
        private const int MaxPixelSide = 2048;
        private const int MaxSourcePixels = 4096 * 4096;

        public static async Task<IStorageFile> EditAsync(IStorageProvider storageProvider, IStorageFile file, ImagePreSendEditOptions options) {
            if (storageProvider == null || file == null || options == null) return null;

            string directory = Path.Combine(Path.GetTempPath(), "Laney", "ImageEditor");
            Directory.CreateDirectory(directory);

            string outputPath = Path.Combine(directory, $"{GetSafeFileName(Path.GetFileNameWithoutExtension(file.Name))}-{Guid.NewGuid():N}.png");

            try {
                using Stream stream = await file.OpenReadAsync();
                using Bitmap source = new Bitmap(stream);
                PixelSize sourceSize = source.PixelSize;
                if (sourceSize.Width <= 0 || sourceSize.Height <= 0 || sourceSize.Width * sourceSize.Height > MaxSourcePixels) {
                    throw new InvalidOperationException("Image is empty or too large for editor.");
                }

                byte[] sourcePixels = CopyPixels(source, sourceSize);
                PixelRect crop = options.CropSquare ? GetCenterSquare(sourceSize) : new PixelRect(0, 0, sourceSize.Width, sourceSize.Height);
                PixelSize outputSize = GetOutputSize(crop);
                byte[] pixels = Resample(sourcePixels, sourceSize.Width, sourceSize.Height, crop, outputSize.Width, outputSize.Height);

                if (options.BlurCenter) BlurRect(pixels, outputSize.Width, outputSize.Height, GetCenterPrivacyRect(outputSize), 8);
                if (options.PrivacyMask) FillRect(pixels, outputSize.Width, outputSize.Height, GetBottomPrivacyRect(outputSize), 0, 0, 0, 230);
                if (options.Arrow) DrawArrow(pixels, outputSize.Width, outputSize.Height);
                if (options.DrawMarker) DrawMarkerStroke(pixels, outputSize.Width, outputSize.Height);
                OverlaySticker(pixels, outputSize, options.OverlayStickerPath);

                if (String.IsNullOrWhiteSpace(options.Text)) {
                    SavePixels(outputPath, pixels, outputSize);
                } else {
                    await SavePixelsWithTextAsync(outputPath, pixels, outputSize, options.Text.Trim());
                }

                return await storageProvider.TryGetFileFromPathAsync(outputPath);
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot edit image {FileName} before send.", file.Name);
                throw;
            }
        }

        private static PixelRect GetCenterSquare(PixelSize size) {
            int side = Math.Min(size.Width, size.Height);
            return new PixelRect((size.Width - side) / 2, (size.Height - side) / 2, side, side);
        }

        private static PixelSize GetOutputSize(PixelRect crop) {
            int maxSide = Math.Max(crop.Width, crop.Height);
            if (maxSide <= MaxPixelSide) return new PixelSize(crop.Width, crop.Height);

            double scale = (double)MaxPixelSide / maxSide;
            return new PixelSize(
                Math.Max(1, (int)Math.Round(crop.Width * scale)),
                Math.Max(1, (int)Math.Round(crop.Height * scale)));
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

        private static byte[] Resample(byte[] source, int sourceWidth, int sourceHeight, PixelRect crop, int outputWidth, int outputHeight) {
            byte[] output = new byte[outputWidth * outputHeight * 4];
            double scaleX = (double)crop.Width / outputWidth;
            double scaleY = (double)crop.Height / outputHeight;

            for (int y = 0; y < outputHeight; y++) {
                double sourceY = crop.Y + (y + 0.5) * scaleY - 0.5;
                for (int x = 0; x < outputWidth; x++) {
                    double sourceX = crop.X + (x + 0.5) * scaleX - 0.5;
                    SampleBilinear(source, sourceWidth, sourceHeight, sourceX, sourceY, output, GetIndex(outputWidth, x, y));
                }
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

        private static PixelRect GetCenterPrivacyRect(PixelSize size) {
            return new PixelRect(size.Width / 4, size.Height / 3, size.Width / 2, size.Height / 4);
        }

        private static PixelRect GetBottomPrivacyRect(PixelSize size) {
            return new PixelRect(size.Width / 12, size.Height * 3 / 4, size.Width * 5 / 6, Math.Max(1, size.Height / 7));
        }

        private static void BlurRect(byte[] pixels, int width, int height, PixelRect rect, int radius) {
            byte[] copy = (byte[])pixels.Clone();
            int left = Math.Clamp(rect.X, 0, width - 1);
            int top = Math.Clamp(rect.Y, 0, height - 1);
            int right = Math.Clamp(rect.X + rect.Width, 0, width);
            int bottom = Math.Clamp(rect.Y + rect.Height, 0, height);

            for (int y = top; y < bottom; y++) {
                for (int x = left; x < right; x++) {
                    int count = 0;
                    int b = 0;
                    int g = 0;
                    int r = 0;
                    int a = 0;

                    for (int yy = Math.Max(top, y - radius); yy < Math.Min(bottom, y + radius + 1); yy++) {
                        for (int xx = Math.Max(left, x - radius); xx < Math.Min(right, x + radius + 1); xx++) {
                            int sourceIndex = GetIndex(width, xx, yy);
                            b += copy[sourceIndex];
                            g += copy[sourceIndex + 1];
                            r += copy[sourceIndex + 2];
                            a += copy[sourceIndex + 3];
                            count++;
                        }
                    }

                    int targetIndex = GetIndex(width, x, y);
                    pixels[targetIndex] = (byte)(b / count);
                    pixels[targetIndex + 1] = (byte)(g / count);
                    pixels[targetIndex + 2] = (byte)(r / count);
                    pixels[targetIndex + 3] = (byte)(a / count);
                }
            }
        }

        private static void FillRect(byte[] pixels, int width, int height, PixelRect rect, byte b, byte g, byte r, byte a) {
            int left = Math.Clamp(rect.X, 0, width - 1);
            int top = Math.Clamp(rect.Y, 0, height - 1);
            int right = Math.Clamp(rect.X + rect.Width, 0, width);
            int bottom = Math.Clamp(rect.Y + rect.Height, 0, height);

            for (int y = top; y < bottom; y++) {
                for (int x = left; x < right; x++) {
                    int index = GetIndex(width, x, y);
                    BlendPixel(pixels, index, b, g, r, a);
                }
            }
        }

        private static void DrawArrow(byte[] pixels, int width, int height) {
            int x1 = width / 8;
            int y1 = height / 5;
            int x2 = width * 3 / 4;
            int y2 = height / 2;
            int thickness = Math.Max(4, Math.Min(width, height) / 90);

            DrawLine(pixels, width, height, x1, y1, x2, y2, thickness, 20, 82, 255, 255);
            DrawLine(pixels, width, height, x2, y2, x2 - width / 9, y2 - height / 18, thickness, 20, 82, 255, 255);
            DrawLine(pixels, width, height, x2, y2, x2 - width / 14, y2 + height / 10, thickness, 20, 82, 255, 255);
        }

        private static void DrawMarkerStroke(byte[] pixels, int width, int height) {
            int thickness = Math.Max(5, Math.Min(width, height) / 70);
            Point[] points = [
                new Point(width * 0.12, height * 0.64),
                new Point(width * 0.28, height * 0.54),
                new Point(width * 0.46, height * 0.62),
                new Point(width * 0.64, height * 0.50),
                new Point(width * 0.84, height * 0.58)
            ];

            for (int i = 1; i < points.Length; i++) {
                DrawLine(
                    pixels,
                    width,
                    height,
                    (int)Math.Round(points[i - 1].X),
                    (int)Math.Round(points[i - 1].Y),
                    (int)Math.Round(points[i].X),
                    (int)Math.Round(points[i].Y),
                    thickness,
                    48,
                    200,
                    255,
                    210);
            }
        }

        private static void OverlaySticker(byte[] pixels, PixelSize outputSize, string stickerPath) {
            if (String.IsNullOrWhiteSpace(stickerPath) || !File.Exists(stickerPath)) return;

            try {
                using FileStream stream = File.OpenRead(stickerPath);
                using Bitmap sticker = new Bitmap(stream);
                PixelSize stickerSize = sticker.PixelSize;
                if (stickerSize.Width <= 0 || stickerSize.Height <= 0) return;

                byte[] stickerPixels = CopyPixels(sticker, stickerSize);
                int maxSide = Math.Max(48, Math.Min(outputSize.Width, outputSize.Height) / 4);
                double scale = Math.Min((double)maxSide / stickerSize.Width, (double)maxSide / stickerSize.Height);
                int overlayWidth = Math.Max(1, (int)Math.Round(stickerSize.Width * scale));
                int overlayHeight = Math.Max(1, (int)Math.Round(stickerSize.Height * scale));
                byte[] overlayPixels = Resample(stickerPixels, stickerSize.Width, stickerSize.Height, new PixelRect(0, 0, stickerSize.Width, stickerSize.Height), overlayWidth, overlayHeight);

                int margin = Math.Max(8, Math.Min(outputSize.Width, outputSize.Height) / 30);
                int x = Math.Max(0, outputSize.Width - overlayWidth - margin);
                int y = margin;
                BlendImage(pixels, outputSize.Width, outputSize.Height, overlayPixels, overlayWidth, overlayHeight, x, y);
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot overlay sticker {StickerPath} before send.", stickerPath);
            }
        }

        private static void BlendImage(byte[] target, int targetWidth, int targetHeight, byte[] source, int sourceWidth, int sourceHeight, int x, int y) {
            for (int sy = 0; sy < sourceHeight; sy++) {
                int ty = y + sy;
                if (ty < 0 || ty >= targetHeight) continue;

                for (int sx = 0; sx < sourceWidth; sx++) {
                    int tx = x + sx;
                    if (tx < 0 || tx >= targetWidth) continue;

                    int sourceIndex = GetIndex(sourceWidth, sx, sy);
                    int targetIndex = GetIndex(targetWidth, tx, ty);
                    BlendPixel(target, targetIndex, source[sourceIndex], source[sourceIndex + 1], source[sourceIndex + 2], source[sourceIndex + 3]);
                }
            }
        }

        private static void DrawLine(byte[] pixels, int width, int height, int x1, int y1, int x2, int y2, int thickness, byte b, byte g, byte r, byte a) {
            int dx = Math.Abs(x2 - x1);
            int sx = x1 < x2 ? 1 : -1;
            int dy = -Math.Abs(y2 - y1);
            int sy = y1 < y2 ? 1 : -1;
            int err = dx + dy;

            while (true) {
                FillCircle(pixels, width, height, x1, y1, thickness, b, g, r, a);
                if (x1 == x2 && y1 == y2) break;

                int e2 = 2 * err;
                if (e2 >= dy) {
                    err += dy;
                    x1 += sx;
                }
                if (e2 <= dx) {
                    err += dx;
                    y1 += sy;
                }
            }
        }

        private static void FillCircle(byte[] pixels, int width, int height, int cx, int cy, int radius, byte b, byte g, byte r, byte a) {
            int radiusSq = radius * radius;
            for (int y = cy - radius; y <= cy + radius; y++) {
                if (y < 0 || y >= height) continue;
                for (int x = cx - radius; x <= cx + radius; x++) {
                    if (x < 0 || x >= width) continue;
                    int dx = x - cx;
                    int dy = y - cy;
                    if (dx * dx + dy * dy > radiusSq) continue;
                    BlendPixel(pixels, GetIndex(width, x, y), b, g, r, a);
                }
            }
        }

        private static void BlendPixel(byte[] pixels, int index, byte b, byte g, byte r, byte a) {
            double alpha = a / 255.0;
            pixels[index] = (byte)Math.Clamp((int)Math.Round(b * alpha + pixels[index] * (1 - alpha)), 0, 255);
            pixels[index + 1] = (byte)Math.Clamp((int)Math.Round(g * alpha + pixels[index + 1] * (1 - alpha)), 0, 255);
            pixels[index + 2] = (byte)Math.Clamp((int)Math.Round(r * alpha + pixels[index + 2] * (1 - alpha)), 0, 255);
            pixels[index + 3] = 255;
        }

        private static async Task SavePixelsWithTextAsync(string path, byte[] pixels, PixelSize size, string text) {
            using WriteableBitmap baseBitmap = CreateBitmap(pixels, size);
            using RenderTargetBitmap target = new RenderTargetBitmap(size, new Vector(96, 96));
            using (DrawingContext context = target.CreateDrawingContext(true)) {
                Rect bounds = new Rect(0, 0, size.Width, size.Height);
                context.DrawImage(baseBitmap, bounds);

                double fontSize = Math.Clamp(size.Width / 18.0, 20, 64);
                FormattedText shadow = CreateFormattedText(text, fontSize, Brushes.Black, size.Width * 0.86);
                FormattedText foreground = CreateFormattedText(text, fontSize, Brushes.White, size.Width * 0.86);
                double x = size.Width * 0.07;
                double y = Math.Max(8, size.Height - foreground.Height - size.Height * 0.08);

                context.DrawRectangle(new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), null, new Rect(x - 10, y - 8, foreground.Width + 20, foreground.Height + 16), 10, 10);
                context.DrawText(shadow, new Point(x + 2, y + 2));
                context.DrawText(foreground, new Point(x, y));
            }

            await using FileStream output = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            target.Save(output);
        }

        private static FormattedText CreateFormattedText(string text, double fontSize, IBrush brush, double maxWidth) {
            return new FormattedText(
                text,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold, FontStretch.Normal),
                fontSize,
                brush) {
                MaxTextWidth = maxWidth,
                MaxLineCount = 2,
                Trimming = TextTrimming.CharacterEllipsis
            };
        }

        private static void SavePixels(string path, byte[] pixels, PixelSize size) {
            using WriteableBitmap bitmap = CreateBitmap(pixels, size);
            bitmap.Save(path);
        }

        private static WriteableBitmap CreateBitmap(byte[] pixels, PixelSize size) {
            WriteableBitmap bitmap = new WriteableBitmap(size, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            using ILockedFramebuffer framebuffer = bitmap.Lock();
            Marshal.Copy(pixels, 0, framebuffer.Address, pixels.Length);
            return bitmap;
        }

        private static int GetIndex(int width, int x, int y) {
            return (y * width + x) * 4;
        }

        private static string GetSafeFileName(string fileName) {
            if (String.IsNullOrWhiteSpace(fileName)) return "image";

            char[] invalid = Path.GetInvalidFileNameChars();
            string safe = new string(fileName.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
            return String.IsNullOrWhiteSpace(safe) ? "image" : safe;
        }
    }
}
