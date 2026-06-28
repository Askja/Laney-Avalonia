using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;
using System.Diagnostics;

namespace VKUI.Controls {
    public sealed class Avatar : TemplatedControl {
        public Avatar() { }

        #region Properties

        public static readonly StyledProperty<Bitmap> ImageProperty =
            AvaloniaProperty.Register<Avatar, Bitmap>(nameof(Image));

        public static readonly StyledProperty<string> InitialsProperty =
            AvaloniaProperty.Register<Avatar, string>(nameof(Initials));

        public Bitmap Image {
            get => GetValue(ImageProperty);
            set => SetValue(ImageProperty, value);
        }

        public string Initials {
            get => GetValue(InitialsProperty);
            set => SetValue(InitialsProperty, value);
        }

        #endregion

        #region Template elements

        Border ImageBorder;

        #endregion

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
            base.OnApplyTemplate(e);
            ImageBorder = e.NameScope.Find<Border>(nameof(ImageBorder));
            SetImage();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
            base.OnPropertyChanged(change);

            if (change.Property == ImageProperty) {
                if (change.OldValue != change.NewValue) SetImage();
            }

            if (change.Property == BoundsProperty) SetImage();
        }

        private void SetImage() {
            if (ImageBorder == null) return;
            double size = Math.Min(Bounds.Width, Bounds.Height);
            ImageBorder.Width = size;
            ImageBorder.Height = size;

            if (Image == null) {
                ImageBorder.Background = null;
                return;
            }

            try {
                ImageBorder.Background = new ImageBrush(Image);
            } catch (Exception ex) {
                Debug.WriteLine($"Error while drawing in Avatar! 0x{ex.HResult.ToString("x8")}");
            }
        }
    }
}
