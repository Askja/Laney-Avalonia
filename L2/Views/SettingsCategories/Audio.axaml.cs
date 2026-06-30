using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ELOR.Laney.ViewModels.SettingsCategories;
using System.Collections.Generic;
using System.Linq;

namespace ELOR.Laney.Views.SettingsCategories {
    public partial class Audio : UserControl {
        public Audio() {
            InitializeComponent();
        }

        private async void SelectWhisperModel_Click(object sender, RoutedEventArgs e) {
            TopLevel topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider?.CanOpen != true) return;

            IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
                Title = "Выбрать модель Whisper",
                AllowMultiple = false,
                FileTypeFilter = [
                    new FilePickerFileType("Whisper ggml") {
                        Patterns = ["*.bin"]
                    },
                    FilePickerFileTypes.All
                ]
            });

            IStorageFile file = files?.FirstOrDefault();
            if (file == null || DataContext is not AudioViewModel viewModel) return;

            viewModel.LocalVoiceTranscriptionModelPath = file.TryGetLocalPath() ?? file.Path.LocalPath;
        }
    }
}
