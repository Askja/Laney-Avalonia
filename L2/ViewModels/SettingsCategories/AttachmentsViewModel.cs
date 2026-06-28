using ELOR.Laney.Core;
using ELOR.Laney.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ELOR.Laney.ViewModels.SettingsCategories {
    public sealed class AttachmentVaultItemViewModel : CommonViewModel {
        private readonly OfflineDownloadedAttachmentRecord record;
        private string _tagsText;

        public string FilePath => record.FilePath;
        public string FileName => record.FileName;
        public string Kind => record.Kind;
        public string OriginalName => record.OriginalName;
        public string TagsText { get { return _tagsText; } set { _tagsText = value; OnPropertyChanged(); } }
        public bool Exists => File.Exists(record.FilePath);
        public string Title => String.IsNullOrWhiteSpace(record.OriginalName) ? record.FileName : record.OriginalName;
        public string Subtitle => $"{record.Kind} · {ChatAttachmentDownloadHelper.FormatBytes((ulong)Math.Max(0, record.Bytes))} · peer:{record.PeerId} · cmid:{record.ParentConversationMessageId} · {record.SavedAt:dd.MM.yyyy HH:mm}";
        public string StatusText => Exists ? "offline preview готов" : "файл не найден";

        public AttachmentVaultItemViewModel(OfflineDownloadedAttachmentRecord record) {
            this.record = record;
            _tagsText = record.Tags;
        }
    }

    public sealed class AttachmentsViewModel : CommonViewModel {
        private string _searchQuery;
        private string _summary;
        private bool _isVaultLoading;
        private RelayCommand _refreshCommand;
        private RelayCommand _cleanupMissingCommand;
        private RelayCommand _openCommand;
        private RelayCommand _revealCommand;
        private RelayCommand _saveTagsCommand;

        public ObservableCollection<AttachmentVaultItemViewModel> Items { get; } = new ObservableCollection<AttachmentVaultItemViewModel>();
        public string SearchQuery { get { return _searchQuery; } set { _searchQuery = value; OnPropertyChanged(); _ = ReloadAsync(); } }
        public string Summary { get { return _summary; } private set { _summary = value; OnPropertyChanged(); } }
        public bool IsVaultLoading { get { return _isVaultLoading; } private set { _isVaultLoading = value; OnPropertyChanged(); } }
        public RelayCommand RefreshCommand { get { return _refreshCommand; } private set { _refreshCommand = value; OnPropertyChanged(); } }
        public RelayCommand CleanupMissingCommand { get { return _cleanupMissingCommand; } private set { _cleanupMissingCommand = value; OnPropertyChanged(); } }
        public RelayCommand OpenCommand { get { return _openCommand; } private set { _openCommand = value; OnPropertyChanged(); } }
        public RelayCommand RevealCommand { get { return _revealCommand; } private set { _revealCommand = value; OnPropertyChanged(); } }
        public RelayCommand SaveTagsCommand { get { return _saveTagsCommand; } private set { _saveTagsCommand = value; OnPropertyChanged(); } }

        public AttachmentsViewModel() {
            RefreshCommand = new RelayCommand((o) => _ = ReloadAsync());
            CleanupMissingCommand = new RelayCommand((o) => _ = CleanupMissingAsync());
            OpenCommand = new RelayCommand((o) => Open((o as AttachmentVaultItemViewModel)?.FilePath));
            RevealCommand = new RelayCommand((o) => Reveal((o as AttachmentVaultItemViewModel)?.FilePath));
            SaveTagsCommand = new RelayCommand((o) => _ = SaveTagsAsync(o as AttachmentVaultItemViewModel));
            _ = ReloadAsync();
        }

        private async Task ReloadAsync() {
            IsVaultLoading = true;
            try {
                var records = await OfflineCacheStore.SearchDownloadedAttachmentsAsync(SearchQuery, 200);
                Items.Clear();
                foreach (var record in records) Items.Add(new AttachmentVaultItemViewModel(record));
                Summary = $"Файлов: {Items.Count}. Поиск идет по имени, типу, URL, peer/sender/cmid, SHA-256 и тегам.";
            } finally {
                IsVaultLoading = false;
            }
        }

        private async Task SaveTagsAsync(AttachmentVaultItemViewModel item) {
            if (item == null) return;
            await OfflineCacheStore.SetDownloadedAttachmentTagsAsync(item.FilePath, item.TagsText);
            await ReloadAsync();
        }

        private async Task CleanupMissingAsync() {
            int removed = await OfflineCacheStore.CleanupMissingDownloadedAttachmentsAsync();
            await ReloadAsync();
            Summary = $"Vault очищен. Убрано битых записей: {removed}.";
        }

        private static void Open(string path) {
            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }

        private static void Reveal(string path) {
            if (String.IsNullOrWhiteSpace(path)) return;
            if (OperatingSystem.IsWindows() && File.Exists(path)) {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
                return;
            }

            string directory = File.Exists(path) ? Path.GetDirectoryName(path) : path;
            if (!String.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)) {
                Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
            }
        }
    }
}
