using Avalonia.Controls;
using Avalonia.Controls.Selection;
using Avalonia.Threading;
using ELOR.Laney.Collections;
using ELOR.Laney.Controls;
using ELOR.Laney.Core;
using ELOR.Laney.Core.Localization;
using ELOR.Laney.DataModels;
using ELOR.Laney.Extensions;
using ELOR.Laney.Helpers;
using ELOR.Laney.ViewModels.Controls;
using ELOR.Laney.Views.Modals;
using ELOR.VKAPILib.Objects;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ToastNotifications.Avalonia;
using VKUI.Controls;
using Regex = System.Text.RegularExpressions.Regex;
using RegexMatchTimeoutException = System.Text.RegularExpressions.RegexMatchTimeoutException;
using RegexOptions = System.Text.RegularExpressions.RegexOptions;

namespace ELOR.Laney.ViewModels {
    public sealed class ChatViewModel : CommonViewModel, IMessagesListHolder {
        private static uint _instances = 0;
        private static readonly string[] BuiltInUrgentNotificationTokens = [
            "срочно",
            "важно",
            "критично",
            "горит",
            "авария",
            "инцидент",
            "прод",
            "urgent",
            "asap",
            "critical",
            "incident",
            "prod",
            "alert"
        ];
        private static readonly string[] VipLocalTags = ["vip", "important", "favorite", "critical"];
        private VKSession session;

        private PeerType _peerType;
        private long _peerId;
        private string _title;
        private string _subtitle;
        private string _activityStatus;
        private Uri _avatar;
        private string _sourceTitle;
        private Uri _sourceAvatar;
        private bool _isVerified;
        private UserOnlineInfo _online;
        private int _onlineMembersCount;
        private SortId _sortId;
        private int _unreadMessagesCount;
        private BatchedObservableCollection<MessageViewModel> _receivedMessages = new BatchedObservableCollection<MessageViewModel>();
        private MessagesCollection _displayedMessages;
        private MessageViewModel _pinnedMessage;
        private PushSettings _pushSettings;
        private int _inread;
        private int _outread;
        private ChatSettings _csettings;
        private CanWrite _canwrite;
        private ComposerViewModel _composer;
        private bool _isMarkedAsUnread;
        private bool _isImportant;
        private bool _isUnanswered;
        private bool _isPinned;
        private bool _isFavoritesChat;
        private ObservableCollection<int> _mentions;
        private bool _hasMention;
        private bool _hasSelfDestructMessage;
        private bool _hasE2EStatus;
        private string _e2eStatusText;
        private string _e2eStatusIconId;
        private string _e2eStatusTip;
        private ObservableCollection<int> _unreadReactions;
        private string _restrictionReason;
        private bool _isCurrentOpenedChat;
        private int _selectedMessagesCount;
        private bool _isPreviousMessagesLoading;
        private bool _isNextMessagesLoading;
        private ObservableCollection<Command> _messagesCommands = new ObservableCollection<Command>();
        private RelayCommand _openProfileCommand;
        private RelayCommand _goToLastMessageCommand;
        private RelayCommand _goToLastReactedMessageCommand;
        private const int ShadowBanTextRuleLimit = 32;
        private const int ShadowBanTextRuleMaxLength = 160;
        private static readonly TimeSpan ShadowBanRegexTimeout = TimeSpan.FromMilliseconds(60);
        private static readonly string[] AntiSpamLinkMarkers = ["http://", "https://", "vk.cc/", "t.me/", "bit.ly/", "clck.ru/"];
        private static readonly string[] AntiSpamKeywords = ["казино", "ставк", "букмек", "крипт", "airdrop", "заработ", "доход", "инвест", "скидк", "промокод", "подпишись", "розыгрыш"];
        private static readonly string[] SuspiciousLinkMarkers = ["vk.cc/", "bit.ly/", "clck.ru/", "tinyurl.com/", "goo.gl/", "t.co/", "xn--"];
        private static readonly string[] SuspiciousLinkTokens = ["free", "bonus", "airdrop", "giveaway", "casino", "wallet", "seed", "verify", "приз", "бонус", "розыгрыш", "кошелек", "подтверди"];

        public VKSession Session { get { return session; } }
        public PeerType PeerType { get { return _peerType; } private set { _peerType = value; OnPropertyChanged(); } }
        public long PeerId { get { return _peerId; } private set { _peerId = value; OnPropertyChanged(); } }
        public string Title { get { return _title; } private set { _title = value; OnPropertyChanged(); OnPropertyChanged(nameof(Initials)); OnPropertyChanged(nameof(DisplayTitle)); OnPropertyChanged(nameof(DisplayInitials)); } }
        public string Subtitle { get { return _subtitle; } private set { _subtitle = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplaySubtitle)); } }
        public string ActivityStatus { get { return _activityStatus; } set { _activityStatus = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayActivityStatus)); } }
        public Uri Avatar { get { return _avatar; } private set { _avatar = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayAvatar)); } }
        public bool IsVerified { get { return _isVerified; } private set { _isVerified = value; OnPropertyChanged(); } }
        public UserOnlineInfo Online { get { return _online; } set { _online = value; OnPropertyChanged(); } }
        public int OnlineMembersCount { get { return _onlineMembersCount; } set { _onlineMembersCount = value; OnPropertyChanged(); } }
        public string Initials { get { return _title.GetInitials(PeerId.IsChat() || PeerId.IsGroup()); } }
        public string DisplayTitle { get { return Settings.StreamerMode ? PrivacyMask.GetPeerTitle(PeerId) : Title; } }
        public string DisplaySubtitle { get { return Settings.StreamerMode && !String.IsNullOrWhiteSpace(Subtitle) ? PrivacyMask.HiddenSubtitle : Subtitle; } }
        public string DisplayActivityStatus { get { return Settings.StreamerMode && !String.IsNullOrWhiteSpace(ActivityStatus) ? PrivacyMask.HiddenActivity : ActivityStatus; } }
        public Uri DisplayAvatar { get { return Settings.StreamerMode ? null : Avatar; } }
        public string DisplayInitials { get { return Settings.StreamerMode ? PrivacyMask.GetPeerInitials(PeerId) : Initials; } }
        public long DisplayAvatarSeed { get { return Settings.StreamerMode ? 0 : PeerId; } }
        public SortId SortId { get { return _sortId; } set { _sortId = value; OnPropertyChanged(); OnPropertyChanged(nameof(SortIndex)); } }
        public ulong SortIndex { get { return GetSortIndex(); } }
        public int UnreadMessagesCount { get { return _unreadMessagesCount; } private set { _unreadMessagesCount = value; OnPropertyChanged(); } }
        public ObservableCollection<MessageViewModel> ReceivedMessages { get { return _receivedMessages; } }
        public MessagesCollection DisplayedMessages { get { return _displayedMessages; } private set { _displayedMessages = value; OnPropertyChanged(); } }
        public MessageViewModel LastMessage { get { return ReceivedMessages.LastOrDefault(); } }
        public MessageViewModel PinnedMessage { get { return _pinnedMessage; } private set { _pinnedMessage = value; OnPropertyChanged(); } }
        public PushSettings PushSettings { get { return _pushSettings; } private set { _pushSettings = value; OnPropertyChanged(); } }
        public int InRead { get { return _inread; } private set { _inread = value; OnPropertyChanged(); } }
        public int OutRead { get { return _outread; } private set { _outread = value; OnPropertyChanged(); } }
        public ChatSettings ChatSettings { get { return _csettings; } private set { _csettings = value; OnPropertyChanged(); } }
        public CanWrite CanWrite { get { return _canwrite; } private set { _canwrite = value; OnPropertyChanged(); } }
        public ComposerViewModel Composer { get { return _composer; } private set { _composer = value; OnPropertyChanged(); } }
        public bool IsMarkedAsUnread { get { return _isMarkedAsUnread; } private set { _isMarkedAsUnread = value; OnPropertyChanged(); } }
        public bool IsImportant { get { return _isImportant; } private set { _isImportant = value; OnPropertyChanged(); } }
        public bool IsUnanswered { get { return _isUnanswered; } private set { _isUnanswered = value; OnPropertyChanged(); } }
        public bool IsPinned { get { return _isPinned; } private set { _isPinned = value; OnPropertyChanged(); } }
        public bool IsArchived { get { return Settings.IsPeerArchived(PeerId); } }
        public IReadOnlyList<string> LocalTags { get { return Settings.GetPeerLocalTags(PeerId); } }
        public string LocalTagsText { get { return Settings.GetPeerLocalTagsText(PeerId); } }
        public bool HasLocalTags { get { return LocalTags.Count > 0; } }
        public bool IsFavoritesChat { get { return _isFavoritesChat; } private set { _isFavoritesChat = value; OnPropertyChanged(); } }
        public ObservableCollection<int> Mentions { get { return _mentions; } private set { _mentions = value; OnPropertyChanged(); } }
        public bool HasMention { get { return _hasMention; } private set { _hasMention = value; OnPropertyChanged(); } }
        public bool HasSelfDestructMessage { get { return _hasSelfDestructMessage; } private set { _hasSelfDestructMessage = value; OnPropertyChanged(); } }
        public bool HasE2EStatus { get { return _hasE2EStatus; } private set { _hasE2EStatus = value; OnPropertyChanged(); } }
        public string E2EStatusText { get { return _e2eStatusText; } private set { _e2eStatusText = value; OnPropertyChanged(); } }
        public string E2EStatusIconId { get { return _e2eStatusIconId; } private set { _e2eStatusIconId = value; OnPropertyChanged(); } }
        public string E2EStatusTip { get { return _e2eStatusTip; } private set { _e2eStatusTip = value; OnPropertyChanged(); } }
        public string MentionIconId { get { return GetMentionIcon(); } }
        public ObservableCollection<int> UnreadReactions { get { return _unreadReactions; } set { _unreadReactions = value; OnPropertyChanged(); } }
        public string RestrictionReason { get { return _restrictionReason; } private set { _restrictionReason = value; OnPropertyChanged(); } }
        public bool IsCurrentOpenedChat { get { return _isCurrentOpenedChat; } private set { _isCurrentOpenedChat = value; OnPropertyChanged(); } }
        public int SelectedMessagesCount { get { return _selectedMessagesCount; } private set { _selectedMessagesCount = value; OnPropertyChanged(); } }
        public bool IsPreviousMessagesLoading { get { return _isPreviousMessagesLoading; } private set { _isPreviousMessagesLoading = value; OnPropertyChanged(); } }
        public bool IsNextMessagesLoading { get { return _isNextMessagesLoading; } private set { _isNextMessagesLoading = value; OnPropertyChanged(); } }
        public ObservableCollection<Command> MessagesCommands { get { return _messagesCommands; } private set { _messagesCommands = value; OnPropertyChanged(); } }
        public RelayCommand OpenProfileCommand { get { return _openProfileCommand; } private set { _openProfileCommand = value; OnPropertyChanged(); } }
        public RelayCommand GoToLastMessageCommand { get { return _goToLastMessageCommand; } private set { _goToLastMessageCommand = value; OnPropertyChanged(); } }
        public RelayCommand GoToLastReactedMessageCommand { get { return _goToLastReactedMessageCommand; } private set { _goToLastReactedMessageCommand = value; OnPropertyChanged(); } }


        public SelectionModel<MessageViewModel> SelectedMessages { get; } = new SelectionModel<MessageViewModel> {
            SingleSelect = false
        };

        public List<ChatMember> Members { get; private set; } = new List<ChatMember>();
        public List<User> MembersUsers { get; private set; } = new List<User>();
        public List<Group> MembersGroups { get; private set; } = new List<Group>();

        public long Id => PeerId;
        public long OwnedSessionId => session.Id;
        public static uint Instances => _instances;

        private long _onlineMembersCountLastUpdateTime = 0;

        private User PeerUser;
        private Group PeerGroup;

        public event EventHandler<IMessageListItem> ScrollToMessageRequested;
        public event EventHandler<bool> MessagesChunkLoaded; // получение сообщений (false - предыдущих, true - следующих)
        public EventHandler<MessageViewModel> MessageAddedToLast;

        Elapser<LongPollActivityInfo> ActivityStatusUsers = new Elapser<LongPollActivityInfo>();
        private readonly object pendingLongPollMessagesLock = new object();
        private readonly List<PendingLongPollMessage> pendingLongPollMessages = new List<PendingLongPollMessage>();
        private bool pendingLongPollFlushQueued;

        private readonly struct PendingLongPollMessage {
            public Message Message { get; }
            public int Flags { get; }
            public bool IncrementUnreadCounter { get; }

            public PendingLongPollMessage(Message message, int flags, bool incrementUnreadCounter) {
                Message = message;
                Flags = flags;
                IncrementUnreadCounter = incrementUnreadCounter;
            }
        }

        public ChatViewModel(VKSession session, long peerId, Message lastMessage = null, bool needSetup = false) {
            _instances++;
            int cmid = lastMessage != null ? lastMessage.ConversationMessageId : 0;
            Log.Verbose($"New ChatViewModel for peer {peerId}. Last message: {cmid}, need setup: {needSetup}");

            this.session = session;
            Composer = new ComposerViewModel(session, this);
            SetUpEvents();
            PeerId = peerId;
            RefreshE2EState();
            Title = peerId.ToString();
            MessageViewModel msg = null;
            if (lastMessage != null) {
                msg = MessageViewModel.Create(lastMessage, session);
                if (!lastMessage.IsPartial) FixState(msg);
                if (SortId == null) SortId = new SortId { MajorId = 0, MinorId = lastMessage.Id };
                if (!IsMessageLocallyHidden(msg)) ReceivedMessages.Add(msg);
            }
            // needSetup нужен в случае, когда мы не переходим в беседу и не загружаем сообщения,
            // но надо загрузить инфу о чате, которую можно получить при загрузке сообщений.
            if (needSetup) new System.Action(async () => await GetInfoFromAPIAndSetupAsync(lastMessage, msg))();
        }

        public ChatViewModel(VKSession session, Conversation c, Message lastMessage = null) {
            _instances++;
            Log.Verbose($"New ChatViewModel for conversation with peer {c.Peer.Id}. Last message: {lastMessage?.ConversationMessageId}");

            this.session = session;
            Composer = new ComposerViewModel(session, this);
            SetUpEvents();
            Setup(c);
            if (lastMessage != null) {
                MessageViewModel msg = MessageViewModel.Create(lastMessage, session);
                FixState(msg);
                if (!IsMessageLocallyHidden(msg)) ReceivedMessages.Add(msg);
            }
            RefreshE2EState();
        }

        ~ChatViewModel() {
            _instances--;
        }

        // Вызывается при отображении беседы на окне
        public void OnDisplayed(int messageId = -1) {
            bool isDisplayedMessagesEmpty = DisplayedMessages == null || DisplayedMessages.Count == 0;
            int targetMessageId = GetOpenTargetMessageId(messageId);
            Log.Information("Chat {0} is opened. isDisplayedMessagesEmpty: {1}, CMID: {2}, target: {3}", PeerId, isDisplayedMessagesEmpty, messageId, targetMessageId);

            if (targetMessageId > 0) {
                new System.Action(async () => await GoToMessageAsync(targetMessageId))();
            } else if (isDisplayedMessagesEmpty) {
                new System.Action(async () => await LoadMessagesAsync())();
            } else if (Settings.ChatOpenBehavior == ChatOpenBehaviorIds.Bottom) {
                new System.Action(async () => await GoToLastMessageAsync())();
            } else {
                new System.Action(async () => {
                    await UpdateOnlineMembersCountAsync();
                })();
            }
        }

        private int GetOpenTargetMessageId(int requestedMessageId) {
            if (requestedMessageId >= 0) return requestedMessageId;
            if (Settings.ChatOpenBehavior == ChatOpenBehaviorIds.FirstUnread && UnreadMessagesCount > 0 && InRead > 0) {
                return InRead + 1;
            }

            return -1;
        }

        public void ApplyConversationUpdate(Conversation conversation) {
            if (conversation == null) return;
            Setup(conversation);
        }

        private async Task GetInfoFromAPIAndSetupAsync(Message message, MessageViewModel msg) {
            try {
                var response = await session.API.Messages.GetConversationsByIdAsync(session.GroupId, new List<long> { PeerId }, true, VKAPIHelper.Fields);
                CacheManager.Add(response.Profiles);
                CacheManager.Add(response.Groups);
                Setup(response.Items.FirstOrDefault());

                // Если чат новый, то нам надо отобразить уведомление о новом сообщении,
                // т. к. обычно уведомления отправляет метод LongPoll_MessageReceived.
                if (message == null || msg == null) return;

                bool isMention = false;
                if (!message.IsSilent && message.MentionedUsers != null) {
                    if (message.MentionedUsers.Count == 0) { // признак того, что пушнули всех (@all)
                        isMention = true;
                    } else {
                        isMention = message.MentionedUsers.Contains(session.Id);
                    }
                }

                // Если сообщение неполное даже после получения инфы о чате, то добавляем сообщение в pending.
                if (msg.State == MessageVMState.Loading) {
                    Log.Information($"Adding message {message.PeerId}_{message.ConversationMessageId} to pending for notification... (by new chat)");
                    if (!message.IsSilent) pendingMessages.Add(message.ConversationMessageId, isMention);
                } else {
                    if (!message.IsSilent) await ShowSystemNotificationAsync(msg, isMention);
                }
            } catch (Exception ex) {
                Log.Error(ex, $"Cannot get conversation from API! Peer={PeerId}");
            }
        }

        bool _isConvoRefreshing = false;
        private async Task RefreshConvoInfoAsync(Conversation convo = null, bool needRefreshChatMembers = false) {
            if (_isConvoRefreshing) return;
            _isConvoRefreshing = true;
            try {
                Log.Information($"RefreshConvoInfo peer: {PeerId}, needRefreshChatMembers: {needRefreshChatMembers}");
                if (convo != null) {
                    Setup(convo);
                } else {
                    var response = await session.API.Messages.GetConversationsByIdAsync(session.GroupId, new List<long> { PeerId }, true, VKAPIHelper.Fields);
                    if (response.Items.Count > 0) Setup(response.Items[0]);
                }

                if (needRefreshChatMembers) await LoadMembersAsync();
            } catch (Exception ex) {
                await ExceptionHelper.ShowErrorDialogAsync(session.ModalWindow, ex, true);
            } finally {
                _isConvoRefreshing = false;
            }
        }

        bool _isMembersLoading = false;
        private async Task LoadMembersAsync() {
            if (PeerType != PeerType.Chat) return;
            if (ChatSettings.State != UserStateInChat.In || ChatSettings.IsGroupChannel || _isMembersLoading) return;

            _isMembersLoading = true;
            try {
                Log.Information($"LoadMembers peer: {PeerId}");
                var response = await session.API.Messages.GetConversationMembersAsync(session.GroupId, PeerId, extended: true, fields: VKAPIHelper.Fields);
                Members = response.Items;

                CacheManager.Add(response.Profiles);
                CacheManager.Add(response.Groups);

                MembersUsers = response.Profiles;
                MembersGroups = response.Groups;
            } catch (Exception ex) {
                // TODO: snackbar
                await ExceptionHelper.ShowErrorDialogAsync(session.ModalWindow, ex, true);
            } finally {
                _isMembersLoading = false;
            }
        }

        bool _isGettingOnlineMembersCountInProcess = false;
        private const int OMCountUpdateInverval = 300;
        public async Task UpdateOnlineMembersCountAsync() {
            if (PeerType != PeerType.Chat) return;
            if (ChatSettings.State != UserStateInChat.In || ChatSettings.IsGroupChannel || _isGettingOnlineMembersCountInProcess) return;
            if (_onlineMembersCountLastUpdateTime + OMCountUpdateInverval > DateTimeOffset.Now.ToUnixTimeSeconds()) {
                Log.Warning($"UpdateOnlineMembersCountAsync: too early to update counter!");
                return;
            }

            _isGettingOnlineMembersCountInProcess = true;
            try {
                Log.Information($"UpdateOnlineMembersCountAsync peer: {PeerId}");
                var response = await session.API.Messages.GetChatOnlineAsync(session.GroupId, PeerId);
                OnlineMembersCount = response.OnlineCount;
                _onlineMembersCountLastUpdateTime = DateTimeOffset.Now.ToUnixTimeSeconds();
            } catch (Exception ex) {
                Log.Error(ex, "UpdateOnlineMembersCountAsync");
            } finally {
                _isGettingOnlineMembersCountInProcess = false;
            }
        }

        private void Setup(Conversation c) {
            PeerId = c.Peer.Id;
            if (SortId?.MajorId != c.SortId.MajorId || SortId?.MinorId != c.SortId.MinorId) SortId = c.SortId; // чтобы не дёргался listbox.
            UnreadMessagesCount = c.UnreadCount;
            CanWrite = c.CanWrite;
            InRead = c.InReadCMID;
            OutRead = c.OutReadCMID;
            PushSettings = c.PushSettings;
            IsMarkedAsUnread = c.IsMarkedUnread;
            IsImportant = c.Important;
            IsUnanswered = c.Unanswered;
            IsPinned = SortId.MajorId > 0 && SortId.MajorId % 16 == 0;

            if (PushSettings == null) PushSettings = new PushSettings {
                NoSound = false,
                DisabledForever = false,
                DisabledUntil = 0
            };

            if (c.UnreadReactions != null && c.UnreadReactions.Count > 0) UnreadReactions = new ObservableCollection<int>(c.UnreadReactions);
            if (c.Mentions != null && c.Mentions.Count > 0) {
                Mentions = new ObservableCollection<int>(c.Mentions);
                HasMention = true;
            }
            if (c.ExpireConvMessageIds != null && c.ExpireConvMessageIds.Count > 0) {
                HasSelfDestructMessage = true;
            }
            if (c.CurrentKeyboard != null && c.CurrentKeyboard.Buttons.Count > 0) Composer.BotKeyboard = c.CurrentKeyboard;

            if (PeerId.IsUser()) { // User
                PeerType = PeerType.User;
                PeerUser = CacheManager.GetUser(PeerId);
                IsFavoritesChat = PeerId == session.Id;
                if (IsFavoritesChat) {
                    Title = Assets.i18n.Resources.favorites;
                    Avatar = new Uri("https://vk.ru/images/icons/im_favorites_200.png");
                    // make favorites offline
                    var tmp = PeerUser.OnlineInfo;
                    tmp.IsMobile = false;
                    tmp.IsOnline = false;
                    Online = tmp;
                } else {
                    Title = string.Intern(PeerUser.FullName);
                    Avatar = new Uri(PeerUser.Photo200);
                    Online = PeerUser.OnlineInfo;
                }
                IsVerified = PeerUser.Verified == 1;
            } else if (PeerId.IsGroup()) { // Group
                PeerType = PeerType.Group;
                PeerGroup = CacheManager.GetGroup(PeerId);
                Title = string.Intern(PeerGroup.Name);
                Avatar = new Uri(PeerGroup.Photo200);
                IsVerified = PeerGroup.Verified == 1;
                Subtitle = PeerGroup.Activity?.ToLowerInvariant();
            } else if (PeerId.IsChat()) { // Chat
                PeerType = PeerType.Chat;
                ChatSettings = c.ChatSettings;
                Title = string.Intern(ChatSettings.Title);
                Avatar = ChatSettings?.Photo?.Uri;
                if (ChatSettings.PinnedMessage != null)
                    PinnedMessage = MessageViewModel.Create(ChatSettings.PinnedMessage, session);
                UpdateSubtitleForChat();
            } else if (PeerId.IsContact()) { // Contact?
                PeerType = PeerType.Contact;
                Title = $"Contact {PeerId}";
            } else if (PeerId.IsEmail()) { // E-mail
                PeerType = PeerType.Email;
                Title = $"E-Mail {PeerId}";
            }

            _sourceTitle = Title;
            _sourceAvatar = Avatar;
            ApplyLocalPeerOverrides();
            RefreshE2EState();

            // Checking and displaying activity status
            if (DemoMode.IsEnabled) {
                var ds = DemoMode.GetDemoSessionById(session.Id);
                if (ds.ActivityStatuses.ContainsKey(PeerId.ToString())) {
                    foreach (var status in ds.ActivityStatuses[PeerId.ToString()]) {
                        ActivityStatusUsers.Add(status, 1000 * 3600);
                    }
                    UpdateActivityStatus();
                }
                OpenProfileCommand = new RelayCommand((o) => { });
            } else {
                OpenProfileCommand = new RelayCommand(OpenPeerProfile);
                GoToLastMessageCommand = new RelayCommand(async (o) => await GoToLastMessageAsync());
                GoToLastReactedMessageCommand = new RelayCommand(async (o) => await GoToLastReactedMessageAsync());
            }

            UpdateRestrictionInfo();
        }

        public void ApplyLocalPeerOverrides() {
            string alias = Settings.GetPeerLocalAlias(PeerId);
            Uri localAvatar = Settings.GetPeerLocalAvatarUri(PeerId);

            Title = String.IsNullOrWhiteSpace(alias) ? _sourceTitle ?? Title : alias;
            Avatar = localAvatar ?? _sourceAvatar;
        }

        public void RefreshStreamerMode() {
            OnPropertyChanged(nameof(DisplayTitle));
            OnPropertyChanged(nameof(DisplaySubtitle));
            OnPropertyChanged(nameof(DisplayActivityStatus));
            OnPropertyChanged(nameof(DisplayAvatar));
            OnPropertyChanged(nameof(DisplayInitials));
            OnPropertyChanged(nameof(DisplayAvatarSeed));
            OnPropertyChanged(nameof(LastMessage));

            foreach (MessageViewModel message in ReceivedMessages) {
                message.RefreshStreamerMode();
            }
            PinnedMessage?.RefreshStreamerMode();
        }

        public void RefreshLocalFolderState() {
            OnPropertyChanged(nameof(IsArchived));
            OnPropertyChanged(nameof(LocalTags));
            OnPropertyChanged(nameof(LocalTagsText));
            OnPropertyChanged(nameof(HasLocalTags));
        }

        public void RefreshE2EState() {
            E2EPeerState state = E2EManager.GetPeerState(PeerId);
            if (state == null) {
                HasE2EStatus = false;
                E2EStatusText = null;
                E2EStatusIconId = null;
                E2EStatusTip = null;
                return;
            }

            string profileTitle = E2ESecurityProfileIds.GetTitle(state.ProfileId);
            bool hasLocalKeys = E2EKeyStore.HasPeerKeys(PeerId, state.ProfileId);

            HasE2EStatus = true;
            E2EStatusIconId = state.IsKeyChanged
                ? VKIconNames.Icon20UnlockOutline
                : state.IsVerified ? VKIconNames.Icon20Check : VKIconNames.Icon20LockOutline;

            if (state.IsKeyChanged) {
                E2EStatusText = $"E2E {profileTitle}: ключ изменился";
            } else if (state.IsVerified) {
                E2EStatusText = $"E2E {profileTitle}: сверен";
            } else if (hasLocalKeys) {
                E2EStatusText = $"E2E {profileTitle}: не сверен";
            } else if (state.IsLaneyPeer) {
                E2EStatusText = $"Laney E2E: нужен ключ";
            } else {
                E2EStatusText = $"E2E {profileTitle}";
            }
            if (state.UsesX25519Handshake && hasLocalKeys) E2EStatusText += " · X25519";
            if (state.AutoEncryptText && hasLocalKeys) E2EStatusText += " · auto";

            string fingerprint = String.IsNullOrWhiteSpace(state.Fingerprint) ? "нет fingerprint" : state.Fingerprint;
            string sas = String.IsNullOrWhiteSpace(state.Sas) ? "нет SAS" : state.Sas;
            string backup = state.TrustedBackupCreatedAtUnix > 0
                ? DateTimeOffset.FromUnixTimeSeconds(state.TrustedBackupCreatedAtUnix).ToLocalTime().ToString("g")
                : "нет";
            E2EStatusTip = $"{E2EStatusText}\nFingerprint: {fingerprint}\nSAS: {sas}\nMode: {(state.UsesX25519Handshake ? "X25519/HKDF chain" : "passphrase static")}\nAuto-encrypt: {(state.AutoEncryptText ? "on" : "off")}\nTrusted backup: {backup}";
        }

        private void UpdateSubtitleForChat() {
            if (ChatSettings.State == UserStateInChat.In) {
                Subtitle = String.Empty;
                StringBuilder sb = new StringBuilder();

                if (ChatSettings.IsDisappearing) sb.Append($"{Assets.i18n.Resources.casper_chat.ToLowerInvariant()}, ");
                sb.Append(Localizer.GetDeclensionFormatted(ChatSettings.MembersCount, "members_sub"));
                if (OnlineMembersCount > 0) sb.Append($", {OnlineMembersCount} {Assets.i18n.Resources.online}");
                Subtitle = sb.ToString();
            } else {
                Subtitle = ChatSettings.State == UserStateInChat.Left ? Assets.i18n.Resources.chat_left.ToLowerInvariant() : Assets.i18n.Resources.chat_kicked.ToLowerInvariant();
            }
        }

        private void UpdateRestrictionInfo() {
            if (CanWrite.Allowed) {
                RestrictionReason = String.Empty; return;
            }

            if (PeerType == PeerType.Chat) {
                if (ChatSettings.State != UserStateInChat.In) {
                    RestrictionReason = Localizer.Get($"chat_{ChatSettings.State.ToString().ToLower()}");
                } else {
                    DateTime restrictedUntil = DateTime.Now;
                    if (CanWrite.Reason == 983) {
                        restrictedUntil = DateTimeOffset.FromUnixTimeSeconds(CanWrite.Until).LocalDateTime;
                        RestrictionReason = CanWrite.Until > 0
                            ? Localizer.GetFormatted("writing_disabled_for_you_until", restrictedUntil.ToHumanizedString(true))
                            : Assets.i18n.Resources.writing_disabled_for_you;
                    } else if (CanWrite.Reason == 1012) {
                        restrictedUntil = DateTimeOffset.FromUnixTimeSeconds(ChatSettings.WritingDisabled.UntilTS).LocalDateTime;
                        RestrictionReason = ChatSettings.WritingDisabled.UntilTS > 0
                            ? Localizer.GetFormatted("writing_disabled_for_all_until", restrictedUntil.ToHumanizedString(true))
                            : Assets.i18n.Resources.writing_disabled_for_all;
                    } else {
                        RestrictionReason = VKAPIHelper.GetUnderstandableErrorMessage(CanWrite.Reason, Assets.i18n.Resources.cannot_write);
                    }
                }
            } else if (PeerType == PeerType.User) {
                switch (CanWrite.Reason) {
                    case 18:
                        if (PeerUser.Deactivated == DeactivationState.Deleted) RestrictionReason = Assets.i18n.Resources.user_deleted;
                        if (PeerUser.Deactivated == DeactivationState.Banned) RestrictionReason = Assets.i18n.Resources.user_blocked;
                        break;
                    case 900:
                        if (PeerUser.Blacklisted == 1) RestrictionReason = Localizer.Get("user_blacklisted", PeerUser.Sex);
                        if (PeerUser.BlacklistedByMe == 1) RestrictionReason = Localizer.Get("user_blacklisted_by_me", PeerUser.Sex);
                        break;
                    default:
                        RestrictionReason = VKAPIHelper.GetUnderstandableErrorMessage(CanWrite.Reason, Assets.i18n.Resources.cannot_write);
                        break;
                }
            } else if (PeerType == PeerType.Group) {
                RestrictionReason = VKAPIHelper.GetUnderstandableErrorMessage(CanWrite.Reason, Assets.i18n.Resources.cannot_write);
            }
        }

        private string GetMentionIcon() {
            if (HasSelfDestructMessage) return VKIconNames.Icon12Bomb;
            if (HasMention) return VKIconNames.Icon12Mention;
            return null;
        }

        bool eventsAlreadySetup = false;
        private void SetUpEvents() {
            if (eventsAlreadySetup) return;
            eventsAlreadySetup = true;
            // При приёме сообщения обновляем последнее сообщение.
            ReceivedMessages.CollectionChanged += (a, b) => OnPropertyChanged(nameof(LastMessage));
            SelectedMessages.SelectionChanged += SelectedMessages_SelectionChanged;

            PropertyChanged += (a, b) => {
                if (b.PropertyName == nameof(Online)) {
                    // make an empty subtitle if it is favorites
                    if (PeerId == session.Id) Subtitle = Assets.i18n.Resources.saved_messages;
                    else Subtitle = VKAPIHelper.GetOnlineInfo(Online, PeerUser.Sex).ToLowerInvariant();
                }

                if (b.PropertyName == nameof(OnlineMembersCount)) {
                    UpdateSubtitleForChat();
                }

                if (b.PropertyName == nameof(HasMention) || b.PropertyName == nameof(HasSelfDestructMessage))
                    OnPropertyChanged(nameof(MentionIconId));
            };

            if (!DemoMode.IsEnabled) {
                session.CurrentOpenedChatChanged += (a, b) => IsCurrentOpenedChat = b == PeerId;

                session.LongPoll.MessageFlagSet += LongPoll_MessageFlagSet;
                session.LongPoll.MessageReceived += LongPoll_MessageReceived;
                session.LongPoll.MessageEdited += LongPoll_MessageEdited;
                session.LongPoll.MentionReceived += LongPoll_MentionReceived;
                session.LongPoll.IncomingMessagesRead += LongPoll_IncomingMessagesRead;
                session.LongPoll.OutgoingMessagesRead += LongPoll_OutgoingMessagesRead;
                session.LongPoll.ConversationFlagReset += LongPoll_ConversationFlagReset;
                session.LongPoll.ConversationFlagSet += LongPoll_ConversationFlagSet;
                session.LongPoll.ConversationRemoved += LongPoll_ConversationRemoved;
                session.LongPoll.MajorIdChanged += LongPoll_MajorIdChanged;
                session.LongPoll.MinorIdChanged += LongPoll_MinorIdChanged;
                session.LongPoll.ConversationDataChanged += LongPoll_ConversationDataChanged;
                session.LongPoll.ActivityStatusChanged += LongPoll_ActivityStatusChanged;
                session.LongPoll.CanWriteChanged += LongPoll_CanWriteChanged;
                session.LongPoll.NotificationsSettingsChanged += LongPoll_NotificationsSettingsChanged;
                session.LongPoll.UnreadReactionsChanged += LongPoll_UnreadReactionsChanged;

                if (!session.IsGroup) VKQueue.Online += VKQueue_Online;
            }

            ActivityStatusUsers.Elapsed += ActivityStatusUsers_Elapsed;
        }

        private void ActivityStatusUsers_Elapsed(object sender, LongPollActivityInfo e) {
            UpdateActivityStatus();
        }

        private void SelectedMessages_SelectionChanged(object sender, SelectionModelSelectionChangedEventArgs<MessageViewModel> e) {
            SelectedMessagesCount = SelectedMessages.Count;
            MessagesCommands.Clear();
            if (SelectedMessagesCount > 0) {
                Command reply = new Command(VKIconNames.Icon24ReplyOutline, Assets.i18n.Resources.reply, false, ReplyToMessageCommand);
                Command fwdhere = new Command(VKIconNames.Icon24ReplyOutline, Assets.i18n.Resources.forward_here, false, ForwardHereCommand);
                Command forward = new Command(VKIconNames.Icon24ShareOutline, Assets.i18n.Resources.forward, false, ForwardCommand);

                bool isChannel = ChatSettings != null && ChatSettings.IsGroupChannel;
                if (!isChannel) MessagesCommands.Add(SelectedMessagesCount == 1 ? reply : fwdhere);
                MessagesCommands.Add(forward);
            }
        }

        #region Commands

        private void OpenPeerProfile(object o) {
            new System.Action(async () => await Router.OpenPeerProfileAsync(session, PeerId))();
        }

        public async Task GoToLastMessageAsync() {
            if (IsLoading) return;

            int lastMessageId = LastMessage?.ConversationMessageId ?? DisplayedMessages?.LastOrDefault()?.ConversationMessageId ?? 0;
            if (lastMessageId <= 0) return;

            if (DisplayedMessages?.LastOrDefault()?.ConversationMessageId == lastMessageId) {
                MessageViewModel lastDisplayed = DisplayedMessages.LastOrDefault();
                if (lastDisplayed != null) ScrollToMessageRequested?.Invoke(this, lastDisplayed);
                return;
            }

            Log.Information("GoToLastMessage: last message is not displayed. Loading tail page from API. Peer={0}; cmid={1}", PeerId, lastMessageId);
            await LoadMessagesAsync(lastMessageId);
        }

        private async Task GoToLastReactedMessageAsync() {
            if (IsLoading) return;
            if (UnreadReactions != null && UnreadReactions.Count > 0) await GoToMessageAsync(UnreadReactions.LastOrDefault());
        }

        public void ClearSelectedMessages() {
            SelectedMessages.Clear();
        }

        private void ReplyToMessageCommand(object o) {
            if (SelectedMessages.Count > 0) Composer.AddReply(SelectedMessages.SelectedItem);
            SelectedMessages.Clear();
        }

        private void ForwardHereCommand(object o) {
            Composer.Clear();
            Composer.AddForwardedMessages(PeerId, SelectedMessages.SelectedItems.ToList(), session.GroupId);
            SelectedMessages.Clear();
        }

        private void ForwardCommand(object o) {
            session.Share(PeerId, SelectedMessages.SelectedItems.ToList());
        }

        public void ShowContextMenuForSelectedMessages(object p) {
            ContextMenuHelper.ShowForMultipleMessages(SelectedMessages.SelectedItems.ToList(), this, (Control)p);
        }

        #endregion

        #region Loading messages

        public async Task GoToMessageAsync(MessageViewModel message) {
            if (message == null) return;
            if (!message.IsUnavailable) {
                await GoToMessageAsync(message.ConversationMessageId);
            } else {
                StandaloneMessageViewer smv = new StandaloneMessageViewer(session, message.RootMessage);
                await smv.ShowDialog(session.Window);
            }
        }

        public async Task ReloadMessagesAsync() {
            await LoadMessagesAsync();
        }

        public async Task GoToMessageAsync(int id) {
            if (id == 0) return;
            if (DisplayedMessages == null) {
                await LoadMessagesAsync(id);
                return;
            }
            MessageViewModel msg = DisplayedMessages.GetById(id);
            // TODO: искать ещё и в received messages.
            if (msg != null) {
                ScrollToMessageRequested?.Invoke(this, msg);
            } else {
                await LoadMessagesAsync(id);
            }
        }

        public async Task JumpToDateAsync(DateTime date) {
            DateTime target = date.Date;
            if (DemoMode.IsEnabled) {
                await JumpToDateInDemoAsync(target);
                return;
            }

            int loadedMessageId = FindLoadedMessageIdByDate(target);
            if (loadedMessageId > 0) {
                await GoToMessageAsync(loadedMessageId);
                return;
            }

            int messageId = await FindMessageIdByDateViaApiAsync(target);
            if (messageId > 0) await GoToMessageAsync(messageId);
        }

        private async Task JumpToDateInDemoAsync(DateTime target) {
            DemoModeSession ds = DemoMode.GetDemoSessionById(session.Id);
            Message message = ds.Messages
                .Where(m => m.PeerId == PeerId && m.DateTime >= target)
                .OrderBy(m => m.DateUnix)
                .ThenBy(m => m.ConversationMessageId)
                .FirstOrDefault();

            message ??= ds.Messages
                .Where(m => m.PeerId == PeerId)
                .OrderByDescending(m => m.DateUnix)
                .ThenByDescending(m => m.ConversationMessageId)
                .FirstOrDefault();

            if (message != null) await GoToMessageAsync(message.ConversationMessageId);
        }

        private int FindLoadedMessageIdByDate(DateTime target) {
            if (DisplayedMessages == null || DisplayedMessages.Count == 0) return 0;

            MessageViewModel message = DisplayedMessages
                .Where(m => m.SentTime >= target)
                .OrderBy(m => m.SentTime)
                .ThenBy(m => m.ConversationMessageId)
                .FirstOrDefault();

            return message?.ConversationMessageId ?? 0;
        }

        private async Task<int> FindMessageIdByDateViaApiAsync(DateTime target) {
            int lastMessageId = LastMessage?.ConversationMessageId ?? DisplayedMessages?.LastOrDefault()?.ConversationMessageId ?? 0;
            if (lastMessageId <= 0) return 0;

            Message lastMessage = await LoadDateProbeMessageAsync(lastMessageId);
            if (lastMessage != null && lastMessage.DateTime < target) return lastMessage.ConversationMessageId;

            int low = 1;
            int high = lastMessageId;
            int best = lastMessage?.ConversationMessageId ?? 0;

            for (int i = 0; i < 18 && low <= high; i++) {
                int middle = low + (high - low) / 2;
                List<Message> page = await LoadDateProbeMessagesAsync(middle);
                if (page.Count == 0) {
                    high = middle - 1;
                    continue;
                }

                Message firstAtOrAfter = page
                    .Where(m => m.DateTime >= target)
                    .OrderBy(m => m.DateUnix)
                    .ThenBy(m => m.ConversationMessageId)
                    .FirstOrDefault();

                int minId = page.Min(m => m.ConversationMessageId);
                int maxId = page.Max(m => m.ConversationMessageId);

                if (firstAtOrAfter != null) {
                    best = firstAtOrAfter.ConversationMessageId;
                    high = minId - 1;
                } else {
                    low = maxId + 1;
                }
            }

            return best;
        }

        private async Task<Message> LoadDateProbeMessageAsync(int conversationMessageId) {
            List<Message> messages = await LoadDateProbeMessagesAsync(conversationMessageId, 1);
            return messages.FirstOrDefault();
        }

        private async Task<List<Message>> LoadDateProbeMessagesAsync(int conversationMessageId, int count = 24) {
            MessagesHistoryResponse mhr = await session.API.Messages.GetHistoryAsync(session.GroupId, PeerId, -count / 2, count, conversationMessageId, true, VKAPIHelper.Fields, false, Constants.NestedMessagesLimit);
            CacheManager.Add(mhr.Profiles);
            CacheManager.Add(mhr.Groups);
            return mhr.Items?
                .Where(m => m.PeerId == PeerId && m.ConversationMessageId > 0)
                .DistinctBy(m => m.ConversationMessageId)
                .OrderBy(m => m.ConversationMessageId)
                .ToList() ?? new List<Message>();
        }

        private async Task LoadMessagesAsync(int startMessageId = -1) {
            if (DemoMode.IsEnabled) {
                DemoModeSession ds = DemoMode.GetDemoSessionById(session.Id);
                List<Message> allMessages = ds.Messages.Where(m => m.PeerId == PeerId).OrderBy(m => m.ConversationMessageId).ToList();
                int demoCount = Constants.MessagesCount;
                int center = startMessageId > 0 ? startMessageId : allMessages.LastOrDefault()?.ConversationMessageId ?? 0;
                int centerIndex = Math.Max(0, allMessages.FindIndex(m => m.ConversationMessageId >= center));
                int startIndex = startMessageId > 0
                    ? Math.Max(0, centerIndex - demoCount / 2)
                    : Math.Max(0, allMessages.Count - demoCount);
                var messages = allMessages.Skip(startIndex).Take(demoCount).ToList();
                DisplayedMessages = new MessagesCollection(FilterLocallyVisibleMessages(MessageViewModel.BuildFromAPI(messages, session, true, FixState)));
                await ApplyExpiredSelfDestructMessagesAsync();
                RefreshE2EState();
                _ = OfflineCacheStore.SaveChatSnapshotAsync(this);
                int scrollTo = startMessageId > 0
                    ? startMessageId
                    : DisplayedMessages.LastOrDefault()?.ConversationMessageId ?? 0;
                MessageViewModel scrollToMsg = scrollTo > 0 ? DisplayedMessages.GetById(scrollTo) : null;
                if (scrollToMsg != null) ScrollToMessageRequested?.Invoke(this, scrollToMsg);

                return;
            }

            if (IsLoading) return;
            Placeholder = null;
            DisplayedMessages?.Clear();

            int count = Constants.MessagesCount;
            try {
                Log.Information("LoadMessages peer: {0}, count: {1}, startMessageId: {2}", PeerId, count, startMessageId);
                IsLoading = true;
                int offset = -count / 2;

                // TODO: use messages.getHistory, т. к. участников получаем сразу после первой загрузки сообщений.
                MessagesHistoryResponse mhr = await session.API.Messages.GetHistoryAsync(session.GroupId, PeerId, offset, count, startMessageId, true, VKAPIHelper.Fields, false, Constants.NestedMessagesLimit);
                CacheManager.Add(mhr.Profiles);
                CacheManager.Add(mhr.Groups);

                Setup(mhr.Conversations[0]);
                mhr.Items?.Reverse();
                DisplayedMessages = new MessagesCollection(FilterLocallyVisibleMessages(MessageViewModel.BuildFromAPI(mhr.Items, session, false, FixState)));
                await ApplyExpiredSelfDestructMessagesAsync();
                RefreshE2EState();
                _ = OfflineCacheStore.SaveChatSnapshotAsync(this);

                int scrollTo = startMessageId > 0
                    ? startMessageId
                    : DisplayedMessages.LastOrDefault()?.ConversationMessageId ?? 0;
                if (scrollTo > 0) {
                    MessageViewModel scrollToMsg = DisplayedMessages.SingleOrDefault(m => m.ConversationMessageId == scrollTo);
                    if (scrollToMsg != null) {
                        ScrollToMessageRequested?.Invoke(this, scrollToMsg);
                    } else {
                        Log.Warning($"LoadMessages: cannot find message with cmid {scrollTo}, so cannot scroll to this message!");
                    }
                }

                if (Members.Count == 0) {
                    IsLoading = false; // нужно чтобы LoadMembers не блочило переход по сообщениям
                    await UpdateOnlineMembersCountAsync();
                    await LoadMembersAsync();
                }
            } catch (Exception ex) {
                if (await TryLoadOfflineMessagesAsync(startMessageId, ex)) return;
                Placeholder = PlaceholderViewModel.GetForException(ex, async (o) => await LoadMessagesAsync(startMessageId));
            } finally {
                IsLoading = false;

                await Task.Delay(2000);
                GC.Collect(2, GCCollectionMode.Aggressive);
            }
        }

        public async Task LoadPreviousMessagesAsync(CancellationToken? ct) {
            if (DemoMode.IsEnabled) {
                await LoadPreviousDemoMessagesAsync();
                return;
            }
            if (DisplayedMessages?.Count == 0 || IsLoading) return;
            int count = Constants.MessagesCount;

            try {
                Log.Information("LoadPreviousMessages peer: {0}, count: {1}, displayed messages count: {2}", PeerId, count, DisplayedMessages?.Count);
                IsLoading = true;
                IsPreviousMessagesLoading = true;
                MessagesHistoryResponse mhr = await session.API.Messages.GetHistoryAsync(session.GroupId, PeerId, 1, count, DisplayedMessages.First.ConversationMessageId, true, VKAPIHelper.Fields, false, Constants.NestedMessagesLimit);
                CacheManager.Add(mhr.Profiles);
                CacheManager.Add(mhr.Groups);
                mhr.Items?.Reverse();
                DisplayedMessages.InsertRange(FilterLocallyVisibleMessages(mhr.Items?.Select(m => {
                    var msg = MessageViewModel.Create(m, session);
                    FixState(msg);
                    return msg;
                })));
                MessagesChunkLoaded?.Invoke(this, false);
                await ApplyExpiredSelfDestructMessagesAsync();
                RefreshE2EState();
                _ = OfflineCacheStore.SaveChatSnapshotAsync(this);
            } catch (Exception ex) {
                if (await ExceptionHelper.ShowErrorDialogAsync(session.Window, ex)) {
                    await LoadPreviousMessagesAsync(ct);
                }
            } finally {
                IsPreviousMessagesLoading = false;
                IsLoading = false;
            }
        }

        public async Task LoadNextMessagesAsync(CancellationToken? ct) {
            if (DemoMode.IsEnabled) {
                await LoadNextDemoMessagesAsync();
                return;
            }
            if (DisplayedMessages?.Count == 0 || IsLoading) return;
            if (LastMessage?.ConversationMessageId == DisplayedMessages.LastOrDefault()?.ConversationMessageId) return;
            int count = Constants.MessagesCount;

            try {
                Log.Information("LoadNextMessages peer: {0}, count: {1}, displayed messages count: {2}", PeerId, count, DisplayedMessages.Count);
                IsLoading = true;
                IsNextMessagesLoading = true;
                MessagesHistoryResponse mhr = await session.API.Messages.GetHistoryAsync(session.GroupId, PeerId, -count, count, DisplayedMessages.Last.ConversationMessageId, true, VKAPIHelper.Fields, false, Constants.NestedMessagesLimit);
                CacheManager.Add(mhr.Profiles);
                CacheManager.Add(mhr.Groups);
                mhr.Items?.Reverse();
                DisplayedMessages.InsertRange(FilterLocallyVisibleMessages(mhr.Items?.Select(m => {
                    var msg = MessageViewModel.Create(m, session);
                    FixState(msg);
                    return msg;
                })));
                MessagesChunkLoaded?.Invoke(this, true);
                await ApplyExpiredSelfDestructMessagesAsync();
                RefreshE2EState();
                _ = OfflineCacheStore.SaveChatSnapshotAsync(this);
            } catch (Exception ex) {
                if (await ExceptionHelper.ShowErrorDialogAsync(session.Window, ex)) {
                    await LoadNextMessagesAsync(ct);
                }
            } finally {
                IsNextMessagesLoading = false;
                IsLoading = false;
            }
        }

        private async Task LoadPreviousDemoMessagesAsync() {
            if (DisplayedMessages?.Count == 0 || IsLoading) return;
            IsLoading = true;
            IsPreviousMessagesLoading = true;

            try {
                DemoModeSession ds = DemoMode.GetDemoSessionById(session.Id);
                int firstId = DisplayedMessages.First.ConversationMessageId;
                List<Message> messages = ds.Messages
                    .Where(m => m.PeerId == PeerId && m.ConversationMessageId < firstId)
                    .OrderByDescending(m => m.ConversationMessageId)
                    .Take(Constants.MessagesCount)
                    .OrderBy(m => m.ConversationMessageId)
                    .ToList();
                if (messages.Count == 0) return;

                Log.Information("LoadPreviousMessages demo peer: {0}, count: {1}, displayed messages count: {2}", PeerId, messages.Count, DisplayedMessages.Count);
                DisplayedMessages.InsertRange(FilterLocallyVisibleMessages(MessageViewModel.BuildFromAPI(messages, session, true, FixState)));
                MessagesChunkLoaded?.Invoke(this, false);
                await ApplyExpiredSelfDestructMessagesAsync();
                RefreshE2EState();
                _ = OfflineCacheStore.SaveChatSnapshotAsync(this);
            } finally {
                IsPreviousMessagesLoading = false;
                IsLoading = false;
            }
        }

        private async Task LoadNextDemoMessagesAsync() {
            if (DisplayedMessages?.Count == 0 || IsLoading) return;
            if (LastMessage?.ConversationMessageId == DisplayedMessages.LastOrDefault()?.ConversationMessageId) return;
            IsLoading = true;
            IsNextMessagesLoading = true;

            try {
                DemoModeSession ds = DemoMode.GetDemoSessionById(session.Id);
                int lastId = DisplayedMessages.Last.ConversationMessageId;
                List<Message> messages = ds.Messages
                    .Where(m => m.PeerId == PeerId && m.ConversationMessageId > lastId)
                    .OrderBy(m => m.ConversationMessageId)
                    .Take(Constants.MessagesCount)
                    .ToList();
                if (messages.Count == 0) return;

                Log.Information("LoadNextMessages demo peer: {0}, count: {1}, displayed messages count: {2}", PeerId, messages.Count, DisplayedMessages.Count);
                DisplayedMessages.InsertRange(FilterLocallyVisibleMessages(MessageViewModel.BuildFromAPI(messages, session, true, FixState)));
                MessagesChunkLoaded?.Invoke(this, true);
                await ApplyExpiredSelfDestructMessagesAsync();
                RefreshE2EState();
                _ = OfflineCacheStore.SaveChatSnapshotAsync(this);
            } finally {
                IsNextMessagesLoading = false;
                IsLoading = false;
            }
        }

        private async Task<bool> TryLoadOfflineMessagesAsync(int startMessageId, Exception sourceException) {
            List<MessageViewModel> messages = await OfflineCacheStore.LoadChatMessagesAsync(session, this, startMessageId, Constants.MessagesCount, FixState);
            if (messages.Count == 0) return false;

            Log.Warning(sourceException, "Loaded offline chat snapshot after history load failure. Peer={PeerId}; messages={Count}", PeerId, messages.Count);
            Placeholder = null;
            DisplayedMessages = new MessagesCollection(FilterLocallyVisibleMessages(messages));
            RefreshE2EState();

            int scrollTo = startMessageId > 0 ? startMessageId : DisplayedMessages.LastOrDefault()?.ConversationMessageId ?? 0;
            MessageViewModel scrollToMsg = scrollTo > 0 ? DisplayedMessages.GetById(scrollTo) : null;
            scrollToMsg ??= DisplayedMessages.LastOrDefault();
            if (scrollToMsg != null) ScrollToMessageRequested?.Invoke(this, scrollToMsg);
            return true;
        }

        public List<MessageViewModel> HideMessagesLocally(IEnumerable<MessageViewModel> messages) {
            if (DisplayedMessages == null || messages == null) return new List<MessageViewModel>();

            List<MessageViewModel> hiddenMessages = messages
                .Where(m => m != null && DisplayedMessages.Contains(m))
                .DistinctBy(m => m.ConversationMessageId)
                .OrderBy(m => m.ConversationMessageId)
                .ToList();

            if (hiddenMessages.Count == 0) return hiddenMessages;

            Settings.HideMessagesLocally(PeerId, hiddenMessages.Select(m => m.ConversationMessageId));
            foreach (MessageViewModel message in hiddenMessages) {
                DisplayedMessages.Remove(message);
            }

            Log.Information("Messages hidden locally. Peer={PeerId}; count={Count}", PeerId, hiddenMessages.Count);
            return hiddenMessages;
        }

        public void RestoreLocallyHiddenMessages(IEnumerable<MessageViewModel> messages) {
            List<MessageViewModel> restoredMessages = messages?
                .Where(m => m != null)
                .DistinctBy(m => m.ConversationMessageId)
                .OrderBy(m => m.ConversationMessageId)
                .ToList() ?? new List<MessageViewModel>();

            if (restoredMessages.Count == 0) return;

            Settings.UnhideMessagesLocally(PeerId, restoredMessages.Select(m => m.ConversationMessageId));
            if (DisplayedMessages == null) {
                DisplayedMessages = new MessagesCollection(restoredMessages);
            } else {
                DisplayedMessages.InsertRange(restoredMessages);
            }

            Log.Information("Locally hidden messages restored. Peer={PeerId}; count={Count}", PeerId, restoredMessages.Count);
        }

        public int ShadowBanSenderLocally(long senderId) {
            if (senderId == 0 || senderId == session.Id) return 0;

            Settings.ShadowBanSenderLocally(PeerId, senderId);
            List<MessageViewModel> displayed = DisplayedMessages?
                .Where(m => m.SenderId == senderId)
                .ToList() ?? new List<MessageViewModel>();
            List<MessageViewModel> received = ReceivedMessages
                .Where(m => m.SenderId == senderId)
                .ToList();

            foreach (MessageViewModel message in displayed) {
                DisplayedMessages.Remove(message);
            }

            foreach (MessageViewModel message in received) {
                ReceivedMessages.Remove(message);
            }

            Log.Information("Sender shadow-banned locally. Peer={PeerId}; sender={SenderId}; displayed={DisplayedCount}; received={ReceivedCount}", PeerId, senderId, displayed.Count, received.Count);
            return displayed.Count + received.Count;
        }

        public bool IsSenderShadowBanned(long senderId) {
            return Settings.IsSenderShadowBanned(PeerId, senderId);
        }

        public void MuteSenderLocally(long senderId) {
            if (senderId == 0 || senderId == session.Id) return;

            Settings.MuteSenderLocally(PeerId, senderId);
            Log.Information("Sender muted locally. Peer={PeerId}; sender={SenderId}", PeerId, senderId);
        }

        public bool IsSenderMutedLocally(long senderId) {
            return Settings.IsSenderMutedLocally(PeerId, senderId);
        }

        public void RefreshSelfDestructState() {
            HasSelfDestructMessage = Settings.GetSelfDestructMessages(PeerId).Count > 0;
        }

        public async Task ApplyExpiredSelfDestructMessagesAsync() {
            List<SelfDestructMessageSchedule> expired = Settings.GetExpiredSelfDestructMessages(PeerId, DateTimeOffset.Now);
            if (expired.Count == 0) {
                RefreshSelfDestructState();
                return;
            }

            List<int> ids = expired.Select(s => s.ConversationMessageId).Distinct().ToList();
            List<MessageViewModel> displayed = DisplayedMessages?
                .Where(m => ids.Contains(m.ConversationMessageId))
                .ToList() ?? new List<MessageViewModel>();

            Settings.HideMessagesLocally(PeerId, ids);
            foreach (MessageViewModel message in displayed) {
                DisplayedMessages.Remove(message);
            }

            if (!DemoMode.IsEnabled && expired.Any(s => s.BestEffortDelete)) {
                List<int> deleteIds = expired.Where(s => s.BestEffortDelete).Select(s => s.ConversationMessageId).Distinct().ToList();
                try {
                    await session.API.Messages.DeleteAsync(session.GroupId, PeerId, deleteIds, false, false);
                } catch (Exception ex) {
                    Log.Warning(ex, "Self-destruct best-effort VK delete failed. Peer={PeerId}; count={Count}", PeerId, deleteIds.Count);
                }
            }

            Settings.ClearSelfDestructMessages(PeerId, ids);
            RefreshSelfDestructState();
            Log.Information("Self-destruct applied. Peer={PeerId}; count={Count}", PeerId, ids.Count);
        }

        private List<MessageViewModel> FilterLocallyVisibleMessages(IEnumerable<MessageViewModel> messages) {
            List<MessageViewModel> source = messages?.ToList() ?? new List<MessageViewModel>();
            HashSet<int> hiddenIds = Settings.GetLocallyHiddenMessageIds(PeerId);
            HashSet<long> shadowBannedSenderIds = Settings.GetShadowBannedSenderIds(PeerId);
            List<ShadowBanTextRule> shadowTextRules = BuildShadowBanTextRules(Settings.GetShadowBannedTextRules(PeerId));
            ShadowBannedAttachmentKinds shadowAttachmentKinds = Settings.GetShadowBannedAttachmentKinds(PeerId);
            if (hiddenIds.Count == 0
                && shadowBannedSenderIds.Count == 0
                && shadowTextRules.Count == 0
                && shadowAttachmentKinds == ShadowBannedAttachmentKinds.None
                && !IsGroupAntiSpamEnabled()) return source;

            List<MessageViewModel> visible = source.Where(m => !IsMessageLocallyHidden(m, hiddenIds, shadowBannedSenderIds, shadowTextRules, shadowAttachmentKinds)).ToList();
            return FilterGroupSpamMessages(visible);
        }

        private bool IsMessageLocallyHidden(MessageViewModel message) {
            return IsMessageLocallyHidden(
                message,
                Settings.GetLocallyHiddenMessageIds(PeerId),
                Settings.GetShadowBannedSenderIds(PeerId),
                BuildShadowBanTextRules(Settings.GetShadowBannedTextRules(PeerId)),
                Settings.GetShadowBannedAttachmentKinds(PeerId));
        }

        private bool IsGroupAntiSpamEnabled() {
            return PeerType == PeerType.Chat && Settings.IsPeerAntiSpamEnabled(PeerId);
        }

        private List<MessageViewModel> FilterGroupSpamMessages(List<MessageViewModel> source) {
            if (!IsGroupAntiSpamEnabled() || source.Count == 0) return source;

            List<MessageViewModel> visible = new List<MessageViewModel>(source.Count);
            Dictionary<long, HashSet<string>> senderRecentTexts = new Dictionary<long, HashSet<string>>();
            List<int> quarantineIds = new List<int>();

            foreach (MessageViewModel message in source) {
                if (IsGroupSpamMessage(message, senderRecentTexts)) {
                    if (message.ConversationMessageId > 0) quarantineIds.Add(message.ConversationMessageId);
                    Log.Information("Local anti-spam hidden message. Peer={PeerId}; cmid={Cmid}; sender={SenderId}", PeerId, message.ConversationMessageId, message.SenderId);
                    continue;
                }

                RememberSpamSignature(message, senderRecentTexts);
                visible.Add(message);
            }

            if (quarantineIds.Count > 0) Settings.HideMessagesLocally(PeerId, quarantineIds);
            return visible;
        }

        private bool IsGroupSpamMessage(MessageViewModel message, Dictionary<long, HashSet<string>> senderRecentTexts) {
            if (!IsGroupAntiSpamEnabled() || message == null || message.IsOutgoing || message.IsImportant || message.Action != null) return false;

            string signature = NormalizeSpamText(message.Text);
            if (!String.IsNullOrWhiteSpace(signature)
                && senderRecentTexts.TryGetValue(message.SenderId, out HashSet<string> senderTexts)
                && senderTexts.Contains(signature)) return true;

            return IsLinkKeywordSpam(message) || IsCapsSpam(message) || IsSuspiciousLinkSpam(message);
        }

        private bool IsGroupSpamMessage(MessageViewModel message) {
            if (!IsGroupAntiSpamEnabled() || message == null || message.IsOutgoing || message.IsImportant || message.Action != null) return false;

            IEnumerable<MessageViewModel> recentMessages = ReceivedMessages?
                .Where(m => m != null && m.SenderId == message.SenderId)
                .Reverse()
                .Take(12) ?? Array.Empty<MessageViewModel>();
            string signature = NormalizeSpamText(message.Text);
            if (!String.IsNullOrWhiteSpace(signature) && recentMessages.Any(m => NormalizeSpamText(m.Text) == signature)) return true;

            return IsLinkKeywordSpam(message) || IsCapsSpam(message) || IsSuspiciousLinkSpam(message);
        }

        private static void RememberSpamSignature(MessageViewModel message, Dictionary<long, HashSet<string>> senderRecentTexts) {
            string signature = NormalizeSpamText(message?.Text);
            if (String.IsNullOrWhiteSpace(signature)) return;

            if (!senderRecentTexts.TryGetValue(message.SenderId, out HashSet<string> senderTexts)) {
                senderTexts = new HashSet<string>(StringComparer.Ordinal);
                senderRecentTexts[message.SenderId] = senderTexts;
            }

            if (senderTexts.Count >= 12) senderTexts.Clear();
            senderTexts.Add(signature);
        }

        private static bool IsLinkKeywordSpam(MessageViewModel message) {
            string text = message.Text;
            if (String.IsNullOrWhiteSpace(text)) return false;

            bool hasLink = AntiSpamLinkMarkers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase))
                || message.Attachments?.Any(a => a.Type == AttachmentType.Link) == true;
            if (!hasLink) return false;

            return AntiSpamKeywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsCapsSpam(MessageViewModel message) {
            string text = message?.Text;
            if (String.IsNullOrWhiteSpace(text) || text.Length < 24) return false;

            int letters = 0;
            int upper = 0;
            foreach (char c in text) {
                if (!Char.IsLetter(c)) continue;

                letters++;
                if (Char.IsUpper(c)) upper++;
            }

            return letters >= 14 && (double)upper / letters >= 0.78;
        }

        private static bool IsSuspiciousLinkSpam(MessageViewModel message) {
            string text = message?.Text;
            if (String.IsNullOrWhiteSpace(text)) return false;

            bool hasLink = AntiSpamLinkMarkers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase))
                || message.Attachments?.Any(a => a.Type == AttachmentType.Link) == true;
            if (!hasLink) return false;

            int linkMarkers = AntiSpamLinkMarkers.Count(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));
            bool hasSuspiciousMarker = SuspiciousLinkMarkers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));
            bool hasSuspiciousToken = SuspiciousLinkTokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));

            return linkMarkers >= 2 || hasSuspiciousMarker && (text.Length < 220 || hasSuspiciousToken);
        }

        private static string NormalizeSpamText(string text) {
            if (String.IsNullOrWhiteSpace(text)) return String.Empty;

            string normalized = new string(text
                .Where(c => !Char.IsWhiteSpace(c) && !Char.IsPunctuation(c))
                .Take(180)
                .ToArray())
                .ToLowerInvariant();

            return normalized.Length < 12 ? String.Empty : normalized;
        }

        private static bool IsMessageLocallyHidden(MessageViewModel message, HashSet<int> hiddenIds, HashSet<long> shadowBannedSenderIds, List<ShadowBanTextRule> shadowTextRules, ShadowBannedAttachmentKinds shadowAttachmentKinds) {
            if (message == null) return true;
            return hiddenIds.Contains(message.ConversationMessageId)
                || shadowBannedSenderIds.Contains(message.SenderId)
                || IsMessageFromAutoBlockedUser(message)
                || IsMessageShadowBannedByRelation(message, shadowBannedSenderIds, shadowTextRules)
                || IsMessageShadowBannedByText(message, shadowTextRules)
                || IsMessageShadowBannedByAttachment(message, shadowAttachmentKinds);
        }

        private static List<ShadowBanTextRule> BuildShadowBanTextRules(string rulesText) {
            if (String.IsNullOrWhiteSpace(rulesText)) return new List<ShadowBanTextRule>();

            List<ShadowBanTextRule> rules = new List<ShadowBanTextRule>();
            string[] lines = rulesText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string line in lines) {
                if (rules.Count >= ShadowBanTextRuleLimit) break;

                string rule = line.Trim();
                if (rule.Length == 0 || rule.Length > ShadowBanTextRuleMaxLength) continue;

                if (rule.Length > 2 && rule.StartsWith("/") && rule.EndsWith("/")) {
                    try {
                        rules.Add(new ShadowBanTextRule(new Regex(rule[1..^1], RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, ShadowBanRegexTimeout)));
                    } catch (ArgumentException ex) {
                        Log.Warning(ex, "Invalid shadow-ban regex rule skipped.");
                    }
                } else {
                    rules.Add(new ShadowBanTextRule(rule));
                }
            }

            return rules;
        }

        private static bool IsMessageShadowBannedByText(MessageViewModel message, List<ShadowBanTextRule> rules) {
            return IsTextShadowBanned(message?.Text, rules);
        }

        private static bool IsMessageFromAutoBlockedUser(MessageViewModel message) {
            return IsSenderAutoBlocked(message.SenderId);
        }

        private static bool IsSenderAutoBlocked(long senderId) {
            if (!Settings.AutoBlockBlacklistedUsers || !senderId.IsUser()) return false;

            User user = CacheManager.GetUser(senderId);
            return user?.BlacklistedByMe == 1;
        }

        private static bool IsMessageShadowBannedByRelation(MessageViewModel message, HashSet<long> shadowBannedSenderIds, List<ShadowBanTextRule> shadowTextRules) {
            if (message == null) return false;
            if ((shadowBannedSenderIds == null || shadowBannedSenderIds.Count == 0)
                && (shadowTextRules == null || shadowTextRules.Count == 0)
                && !Settings.AutoBlockBlacklistedUsers) return false;

            if (IsApiMessageShadowBannedByRelation(message.ReplyMessage, shadowBannedSenderIds, shadowTextRules, 0)) return true;
            if (message.ForwardedMessages == null || message.ForwardedMessages.Count == 0) return false;

            foreach (Message forwarded in message.ForwardedMessages) {
                if (IsApiMessageShadowBannedByRelation(forwarded, shadowBannedSenderIds, shadowTextRules, 0)) return true;
            }

            return false;
        }

        private static bool IsApiMessageShadowBannedByRelation(Message message, HashSet<long> shadowBannedSenderIds, List<ShadowBanTextRule> shadowTextRules, int depth) {
            if (message == null || depth > 16) return false;
            if (shadowBannedSenderIds?.Contains(message.FromId) == true) return true;
            if (IsSenderAutoBlocked(message.FromId)) return true;
            if (IsTextShadowBanned(message.Text, shadowTextRules)) return true;

            if (IsApiMessageShadowBannedByRelation(message.ReplyMessage, shadowBannedSenderIds, shadowTextRules, depth + 1)) return true;
            if (message.ForwardedMessages == null || message.ForwardedMessages.Count == 0) return false;

            foreach (Message forwarded in message.ForwardedMessages) {
                if (IsApiMessageShadowBannedByRelation(forwarded, shadowBannedSenderIds, shadowTextRules, depth + 1)) return true;
            }

            return false;
        }

        private static bool IsMessageShadowBannedByAttachment(MessageViewModel message, ShadowBannedAttachmentKinds kinds) {
            if (kinds == ShadowBannedAttachmentKinds.None) return false;

            if (kinds.HasFlag(ShadowBannedAttachmentKinds.Forwarded) && message.ForwardedMessages?.Count > 0) return true;
            if (message.Attachments == null || message.Attachments.Count == 0) return false;

            foreach (Attachment attachment in message.Attachments) {
                if (attachment == null) continue;

                if (kinds.HasFlag(ShadowBannedAttachmentKinds.Voice)
                    && attachment.Type == AttachmentType.AudioMessage) return true;

                if (kinds.HasFlag(ShadowBannedAttachmentKinds.Link)
                    && attachment.Type == AttachmentType.Link) return true;

                if (kinds.HasFlag(ShadowBannedAttachmentKinds.Sticker)
                    && (attachment.Type == AttachmentType.Sticker || attachment.Type == AttachmentType.UGCSticker)) return true;

                if (kinds.HasFlag(ShadowBannedAttachmentKinds.Graffiti)
                    && attachment.Type == AttachmentType.Graffiti) return true;
            }

            return false;
        }

        private bool ShouldSuppressLocalNotification(MessageViewModel message) {
            if (message == null || message.SenderId == session.Id) return false;
            if (Settings.IsPeerQuietNow(PeerId, DateTimeOffset.Now)) return true;
            if (Settings.IsSenderMutedLocally(PeerId, message.SenderId)) return true;

            List<ShadowBanTextRule> quietTextRules = BuildShadowBanTextRules(Settings.GetPeerQuietTextRules(PeerId));
            return IsMessageShadowBannedByText(message, quietTextRules);
        }

        private static bool IsTextShadowBanned(string text, List<ShadowBanTextRule> rules) {
            if (rules == null || rules.Count == 0 || String.IsNullOrEmpty(text)) return false;

            foreach (ShadowBanTextRule rule in rules) {
                if (rule.IsMatch(text)) return true;
            }

            return false;
        }

        private sealed class ShadowBanTextRule {
            private readonly string text;
            private readonly Regex regex;

            public ShadowBanTextRule(string text) {
                this.text = text;
            }

            public ShadowBanTextRule(Regex regex) {
                this.regex = regex;
            }

            public bool IsMatch(string value) {
                if (String.IsNullOrEmpty(value)) return false;
                if (regex == null) return value.Contains(text, StringComparison.OrdinalIgnoreCase);

                try {
                    return regex.IsMatch(value);
                } catch (RegexMatchTimeoutException ex) {
                    Log.Warning(ex, "Shadow-ban regex timeout.");
                    return false;
                }
            }
        }

        private void FixState(MessageViewModel msg) {
            long senderId = session.Id;
            bool isOutgoing = msg.SenderId == senderId;
            if (isOutgoing) {
                msg.State = msg.ConversationMessageId > OutRead ? MessageVMState.Unread : MessageVMState.Read;
            } else {
                msg.State = msg.ConversationMessageId > InRead ? MessageVMState.Unread : MessageVMState.Read;
            }
        }

        #endregion

        #region LongPoll events

        private void LongPoll_MessageFlagSet(LongPoll longPoll, int messageId, int flags, long peerId) {
            if (peerId != PeerId) return;
            new System.Action(async () => {
                await Dispatcher.UIThread.InvokeAsync(() => {
                    if (flags.HasFlag(128)) { // Удаление сообщения
                        if (messageId > InRead && UnreadMessagesCount > 0) UnreadMessagesCount--;
                        if (ReceivedMessages.Count > 1) {
                            MessageViewModel prev = ReceivedMessages[ReceivedMessages.Count - 2];
                            UpdateSortId(SortId.MajorId, prev.GlobalId);
                        } else {
                            Log.Warning("Cannot update minor_id after last message is deleted!");
                        }

                        MessageViewModel msg = ReceivedMessages.Where(m => m.ConversationMessageId == messageId).FirstOrDefault();
                        if (msg != null) ReceivedMessages.Remove(msg);

                        MessageViewModel dmsg = DisplayedMessages?.GetById(messageId);
                        if (dmsg != null) DisplayedMessages.Remove(dmsg);
                    }
                });
            })();
        }

        Dictionary<int, bool> pendingMessages = new Dictionary<int, bool>();

        private void LongPoll_MessageReceived(LongPoll longPoll, Message message, int flags, bool incrementUnreadCounter) {
            if (message.PeerId != PeerId) return;
            QueueLongPollMessage(message, flags, incrementUnreadCounter);
        }

        private void QueueLongPollMessage(Message message, int flags, bool incrementUnreadCounter) {
            lock (pendingLongPollMessagesLock) {
                pendingLongPollMessages.Add(new PendingLongPollMessage(message, flags, incrementUnreadCounter));
                if (pendingLongPollFlushQueued) return;
                pendingLongPollFlushQueued = true;
            }

            Dispatcher.UIThread.Post(async () => await FlushLongPollMessagesAsync(), DispatcherPriority.Background);
        }

        private async Task FlushLongPollMessagesAsync() {
            await Task.Delay(16);

            List<PendingLongPollMessage> batch;
            lock (pendingLongPollMessagesLock) {
                batch = pendingLongPollMessages.ToList();
                pendingLongPollMessages.Clear();
                pendingLongPollFlushQueued = false;
            }

            if (batch.Count == 0) return;
            if (!Dispatcher.UIThread.CheckAccess()) {
                await Dispatcher.UIThread.InvokeAsync(async () => await ApplyLongPollMessagesAsync(batch));
                return;
            }

            await ApplyLongPollMessagesAsync(batch);
        }

        private async Task ApplyLongPollMessagesAsync(List<PendingLongPollMessage> batch) {
            IDisposable receivedBatch = _receivedMessages.DeferNotifications();
            IDisposable displayedBatch = DisplayedMessages?.DeferNotifications();
            try {
                foreach (PendingLongPollMessage item in batch) {
                    await ApplyLongPollMessageAsync(item.Message, item.Flags, item.IncrementUnreadCounter);
                }
            } finally {
                displayedBatch?.Dispose();
                receivedBatch.Dispose();
            }
        }

        private async Task ApplyLongPollMessageAsync(Message message, int flags, bool incrementUnreadCounter) {
            MessageViewModel msg = MessageViewModel.Create(message, session);
            if (IsMessageLocallyHidden(msg)) return;
            if (IsGroupSpamMessage(msg)) {
                Settings.HideMessagesLocally(PeerId, new[] { msg.ConversationMessageId });
                await QuickActionStore.AddAutoRuleHitAsync(PeerId, msg.ConversationMessageId, "moderation:quarantine", 14);
                Log.Information("Local anti-spam suppressed incoming message. Peer={PeerId}; cmid={Cmid}; sender={SenderId}", PeerId, msg.ConversationMessageId, msg.SenderId);
                return;
            }

            bool suppressLocalNotification = ShouldSuppressLocalNotification(msg);
            bool isMention = false;
            if (!message.IsSilent && message.MentionedUsers != null) {
                if (message.MentionedUsers.Count == 0) { // признак того, что пушнули всех (@all)
                    isMention = true;
                } else {
                    isMention = message.MentionedUsers.Contains(session.Id);
                }
            }

            if (!message.IsPartial) {
                bool isUnread = flags.HasFlag(1) && !flags.HasFlag(8388608);
                msg.State = isUnread ? MessageVMState.Unread : MessageVMState.Read;
                _ = AutoReplyEngine.ApplyAsync(this, msg, isMention);
                if (!message.IsSilent && !suppressLocalNotification) await ShowSystemNotificationAsync(msg, isMention);
            } else {
                if (!message.IsSilent) {
                    Log.Information($"Adding message {message.PeerId}_{message.ConversationMessageId} to pending for notification... (by longpoll)");
                    pendingMessages[message.ConversationMessageId] = suppressLocalNotification ? false : isMention;
                }
            }

            bool canAddToDisplayedMessages = DisplayedMessages?.Last?.ConversationMessageId == ReceivedMessages.LastOrDefault()?.ConversationMessageId;

            if (ReceivedMessages.Count >= Constants.MessagesCount) {
                ReceivedMessages.Clear();
                ReceivedMessages.Add(msg);
                Log.Information($"All received messages except the last one is removed from cache in chat {Id}");
            } else {
                ReceivedMessages.Add(msg);
            }

            // if (message.Action != null) ParseActionMessage(message.FromId, message.Action, message.Attachments);
            // if (!flags.HasFlag(65536)) UpdateSortId(SortId.MajorId, msg.Id);
            if (msg.SenderId != session.Id && incrementUnreadCounter) UnreadMessagesCount++;
            if (canAddToDisplayedMessages) {
                if (DisplayedMessages == null) {
                    DisplayedMessages = new MessagesCollection(new List<MessageViewModel>() { msg });
                } else {
                    DisplayedMessages.Insert(msg);
                }
            }

            // Remove user from activity status
            var status = ActivityStatusUsers.RegisteredObjects.Where(m => m.MemberId == message.FromId).FirstOrDefault();
            if (status != null) ActivityStatusUsers.Remove(status);
        }

        private void LongPoll_MessageEdited(LongPoll longPoll, Message message, int flags, bool incrementUnreadCounter) {
            if (PeerId != message.PeerId) return;
            bool isFullReceived = pendingMessages.ContainsKey(message.ConversationMessageId);

            new System.Action(async () => {
                await Dispatcher.UIThread.InvokeAsync(async () => {
                    if (isFullReceived) {
                        bool isMention = pendingMessages[message.ConversationMessageId];
                        pendingMessages.Remove(message.ConversationMessageId);
                        MessageViewModel msg = ReceivedMessages.Where(m => m.ConversationMessageId == message.ConversationMessageId).FirstOrDefault();
                        if (msg != null && !ShouldSuppressLocalNotification(msg)) await ShowSystemNotificationAsync(msg, isMention);
                    }

                    if (LastMessage?.ConversationMessageId == message.ConversationMessageId) {
                        // нужно для корректной обработки смены фото чата.
                        //if (message.Action != null) ParseActionMessage(message.FromId, message.Action, message.Attachments);

                        await Task.Delay(16); // ибо первым выполняется событие в объекте сообщения, и только потом тут.
                        OnPropertyChanged(nameof(LastMessage));
                    }
                });
            })();
        }

        //private void ParseActionMessage(long fromId, VKAPILib.Objects.Action action, List<Attachment> attachments) {
        //    switch (action.Type) {
        //        case "chat_title_update":
        //            Title = action.Text;
        //            break;
        //        case "chat_photo_update":
        //            if (attachments != null) Avatar = attachments[0].Photo.GetSizeAndUriForThumbnail(Constants.ChatHeaderAvatarSize, Constants.ChatHeaderAvatarSize).Uri;
        //            break;
        //        case "chat_photo_remove":
        //            Avatar = new Uri("https://vk.ru/images/icons/im_multichat_200.png");
        //            break;
        //        case "chat_pin_message":
        //            UpdatePinnedMessage(action.ConversationMessageId);
        //            break;
        //        case "chat_unpin_message":
        //            PinnedMessage = null;
        //            break;
        //    }
        //}

        private void UpdatePinnedMessage(int cmid) {
            var msg = ReceivedMessages.Where(m => m.ConversationMessageId == cmid).FirstOrDefault();
            if (msg == null) msg = DisplayedMessages.GetById(cmid);
            if (msg != null) {
                PinnedMessage = msg;
            } else {
                new System.Action(async () => {
                    try {
                        var resp = await session.API.Messages.GetByConversationMessageIdAsync(session.GroupId, PeerId, new List<int> { cmid });
                        PinnedMessage = MessageViewModel.Create(resp.Items[0], session);
                    } catch (Exception ex) {
                        Log.Error(ex, $"Cannot get pinned message from event! peer={PeerId} cmid={cmid}");
                    }
                })();
            }
        }

        private void LongPoll_MentionReceived(LongPoll longPoll, long peerId, int messageId, bool isSelfDestruct) {
            if (PeerId != peerId) return;
            new System.Action(async () => {
                await Dispatcher.UIThread.InvokeAsync(() => {
                    if (!isSelfDestruct) {
                        if (Mentions == null) {
                            Mentions = new ObservableCollection<int>() { messageId };
                        } else {
                            Mentions.Add(messageId);
                        }
                    }
                });
            })();
        }

        private void LongPoll_IncomingMessagesRead(LongPoll longPoll, long peerId, int messageId, int count) {
            if (PeerId != peerId) return;
            new System.Action(async () => {
                await Dispatcher.UIThread.InvokeAsync(() => {
                    InRead = messageId;
                    LongPoll_MessagesRead(longPoll, peerId, messageId, count);
                });
            })();
        }

        private void LongPoll_OutgoingMessagesRead(LongPoll longPoll, long peerId, int messageId, int count) {
            if (PeerId != peerId) return;
            new System.Action(async () => {
                await Dispatcher.UIThread.InvokeAsync(() => {
                    OutRead = messageId;
                    LongPoll_MessagesRead(longPoll, peerId, messageId, count);
                });
            })();
        }

        private void LongPoll_MessagesRead(LongPoll longPoll, long peerId, int messageId, int count) {
            UnreadMessagesCount = count;

            if (Mentions != null && Mentions.Count > 0) {
                var mentions = Mentions.ToList();
                foreach (int id in mentions) {
                    if (id <= messageId) Mentions.Remove(id);
                }
                if (Mentions.Count == 0) Mentions = null;
            }
        }

        private void LongPoll_ConversationFlagReset(LongPoll longPoll, long peerId, int flags) {
            if (PeerId != peerId) return;
            new System.Action(async () => {
                await Dispatcher.UIThread.InvokeAsync(() => {
                    if (flags.HasFlag(1048576)) IsMarkedAsUnread = false;
                    bool mention = flags.HasFlag(1024); // Упоминаний больше нет
                    bool mark = flags.HasFlag(16384); // Маркированного сообщения больше нет
                    if (mark) {
                        HasMention = false;
                        HasSelfDestructMessage = false;
                    }
                });
            })();
        }

        private void LongPoll_ConversationFlagSet(LongPoll longPoll, long peerId, int flags) {
            if (peerId != PeerId) return;
            new System.Action(async () => {
                await Dispatcher.UIThread.InvokeAsync(() => {
                    if (flags.HasFlag(1048576)) IsMarkedAsUnread = true;
                    bool mention = flags.HasFlag(1024); // Наличие упоминания
                    bool mark = flags.HasFlag(16384); // Наличие маркированного сообщения
                    if (mark) {
                        if (mention) {
                            HasMention = true;
                        } else {
                            HasSelfDestructMessage = true;
                        }
                    }
                });
            })();
        }

        private void LongPoll_ConversationRemoved(object sender, long peerId) {
            if (peerId != PeerId) return;
            new System.Action(async () => {
                await Dispatcher.UIThread.InvokeAsync(() => {
                    DisplayedMessages?.Clear();
                    ReceivedMessages.Clear();
                });
            })();
        }

        // Если что, flags и есть major/minor_id.
        private void LongPoll_MajorIdChanged(LongPoll longPoll, long peerId, int flags) {
            if (peerId != PeerId) return;
            new System.Action(async () => {
                await Dispatcher.UIThread.InvokeAsync(() => {
                    UpdateSortId(flags, SortId.MinorId);
                });
            })();
        }

        private void LongPoll_MinorIdChanged(LongPoll longPoll, long peerId, int flags) {
            if (peerId != PeerId || flags == 0) return;
            new System.Action(async () => {
                await Dispatcher.UIThread.InvokeAsync(() => {
                    UpdateSortId(SortId.MajorId, flags);
                });
            })();
        }

        // Надо задать новый объект SortId, чтобы сработало событие OnPropertyChanged для SortIndex.
        private void UpdateSortId(int major, int minor) {
            SortId = new SortId {
                MajorId = major,
                MinorId = minor
            };
        }

        private void LongPoll_ConversationDataChanged(LongPoll longPoll, int type, long peerId, long extra, Conversation convo) {
            if (peerId != PeerId) return;
            new System.Action(async () => {
                await Dispatcher.UIThread.InvokeAsync(async () => {
                    // TODO: type 6
                    switch (type) {
                        case 1: // Измененилось название чата
                        case 2: // Обновилась аватарка чата
                        case 4: // Изменились права доступа в чате
                        case 10: // Изменился баннер (будет поддерживаться в будущем)
                                 // case 19: // Начало или окончание звонка
                            await RefreshConvoInfoAsync(convo, type == 4);
                            break;
                        case 3: // Назначен новый администратор
                            if (ChatSettings.AdminIDs == null) ChatSettings.AdminIDs = new List<long>();
                            ChatSettings.AdminIDs?.Add(extra);
                            break;
                        case 5: // Закрепление или открепление сообщения
                            if (extra == 0) {
                                PinnedMessage = null;
                            } else {
                                UpdatePinnedMessage(Convert.ToInt32(extra));
                            }
                            break;
                        case 7: // Выход из беседы
                        case 8: // Исключение из беседы
                            if (extra.IsUser()) {
                                var um = Members.SingleOrDefault(m => m.MemberId == extra);
                                if (um != null) Members.Remove(um);

                                User user = MembersUsers?.Where(u => u.Id == extra).FirstOrDefault();
                                if (user != null) MembersUsers?.Remove(user);
                            } else if (extra.IsGroup()) {
                                var gm = Members.SingleOrDefault(m => m.MemberId == -extra);
                                if (gm != null) Members.Remove(gm);

                                Group group = MembersGroups?.Where(g => g.Id == -extra).FirstOrDefault();
                                if (group != null) MembersGroups?.Remove(group);
                            }
                            if (extra == session.Id) {
                                ChatSettings.State = type == 8 ? UserStateInChat.Kicked : UserStateInChat.Left;
                            }
                            ChatSettings.MembersCount--;
                            UpdateSubtitleForChat();
                            UpdateRestrictionInfo();
                            break;
                        case 9: // Разжалован администратор
                            if (ChatSettings.AdminIDs != null && ChatSettings.AdminIDs.Contains(extra)) ChatSettings.AdminIDs?.Remove(extra);
                            break;
                    }
                });
            })();
        }

        private void LongPoll_ActivityStatusChanged(LongPoll longPoll, long peerId, List<LongPollActivityInfo> infos) {
            if (peerId != PeerId) return;
            new System.Action(async () => {
                await Dispatcher.UIThread.InvokeAsync(() => {
                    double timeout = 5000;
                    try {
                        foreach (LongPollActivityInfo info in infos) {
                            if (info.MemberId == session.Id) continue;
                            var exist = ActivityStatusUsers.RegisteredObjects.Where(u => u.MemberId == info.MemberId).FirstOrDefault();
                            if (exist != null) ActivityStatusUsers.Remove(exist);
                            ActivityStatusUsers.Add(info, timeout);
                        }
                        UpdateActivityStatus();
                    } catch (Exception ex) {
                        ActivityStatusUsers.Clear();
                        ActivityStatus = String.Empty;
                        Log.Error(ex, $"Error while parsing user activity status!");
                    }
                });
            })();
        }

        private void LongPoll_CanWriteChanged(LongPoll longPoll, long peerId, long memberId, bool isRestrictedToWrite, long untilTime) {
            if (peerId != PeerId) return;
            new System.Action(async () => {
                await Dispatcher.UIThread.InvokeAsync(async () => {
                    ChatMember member = Members.SingleOrDefault(m => m.MemberId == memberId);
                    if (member != null) member.IsRestrictedToWrite = isRestrictedToWrite;

                    if (memberId == session.Id) {
                        if (isRestrictedToWrite) {
                            CanWrite = new CanWrite {
                                Allowed = false,
                                Reason = 983,
                                Until = untilTime
                            };
                            UpdateRestrictionInfo();
                        } else {
                            await RefreshConvoInfoAsync();
                        }
                    }
                });
            })();
        }

        private void UpdateActivityStatus() {
            try {
                var acts = ActivityStatusUsers.RegisteredObjects;
                int count = acts.Count();

                Debug.WriteLine($"UpdateActivityStatus: {String.Join(";", acts)}");

                if (count == 0) {
                    ActivityStatus = String.Empty;
                    return;
                }

                if (PeerType != PeerType.Chat) {
                    if (count == 1) {
                        ActivityStatus = GetLocalizedActivityStatus(acts.FirstOrDefault().Status, 1) + "...";
                    }
                } else {
                    var typing = acts.Where(a => a?.Status == LongPollActivityType.Typing).ToList();
                    var voice = acts.Where(a => a?.Status == LongPollActivityType.RecordingAudioMessage).ToList();
                    var photo = acts.Where(a => a?.Status == LongPollActivityType.UploadingPhoto).ToList();
                    var video = acts.Where(a => a?.Status == LongPollActivityType.UploadingVideo).ToList();
                    var file = acts.Where(a => a?.Status == LongPollActivityType.UploadingFile).ToList();
                    var circle = acts.Where(a => a?.Status == LongPollActivityType.UploadingVideoMessage).ToList();
                    var choosingFile = acts.Where(a => a?.Status == LongPollActivityType.ChoosingFile).ToList();
                    var choosingTemplate = acts.Where(a => a?.Status == LongPollActivityType.ChoosingTemplate).ToList();
                    List<List<LongPollActivityInfo>> groupedActivities = new List<List<LongPollActivityInfo>> {
                        typing, voice, photo, video, file, circle, choosingFile, choosingTemplate
                    };

                    bool has3AndMoreDifferentTypes = groupedActivities.Where(a => a.Count > 0).Count() >= 3;

                    StringBuilder status = new StringBuilder();
                    foreach (var act in groupedActivities) {
                        if (act.Count == 0) continue;
                        var type = act[0].Status;
                        string actstr = GetLocalizedActivityStatus(type, act.Count);

                        if (has3AndMoreDifferentTypes) {
                            if (status.Length != 0) status.Append(", ");
                            status.Append($"{act.Count} {actstr}");
                        } else {
                            if (status.Length != 0) status.Append(", ");
                            List<long> ids = act.Select(s => s.MemberId).ToList();
                            status.Append($"{GetNamesForActivityStatus(ids, act.Count, act.Count == 1)} {actstr}");
                        }
                    }

                    ActivityStatus = $"{status.ToString().Trim()}…";
                }
            } catch (Exception ex) {
                ActivityStatus = String.Empty;
                string count = ActivityStatusUsers.RegisteredObjects == null ? "null" : ActivityStatusUsers.RegisteredObjects.Count.ToString();
                Log.Error(ex, $"Exception in UpdateActivityStatus (0x{ex.HResult.ToString("x8")}), current au count: {count}");
            }
        }

        private string GetLocalizedActivityStatus(LongPollActivityType status, int count) {
            string suffix = count == 1 ? "_single" : "_multi";
            switch (status) {
                case LongPollActivityType.Typing: return Localizer.Get($"lp_act_typing{suffix}");
                case LongPollActivityType.RecordingAudioMessage: return Localizer.Get($"lp_act_voice{suffix}");
                case LongPollActivityType.UploadingPhoto: return Localizer.Get($"lp_act_photo{suffix}");
                case LongPollActivityType.UploadingVideo: return Localizer.Get($"lp_act_video{suffix}");
                case LongPollActivityType.UploadingFile: return Localizer.Get($"lp_act_file{suffix}");
                case LongPollActivityType.UploadingVideoMessage: return Localizer.Get($"lp_act_videomsg{suffix}");
                case LongPollActivityType.ChoosingFile: return Localizer.Get($"lp_act_choosing_file{suffix}");
                case LongPollActivityType.ChoosingTemplate: return Localizer.Get($"lp_act_choosing_template{suffix}");
            }
            return string.Empty;
        }

        private string GetNamesForActivityStatus(IReadOnlyList<long> ids, int count, bool showFullLastName) {
            StringBuilder sb = new StringBuilder();
            foreach (long id in ids) {
                if (id.IsUser()) {
                    User u = CacheManager.GetUser(id);
                    if (u != null) {
                        string lastName = showFullLastName ? u.LastName : $"{u.LastName[0]}.";
                        sb.Append($"{u.FirstName} {lastName}");
                    }
                } else if (id.IsGroup()) {
                    var g = CacheManager.GetGroup(id);
                    if (g != null) sb.Append($"\"{g.Name}\"");
                }
            }
            if (sb.Length > 0 && count > 1) sb.Append($" {Localizer.GetFormatted("im_status_more", count - 1)}");
            return sb.ToString();
        }

        private void LongPoll_NotificationsSettingsChanged(object sender, LongPollPushNotificationData e) {
            if (e.PeerId != PeerId) return;
            new System.Action(async () => {
                await Dispatcher.UIThread.InvokeAsync(() => {
                    PushSettings ps = new PushSettings {
                        DisabledForever = e.DisabledUntil == -1,
                        DisabledUntil = e.DisabledUntil,
                        NoSound = e.Sound == 0
                    };
                    PushSettings = ps;
                });
            })();
        }

        private void LongPoll_UnreadReactionsChanged(LongPoll longPoll, long peerId, List<int> cmIds) {
            if (peerId != PeerId) return;
            new System.Action(async () => {
                await Dispatcher.UIThread.InvokeAsync(() => {
                    if (cmIds == null || cmIds.Count == 0) {
                        UnreadReactions = null;
                        return;
                    }
                    UnreadReactions = new ObservableCollection<int>(cmIds);
                });
            })();
        }

        private void VKQueue_Online(object sender, DataModels.VKQueue.OnlineEvent e) {
            if (PeerId != e.UserId) return;
            Log.Verbose($"ChatViewModel > Online event. User: {e.UserId}; IsOnline: {e.Online}; Platform: {e.Platform}; App: {e.AppId}; LastSeen: {e.LastSeen}");
            new System.Action(async () => {
                await Dispatcher.UIThread.InvokeAsync(() => {
                    Online.IsOnline = e.Online;
                    Online.IsMobile = e.Platform != 6 && e.Platform != 7;
                    Online.AppId = e.AppId;
                    Online.LastSeenUnix = e.LastSeenUnix;
                    OnPropertyChanged(nameof(Online));
                });
            })();
        }

        #endregion

        #region Notification

        private async Task ShowSystemNotificationAsync(MessageViewModel message, bool isMention) {
            bool notifsEnabled = PeerType == PeerType.Chat ? Settings.NotificationsGroupChat : Settings.NotificationsPrivate;
            if (!notifsEnabled) return;
            if (message.IsOutgoing) return;
            if (await TryApplyAutoRulesAsync(message, isMention)) return;
            if (!CanShowNotificationInQuietMode(message, isMention)) return;

            bool soundSettings = PeerType == PeerType.Chat ? Settings.NotificationsGroupChatSound : Settings.NotificationsPrivateSound;
            bool sound = !PushSettings.NoSound && soundSettings;
            bool canNotify = isMention ? true : CanNotify();
            if (!canNotify) return;
            Log.Information($"ChatViewModel: about to show new message notification ({message.PeerId}_{message.ConversationMessageId}). Is mention: {isMention}.");
            await Task.Delay(20); // имя отправителя может не оказаться в кеше вовремя.

            string text = Settings.StreamerMode ? "Сообщение скрыто" : message.ToString();
            string senderName = Settings.StreamerMode ? "Скрытый отправитель" : message.SenderName;
            string chatName = Settings.StreamerMode ? null : PeerType == PeerType.Chat ? Localizer.GetFormatted("in_chat", Title) : null;

            var ava = Settings.StreamerMode ? null : await BitmapManager.GetBitmapAsync(message.SenderAvatar, 56, 56, BitmapCacheKind.Avatar);
            var t = new ToastNotification(message, session.Name, senderName, text, chatName, ava);
            System.Action openMessage = () => {
                Log.Information($"ChatViewModel: clicked on message {message.PeerId}_{message.ConversationMessageId}");
                session.TryOpenWindow();
                session.GoToChat(message.PeerId, message.ConversationMessageId);
            };
            t.OnClick += openMessage;
            t.Actions.Add(new ToastNotificationAction(Localizer.Get("notification_action_open"), openMessage));
            t.Actions.Add(new ToastNotificationAction(Localizer.Get("notification_action_mark_read"), () => _ = MarkToastMessageAsReadAsync(message)));
            t.Actions.Add(new ToastNotificationAction(Localizer.Get("notification_action_mute_1h"), () => Settings.SetPeerQuietUntil(message.PeerId, DateTimeOffset.Now.AddHours(1))));
            //if (CanWrite.Allowed) t.OnSendClick += (text) => {
            //    // TODO: send message from toast
            //};
            session.ShowSystemNotification(t);
            if (sound) {
                var bb2 = AssetsManager.OpenAsset(new Uri("avares://laney/Assets/Audio/bb2.mp3"));
                LMediaPlayer.SFX?.PlayStream(bb2);
            }
        }

        private async Task MarkToastMessageAsReadAsync(MessageViewModel message) {
            if (Settings.DisableMarkingMessagesAsRead) return;

            try {
                await session.API.Messages.MarkAsReadAsync(session.GroupId, message.PeerId, message.ConversationMessageId, true);
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot mark toast message as read. Peer={PeerId}; Cmid={Cmid}", message.PeerId, message.ConversationMessageId);
            }
        }

        private async Task<bool> TryApplyAutoRulesAsync(MessageViewModel message, bool isMention) {
            AutomationRuleRunResult userRules = await AutomationRuleEngine.ApplyAsync(this, message, isMention);
            if (userRules.SuppressNotification) return true;

            if (!SmartChatClassifier.TryGetAutoRule(this, message, isMention, out string category, out int ttlDays)) return false;

            await QuickActionStore.AddAutoRuleHitAsync(PeerId, message.ConversationMessageId, category, ttlDays);
            Log.Information($"Auto-rules muted notification ({message.PeerId}_{message.ConversationMessageId}). Category: {category}; TTL: {ttlDays} days.");
            return true;
        }

        private bool CanShowNotificationInQuietMode(MessageViewModel message, bool isMention) {
            if (!Settings.IsDontAnnoyMeActive(DateTime.Now)) return true;
            if (isMention && Settings.DontAnnoyMeAllowMentions) return true;
            if (Settings.DontAnnoyMeAllowImportant && IsVipOrImportantNotification(message)) return true;
            return HasQuietModeKeyword(message) || HasBuiltInUrgentPattern(message);
        }

        private bool IsVipOrImportantNotification(MessageViewModel message) {
            if (IsImportant || IsPinned || message?.IsImportant == true) return true;
            if (HasVipLocalTag(PeerId)) return true;
            return message != null && HasVipLocalTag(message.SenderId);
        }

        private static bool HasVipLocalTag(long peerId) {
            if (peerId == 0) return false;
            return VipLocalTags.Any(tag => Settings.PeerHasLocalTag(peerId, tag));
        }

        private static bool HasQuietModeKeyword(MessageViewModel message) {
            string keywords = Settings.DontAnnoyMeKeywords;
            if (String.IsNullOrWhiteSpace(keywords)) return false;

            string text = message?.Text;
            if (String.IsNullOrWhiteSpace(text)) return false;

            string[] tokens = keywords.Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string token in tokens) {
                if (token.Length == 0) continue;
                if (text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }

            return false;
        }

        private static bool HasBuiltInUrgentPattern(MessageViewModel message) {
            string text = message?.Text;
            if (String.IsNullOrWhiteSpace(text)) return false;

            foreach (string token in BuiltInUrgentNotificationTokens) {
                if (text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }

            return false;
        }

        private bool CanNotify() {
            if (PushSettings.DisabledForever) return false;
            return PushSettings.DisabledUntil == 0 || PushSettings.DisabledUntil < DateTimeOffset.Now.ToUnixTimeSeconds();
        }

        #endregion


        private ulong GetSortIndex() {
            if (SortId.MajorId == 0) return (ulong)SortId.MinorId;
            ulong index = ((ulong)SortId.MajorId * 100000000) + (ulong)SortId.MinorId;
            return index;
        }

        public void Unload() {
            Log.Information($"Unloading chat {Id}...");
            if (!DemoMode.IsEnabled) {
                session.LongPoll.MessageFlagSet -= LongPoll_MessageFlagSet;
                session.LongPoll.MessageReceived -= LongPoll_MessageReceived;
                session.LongPoll.MessageEdited -= LongPoll_MessageEdited;
                session.LongPoll.MentionReceived -= LongPoll_MentionReceived;
                session.LongPoll.IncomingMessagesRead -= LongPoll_IncomingMessagesRead;
                session.LongPoll.OutgoingMessagesRead -= LongPoll_OutgoingMessagesRead;
                session.LongPoll.ConversationFlagReset -= LongPoll_ConversationFlagReset;
                session.LongPoll.ConversationFlagSet -= LongPoll_ConversationFlagSet;
                session.LongPoll.ConversationRemoved -= LongPoll_ConversationRemoved;
                session.LongPoll.MajorIdChanged -= LongPoll_MajorIdChanged;
                session.LongPoll.MinorIdChanged -= LongPoll_MinorIdChanged;
                session.LongPoll.ConversationDataChanged -= LongPoll_ConversationDataChanged;
                session.LongPoll.ActivityStatusChanged -= LongPoll_ActivityStatusChanged;
                session.LongPoll.NotificationsSettingsChanged -= LongPoll_NotificationsSettingsChanged;
                session.LongPoll.UnreadReactionsChanged -= LongPoll_UnreadReactionsChanged;

                if (!session.IsGroup) VKQueue.Online -= VKQueue_Online;
            }

            ActivityStatusUsers.Elapsed -= ActivityStatusUsers_Elapsed;
            DisplayedMessages?.Clear();
            ReceivedMessages?.Clear();
        }
    }
}
