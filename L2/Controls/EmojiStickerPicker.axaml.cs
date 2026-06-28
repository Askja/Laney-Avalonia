using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Labs.Lottie;
using Avalonia.Media;
using ELOR.Laney.Core;
using ELOR.Laney.DataModels;
using ELOR.Laney.Extensions;
using ELOR.Laney.ViewModels.Controls;
using ELOR.VKAPILib.Objects;
using NeoSmart.Unicode;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ELOR.Laney.Controls {
    public partial class EmojiStickerPicker : UserControl {
        private const string LocalStickerAnimationTag = "local-sticker-animation";
        private static readonly object activeLocalStickerAnimationsLock = new object();
        private static readonly LinkedList<WeakReference<Grid>> activeLocalStickerAnimationHosts = new LinkedList<WeakReference<Grid>>();

        EmojiStickerPickerViewModel ViewModel { get => DataContext as EmojiStickerPickerViewModel; }

        public EmojiStickerPicker() {
            InitializeComponent();
            Unloaded += EmojiStickerPicker_Unloaded;
        }

        public event EventHandler<string> EmojiPicked;
        public event EventHandler<Sticker> StickerPicked;
        public event EventHandler<LocalSticker> LocalStickerPicked;

        private void EmojiStickerPicker_Unloaded(object sender, RoutedEventArgs e) {
            Unloaded -= EmojiStickerPicker_Unloaded;
            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            BitmapManager.ClearCachedImages();
        }

        private void EmojiStickerPicker_Loaded(object sender, RoutedEventArgs e) {
            if (Design.IsDesignMode) return;
            ChangeTabContentTemplate();
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(EmojiStickerPickerViewModel.SelectedTab)) {
                ChangeTabContentTemplate();
            }
        }

        private void ChangeTabContentTemplate() {
            var content = ViewModel.SelectedTab.Content;
            if (content is ObservableCollection<EmojiGroup>) {
                StickersTabContent.IsVisible = false;
                LocalStickersTabContent.IsVisible = false;
                EmojisTabContent.IsVisible = true;
            } else if (content is ObservableCollection<Sticker>) {
                EmojisTabContent.IsVisible = false;
                LocalStickersTabContent.IsVisible = false;
                StickersTabContent.IsVisible = true;
            } else if (content is ObservableCollection<LocalSticker>) {
                EmojisTabContent.IsVisible = false;
                StickersTabContent.IsVisible = false;
                LocalStickersTabContent.IsVisible = true;
            }
        }

        private void EmojiListBoxItem_Tapped(object sender, TappedEventArgs e) {
            Control c = sender as Control;
            SingleEmoji emoji = (SingleEmoji)c.DataContext;
            EmojiPicked?.Invoke(this, emoji.ToString());
        }

        private void EmojiListBox_KeyUp(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                ListBox lb = sender as ListBox;
                SingleEmoji emoji = (SingleEmoji)lb.SelectedItem;
                EmojiPicked?.Invoke(this, emoji.ToString());
            }
        }

        private void StickersListBoxItem_Tapped(object sender, TappedEventArgs e) {
            Control c = sender as Control;
            Sticker sticker = (Sticker)c.DataContext;
            StickerPicked?.Invoke(this, sticker);
        }

        private void StickersListBox_KeyUp(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                ListBox lb = sender as ListBox;
                Sticker sticker = (Sticker)lb.SelectedItem;
                StickerPicked?.Invoke(this, sticker);
            }
        }

        private void LocalStickersListBoxItem_Tapped(object sender, TappedEventArgs e) {
            Control c = sender as Control;
            LocalSticker sticker = (LocalSticker)c.DataContext;
            LocalStickerPicked?.Invoke(this, sticker);
        }

        private void LocalStickersListBox_KeyUp(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                ListBox lb = sender as ListBox;
                LocalSticker sticker = (LocalSticker)lb.SelectedItem;
                LocalStickerPicked?.Invoke(this, sticker);
            }
        }

        private void LocalStickerFavorite_Click(object sender, RoutedEventArgs e) {
            if (sender is Control control && control.DataContext is LocalSticker sticker) {
                ViewModel.ToggleLocalStickerFavorite(sticker);
                e.Handled = true;
            }
        }

        private void LocalStickerPreview_PointerEntered(object sender, PointerEventArgs e) {
            if (sender is Border border && border.DataContext is LocalSticker sticker) {
                new System.Action(async () => await StartLocalStickerAnimationAsync(border, sticker))();
            }
        }

        private void LocalStickerPreview_PointerExited(object sender, PointerEventArgs e) {
            if (sender is Border { Child: Grid grid }) StopLocalStickerAnimation(grid);
        }

        private static async Task StartLocalStickerAnimationAsync(Border host, LocalSticker sticker) {
            if (host?.Child is not Grid grid || sticker?.CanInlineAnimate != true) return;
            if (Settings.StickerAnimation == StickerAnimationMode.Never) return;
            if (!MediaMemoryGovernor.CanStartLocalStickerAnimation()) return;
            if (!File.Exists(sticker.FilePath)) return;

            await Task.Delay(180);
            if (!host.IsPointerOver || host.Child is not Grid currentGrid || !ReferenceEquals(grid, currentGrid)) return;

            StopLocalStickerAnimation(grid);
            Lottie animation = new Lottie(new Uri("file://")) {
                Tag = LocalStickerAnimationTag,
                Path = new Uri(sticker.FilePath).AbsoluteUri,
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.Both,
                RepeatCount = 3,
                IsHitTestVisible = false
            };

            grid.ClipToBounds = true;
            grid.Children.Add(animation);
            RegisterLocalStickerAnimation(grid);
        }

        private static void StopLocalStickerAnimation(Grid grid) {
            if (grid == null) return;

            foreach (Lottie animation in grid.Children.OfType<Lottie>().Where(c => Equals(c.Tag, LocalStickerAnimationTag)).ToList()) {
                grid.Children.Remove(animation);
            }

            UnregisterLocalStickerAnimation(grid);
        }

        private static void RegisterLocalStickerAnimation(Grid grid) {
            List<Grid> animationsToStop = new List<Grid>();

            lock (activeLocalStickerAnimationsLock) {
                CleanupLocalStickerAnimations();
                activeLocalStickerAnimationHosts.AddLast(new WeakReference<Grid>(grid));

                while (CountLocalStickerAnimations() > MediaMemoryGovernor.GetLocalStickerAnimationLimit() && activeLocalStickerAnimationHosts.First != null) {
                    WeakReference<Grid> oldest = activeLocalStickerAnimationHosts.First.Value;
                    activeLocalStickerAnimationHosts.RemoveFirst();

                    if (oldest.TryGetTarget(out Grid oldGrid) && !ReferenceEquals(oldGrid, grid)) animationsToStop.Add(oldGrid);
                }

                MediaMemoryGovernor.ReportActiveLocalStickerAnimations(CountLocalStickerAnimations());
            }

            foreach (Grid oldGrid in animationsToStop) StopLocalStickerAnimation(oldGrid);
        }

        private static void UnregisterLocalStickerAnimation(Grid grid) {
            lock (activeLocalStickerAnimationsLock) {
                LinkedListNode<WeakReference<Grid>> node = activeLocalStickerAnimationHosts.First;
                while (node != null) {
                    LinkedListNode<WeakReference<Grid>> next = node.Next;
                    if (!node.Value.TryGetTarget(out Grid activeGrid) || ReferenceEquals(activeGrid, grid)) {
                        activeLocalStickerAnimationHosts.Remove(node);
                    }
                    node = next;
                }

                MediaMemoryGovernor.ReportActiveLocalStickerAnimations(CountLocalStickerAnimations());
            }
        }

        private static void CleanupLocalStickerAnimations() {
            LinkedListNode<WeakReference<Grid>> node = activeLocalStickerAnimationHosts.First;
            while (node != null) {
                LinkedListNode<WeakReference<Grid>> next = node.Next;
                if (!node.Value.TryGetTarget(out _)) activeLocalStickerAnimationHosts.Remove(node);
                node = next;
            }
        }

        private static int CountLocalStickerAnimations() {
            int count = 0;
            foreach (WeakReference<Grid> reference in activeLocalStickerAnimationHosts) {
                if (reference.TryGetTarget(out _)) count++;
            }
            return count;
        }

        // TODO: method to download images without caching
        // WriteableBitmap have a issue!
        private void PackImage_DataContextChanged(object? sender, EventArgs e) {
            if (sender is Image img && img.DataContext is TabItem<object> tab) {
                if (tab.Image != null) {
                    try {
                        // Закомеченный код внезапно стал приводить к падению. Возможно, после обновлении Авалонии?
                        //using var response = await LNet.GetAsync(tab.Image);
                        //response.EnsureSuccessStatusCode();
                        //using Stream stream = await response.Content.ReadAsStreamAsync();
                        //using var bitmap = WriteableBitmap.DecodeToWidth(stream, 22, BitmapInterpolationMode.MediumQuality);  
                        //img.Source = bitmap;

                        img.SetUriSource(tab.Image, 22, 22);
                    } catch (Exception ex) {
                        Log.Error(ex, $"EmojiStickerPickerUI: cannot load a sticker pack icon!");
                    }
                }
            }
        }
    }
}
