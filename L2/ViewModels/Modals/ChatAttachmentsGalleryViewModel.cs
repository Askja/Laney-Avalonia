using ELOR.Laney.Helpers;
using ELOR.Laney.ViewModels.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ELOR.Laney.ViewModels.Modals {
    public sealed class ChatAttachmentsGalleryViewModel : ViewModelBase {
        private static readonly string[] FilterIds = ["all", "photo", "document", "audio", "voice", "video", "sticker"];

        private readonly List<DownloadableChatAttachment> allItems;
        private string filterId = "all";
        private string searchText;
        private string summary;

        public string Header { get; }
        public ObservableCollection<DownloadableChatAttachment> Items { get; } = new ObservableCollection<DownloadableChatAttachment>();
        public IReadOnlyList<string> FilterTitles { get; } = ["Все", "Фото", "Документы", "Аудио", "Голосовые", "Видео", "Стикеры"];
        public string Summary { get { return summary; } private set { summary = value; OnPropertyChanged(); } }
        public bool IsEmpty { get { return Items.Count == 0; } }

        public ChatAttachmentsGalleryViewModel(ChatViewModel chat) {
            Header = $"Вложения: {chat?.DisplayTitle ?? "чат"}";
            allItems = ChatAttachmentDownloadHelper.GetLoadedAttachments(chat)
                .OrderByDescending(a => a.SentTime)
                .ThenByDescending(a => a.ParentConversationMessageId)
                .ToList();
            Refresh();
        }

        public void SetFilterIndex(int index) {
            filterId = FilterIds[Math.Clamp(index, 0, FilterIds.Length - 1)];
            Refresh();
        }

        public void SetSearchText(string text) {
            searchText = text?.Trim();
            Refresh();
        }

        private void Refresh() {
            IEnumerable<DownloadableChatAttachment> query = allItems;
            if (filterId != "all") query = query.Where(a => a.FilterKind == filterId);

            if (!String.IsNullOrWhiteSpace(searchText)) {
                query = query.Where(a =>
                    Contains(a.DisplayTitle, searchText)
                    || Contains(a.KindTitle, searchText)
                    || Contains(a.Extension, searchText)
                    || Contains(a.Uri?.AbsoluteUri, searchText)
                    || Contains(a.SenderId.ToString(), searchText));
            }

            List<DownloadableChatAttachment> filtered = query.ToList();
            Items.Clear();
            foreach (DownloadableChatAttachment item in filtered) Items.Add(item);

            Summary = $"Показано {filtered.Count} из {allItems.Count}";
            OnPropertyChanged(nameof(IsEmpty));
        }

        private static bool Contains(string source, string value) {
            return source?.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
