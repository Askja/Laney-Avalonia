using Avalonia.Threading;
using DynamicData;
using DynamicData.Binding;
using ELOR.Laney.Assets.i18n;
using ELOR.Laney.Core;
using ELOR.Laney.DataModels;
using ELOR.Laney.Helpers;
using ELOR.VKAPILib.Methods;
using ELOR.VKAPILib.Objects;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace ELOR.Laney.ViewModels {
    public sealed class ImViewModel : CommonViewModel {
        private const string FilterArchive = "LocalArchive";
        private const string FilterPersonal = "LocalPersonal";
        private const string FilterChats = "LocalChats";
        private const string FilterCommunities = "LocalCommunities";
        private const string FilterMentions = "LocalMentions";
        private const string FilterSmartMoney = "SmartMoney";
        private const string FilterSmartDocs = "SmartDocs";
        private const string FilterSmartWork = "SmartWork";
        private const string FilterSmartShopping = "SmartShopping";
        private const string FilterSmartServices = "SmartServices";
        private const string FilterSmartSpam = "SmartSpam";
        private const string FilterSmartMemes = "SmartMemes";
        private const string FilterTagPrefix = "LocalTag:";

        private VKSession session;

        private SourceCache<ChatViewModel, long> _chats = new SourceCache<ChatViewModel, long>(c => c.PeerId);
        private BehaviorSubject<Func<ChatViewModel, bool>> _chatFilter = new BehaviorSubject<Func<ChatViewModel, bool>>(BuildChatFilter(ConversationsFilter.All.ToString()));
        private ReadOnlyObservableCollection<ChatViewModel> _sortedChats;
        private ChatViewModel _visualSelectedChat;
        private bool _isEmpty = true;
        private bool _reloadAfterCurrentLoad = false;
        private bool _isStoriesLoading = false;
        private bool _storiesLoadFailed = false;
        private bool _storiesLoaded = false;
        private ConversationsFilter _conversationsFilter = ConversationsFilter.All;
        private string _currentChatFilterId = ConversationsFilter.All.ToString();
        private TwoStringTuple _currentChatFilter;

        public ReadOnlyObservableCollection<ChatViewModel> SortedChats { get { return _sortedChats; } }
        public ObservableCollection<TwoStringTuple> ChatFilters { get; private set; } = new ObservableCollection<TwoStringTuple>();
        public ObservableCollection<StoryRailItemViewModel> Stories { get; private set; } = new ObservableCollection<StoryRailItemViewModel>();
        public TwoStringTuple CurrentChatFilter { get { return _currentChatFilter; } set { ChangeChatFilter(value); } }
        public string CurrentChatFilterId { get { return _currentChatFilterId; } }
        public string CurrentChatFilterTitle { get { return _currentChatFilter?.Item2 ?? Resources.all; } }
        public ChatViewModel VisualSelectedChat { get { return _visualSelectedChat; } private set { _visualSelectedChat = value; OnPropertyChanged(); } }
        public bool IsEmpty { get { return _isEmpty; } private set { _isEmpty = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowFilterEmpty)); } }
        public bool ShowFilterEmpty { get { return IsEmpty && !IsLoading && Placeholder == null && _currentChatFilterId != ConversationsFilter.All.ToString(); } }
        public bool IsStoriesLoading { get { return _isStoriesLoading; } private set { _isStoriesLoading = value; OnPropertyChanged(); RefreshStoriesState(); } }
        public bool StoriesLoadFailed { get { return _storiesLoadFailed; } private set { _storiesLoadFailed = value; OnPropertyChanged(); RefreshStoriesState(); } }
        public bool HasStories { get { return Stories.Count > 0; } }
        public bool IsStoriesSkeletonVisible { get { return IsStoriesLoading && Stories.Count == 0; } }
        public bool IsStoriesStatusVisible { get { return _storiesLoaded && !IsStoriesLoading && Stories.Count == 0; } }
        public bool HasStoriesRail { get { return IsStoriesLoading || Stories.Count > 0 || IsStoriesStatusVisible; } }
        public string StoriesStatusText { get { return StoriesLoadFailed ? "Истории не загрузились" : "Историй сейчас нет"; } }
        public string StoriesStatusSubtitle { get { return StoriesLoadFailed ? "Нажми обновить или открой VK Stories." : "Новые истории друзей и сообществ появятся тут."; } }
        public string FilterEmptyText { get { return Resources.chat_filter_empty; } }

        public ImViewModel(VKSession session) {
            this.session = session;
            RefreshLocalFilters();
            _currentChatFilter = ChatFilters.FirstOrDefault();

            session.CurrentOpenedChatChanged += (a, b) => {
                VisualSelectedChat = SortedChats.Where(c => c.PeerId == b).FirstOrDefault();
            };

            var observableChats = _chats.Connect()
                .AutoRefresh(c => c.SortIndex)
                .AutoRefresh(c => c.UnreadMessagesCount)
                .AutoRefresh(c => c.IsMarkedAsUnread)
                .AutoRefresh(c => c.UnreadReactions)
                .AutoRefresh(c => c.IsImportant)
                .AutoRefresh(c => c.IsUnanswered)
                .AutoRefresh(c => c.HasMention)
                .AutoRefresh(c => c.IsArchived)
                .AutoRefresh(c => c.LocalTagsText)
                .AutoRefresh(c => c.IsPinned)
                .AutoRefresh(c => c.LastMessage)
                .Filter(_chatFilter);
            var prop = observableChats.WhenPropertyChanged(c => c.SortIndex).Select(_ => Unit.Default);

#pragma warning disable CS0618 // Type or member is obsolete
            // Sort method is obsolete, but no working analog of this. The commented code below (SortAndBind) does not work correctly.
            var loader = observableChats
                .Sort(SortExpressionComparer<ChatViewModel>.Descending(c => c.SortIndex), prop)
                .TreatMovesAsRemoveAdd()
                .Bind(out _sortedChats)
                .Subscribe(t => {
                    IsEmpty = _sortedChats.Count == 0;
                    Debug.WriteLine($"Chats count: {_chats.Count}; sorted count: {_sortedChats.Count}");
                });
#pragma warning restore CS0618 // Type or member is obsolete

            //var comparer = SortExpressionComparer<ChatViewModel>.Descending(c => c.SortIndex);

            //var loader = observableChats
            //    .SortAndBind(out _sortedChats, comparer, new SortAndBindOptions {
            //        UseReplaceForUpdates = true
            //    })
            //    .WhenPropertyChanged(c => c.SortIndex)
            //    .Subscribe(t => {
            //        IsEmpty = _chats.Count == 0;
            //        Debug.WriteLine($"Chats count: {_chats.Count}; sorted count: {_sortedChats.Count}");
            //    });

            if (!DemoMode.IsEnabled) {
                session.LongPoll.NeedFullResync += LongPoll_NeedFullResync;
                session.LongPoll.MessageReceived += LongPoll_MessageReceived;
                session.LongPoll.ConversationRemoved += LongPoll_ConversationRemoved;
            }
        }

        public async Task LoadConversationsAsync() {
            if (DemoMode.IsEnabled) {
                DemoModeSession ds = DemoMode.GetDemoSessionById(session.Id);
                foreach (var conv in ds.Conversations) {
                    ChatViewModel chat = new ChatViewModel(session, conv.Conversation, conv.LastMessage);
                    CacheManager.Add(session.Id, chat);
                    _chats.AddOrUpdate(chat);
                }

                return;
            }

            if (IsLoading) return;
            IsLoading = true;
            OnPropertyChanged(nameof(ShowFilterEmpty));
            Placeholder = null;
            ConversationsFilter filter = _conversationsFilter;
            try {
                var response = await session.API.Messages.GetConversationsAsync(session.GroupId, VKAPIHelper.Fields, filter, true, 60, _chats.Count, Constants.NestedMessagesLimit);
                if (filter != _conversationsFilter) {
                    _reloadAfterCurrentLoad = true;
                } else {
                    CacheManager.Add(response.Profiles);
                    CacheManager.Add(response.Groups);

                    foreach (var conv in response.Items) {
                        ChatViewModel chat = CacheManager.GetChat(session.Id, conv.Conversation.Peer.Id);
                        if (chat == null) {
                            chat = new ChatViewModel(session, conv.Conversation, conv.LastMessage);
                            CacheManager.Add(session.Id, chat);
                        } else {
                            chat.ApplyConversationUpdate(conv.Conversation);
                        }
                        _chats.AddOrUpdate(chat);
                    }
                }
            } catch (Exception ex) {
                if (_chats.Count > 0) {
                    if (await ExceptionHelper.ShowErrorDialogAsync(session.Window, ex)) await LoadConversationsAsync();
                } else {
                    Placeholder = PlaceholderViewModel.GetForException(ex, async (o) => await LoadConversationsAsync());
                }
            }
            IsLoading = false;
            OnPropertyChanged(nameof(ShowFilterEmpty));
            if (_reloadAfterCurrentLoad) {
                _reloadAfterCurrentLoad = false;
                await LoadConversationsAsync();
                return;
            }
            await Task.Delay(2000);
            GC.Collect(2, GCCollectionMode.Aggressive);
        }

        public async Task LoadStoriesAsync(bool force = false) {
            if (DemoMode.IsEnabled) {
                await Dispatcher.UIThread.InvokeAsync(() => {
                    Stories.Clear();
                    StoriesLoadFailed = false;
                    _storiesLoaded = true;
                    RefreshStoriesState();
                });
                return;
            }

            if (session?.API == null) return;
            if (IsStoriesLoading) return;
            if (_storiesLoaded && !force) return;

            IsStoriesLoading = true;
            try {
                IReadOnlyList<Story> stories = await VKStoriesService.LoadStoriesAsync(session, 24);
                await Dispatcher.UIThread.InvokeAsync(() => {
                    Stories.Clear();
                    foreach (Story story in stories) {
                        Stories.Add(new StoryRailItemViewModel(story));
                    }
                    StoriesLoadFailed = false;
                    _storiesLoaded = true;
                    RefreshStoriesState();
                });
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot load VK stories rail.");
                await Dispatcher.UIThread.InvokeAsync(() => {
                    StoriesLoadFailed = true;
                    _storiesLoaded = true;
                    Stories.Clear();
                    RefreshStoriesState();
                });
            } finally {
                await Dispatcher.UIThread.InvokeAsync(() => IsStoriesLoading = false);
            }
        }

        private void RefreshStoriesState() {
            OnPropertyChanged(nameof(Stories));
            OnPropertyChanged(nameof(HasStories));
            OnPropertyChanged(nameof(IsStoriesSkeletonVisible));
            OnPropertyChanged(nameof(HasStoriesRail));
            OnPropertyChanged(nameof(IsStoriesStatusVisible));
            OnPropertyChanged(nameof(StoriesStatusText));
            OnPropertyChanged(nameof(StoriesStatusSubtitle));
        }

        private void ChangeChatFilter(TwoStringTuple value) {
            if (value == null || value == _currentChatFilter) return;

            ConversationsFilter filter = ConversationsFilter.All;
            bool isServerFilter = Enum.TryParse(value.Item1, out filter);
            if (!isServerFilter) filter = ConversationsFilter.All;
            bool shouldReload = filter != _conversationsFilter;

            _currentChatFilter = value;
            _currentChatFilterId = value.Item1;
            _conversationsFilter = filter;
            OnPropertyChanged(nameof(CurrentChatFilter));
            OnPropertyChanged(nameof(CurrentChatFilterId));
            OnPropertyChanged(nameof(CurrentChatFilterTitle));
            OnPropertyChanged(nameof(ShowFilterEmpty));

            _chatFilter.OnNext(BuildChatFilter(value.Item1));
            if (shouldReload) {
                _chats.Clear();
                IsEmpty = true;
                Placeholder = null;
            }

            new System.Action(async () => {
                if (!shouldReload) return;

                if (IsLoading) {
                    _reloadAfterCurrentLoad = true;
                    return;
                }

                await LoadConversationsAsync();
            })();
        }

        private static Func<ChatViewModel, bool> BuildChatFilter(string filterId) {
            return filterId switch {
                nameof(ConversationsFilter.Unread) => c => !c.IsArchived && (c.UnreadMessagesCount > 0 || c.IsMarkedAsUnread || c.UnreadReactions?.Count > 0),
                nameof(ConversationsFilter.Important) => c => !c.IsArchived && c.IsImportant,
                nameof(ConversationsFilter.Unanswered) => c => !c.IsArchived && c.IsUnanswered,
                FilterPersonal => c => !c.IsArchived && c.PeerType == PeerType.User,
                FilterChats => c => !c.IsArchived && c.PeerType == PeerType.Chat,
                FilterCommunities => c => !c.IsArchived && c.PeerType == PeerType.Group,
                FilterMentions => c => !c.IsArchived && c.HasMention,
                FilterSmartMoney => c => !c.IsArchived && SmartChatClassifier.MatchesMoney(c),
                FilterSmartDocs => c => !c.IsArchived && SmartChatClassifier.MatchesDocuments(c),
                FilterSmartWork => c => !c.IsArchived && SmartChatClassifier.MatchesWork(c),
                FilterSmartShopping => c => !c.IsArchived && SmartChatClassifier.MatchesShopping(c),
                FilterSmartServices => c => !c.IsArchived && SmartChatClassifier.MatchesServices(c),
                FilterSmartSpam => c => !c.IsArchived && SmartChatClassifier.MatchesSpam(c),
                FilterSmartMemes => c => !c.IsArchived && SmartChatClassifier.MatchesMemes(c),
                FilterArchive => c => c.IsArchived,
                string tagFilter when tagFilter.StartsWith(FilterTagPrefix, StringComparison.Ordinal) => c => !c.IsArchived && Settings.PeerHasLocalTag(c.PeerId, tagFilter[FilterTagPrefix.Length..]),
                _ => c => !c.IsArchived
            };
        }

        public void RefreshLocalFilters() {
            string selectedId = _currentChatFilterId;
            ChatFilters.Clear();
            foreach (TwoStringTuple filter in BuildBaseChatFilters()) {
                ChatFilters.Add(filter);
            }

            foreach (string tag in Settings.GetKnownPeerLocalTags()) {
                ChatFilters.Add(new TwoStringTuple($"{FilterTagPrefix}{tag}", $"#{tag}"));
            }

            _currentChatFilter = ChatFilters.FirstOrDefault(f => f.Item1 == selectedId) ?? ChatFilters.FirstOrDefault();
            _currentChatFilterId = _currentChatFilter?.Item1 ?? ConversationsFilter.All.ToString();
            OnPropertyChanged(nameof(ChatFilters));
            OnPropertyChanged(nameof(CurrentChatFilter));
            OnPropertyChanged(nameof(CurrentChatFilterId));
            OnPropertyChanged(nameof(CurrentChatFilterTitle));
        }

        public bool TrySetChatFilter(string filterId) {
            if (String.IsNullOrWhiteSpace(filterId)) return false;

            RefreshLocalFilters();
            TwoStringTuple filter = ChatFilters.FirstOrDefault(f => f.Item1 == filterId);
            if (filter == null) return false;

            CurrentChatFilter = filter;
            return true;
        }

        private static IReadOnlyList<TwoStringTuple> BuildBaseChatFilters() {
            return new List<TwoStringTuple> {
                new TwoStringTuple(ConversationsFilter.All.ToString(), Resources.all),
                new TwoStringTuple(ConversationsFilter.Unread.ToString(), Resources.chat_filter_unread),
                new TwoStringTuple(ConversationsFilter.Important.ToString(), Resources.chat_filter_important),
                new TwoStringTuple(ConversationsFilter.Unanswered.ToString(), Resources.chat_filter_unanswered),
                new TwoStringTuple(FilterPersonal, "Личные"),
                new TwoStringTuple(FilterChats, "Беседы"),
                new TwoStringTuple(FilterCommunities, "Сообщества"),
                new TwoStringTuple(FilterMentions, "Упоминания"),
                new TwoStringTuple(FilterSmartWork, "Работа"),
                new TwoStringTuple(FilterSmartShopping, "Покупки"),
                new TwoStringTuple(FilterSmartServices, "Сервисы"),
                new TwoStringTuple(FilterSmartSpam, "Спам"),
                new TwoStringTuple(FilterSmartMoney, "Деньги/чеки"),
                new TwoStringTuple(FilterSmartDocs, "Доки"),
                new TwoStringTuple(FilterSmartMemes, "Мемы"),
                new TwoStringTuple(FilterArchive, "Архив")
            };
        }

        public void RefreshStreamerMode() {
            if (SortedChats == null) return;
            foreach (ChatViewModel chat in SortedChats) {
                chat.RefreshStreamerMode();
            }
        }

        #region Longpoll events

        private void LongPoll_NeedFullResync(object sender, EventArgs e) {
            new System.Action(async () => {
                await Dispatcher.UIThread.InvokeAsync(async () => {
                    _chats.Clear();
                    await LoadConversationsAsync();
                });
            })();
        }

        private void LongPoll_MessageReceived(LongPoll longPoll, Message message, int flags, bool incrementUnreadCounter) {
            new System.Action(async () => {
                await Dispatcher.UIThread.InvokeAsync(() => {
                    var lookup = _chats.Lookup(message.PeerId);
                    if (!lookup.HasValue) { // В списке нет, и нам надо его (чат) получить.
                        ChatViewModel chat = CacheManager.GetChat(session.Id, message.PeerId);
                        if (chat == null) {
                            Log.Information($"Received message from peer {message.PeerId}, which is not found in cache");
                            chat = new ChatViewModel(session, message.PeerId, message, true);
                        }
                        _chats.AddOrUpdate(chat);
                    }
                });
            })();
        }

        private void LongPoll_ConversationRemoved(object sender, long peerId) {
            new System.Action(async () => {
                await Dispatcher.UIThread.InvokeAsync(() => {
                    var lookup = _chats.Lookup(peerId);
                    if (lookup.HasValue) {
                        _chats.Remove(lookup.Value);
                        CacheManager.RemoveChat(lookup.Value);
                    }
                });
            })();
        }

        #endregion
    }
}
