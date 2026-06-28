using Avalonia.Controls;
using Avalonia.Interactivity;
using ELOR.Laney.ViewModels.SettingsCategories;
using VKUI.Controls;

namespace ELOR.Laney.Views.SettingsCategories {
    public partial class Stickers : UserControl {
        public Stickers() {
            InitializeComponent();
        }

        private StickersViewModel ViewModel => DataContext as StickersViewModel;

        private void LocalStickerPackEnabled_Click(object sender, RoutedEventArgs e) {
            if (sender is ToggleSwitch toggle && toggle.DataContext is LocalStickerPackItemViewModel pack) {
                ViewModel?.SetPackEnabled(pack, toggle.IsChecked == true);
            }
        }

        private void LocalStickerPackUp_Click(object sender, RoutedEventArgs e) {
            if (sender is Control control && control.DataContext is LocalStickerPackItemViewModel pack) {
                ViewModel?.MovePack(pack, -1);
            }
        }

        private void LocalStickerPackDown_Click(object sender, RoutedEventArgs e) {
            if (sender is Control control && control.DataContext is LocalStickerPackItemViewModel pack) {
                ViewModel?.MovePack(pack, 1);
            }
        }

        private void LocalStickerFavorite_Click(object sender, RoutedEventArgs e) {
            if (sender is Control control && control.DataContext is LocalStickerItemViewModel item) {
                ViewModel?.ToggleFavorite(item);
            }
        }

        private void LocalStickerTagsSave_Click(object sender, RoutedEventArgs e) {
            if (sender is Control control && control.DataContext is LocalStickerItemViewModel item) {
                ViewModel?.SaveTags(item);
            }
        }
    }
}
