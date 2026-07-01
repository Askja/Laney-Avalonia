using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ELOR.Laney.ViewModels.SettingsCategories;
using System;

namespace ELOR.Laney.Views.SettingsCategories {
    public partial class Appearance : UserControl {
        public Appearance() {
            InitializeComponent();
        }

        private void AppFontFamily_Click(object sender, RoutedEventArgs e) {
            if (sender is Control { DataContext: AppFontFamilyOption option } && DataContext is AppearanceViewModel viewModel) {
                viewModel.SelectAppFontFamily(option);
            }
        }

        private void AppIconVariant_Click(object sender, RoutedEventArgs e) {
            if (sender is Control { DataContext: AppIconVariantOption option } && DataContext is AppearanceViewModel viewModel) {
                viewModel.SelectAppIconVariant(option);
            }
        }

        private void FontCarouselScroll_PointerWheelChanged(object sender, PointerWheelEventArgs e) {
            if (sender is not ScrollViewer scrollViewer || Math.Abs(e.Delta.Y) < 0.01) return;

            double maxOffset = Math.Max(0, scrollViewer.Extent.Width - scrollViewer.Viewport.Width);
            if (maxOffset <= 0) return;

            double nextOffset = Math.Clamp(scrollViewer.Offset.X - e.Delta.Y * 56, 0, maxOffset);
            scrollViewer.Offset = new Vector(nextOffset, scrollViewer.Offset.Y);
            e.Handled = true;
        }
    }
}
