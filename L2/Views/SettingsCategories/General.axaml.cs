using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ELOR.Laney.Core;
using ELOR.Laney.DataModels;
using ELOR.Laney.ViewModels.SettingsCategories;
using ELOR.Laney.Views.Modals;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELOR.Laney.Views.SettingsCategories {
    public partial class General : UserControl {
        string oldLangId;
        private static readonly FilePickerFileType SettingsJsonFileType = new FilePickerFileType("Laney settings JSON") {
            Patterns = new List<string> { "*.json" },
            MimeTypes = new List<string> { "application/json" }
        };

        public General() {
            InitializeComponent();
            oldLangId = Settings.Get(Settings.LANGUAGE, Constants.DefaultLang);
        }

        private void ComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e) {
            Window window = TopLevel.GetTopLevel(this) as Window;
            if (window == null) return;

            if (e.AddedItems.Count == 1) {
                TwoStringTuple value = e.AddedItems[0] as TwoStringTuple;
                if (value.Item1 == oldLangId) return;

                new System.Action(async () => {
                    VKUIDialog alert = new VKUIDialog(Assets.i18n.Resources.restart_required, Assets.i18n.Resources.restart_required_ext);
                    await alert.ShowDialog(window);
                })();
            }
        }

        private async void ExportSettings_Click(object sender, RoutedEventArgs e) {
            Window window = TopLevel.GetTopLevel(this) as Window;
            if (window?.StorageProvider?.CanSave != true) {
                await new VKUIDialog("Экспорт недоступен", "StorageProvider не дает сохранить файл. Значит, backup пока некуда писать.", ["Понятно"], 1).ShowDialog(window);
                return;
            }

            try {
                IStorageFile file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions {
                    Title = "Экспорт настроек Laney",
                    SuggestedFileName = $"laney-settings-{DateTime.Now:yyyyMMdd-HHmmss}.json",
                    FileTypeChoices = new List<FilePickerFileType> { SettingsJsonFileType }
                });
                if (file == null) return;

                await using Stream stream = await file.OpenWriteAsync();
                if (stream.CanSeek) stream.SetLength(0);

                using StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false));
                await writer.WriteAsync(Settings.ExportClientSettingsToJson());
                await writer.FlushAsync();

                await new VKUIDialog("Настройки экспортированы", "JSON сохранен без токенов, E2E-ключей, черновиков и локальной истории. Как ни странно, это специально.", ["Понятно"], 1).ShowDialog(window);
            } catch (Exception ex) {
                await new VKUIDialog("Экспорт не взлетел", ex.Message, ["Понятно"], 1).ShowDialog(window);
            }
        }

        private async void ImportSettings_Click(object sender, RoutedEventArgs e) {
            Window window = TopLevel.GetTopLevel(this) as Window;
            if (window?.StorageProvider?.CanOpen != true) {
                await new VKUIDialog("Импорт недоступен", "StorageProvider не дает открыть файл. Без файла импортировать, внезапно, нечего.", ["Понятно"], 1).ShowDialog(window);
                return;
            }

            try {
                IReadOnlyList<IStorageFile> files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
                    Title = "Импорт настроек Laney",
                    AllowMultiple = false,
                    FileTypeFilter = new List<FilePickerFileType> { SettingsJsonFileType }
                });
                IStorageFile file = files.FirstOrDefault();
                if (file == null) return;

                await using Stream stream = await file.OpenReadAsync();
                using StreamReader reader = new StreamReader(stream, Encoding.UTF8, true);
                string json = await reader.ReadToEndAsync();
                int imported = Settings.ImportClientSettingsFromJson(json);

                await new VKUIDialog("Настройки импортированы", $"Применено ключей: {imported}. Секреты и неподдерживаемый мусор проигнорированы.", ["Понятно"], 1).ShowDialog(window);
            } catch (Exception ex) {
                await new VKUIDialog("Импорт не взлетел", ex.Message, ["Понятно"], 1).ShowDialog(window);
            }
        }

        private async void ChooseBackupFolder_Click(object sender, RoutedEventArgs e) {
            Window window = TopLevel.GetTopLevel(this) as Window;
            if (window?.StorageProvider?.CanOpen != true) {
                await new VKUIDialog("Папка недоступна", "StorageProvider не дает открыть folder picker. Без папки локальный backup писать некуда.", ["Понятно"], 1).ShowDialog(window);
                return;
            }

            IReadOnlyList<IStorageFolder> folders = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions {
                Title = "Папка для backup Laney",
                AllowMultiple = false
            });
            IStorageFolder folder = folders?.FirstOrDefault();
            if (folder == null) return;

            string path = folder.TryGetLocalPath();
            if (String.IsNullOrWhiteSpace(path)) {
                await new VKUIDialog("Папка не файловая", "Выбранное место не имеет локального пути. Sync пишет обычные файлы, без магии через облачные URI.", ["Понятно"], 1).ShowDialog(window);
                return;
            }

            Settings.LocalBackupDirectory = path;
            (DataContext as GeneralViewModel)?.RefreshLocalBackupDirectory();
        }

        private async void SyncLocalBackup_Click(object sender, RoutedEventArgs e) {
            Window window = TopLevel.GetTopLevel(this) as Window;
            string directory = Settings.LocalBackupDirectory;
            if (String.IsNullOrWhiteSpace(directory)) {
                await new VKUIDialog("Backup без папки", "Сначала выбери папку. Да, скучно, зато потом не ищем файлы по всей системе.", ["Понятно"], 1).ShowDialog(window);
                return;
            }

            try {
                Task<LocalBackupSyncResult> syncTask = LocalBackupSyncManager.SyncCurrentAccountAsync(directory);
                LocalBackupSyncResult result = await new VKUIWaitDialog<LocalBackupSyncResult>().ShowAsync(window, syncTask);
                string errors = result.Errors.Count == 0 ? String.Empty : $"\n\nПервые ошибки:\n{String.Join("\n", result.Errors)}";
                await new VKUIDialog("Backup синхронизирован", $"{result.Summary}\n\n{result.TargetDirectory}{errors}", ["Понятно"], 1).ShowDialog(window);
                (DataContext as GeneralViewModel)?.RefreshLocalBackupDirectory();
            } catch (Exception ex) {
                await new VKUIDialog("Backup не синхронизирован", ex.Message, ["Понятно"], 1).ShowDialog(window);
            }
        }
    }
}
