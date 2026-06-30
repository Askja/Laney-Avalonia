using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Svg.Skia;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ELOR.Laney.Core;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using VKUI.Controls;

namespace ELOR.Laney.Controls {
    public static class ImageLoader {
        private const double ViewportPreloadMargin = 192;
        private const double MaxBackgroundDecodePixels = 384;
        private const double MaxBlurBackgroundDecodePixels = 128;
        private const double ActiveViewportCheckIntervalMs = 240;

        static ImageLoader() {
            SourceProperty.Changed
                .Subscribe(args => OnSourceChanged((Image)args.Sender, args.NewValue.Value));

            SourceCandidatesProperty.Changed
                .Subscribe(args => OnSourceCandidatesChanged((Image)args.Sender, args.NewValue.Value));

            SvgSourceProperty.Changed
                .Subscribe(args => OnSvgSourceChanged((Image)args.Sender, args.NewValue.Value));

            BackgroundSourceProperty.Changed
                .Subscribe(args => OnBackgroundSourceChanged((Control)args.Sender, args.NewValue.Value));

            BackgroundBlurRadiusProperty.Changed
                .Subscribe(args => OnBackgroundBlurRadiusChanged((Control)args.Sender));

            FillSourceProperty.Changed
                .Subscribe(args => OnFillSourceChanged((Shape)args.Sender, args.NewValue.Value));

            ImageProperty.Changed
                .Subscribe(args => OnImageChanged((Avatar)args.Sender, args.NewValue.Value));
        }

        private static void OnAttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e) {
            Control control = sender as Control;
            control.AttachedToVisualTree -= OnAttachedToVisualTree;
            LoadCurrentSource(control);
        }

        private static void OnDetachedFromVisualTree(object sender, VisualTreeAttachmentEventArgs e) {
            if (sender is not Control control) return;
            CancelLoad(control);
            ClearLoadedImage(control);
            control.LayoutUpdated -= OnActiveImageLayoutUpdated;
            control.LayoutUpdated -= OnDeferredImageLayoutUpdated;

            control.AttachedToVisualTree -= OnAttachedToVisualTree;
            control.AttachedToVisualTree += OnAttachedToVisualTree;
        }

        private static void LoadCurrentSource(Control control) {
            switch (control) {
                case Image image when GetSvgSource(image) != null:
                    OnSvgSourceChanged(image, GetSvgSource(image));
                    break;
                case Image image when HasSourceCandidates(GetSourceCandidates(image)):
                    OnSourceCandidatesChanged(image, GetSourceCandidates(image));
                    break;
                case Image image:
                    OnSourceChanged(image, GetSource(image));
                    break;
                case Avatar avatar:
                    OnImageChanged(avatar, GetImage(avatar));
                    break;
                case Shape shape:
                    OnFillSourceChanged(shape, GetFillSource(shape));
                    break;
                case Border border:
                    OnBackgroundSourceChanged(border, GetBackgroundSource(border));
                    break;
                case TemplatedControl templatedControl:
                    OnBackgroundSourceChanged(templatedControl, GetBackgroundSource(templatedControl));
                    break;
            }
        }

        private static bool EnsureAttached(Control control) {
            if (!control.IsAttachedToVisualTree()) {
                control.AttachedToVisualTree -= OnAttachedToVisualTree;
                control.AttachedToVisualTree += OnAttachedToVisualTree;
                return false;
            }

            if (!IsNearViewport(control)) {
                control.LayoutUpdated -= OnDeferredImageLayoutUpdated;
                control.LayoutUpdated += OnDeferredImageLayoutUpdated;
                return false;
            }

            control.LayoutUpdated -= OnDeferredImageLayoutUpdated;
            return true;
        }

        private static void OnDeferredImageLayoutUpdated(object sender, EventArgs e) {
            if (sender is not Control control) return;
            if (!IsNearViewport(control)) return;

            control.LayoutUpdated -= OnDeferredImageLayoutUpdated;
            LoadCurrentSource(control);
        }

        private static void OnActiveImageLayoutUpdated(object sender, EventArgs e) {
            if (sender is not Control control) return;
            if (!ShouldRunActiveViewportCheck(control)) return;
            if (IsNearViewport(control)) return;

            CancelLoad(control);
            ClearLoadedImage(control);
            control.LayoutUpdated -= OnActiveImageLayoutUpdated;
            control.LayoutUpdated -= OnDeferredImageLayoutUpdated;
            control.LayoutUpdated += OnDeferredImageLayoutUpdated;
        }

        private static bool IsNearViewport(Control control) {
            TopLevel topLevel = TopLevel.GetTopLevel(control);
            if (topLevel == null || control.Bounds.Width <= 0 || control.Bounds.Height <= 0) return true;

            Point? topLeft = control.TranslatePoint(new Point(0, 0), topLevel);
            if (topLeft == null) return true;

            Rect viewport = new Rect(0, 0, topLevel.Bounds.Width, topLevel.Bounds.Height).Inflate(ViewportPreloadMargin);
            Rect controlRect = new Rect(topLeft.Value, control.Bounds.Size);
            return viewport.Intersects(controlRect);
        }

        private static bool ShouldRunActiveViewportCheck(Control control) {
            long now = Stopwatch.GetTimestamp();
            long last = GetLastViewportCheckTicks(control);
            if (last != 0) {
                double elapsedMs = (now - last) * 1000.0 / Stopwatch.Frequency;
                if (elapsedMs < ActiveViewportCheckIntervalMs) return false;
            }

            SetLastViewportCheckTicks(control, now);
            return true;
        }

        private static ImageLoadState BeginLoad(Control control, bool showLoadingState = true) {
            CancelLoad(control);

            ImageLoadState state = new ImageLoadState();
            SetLoadState(control, state);
            if (showLoadingState) {
                SetLoadingPlaceholder(control);
                if (!control.Classes.Contains("ImageLoading")) control.Classes.Add("ImageLoading");
            } else {
                control.Classes.Remove("ImageLoading");
            }

            control.DetachedFromVisualTree -= OnDetachedFromVisualTree;
            control.DetachedFromVisualTree += OnDetachedFromVisualTree;
            control.LayoutUpdated -= OnActiveImageLayoutUpdated;
            control.LayoutUpdated += OnActiveImageLayoutUpdated;
            return state;
        }

        private static void FinishLoad(Control control, ImageLoadState state) {
            if (ReferenceEquals(GetLoadState(control), state)) {
                SetLoadState(control, null);
                control.Classes.Remove("ImageLoading");
                control.LayoutUpdated -= OnActiveImageLayoutUpdated;
            }

            state.Dispose();
        }

        private static void CancelLoad(Control control) {
            ImageLoadState state = GetLoadState(control);
            state?.Cancel();

            if (state != null && ReferenceEquals(GetLoadState(control), state)) {
                SetLoadState(control, null);
                control.Classes.Remove("ImageLoading");
            }

            control.LayoutUpdated -= OnActiveImageLayoutUpdated;
        }

        private static void SetLoadingPlaceholder(Control control) {
            IBrush brush = GetResourceFromControl<IBrush>(control, "LaneySkeletonBrush")
                ?? App.GetResource<IBrush>("LaneySkeletonBrush")
                ?? App.GetResource<IBrush>("VKSkeletonForegroundFromBrush")
                ?? new SolidColorBrush(Color.Parse("#2A3442"));

            switch (control) {
                case Shape shape:
                    shape.Fill = brush;
                    break;
                case Border border:
                    border.Background = brush;
                    break;
                case ContentControl contentControl:
                    contentControl.Background = brush;
                    break;
            }
        }

        private static T GetResourceFromControl<T>(Control control, string key) where T : class {
            if (control?.TryFindResource(key, out object resource) == true && resource is T typedResource) return typedResource;
            return null;
        }

        private static bool IsCurrent(Control control, ImageLoadState state, Uri uri) {
            return ReferenceEquals(GetLoadState(control), state)
                && !state.Token.IsCancellationRequested
                && IsCurrentUri(control, uri);
        }

        private static bool IsCurrentUri(Control control, Uri uri) {
            return control switch {
                Image image when GetSvgSource(image) != null => GetSvgSource(image) == uri,
                Image image when HasSourceCandidates(GetSourceCandidates(image)) => ContainsUri(GetSourceCandidates(image), uri),
                Image image => GetSource(image) == uri,
                Avatar avatar => GetImage(avatar) == uri,
                Shape shape => GetFillSource(shape) == uri,
                Border border => GetBackgroundSource(border) == uri,
                TemplatedControl templatedControl => GetBackgroundSource(templatedControl) == uri,
                _ => false
            };
        }

        private static void ClearLoadedImage(Control control) {
            switch (control) {
                case Image image:
                    image.Source = null;
                    break;
                case Avatar:
                    // Аватар не обнуляем: при смене темы/настроек visual tree может пересобраться,
                    // а пользователь не должен видеть пустую дырку до возврата bitmap из RAM-кэша.
                    break;
                case Shape shape:
                    shape.Fill = null;
                    break;
                case Border border:
                    ReplaceOwnedBackgroundBitmap(border, null);
                    border.Background = null;
                    break;
                case ContentControl contentControl:
                    ReplaceOwnedBackgroundBitmap(contentControl, null);
                    contentControl.Background = null;
                    break;
            }
        }

        private static void OnSourceChanged(Image sender, Uri? uri) {
            if (HasSourceCandidates(GetSourceCandidates(sender))) return;

            if (uri == null) {
                CancelLoad(sender);
                UnregisterLifecycle(sender);
                sender.Source = null;
                return;
            }

            if (!EnsureAttached(sender)) return;
            _ = LoadBitmapIntoImageAsync(sender, uri);
        }

        private static async Task LoadBitmapIntoImageAsync(Image sender, Uri uri) {
            ImageLoadState state = BeginLoad(sender);
            double decodeWidth = GetDecodeWidth(sender);
            double decodeHeight = GetDecodeHeight(sender);

            try {
                using IDisposable lease = await MediaMemoryGovernor.EnterMediaLoadAsync(state.Token);
                if (!IsCurrent(sender, state, uri) || !IsNearViewport(sender)) return;

                Bitmap bitmap = await BitmapManager.GetBitmapAsync(uri, decodeWidth, decodeHeight, state.Token, BitmapCacheKind.Attachment);
                if (!IsCurrent(sender, state, uri)) return;

                await Dispatcher.UIThread.InvokeAsync(() => {
                    if (IsCurrent(sender, state, uri)) sender.Source = bitmap;
                });
            } catch (OperationCanceledException) {
                // Виртуализация выгрузила контрол или URI поменялся. Это штатный сценарий.
            } catch (Exception ex) {
                Log.Error(ex, "Cannot set bitmap to Image!");
                await Dispatcher.UIThread.InvokeAsync(() => {
                    if (IsCurrent(sender, state, uri)) sender.Source = null;
                });
            } finally {
                FinishLoad(sender, state);
            }
        }

        private static void OnSourceCandidatesChanged(Image sender, IReadOnlyList<Uri>? uris) {
            if (!HasSourceCandidates(uris)) {
                Uri? fallback = GetSource(sender);
                if (fallback != null) {
                    OnSourceChanged(sender, fallback);
                    return;
                }

                CancelLoad(sender);
                UnregisterLifecycle(sender);
                sender.Source = null;
                return;
            }

            if (!EnsureAttached(sender)) return;
            _ = LoadBitmapCandidatesIntoImageAsync(sender, uris);
        }

        private static async Task LoadBitmapCandidatesIntoImageAsync(Image sender, IReadOnlyList<Uri> uris) {
            ImageLoadState state = BeginLoad(sender);
            Uri[] candidates = DeduplicateUris(uris);
            double decodeWidth = GetDecodeWidth(sender);
            double decodeHeight = GetDecodeHeight(sender);

            try {
                using IDisposable lease = await MediaMemoryGovernor.EnterMediaLoadAsync(state.Token);
                if (!IsCurrentCandidates(sender, state, candidates) || !IsNearViewport(sender)) return;

                Bitmap bitmap = null;
                foreach (Uri candidate in candidates) {
                    if (!IsCurrentCandidates(sender, state, candidates)) return;
                    bitmap = await BitmapManager.GetBitmapAsync(candidate, decodeWidth, decodeHeight, state.Token, BitmapCacheKind.Emoji, false);
                    if (bitmap != null) break;
                }

                if (bitmap == null) {
                    await Dispatcher.UIThread.InvokeAsync(() => {
                        if (IsCurrentCandidates(sender, state, candidates)) sender.Source = null;
                    });
                    return;
                }

                await Dispatcher.UIThread.InvokeAsync(() => {
                    if (IsCurrentCandidates(sender, state, candidates)) sender.Source = bitmap;
                });
            } catch (OperationCanceledException) {
                // Виртуализация выгрузила контрол или список кандидатов поменялся.
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot set candidate bitmap to Image!");
                await Dispatcher.UIThread.InvokeAsync(() => {
                    if (IsCurrentCandidates(sender, state, candidates)) sender.Source = null;
                });
            } finally {
                FinishLoad(sender, state);
            }
        }

        private static void OnSvgSourceChanged(Image sender, Uri? uri) {
            if (uri == null) {
                CancelLoad(sender);
                UnregisterLifecycle(sender);
                sender.Source = null;
                return;
            }

            if (!EnsureAttached(sender)) return;
            _ = LoadSvgIntoImageAsync(sender, uri);
        }

        private static async Task LoadSvgIntoImageAsync(Image sender, Uri uri) {
            ImageLoadState state = BeginLoad(sender);

            try {
                SvgImage image = await CacheManager.GetStaticReactionImageAsync(uri);
                if (image == null || !IsCurrent(sender, state, uri)) return;

                await Dispatcher.UIThread.InvokeAsync(() => {
                    if (IsCurrent(sender, state, uri)) sender.Source = image;
                });
            } catch (OperationCanceledException) {
            } catch (Exception ex) {
                Log.Error(ex, "Cannot set SVG to Image!");
            } finally {
                FinishLoad(sender, state);
            }
        }

        private static void OnBackgroundSourceChanged(Control sender, Uri? uri) {
            if (uri == null) {
                CancelLoad(sender);
                UnregisterLifecycle(sender);
                ClearLoadedImage(sender);
                return;
            }

            if (!EnsureAttached(sender)) return;
            _ = LoadBitmapIntoBackgroundAsync(sender, uri);
        }

        private static async Task LoadBitmapIntoBackgroundAsync(Control sender, Uri uri) {
            ImageLoadState state = BeginLoad(sender, false);
            double decodeWidth = GetDecodeWidth(sender);
            double decodeHeight = GetDecodeHeight(sender);
            int blurRadius = GetBackgroundBlurRadius(sender);
            if (blurRadius > 0) {
                decodeWidth = GetBlurDecodeSize(decodeWidth);
                decodeHeight = GetBlurDecodeSize(decodeHeight);
            } else {
                NormalizeBackgroundDecodeSize(sender, ref decodeWidth, ref decodeHeight);
            }

            try {
                using IDisposable lease = await MediaMemoryGovernor.EnterMediaLoadAsync(state.Token);
                if (!IsCurrent(sender, state, uri) || !IsNearViewport(sender)) return;

                Bitmap bitmap = await BitmapManager.GetBitmapAsync(uri, decodeWidth, decodeHeight, state.Token, BitmapCacheKind.Background);
                if (!IsCurrent(sender, state, uri)) return;
                if (bitmap == null) return;
                IDisposable ownedBitmap = null;
                if (blurRadius > 0) {
                    bitmap = CreateBlurredBitmap(bitmap, blurRadius);
                    ownedBitmap = bitmap;
                }

                ImageBrush brush = new ImageBrush(bitmap) {
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center,
                    Stretch = Stretch.UniformToFill
                };

                await Dispatcher.UIThread.InvokeAsync(() => {
                    if (IsCurrent(sender, state, uri)) {
                        SetBackground(sender, brush, ownedBitmap);
                    } else {
                        ownedBitmap?.Dispose();
                    }
                });
            } catch (OperationCanceledException) {
            } catch (Exception ex) {
                Log.Error(ex, "Cannot set bitmap background!");
            } finally {
                FinishLoad(sender, state);
            }
        }

        private static void OnFillSourceChanged(Shape sender, Uri? uri) {
            if (uri == null) {
                CancelLoad(sender);
                UnregisterLifecycle(sender);
                sender.Fill = null;
                return;
            }

            if (!EnsureAttached(sender)) return;
            SetLoadingPlaceholder(sender);
            _ = LoadBitmapIntoFillAsync(sender, uri);
        }

        private static void OnBackgroundBlurRadiusChanged(Control sender) {
            Uri uri = sender switch {
                Border border => GetBackgroundSource(border),
                TemplatedControl templatedControl => GetBackgroundSource(templatedControl),
                _ => null
            };
            if (uri != null) OnBackgroundSourceChanged(sender, uri);
        }

        private static async Task LoadBitmapIntoFillAsync(Shape sender, Uri uri) {
            ImageLoadState state = BeginLoad(sender);
            double decodeWidth = GetDecodeWidth(sender);
            double decodeHeight = GetDecodeHeight(sender);

            try {
                using IDisposable lease = await MediaMemoryGovernor.EnterMediaLoadAsync(state.Token);
                if (!IsCurrent(sender, state, uri) || !IsNearViewport(sender)) return;

                Bitmap bitmap = await BitmapManager.GetBitmapAsync(uri, decodeWidth, decodeHeight, state.Token, BitmapCacheKind.Attachment);
                if (!IsCurrent(sender, state, uri)) return;

                ImageBrush brush = new ImageBrush(bitmap) {
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center,
                    Stretch = Stretch.UniformToFill
                };

                await Dispatcher.UIThread.InvokeAsync(() => {
                    if (IsCurrent(sender, state, uri)) sender.Fill = brush;
                });
            } catch (OperationCanceledException) {
            } catch (Exception ex) {
                Log.Error(ex, "Cannot set bitmap fill!");
            } finally {
                FinishLoad(sender, state);
            }
        }

        private static void OnImageChanged(Avatar sender, Uri? uri) {
            if (uri == null) {
                CancelLoad(sender);
                UnregisterLifecycle(sender);
                sender.Image = null;
                return;
            }

            if (!EnsureAttached(sender)) return;
            _ = LoadBitmapIntoAvatarAsync(sender, uri);
        }

        private static async Task LoadBitmapIntoAvatarAsync(Avatar sender, Uri uri) {
            ImageLoadState state = BeginLoad(sender);
            double decodeWidth = GetDecodeWidth(sender);
            double decodeHeight = GetDecodeHeight(sender);

            try {
                using IDisposable lease = await MediaMemoryGovernor.EnterMediaLoadAsync(state.Token);
                if (!IsCurrent(sender, state, uri) || !IsNearViewport(sender)) return;

                Bitmap bitmap = await BitmapManager.GetBitmapAsync(uri, decodeWidth, decodeHeight, state.Token, BitmapCacheKind.Avatar);
                if (!IsCurrent(sender, state, uri)) return;

                await Dispatcher.UIThread.InvokeAsync(() => {
                    if (IsCurrent(sender, state, uri)) sender.Image = bitmap;
                });
            } catch (OperationCanceledException) {
            } catch (Exception ex) {
                Log.Error(ex, "Cannot set bitmap to Avatar!");
            } finally {
                FinishLoad(sender, state);
            }
        }

        private static void SetBackground(Control control, IBrush brush, IDisposable ownedBitmap = null) {
            ReplaceOwnedBackgroundBitmap(control, ownedBitmap);

            switch (control) {
                case Border border:
                    border.Background = brush;
                    break;
                case ContentControl contentControl:
                    contentControl.Background = brush;
                    break;
            }
        }

        private static double GetDecodeWidth(Control control) {
            if (!double.IsNaN(control.Width) && control.Width > 0) return control.Width;
            if (control.Bounds.Width > 0) return control.Bounds.Width;
            if (control.DesiredSize.Width > 0) return control.DesiredSize.Width;
            return 0;
        }

        private static double GetDecodeHeight(Control control) {
            if (!double.IsNaN(control.Height) && control.Height > 0) return control.Height;
            if (control.Bounds.Height > 0) return control.Bounds.Height;
            if (control.DesiredSize.Height > 0) return control.DesiredSize.Height;
            return 0;
        }

        private static void NormalizeBackgroundDecodeSize(Control control, ref double decodeWidth, ref double decodeHeight) {
            if (decodeWidth <= 0 || decodeHeight <= 0) {
                TopLevel topLevel = TopLevel.GetTopLevel(control);
                if (decodeWidth <= 0 && topLevel?.Bounds.Width > 0) decodeWidth = topLevel.Bounds.Width;
                if (decodeHeight <= 0 && topLevel?.Bounds.Height > 0) decodeHeight = topLevel.Bounds.Height;
            }

            if (decodeWidth <= 0 && decodeHeight <= 0) {
                decodeWidth = GetDecodeDipLimit(MaxBackgroundDecodePixels);
                return;
            }

            double maxDimension = Math.Max(decodeWidth, decodeHeight);
            double maxBackgroundDecodeDimension = GetDecodeDipLimit(MaxBackgroundDecodePixels);
            if (maxDimension <= maxBackgroundDecodeDimension) return;

            double scale = maxBackgroundDecodeDimension / maxDimension;
            if (decodeWidth > 0) decodeWidth *= scale;
            if (decodeHeight > 0) decodeHeight *= scale;
        }

        private static double GetBlurDecodeSize(double value) {
            double limit = GetDecodeDipLimit(MaxBlurBackgroundDecodePixels);
            if (value <= 0) return limit;
            return Math.Min(value, limit);
        }

        private static double GetDecodeDipLimit(double pixelLimit) {
            double dpi = Math.Max(1, App.Current?.DPI ?? 1);
            return Math.Max(1, pixelLimit / dpi);
        }

        private static Bitmap CreateBlurredBitmap(Bitmap source, int radius) {
            PixelSize size = source.PixelSize;
            if (size.Width <= 0 || size.Height <= 0) return source;

            radius = Math.Clamp(radius, 1, 16);
            int stride = size.Width * 4;
            byte[] pixels = new byte[stride * size.Height];
            GCHandle handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try {
                source.CopyPixels(new PixelRect(0, 0, size.Width, size.Height), handle.AddrOfPinnedObject(), pixels.Length, stride);
            } finally {
                handle.Free();
            }

            BoxBlur(pixels, size.Width, size.Height, radius);
            WriteableBitmap bitmap = new WriteableBitmap(size, new Vector(96, 96), PixelFormats.Bgra8888, AlphaFormat.Premul);
            using (ILockedFramebuffer framebuffer = bitmap.Lock()) {
                Marshal.Copy(pixels, 0, framebuffer.Address, pixels.Length);
            }
            return bitmap;
        }

        private static void BoxBlur(byte[] pixels, int width, int height, int radius) {
            byte[] temp = new byte[pixels.Length];

            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    AverageHorizontal(pixels, temp, width, height, radius, x, y);
                }
            }

            for (int y = 0; y < height; y++) {
                for (int x = 0; x < width; x++) {
                    AverageVertical(temp, pixels, width, height, radius, x, y);
                }
            }
        }

        private static void AverageHorizontal(byte[] source, byte[] target, int width, int height, int radius, int x, int y) {
            int left = Math.Max(0, x - radius);
            int right = Math.Min(width - 1, x + radius);
            int count = right - left + 1;
            int b = 0;
            int g = 0;
            int r = 0;
            int a = 0;

            for (int xx = left; xx <= right; xx++) {
                int sourceIndex = ((y * width) + xx) * 4;
                b += source[sourceIndex];
                g += source[sourceIndex + 1];
                r += source[sourceIndex + 2];
                a += source[sourceIndex + 3];
            }

            int targetIndex = ((y * width) + x) * 4;
            target[targetIndex] = (byte)(b / count);
            target[targetIndex + 1] = (byte)(g / count);
            target[targetIndex + 2] = (byte)(r / count);
            target[targetIndex + 3] = (byte)(a / count);
        }

        private static void AverageVertical(byte[] source, byte[] target, int width, int height, int radius, int x, int y) {
            int top = Math.Max(0, y - radius);
            int bottom = Math.Min(height - 1, y + radius);
            int count = bottom - top + 1;
            int b = 0;
            int g = 0;
            int r = 0;
            int a = 0;

            for (int yy = top; yy <= bottom; yy++) {
                int sourceIndex = ((yy * width) + x) * 4;
                b += source[sourceIndex];
                g += source[sourceIndex + 1];
                r += source[sourceIndex + 2];
                a += source[sourceIndex + 3];
            }

            int targetIndex = ((y * width) + x) * 4;
            target[targetIndex] = (byte)(b / count);
            target[targetIndex + 1] = (byte)(g / count);
            target[targetIndex + 2] = (byte)(r / count);
            target[targetIndex + 3] = (byte)(a / count);
        }

        private static void UnregisterLifecycle(Control control) {
            control.AttachedToVisualTree -= OnAttachedToVisualTree;
            control.DetachedFromVisualTree -= OnDetachedFromVisualTree;
            control.LayoutUpdated -= OnActiveImageLayoutUpdated;
            control.LayoutUpdated -= OnDeferredImageLayoutUpdated;
        }

        private static bool HasSourceCandidates(IReadOnlyList<Uri>? uris) {
            return uris != null && uris.Count > 0;
        }

        private static bool ContainsUri(IReadOnlyList<Uri>? uris, Uri uri) {
            if (uris == null || uri == null) return false;

            for (int i = 0; i < uris.Count; i++) {
                if (uris[i] == uri) return true;
            }

            return false;
        }

        private static Uri[] DeduplicateUris(IReadOnlyList<Uri> uris) {
            if (uris == null || uris.Count == 0) return [];

            List<Uri> result = new List<Uri>(uris.Count);
            for (int i = 0; i < uris.Count; i++) {
                Uri uri = uris[i];
                if (uri == null || result.Contains(uri)) continue;
                result.Add(uri);
            }

            return result.ToArray();
        }

        private static bool IsCurrentCandidates(Image image, ImageLoadState state, IReadOnlyList<Uri> candidates) {
            if (!ReferenceEquals(GetLoadState(image), state) || state.Token.IsCancellationRequested) return false;

            IReadOnlyList<Uri>? current = GetSourceCandidates(image);
            if (!HasSourceCandidates(current) || candidates == null || current.Count != candidates.Count) return false;

            for (int i = 0; i < candidates.Count; i++) {
                if (current[i] != candidates[i]) return false;
            }

            return true;
        }

        private static readonly AttachedProperty<ImageLoadState?> LoadStateProperty =
            AvaloniaProperty.RegisterAttached<Control, ImageLoadState?>("LoadState", typeof(ImageLoader));

        private static ImageLoadState? GetLoadState(Control element) {
            return element.GetValue(LoadStateProperty);
        }

        private static void SetLoadState(Control element, ImageLoadState? value) {
            element.SetValue(LoadStateProperty, value);
        }

        private static readonly AttachedProperty<IDisposable?> OwnedBackgroundBitmapProperty =
            AvaloniaProperty.RegisterAttached<Control, IDisposable?>("OwnedBackgroundBitmap", typeof(ImageLoader));

        private static IDisposable? GetOwnedBackgroundBitmap(Control element) {
            return element.GetValue(OwnedBackgroundBitmapProperty);
        }

        private static void ReplaceOwnedBackgroundBitmap(Control element, IDisposable? value) {
            IDisposable? previous = GetOwnedBackgroundBitmap(element);
            if (!ReferenceEquals(previous, value)) previous?.Dispose();
            element.SetValue(OwnedBackgroundBitmapProperty, value);
        }

        private static readonly AttachedProperty<long> LastViewportCheckTicksProperty =
            AvaloniaProperty.RegisterAttached<Control, long>("LastViewportCheckTicks", typeof(ImageLoader), 0);

        private static long GetLastViewportCheckTicks(Control element) {
            return element.GetValue(LastViewportCheckTicksProperty);
        }

        private static void SetLastViewportCheckTicks(Control element, long value) {
            element.SetValue(LastViewportCheckTicksProperty, value);
        }

        public static readonly AttachedProperty<Uri?> SourceProperty =
            AvaloniaProperty.RegisterAttached<Image, Uri?>("Source", typeof(ImageLoader));

        public static Uri? GetSource(Image element) {
            return element.GetValue(SourceProperty);
        }

        public static void SetSource(Image element, Uri? value) {
            element.SetValue(SourceProperty, value);
        }

        public static readonly AttachedProperty<IReadOnlyList<Uri>?> SourceCandidatesProperty =
            AvaloniaProperty.RegisterAttached<Image, IReadOnlyList<Uri>?>("SourceCandidates", typeof(ImageLoader));

        public static IReadOnlyList<Uri>? GetSourceCandidates(Image element) {
            return element.GetValue(SourceCandidatesProperty);
        }

        public static void SetSourceCandidates(Image element, IReadOnlyList<Uri>? value) {
            element.SetValue(SourceCandidatesProperty, value);
        }

        public static readonly AttachedProperty<Uri?> SvgSourceProperty =
            AvaloniaProperty.RegisterAttached<Image, Uri?>("SvgSource", typeof(ImageLoader));

        public static Uri? GetSvgSource(Image element) {
            return element.GetValue(SvgSourceProperty);
        }

        public static void SetSvgSource(Image element, Uri? value) {
            element.SetValue(SvgSourceProperty, value);
        }

        public static readonly AttachedProperty<Uri?> BackgroundSourceProperty =
            AvaloniaProperty.RegisterAttached<Control, Uri?>("BackgroundSource", typeof(ImageLoader));

        public static Uri? GetBackgroundSource(TemplatedControl element) {
            return element.GetValue(BackgroundSourceProperty);
        }

        public static Uri? GetBackgroundSource(Border element) {
            return element.GetValue(BackgroundSourceProperty);
        }

        public static void SetBackgroundSource(TemplatedControl element, Uri? value) {
            if (element == null) return;
            element.SetValue(BackgroundSourceProperty, value);
        }

        public static void SetBackgroundSource(Border element, Uri? value) {
            if (element == null) return;
            element.SetValue(BackgroundSourceProperty, value);
        }

        public static readonly AttachedProperty<int> BackgroundBlurRadiusProperty =
            AvaloniaProperty.RegisterAttached<Control, int>("BackgroundBlurRadius", typeof(ImageLoader), 0);

        public static int GetBackgroundBlurRadius(Border element) {
            return element.GetValue(BackgroundBlurRadiusProperty);
        }

        public static int GetBackgroundBlurRadius(Control element) {
            return element.GetValue(BackgroundBlurRadiusProperty);
        }

        public static void SetBackgroundBlurRadius(Border element, int value) {
            if (element == null) return;
            element.SetValue(BackgroundBlurRadiusProperty, Math.Clamp(value, 0, 16));
        }

        public static readonly AttachedProperty<Uri?> FillSourceProperty =
            AvaloniaProperty.RegisterAttached<Shape, Uri?>("FillSource", typeof(ImageLoader));

        public static Uri? GetFillSource(Shape element) {
            return element.GetValue(FillSourceProperty);
        }

        public static void SetFillSource(Shape element, Uri? value) {
            if (element == null) return;
            element.SetValue(FillSourceProperty, value);
        }

        public static readonly AttachedProperty<Uri?> ImageProperty =
            AvaloniaProperty.RegisterAttached<Avatar, Uri?>("Image", typeof(ImageLoader));

        public static Uri? GetImage(Avatar element) {
            return element.GetValue(ImageProperty);
        }

        public static void SetImage(Avatar element, Uri? value) {
            element.SetValue(ImageProperty, value);
        }

        private sealed class ImageLoadState : IDisposable {
            private readonly CancellationTokenSource cts = new CancellationTokenSource();

            public CancellationToken Token => cts.Token;

            public void Cancel() {
                if (!cts.IsCancellationRequested) cts.Cancel();
            }

            public void Dispose() {
                cts.Dispose();
            }
        }
    }
}
