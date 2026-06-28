using Avalonia.Controls;
using Avalonia.Controls.PanAndZoom;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using ELOR.Laney.Core;
using ELOR.Laney.Core.Network;
using ELOR.Laney.Extensions;
using ELOR.Laney.Helpers;
using ELOR.VKAPILib.Objects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ELOR.Laney.Views.Media {
    public partial class Gallery : Window {
        public static void Show(List<IPreview> items, IPreview target) {
            new Gallery(items, target).Show();
        }

        List<IPreview> _items;
        IPreview _target;

        public Gallery() {
            InitializeComponent();
            dbg.IsVisible = Settings.ShowDebugInfoInGallery;
            RequestedThemeVariant = ThemeVariant.Dark;
        }

        private Gallery(List<IPreview> items, IPreview target) {
            InitializeComponent();
            dbg.IsVisible = Settings.ShowDebugInfoInGallery;
            RequestedThemeVariant = ThemeVariant.Dark;

            this._items = items;
            this._target = target;

            Activated += Gallery_Activated;
        }

        private void GalleryItems_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            UpdateInfo();
        }

        private void UpdateInfo() {
            Title = $"{Assets.i18n.Resources.wnd_gallery} — {GalleryItems.SelectedIndex + 1}/{GalleryItems.Items.Count}";
            Uri mediaUri = GetCurrentMediaUri();
            OpenOriginalButton.IsEnabled = mediaUri != null;
            CopyLinkButton.IsEnabled = mediaUri != null;
            SaveFileButton.IsEnabled = mediaUri != null;

            AttachmentBase attachment = GalleryItems.SelectedItem as AttachmentBase;
            if (attachment != null) {
                dbgi.Text = attachment.ToString();
                var owner = CacheManager.GetNameAndAvatar(attachment.OwnerId);
                OwnerAvatar.IsVisible = owner.Item3 != null;
                string name = string.Join(" ", new string[] { owner.Item1, owner.Item2 });
                OwnerName.Text = name;
                OwnerAvatar.Background = attachment.OwnerId.GetGradient();
                OwnerAvatar.Initials = name;
                if (owner.Item3 != null) OwnerAvatar.SetImage(owner.Item3, OwnerAvatar.Width, OwnerAvatar.Height);

                if (attachment is Photo photo) {
                    Date.Text = photo.Date.ToHumanizedString(true);
                    Description.Text = photo.Text;
                    Description.IsVisible = !string.IsNullOrEmpty(photo.Text);
                } else if (attachment is Document doc) {
                    Date.Text = doc.DateTime.ToHumanizedString(true);
                    Description.Text = doc.Title;
                    Description.IsVisible = true;
                }
            }
        }

        private Uri GetCurrentMediaUri() {
            if (GalleryItems.SelectedItem is Photo photo) return photo.MaximalSizedPhoto?.Uri;
            if (GalleryItems.SelectedItem is Document doc && (doc.Type == DocumentType.Image || doc.Type == DocumentType.GIF)) return doc.Uri;
            return null;
        }

        private string GetCurrentFileName(Uri mediaUri) {
            string extension = Path.GetExtension(mediaUri.AbsolutePath);
            if (String.IsNullOrWhiteSpace(extension)) extension = ".jpg";

            string name = GalleryItems.SelectedItem switch {
                Photo photo => $"photo{photo.OwnerId}_{photo.Id}{extension}",
                Document doc when !String.IsNullOrWhiteSpace(doc.Title) => doc.Title,
                Document doc => $"doc{doc.OwnerId}_{doc.Id}{extension}",
                _ => $"laney-media{extension}"
            };

            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            if (String.IsNullOrWhiteSpace(Path.GetExtension(name))) name += extension;
            return name;
        }

        private async void OpenOriginal_Click(object? sender, RoutedEventArgs e) {
            try {
                Uri mediaUri = GetCurrentMediaUri();
                if (mediaUri == null) return;
                await ELOR.Laney.Core.Launcher.LaunchUrl(mediaUri);
            } catch (Exception ex) {
                await ExceptionHelper.ShowErrorDialogAsync(this, ex, true);
            }
        }

        private async void CopyLink_Click(object? sender, RoutedEventArgs e) {
            try {
                Uri mediaUri = GetCurrentMediaUri();
                if (mediaUri == null || Clipboard == null) return;
                await Clipboard.SetTextAsync(mediaUri.AbsoluteUri);
            } catch (Exception ex) {
                await ExceptionHelper.ShowErrorDialogAsync(this, ex, true);
            }
        }

        private async void SaveFile_Click(object? sender, RoutedEventArgs e) {
            try {
                Uri mediaUri = GetCurrentMediaUri();
                if (mediaUri == null || StorageProvider?.CanSave != true) return;

                IStorageFile target = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions {
                    SuggestedFileName = GetCurrentFileName(mediaUri),
                    FileTypeChoices = new List<FilePickerFileType> { FilePickerFileTypes.All }
                });
                if (target == null) return;

                SaveFileButton.IsEnabled = false;
                using var response = await LNet.GetAsync(mediaUri);
                response.EnsureSuccessStatusCode();

                await using Stream source = await response.Content.ReadAsStreamAsync();
                await using Stream destination = await target.OpenWriteAsync();
                await source.CopyToAsync(destination);
            } catch (Exception ex) {
                await ExceptionHelper.ShowErrorDialogAsync(this, ex, true);
            } finally {
                SaveFileButton.IsEnabled = GetCurrentMediaUri() != null;
            }
        }

        private void Gallery_Activated(object sender, System.EventArgs e) {
            Activated -= Gallery_Activated;
            if (_items == null || _items.Count == 0) {
                Close();
                return;
            }
            _target = _target != null ? _target : _items[0];

            GalleryItems.SelectionChanged += GalleryItems_SelectionChanged;
            new System.Action(async () => {
                GalleryItems.ItemsSource = _items;
                await Task.Delay(50); // required, without delay scrolling to target in FlipView not properly working.
                GalleryItems.SelectedItem = _target;
            })();
            if (_items.FirstOrDefault() == _target) UpdateInfo(); // required, because GalleryItems_SelectionChanged doesn't called after first loading (because default index is 0).
        }

        private void ImageDataContextChanged(object sender, System.EventArgs e) {
            Image image = sender as Image;
            IPreview item = image.DataContext as IPreview;

            if (item is Photo photo) {
                var p = photo.MaximalSizedPhoto;
                image.SetUriSource(p.Uri);
            } else if (item is Document doc && (doc.Type == DocumentType.Image || doc.Type == DocumentType.GIF)) {
                image.SetUriSource(doc.Uri);
            }
        }

        private void ZoomBorder_PropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e) {
            ZoomBorder zb = sender as ZoomBorder;
            switch (e.Property.Name) {
                case nameof(ZoomBorder.OffsetX):
                case nameof(ZoomBorder.OffsetY):
                case nameof(ZoomBorder.ZoomX):
                case nameof(ZoomBorder.ZoomY):
                case nameof(ZoomBorder.Width):
                case nameof(ZoomBorder.Height):
                    dbgt.Text = $"W:     {zb.Width}\nH:     {zb.Height}\nOffsX: {zb.OffsetX}\nOffsY: {zb.OffsetY}\nZoomX: {zb.ZoomX}\nZoomY: {zb.ZoomY}";
                    break;
            }
        }

        private void ZoomBorder_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e) {
            ZoomBorder zb = sender as ZoomBorder;
            Control child = zb.Child;

            zb.Width = Width;
            zb.Height = Height;

            SizeChanged += (a, b) => {
                zb.Width = b.NewSize.Width;
                zb.Height = b.NewSize.Height;
            };
        }
    }
}
