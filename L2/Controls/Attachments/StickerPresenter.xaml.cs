using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Labs.Lottie;
using Avalonia.Media;
using Avalonia.VisualTree;
using ELOR.Laney.Core;
using ELOR.Laney.Extensions;
using ELOR.VKAPILib.Objects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ELOR.Laney.Controls.Attachments {
    public class StickerPresenter : TemplatedControl {
        private static readonly object activeAnimationsLock = new object();
        private static readonly LinkedList<WeakReference<StickerPresenter>> activeAnimations = new LinkedList<WeakReference<StickerPresenter>>();

        #region Properties

        public static readonly StyledProperty<Sticker> StickerProperty =
            AvaloniaProperty.Register<StickerPresenter, Sticker>(nameof(Sticker));

        public Sticker Sticker {
            get => GetValue(StickerProperty);
            set => SetValue(StickerProperty, value);
        }

        #endregion

        #region Template elements

        Border StickerView;
        IBrush thumbnailBackground;
        CancellationTokenSource renderCancellationTokenSource;
        CancellationTokenSource animationCancellationTokenSource;

        bool isUILoaded = false;
        protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
            base.OnApplyTemplate(e);
            StickerView = e.NameScope.Find<Border>(nameof(StickerView));
            isUILoaded = true;
            new System.Action(async () => await RenderAsync())();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) {
            base.OnDetachedFromVisualTree(e);
            ResetAnimationHandlers();
            StopAnimation();
            CancelRender();
        }

        #endregion

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
            base.OnPropertyChanged(change);

            if (change.Property == StickerProperty) {
                ResetAnimationHandlers();
                StopAnimation();
                new System.Action(async () => await RenderAsync())();
            }
        }

        private async Task RenderAsync() {
            CancelRender();
            if (!isUILoaded || Sticker == null || !this.IsAttachedToVisualTree()) return;

            ResetAnimationHandlers();
            StopAnimation();

            CancellationTokenSource cts = new CancellationTokenSource();
            renderCancellationTokenSource = cts;
            CancellationToken cancellationToken = cts.Token;

            try {
                await StickerView.SetImageBackgroundAsync(Sticker.GetSizeAndUriForThumbnail(this.Width).Uri, Width, Height);
                if (cancellationToken.IsCancellationRequested) return;
                thumbnailBackground = StickerView.Background;

                if (String.IsNullOrEmpty(Sticker.AnimationUrl)) return;

                switch (Settings.StickerAnimation) {
                    case StickerAnimationMode.Always:
                        await StartAnimationAsync(Sticker);
                        break;
                    case StickerAnimationMode.Hover:
                        StickerView.PointerEntered += StickerView_PointerEntered;
                        StickerView.PointerExited += StickerView_PointerExited;
                        break;
                    case StickerAnimationMode.Click:
                        StickerView.PointerReleased += StickerView_PointerReleased;
                        break;
                }
            } catch (OperationCanceledException) {
            } finally {
                if (ReferenceEquals(renderCancellationTokenSource, cts)) {
                    renderCancellationTokenSource = null;
                }

                cts.Dispose();
            }
        }

        private void CancelRender() {
            renderCancellationTokenSource?.Cancel();
        }

        private void StopAnimation() {
            animationCancellationTokenSource?.Cancel();
            if (StickerView == null) return;

            StickerView.Child = null;
            if (thumbnailBackground != null) StickerView.Background = thumbnailBackground;
            UnregisterActiveAnimation();
        }

        private async Task StartAnimationAsync(Sticker sticker) {
            if (StickerView == null || sticker == null || String.IsNullOrEmpty(sticker.AnimationUrl) || !this.IsAttachedToVisualTree()) return;
            if (!MediaMemoryGovernor.CanStartLocalStickerAnimation()) return;

            animationCancellationTokenSource?.Cancel();

            CancellationTokenSource cts = new CancellationTokenSource();
            animationCancellationTokenSource = cts;
            CancellationToken cancellationToken = cts.Token;

            try {
                await Task.Delay(250, cancellationToken); // надо
                if (cancellationToken.IsCancellationRequested || !this.IsAttachedToVisualTree() || Sticker != sticker) return;

                var uri = new Uri(sticker.AnimationUrl);
                var file = await CacheManager.GetFileFromCacheAsync(uri);
                if (cancellationToken.IsCancellationRequested || !this.IsAttachedToVisualTree() || Sticker != sticker) return;

                if (file) {
                    string local = $"file://{Path.Combine(LocalDataProfile.GetCurrentAccountDirectory("cache"), uri.Segments.Last()).Replace("\\", "/")}";
                    Lottie ls = new Lottie(new Uri("file://")) { // разраб либы не прописал конструктор public Lottie() без параметров, пришлось костылить.
                        Stretch = Stretch.Uniform,
                        StretchDirection = StretchDirection.Both,
                        RepeatCount = 4,
                        Path = local
                    };
                    StickerView.Child = ls;
                    StickerView.Background = new SolidColorBrush(Colors.Transparent);
                    RegisterActiveAnimation();
                }
            } catch (OperationCanceledException) {
            } finally {
                if (ReferenceEquals(animationCancellationTokenSource, cts)) {
                    animationCancellationTokenSource = null;
                }

                cts.Dispose();
            }
        }

        private void ResetAnimationHandlers() {
            if (StickerView == null) return;

            StickerView.PointerEntered -= StickerView_PointerEntered;
            StickerView.PointerExited -= StickerView_PointerExited;
            StickerView.PointerReleased -= StickerView_PointerReleased;
        }

        private void StickerView_PointerEntered(object sender, PointerEventArgs e) {
            new System.Action(async () => await StartAnimationAsync(Sticker))();
        }

        private void StickerView_PointerExited(object sender, PointerEventArgs e) {
            StopAnimation();
        }

        private void StickerView_PointerReleased(object sender, PointerReleasedEventArgs e) {
            if (StickerView?.Child != null) {
                StopAnimation();
            } else {
                new System.Action(async () => await StartAnimationAsync(Sticker))();
            }
        }

        private void RegisterActiveAnimation() {
            List<StickerPresenter> animationsToStop = new List<StickerPresenter>();

            lock (activeAnimationsLock) {
                CleanupActiveAnimations();

                activeAnimations.AddLast(new WeakReference<StickerPresenter>(this));

                while (CountActiveAnimations() > GetMaxActiveAnimations() && activeAnimations.First != null) {
                    WeakReference<StickerPresenter> oldest = activeAnimations.First.Value;
                    activeAnimations.RemoveFirst();

                    if (oldest.TryGetTarget(out StickerPresenter presenter) && !ReferenceEquals(presenter, this)) {
                        animationsToStop.Add(presenter);
                    }
                }

                MediaMemoryGovernor.ReportActiveLocalStickerAnimations(CountActiveAnimations());
            }

            foreach (var presenter in animationsToStop) {
                presenter.StopAnimation();
            }
        }

        private void UnregisterActiveAnimation() {
            lock (activeAnimationsLock) {
                LinkedListNode<WeakReference<StickerPresenter>> node = activeAnimations.First;
                while (node != null) {
                    LinkedListNode<WeakReference<StickerPresenter>> next = node.Next;
                    if (!node.Value.TryGetTarget(out StickerPresenter presenter) || ReferenceEquals(presenter, this)) {
                        activeAnimations.Remove(node);
                    }
                    node = next;
                }

                MediaMemoryGovernor.ReportActiveLocalStickerAnimations(CountActiveAnimations());
            }
        }

        private static void CleanupActiveAnimations() {
            LinkedListNode<WeakReference<StickerPresenter>> node = activeAnimations.First;
            while (node != null) {
                LinkedListNode<WeakReference<StickerPresenter>> next = node.Next;
                if (!node.Value.TryGetTarget(out _)) {
                    activeAnimations.Remove(node);
                }
                node = next;
            }
        }

        private static int CountActiveAnimations() {
            int count = 0;
            foreach (var reference in activeAnimations) {
                if (reference.TryGetTarget(out _)) count++;
            }
            return count;
        }

        private static int GetMaxActiveAnimations() {
            return Math.Max(1, MediaMemoryGovernor.GetLocalStickerAnimationLimit());
        }
    }
}
