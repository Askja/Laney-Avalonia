using Avalonia.Controls;
using Avalonia.Interactivity;
using ELOR.Laney.ViewModels.SettingsCategories;

namespace ELOR.Laney.Views.SettingsCategories {
    public partial class AttachmentVault : UserControl {
        private AttachmentsViewModel ViewModel => DataContext as AttachmentsViewModel;

        public AttachmentVault() {
            InitializeComponent();
        }

        private void OpenAttachment_Click(object sender, RoutedEventArgs e) {
            ViewModel?.OpenCommand.Execute((sender as Control)?.DataContext);
        }

        private void RevealAttachment_Click(object sender, RoutedEventArgs e) {
            ViewModel?.RevealCommand.Execute((sender as Control)?.DataContext);
        }

        private void SaveTags_Click(object sender, RoutedEventArgs e) {
            ViewModel?.SaveTagsCommand.Execute((sender as Control)?.DataContext);
        }
    }
}
