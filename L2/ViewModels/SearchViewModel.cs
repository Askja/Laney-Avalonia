using ELOR.Laney.Core;
using ELOR.Laney.DataModels;
using ELOR.Laney.Helpers;
using ELOR.VKAPILib.Objects;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ELOR.Laney.ViewModels {
    public class SearchViewModel : ViewModelBase {
        private VKSession session;

        private int _currentTab;
        private string _query;
        private ObservableCollection<Entity> _foundChats;
        private ObservableCollection<FoundMessageItem> _foundMessages;
        private bool _isChatsLoading;
        private bool _isMessagesLoading;
        private PlaceholderViewModel _chatsPlaceholder;
        private PlaceholderViewModel _messagesPlaceholder;
        private int _messagesApiOffset;

        public int CurrentTab { get { return _currentTab; } set { _currentTab = value; OnPropertyChanged(); } }
        public string Query { get { return _query; } set { _query = value; OnPropertyChanged(); } }
        public ObservableCollection<Entity> FoundChats { get { return _foundChats; } private set { _foundChats = value; OnPropertyChanged(); } }
        public ObservableCollection<FoundMessageItem> FoundMessages { get { return _foundMessages; } private set { _foundMessages = value; OnPropertyChanged(); } }
        public bool IsChatsLoading { get { return _isChatsLoading; } set { _isChatsLoading = value; OnPropertyChanged(); } }
        public bool IsMessagesLoading { get { return _isMessagesLoading; } private set { _isMessagesLoading = value; OnPropertyChanged(); } }
        public PlaceholderViewModel ChatsPlaceholder { get { return _chatsPlaceholder; } private set { _chatsPlaceholder = value; OnPropertyChanged(); } }
        public PlaceholderViewModel MessagesPlaceholder { get { return _messagesPlaceholder; } private set { _messagesPlaceholder = value; OnPropertyChanged(); } }

        public SearchViewModel(VKSession session) {
            this.session = session;

            PropertyChanged += async (a, b) => {
                switch (b.PropertyName) {
                    case nameof(CurrentTab):
                        await DoSearchAsync();
                        break;
                }
            };
        }

        public async Task DoSearchAsync() {
            switch (CurrentTab) {
                case 0:
                    FoundChats?.Clear();
                    if (!String.IsNullOrEmpty(Query)) await SearchChatsAsync();
                    break;
                case 1:
                    FoundMessages?.Clear();
                    _messagesApiOffset = 0;
                    if (!String.IsNullOrEmpty(Query)) await SearchMessagesAsync();
                    break;
            }
        }

        private async Task SearchChatsAsync() {
            if (IsChatsLoading) return;
            ChatsPlaceholder = null;
            IsChatsLoading = true;
            try {
                if (FoundChats == null) FoundChats = new ObservableCollection<Entity>();
                AddLocalChatResults();
                if (DemoMode.IsEnabled) {
                    IsChatsLoading = false;
                    if (FoundChats.Count == 0) ChatsPlaceholder = new PlaceholderViewModel { Text = Assets.i18n.Resources.nothing_found };
                    return;
                }

                var response = await session.API.Messages.SearchConversationsAsync(session.GroupId, Query, 200, true, VKAPIHelper.Fields);
                IsChatsLoading = false;

                if (response.Count == 0) {
                    if (FoundChats.Count == 0) ChatsPlaceholder = new PlaceholderViewModel { Text = Assets.i18n.Resources.nothing_found };
                    return;
                }

                foreach (var chat in response.Items) {
                    long id = chat.Peer.Id;
                    string name = $"{chat.Peer.Type} {chat.Peer.LocalId}";
                    Uri avatar = null;

                    if (chat.Peer.Type == PeerType.User) {
                        User u = response.Profiles.Where(i => i.Id == chat.Peer.LocalId).FirstOrDefault();
                        if (u != null) {
                            name = u.FullName;
                            avatar = u.Photo;
                        }
                    } else if (chat.Peer.Type == PeerType.Group) {
                        Group g = response.Groups.Where(i => i.Id == chat.Peer.LocalId).FirstOrDefault();
                        if (g != null) {
                            name = g.Name;
                            avatar = g.Photo;
                        }
                    } else if (chat.Peer.Type == PeerType.Chat) {
                        name = chat.ChatSettings?.Title;
                        avatar = chat.ChatSettings?.Photo?.Uri;
                    }

                    Entity item = new Entity(id, avatar, name, null, null);
                    AddFoundChat(item);

                }
            } catch (Exception ex) {
                IsChatsLoading = false;
                if (FoundChats != null && FoundChats.Count > 0) {
                    if (await ExceptionHelper.ShowErrorDialogAsync(session.Window, ex)) await SearchChatsAsync();
                } else {
                    ChatsPlaceholder = PlaceholderViewModel.GetForException(ex, async (o) => await SearchChatsAsync());
                }
            }
        }

        private void AddLocalChatResults() {
            if (String.IsNullOrWhiteSpace(Query)) return;

            IEnumerable<ChatViewModel> chats = session.ImViewModel?.SortedChats ?? Enumerable.Empty<ChatViewModel>();
            foreach (ChatViewModel chat in chats) {
                if (!MatchesLocalChat(chat, Query)) continue;

                Entity item = new Entity(
                    chat.PeerId,
                    chat.DisplayAvatar,
                    chat.DisplayTitle,
                    BuildLocalChatSearchDescription(chat),
                    null);
                AddFoundChat(item);
            }
        }

        private static bool MatchesLocalChat(ChatViewModel chat, string query) {
            if (chat == null || String.IsNullOrWhiteSpace(query)) return false;

            return Contains(chat.Title, query)
                || Contains(chat.DisplayTitle, query)
                || Contains(chat.Subtitle, query)
                || Contains(chat.DisplaySubtitle, query)
                || Contains(chat.LocalTagsText, query)
                || Contains(Settings.GetPeerLocalNote(chat.PeerId), query)
                || chat.PeerId.ToString().Contains(query.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildLocalChatSearchDescription(ChatViewModel chat) {
            List<string> parts = new List<string>();
            if (!String.IsNullOrWhiteSpace(chat.DisplaySubtitle)) parts.Add(chat.DisplaySubtitle);
            if (!String.IsNullOrWhiteSpace(chat.LocalTagsText)) parts.Add($"#{chat.LocalTagsText.Replace(", ", " #")}");
            string note = Settings.GetPeerLocalNote(chat.PeerId);
            if (!String.IsNullOrWhiteSpace(note)) parts.Add(note);
            return parts.Count == 0 ? null : String.Join(" · ", parts.Take(3));
        }

        public async Task SearchMessagesAsync() {
            if (IsMessagesLoading) return;
            MessagesPlaceholder = null;
            IsMessagesLoading = true;
            bool firstPage = _messagesApiOffset == 0;
            try {
                if (FoundMessages == null) FoundMessages = new ObservableCollection<FoundMessageItem>();
                if (firstPage) await AddLocalMessageResultsAsync();
                if (DemoMode.IsEnabled) {
                    IsMessagesLoading = false;
                    if (FoundMessages.Count == 0) MessagesPlaceholder = new PlaceholderViewModel { Text = Assets.i18n.Resources.nothing_found };
                    return;
                }

                var response = await session.API.Messages.SearchAsync(session.GroupId, Query, 0, offset: _messagesApiOffset, count: 40, extended: true, fields: VKAPIHelper.Fields);
                _messagesApiOffset += response.Items?.Count ?? 0;
                IsMessagesLoading = false;

                if (response.Count == 0) {
                    if (FoundMessages.Count > 0) return;
                    MessagesPlaceholder = new PlaceholderViewModel { Text = Assets.i18n.Resources.nothing_found };
                    return;
                }

                CacheManager.Add(response.Profiles);
                CacheManager.Add(response.Groups);

                foreach (var message in response.Items) {
                    Conversation chat = response.Conversations.FirstOrDefault(c => message.PeerId == c.Peer.Id);
                    FoundMessageItem fmi = new FoundMessageItem(message.FromId == session.Id, message, chat);
                    AddFoundMessage(fmi);
                }
            } catch (Exception ex) {
                IsMessagesLoading = false;
                if (FoundMessages != null && FoundMessages.Count > 0) {
                    if (await ExceptionHelper.ShowErrorDialogAsync(session.Window, ex)) await SearchMessagesAsync();
                } else {
                    MessagesPlaceholder = PlaceholderViewModel.GetForException(ex, async (o) => await SearchMessagesAsync());
                }
            }
        }

        private async Task AddLocalMessageResultsAsync() {
            await LocalSearchIndex.RefreshFromChatsAsync(session.ImViewModel?.SortedChats);
            IReadOnlyList<LocalSearchIndexEntry> entries = await LocalSearchIndex.SearchMessagesAsync(Query, 40);

            foreach (LocalSearchIndexEntry entry in entries) {
                Uri avatar = null;
                if (!String.IsNullOrWhiteSpace(entry.PeerAvatar)) Uri.TryCreate(entry.PeerAvatar, UriKind.Absolute, out avatar);

                FoundMessageItem item = new FoundMessageItem(
                    entry.PeerId,
                    entry.ConversationMessageId,
                    entry.PeerName,
                    avatar,
                    BuildLocalSearchPreview(entry),
                    entry.SentDate);
                AddFoundMessage(item);
            }
        }

        private static string BuildLocalSearchPreview(LocalSearchIndexEntry entry) {
            if (String.IsNullOrWhiteSpace(entry.AttachmentText)) return entry.Text;
            if (String.IsNullOrWhiteSpace(entry.Text)) return $"Вложение: {entry.AttachmentText}";
            return $"{entry.Text}\nВложение: {entry.AttachmentText}";
        }

        private void AddFoundMessage(FoundMessageItem item) {
            if (FoundMessages.Any(m => m.PeerId == item.PeerId && m.Id == item.Id)) return;
            FoundMessages.Add(item);
        }

        private void AddFoundChat(Entity item) {
            if (item == null || FoundChats.Any(c => c.Id == item.Id)) return;
            FoundChats.Add(item);
        }

        private static bool Contains(string source, string query) {
            return !String.IsNullOrWhiteSpace(source)
                && !String.IsNullOrWhiteSpace(query)
                && source.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
