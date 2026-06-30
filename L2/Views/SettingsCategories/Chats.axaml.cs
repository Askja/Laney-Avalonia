using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ELOR.Laney.ViewModels.SettingsCategories;
using System.Collections.Generic;
using System.Linq;

namespace ELOR.Laney.Views.SettingsCategories {
    public partial class Chats : UserControl {
        public Chats() {
            InitializeComponent();
        }

        private async void SelectTesseract_Click(object sender, RoutedEventArgs e) {
            TopLevel topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider?.CanOpen != true) return;

            IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
                Title = "Выбрать tesseract.exe",
                AllowMultiple = false,
                FileTypeFilter = [
                    new FilePickerFileType("Tesseract") {
                        Patterns = ["tesseract.exe", "*.exe"]
                    },
                    FilePickerFileTypes.All
                ]
            });

            IStorageFile file = files?.FirstOrDefault();
            if (file == null || DataContext is not ChatsViewModel viewModel) return;

            viewModel.LocalOcrTesseractPath = file.TryGetLocalPath() ?? file.Path.LocalPath;
        }
    }
}
