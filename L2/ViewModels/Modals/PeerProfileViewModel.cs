using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DynamicData;
using ELOR.Laney.Core;
using ELOR.Laney.Core.Localization;
using ELOR.Laney.Core.Network;
using ELOR.Laney.DataModels;
using ELOR.Laney.Execute;
using ELOR.Laney.Execute.Objects;
using ELOR.Laney.Extensions;
using ELOR.Laney.Helpers;
using ELOR.Laney.ViewModels.Controls;
using ELOR.Laney.Views.Modals;
using ELOR.VKAPILib.Methods;
using ELOR.VKAPILib.Objects;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using VKUI.Controls;
using VKUI.Popups;

namespace ELOR.Laney.ViewModels.Modals {
    public sealed class ConversationAttachmentsTabViewModel : ItemsViewModel<ConversationAttachment> {
        public bool End { get; set; } = false;
    }

    public sealed class ChatMembersTabViewModel : ItemsViewModel<Entity> {
        private List<Entity> _allMembers;

        private string _searchQuery;
        public string SearchQuery { get { return _searchQuery; } set { _searchQuery = value; OnPropertyChanged(); } }
        public bool SearchAvailable { get { return Items.Count > 0; } }

        public ChatMembersTabViewModel(ObservableCollection<Entity> displayedItems, Func<List<Entity>> getAllMembersCallback) : base(displayedItems) {
            _allMembers = getAllMembersCallback();
            PropertyChanged += OnPropertyChanged;
            Items.CollectionChanged += Items_CollectionChanged;
        }

        private void Items_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
            OnPropertyChanged(nameof(SearchAvailable));
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(SearchQuery):
                    SearchMember();
                    break;
            }
        }

        private void SearchMember() {
            if (IsLoading) return;
            Items.CollectionChanged -= Items_CollectionChanged; // Required, because searchbox is temporary disappear and focus losing from that searchbox.
            Items.Clear();
            if (!String.IsNullOrWhiteSpace(SearchQuery)) {
                var foundMembers = _allMembers.Where(m => m.Name.ToLower().Contains(SearchQuery.ToLower()));
                Items.AddRange(foundMembers);
            } else {
                Items.AddRange(_allMembers);
            }
            Items.CollectionChanged += Items_CollectionChanged;
        }

        ~ChatMembersTabViewModel() {
            PropertyChanged -= OnPropertyChanged;
            Items.CollectionChanged -= Items_CollectionChanged;
        }
    }

    public sealed class LocalNoteHistoryEntry {
        public string DateText { get; }
        public string Text { get; }
        public RelayCommand RestoreCommand { get; }

        public LocalNoteHistoryEntry(PeerLocalNoteHistoryItem item, Action<string> restore) {
            DateText = DateTimeOffset.FromUnixTimeSeconds(item.UpdatedAtUnix).LocalDateTime.ToString("dd.MM.yyyy HH:mm");
            Text = item.Text;
            RestoreCommand = new RelayCommand((o) => restore(Text));
        }
    }

    public sealed class ChatStatisticTile {
        public string IconId { get; }
        public string Label { get; }
        public string Value { get; }
        public string Subtitle { get; }

        public ChatStatisticTile(string iconId, string label, string value, string subtitle) {
            IconId = iconId;
            Label = label;
            Value = value;
            Subtitle = subtitle;
        }
    }

    public sealed class ChatStatisticBar {
        public string Label { get; }
        public string ValueText { get; }
        public double Percent { get; }

        public ChatStatisticBar(string label, string valueText, double percent) {
            Label = label;
            ValueText = valueText;
            Percent = Math.Clamp(percent, 0, 100);
        }
    }

    public sealed class PeerProfileViewModel : CommonViewModel {
        private static readonly HashSet<string> StatsStopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "это", "как", "что", "или", "для", "при", "там", "тут", "уже", "еще", "ещё", "все", "всё", "они", "она", "оно", "его", "мне", "тебе", "меня", "тебя",
            "если", "когда", "чтобы", "потому", "просто", "будет", "было", "была", "были", "есть", "нет", "the", "and", "with", "from", "that", "this", "you", "your"
        };

        private static readonly string[] StatsProfanityRoots = {
            "бля", "бляд", "сука", "хуй", "хуе", "хуи", "пизд", "пздц", "еб", "ёб", "епт", "нах", "мудак", "хер", "говн", "жоп"
        };

        private sealed class ProfanityStats {
            public int Messages { get; set; }
            public int Hits { get; set; }
            public Dictionary<string, int> Counters { get; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        private long _id;
        private string _header;
        private string _subhead;
        private Uri _avatar;
        private string _localNote;
        private string _localAlias;
        private string _localAvatar;
        private string _localTags;
        private string _localBackgroundImage;
        private string _localAppearanceTheme;
        private string _localAppearanceAccent;
        private string _localAppearanceDensity;
        private string _localAppearanceFont;
        private string _localAppearanceBubbleStyle;
        private string _localAppearanceBubbleColor;
        private string _localEmojiPack;
        private int _localAppearanceDim;
        private int _localAppearanceBlur;
        private int _localAppearanceBrightness;
        private string _mutedSendersText;
        private string _quietTextRules;
        private string _shadowBannedSendersText;
        private string _shadowBannedTextRules;
        private string _sourceHeader;
        private Uri _sourceAvatar;
        private string _profileFirstNameDraft;
        private string _profileLastNameDraft;
        private string _profileStatusDraft;
        private int _membersCount;
        private ObservableCollection<TwoStringTuple> _information = new ObservableCollection<TwoStringTuple>();
        private ObservableCollection<TwoStringTuple> _chatStatistics = new ObservableCollection<TwoStringTuple>();
        private ObservableCollection<ChatStatisticTile> _chatStatisticTiles = new ObservableCollection<ChatStatisticTile>();
        private ObservableCollection<ChatStatisticBar> _chatStatisticMix = new ObservableCollection<ChatStatisticBar>();
        private ObservableCollection<ChatStatisticBar> _chatStatisticAttachments = new ObservableCollection<ChatStatisticBar>();
        private ObservableCollection<LocalNoteHistoryEntry> _localNoteHistory = new ObservableCollection<LocalNoteHistoryEntry>();
        private ChatMembersTabViewModel _chatMembers;

        private ConversationAttachmentsTabViewModel _photos = new ConversationAttachmentsTabViewModel();
        private ConversationAttachmentsTabViewModel _videos = new ConversationAttachmentsTabViewModel();
        private ConversationAttachmentsTabViewModel _audios = new ConversationAttachmentsTabViewModel();
        private ConversationAttachmentsTabViewModel _documents = new ConversationAttachmentsTabViewModel();
        private ConversationAttachmentsTabViewModel _share = new ConversationAttachmentsTabViewModel();
        //private ConversationAttachmentsTabViewModel _graffities = new ConversationAttachmentsTabViewModel();
        //private ConversationAttachmentsTabViewModel _audioMessages = new ConversationAttachmentsTabViewModel();

        private Command _firstCommand;
        private Command _secondCommand;
        private Command _thirdCommand;
        private Command _moreCommand;
        private RelayCommand _clearMutedSendersCommand;
        private RelayCommand _clearQuietTextRulesCommand;
        private RelayCommand _clearShadowBannedSendersCommand;
        private RelayCommand _clearShadowBannedTextRulesCommand;
        private RelayCommand _clearShadowBannedAttachmentKindsCommand;
        private RelayCommand _clearLocalNoteHistoryCommand;
        private RelayCommand _updateProfileInfoCommand;
        private RelayCommand _updateProfilePhotoCommand;
        private RelayCommand _updateProfileStatusCommand;
        private RelayCommand _clearProfileStatusCommand;
        private RelayCommand _applyLocalAppearanceCommand;
        private RelayCommand _resetLocalAppearanceCommand;
        private RelayCommand _configureE2ECommand;
        private RelayCommand _createE2EHandshakeCommand;
        private RelayCommand _importE2EHandshakeCommand;
        private RelayCommand _showE2EFingerprintCommand;
        private RelayCommand _rotateE2EKeysCommand;
        private RelayCommand _exportE2EBackupCommand;
        private RelayCommand _importE2EBackupCommand;
        private RelayCommand _resetE2ECommand;

        public long Id { get { return _id; } private set { _id = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsChatStatisticsVisible)); OnPropertyChanged(nameof(IsOwnUserProfile)); RefreshE2ESurface(); } }
        public string Header { get { return _header; } private set { _header = value; OnPropertyChanged(); } }
        public string Subhead { get { return _subhead; } private set { _subhead = value; OnPropertyChanged(); } }
        public Uri Avatar { get { return _avatar; } private set { _avatar = value; OnPropertyChanged(); } }
        public string LocalNote { get { return _localNote; } set { SaveLocalNote(value); } }
        public string LocalAlias { get { return _localAlias; } set { SaveLocalAlias(value); } }
        public string LocalAvatar { get { return _localAvatar; } set { SaveLocalAvatar(value); } }
        public string LocalTags { get { return _localTags; } set { SaveLocalTags(value); } }
        public string LocalBackgroundImage { get { return _localBackgroundImage; } set { SaveLocalBackgroundImage(value); } }
        public string ProfileFirstNameDraft { get { return _profileFirstNameDraft; } set { _profileFirstNameDraft = value; OnPropertyChanged(); } }
        public string ProfileLastNameDraft { get { return _profileLastNameDraft; } set { _profileLastNameDraft = value; OnPropertyChanged(); } }
        public string ProfileStatusDraft { get { return _profileStatusDraft; } set { _profileStatusDraft = value; OnPropertyChanged(); } }
        public string MutedSendersText { get { return _mutedSendersText; } set { SaveMutedSenders(value); } }
        public string QuietTextRules { get { return _quietTextRules; } set { SaveQuietTextRules(value); } }
        public string ShadowBannedSendersText { get { return _shadowBannedSendersText; } set { SaveShadowBannedSenders(value); } }
        public string ShadowBannedTextRules { get { return _shadowBannedTextRules; } set { SaveShadowBannedTextRules(value); } }
        public bool HasMutedSenders { get { return Settings.GetMutedSenderIds(Id).Count > 0; } }
        public bool HasQuietTextRules { get { return !String.IsNullOrWhiteSpace(Settings.GetPeerQuietTextRules(Id)); } }
        public bool AntiSpamEnabled { get { return Settings.IsPeerAntiSpamEnabled(Id); } set { SaveAntiSpamEnabled(value); } }
        public bool HasShadowBannedSenders { get { return Settings.GetShadowBannedSenderIds(Id).Count > 0; } }
        public bool HasShadowBannedTextRules { get { return !String.IsNullOrWhiteSpace(Settings.GetShadowBannedTextRules(Id)); } }
        public bool HasShadowBannedAttachmentKinds { get { return Settings.GetShadowBannedAttachmentKinds(Id) != ShadowBannedAttachmentKinds.None; } }
        public bool ShadowBanVoiceMessages { get { return HasShadowBannedAttachmentKind(ShadowBannedAttachmentKinds.Voice); } set { SaveShadowBannedAttachmentKind(ShadowBannedAttachmentKinds.Voice, value, nameof(ShadowBanVoiceMessages)); } }
        public bool ShadowBanLinks { get { return HasShadowBannedAttachmentKind(ShadowBannedAttachmentKinds.Link); } set { SaveShadowBannedAttachmentKind(ShadowBannedAttachmentKinds.Link, value, nameof(ShadowBanLinks)); } }
        public bool ShadowBanStickers { get { return HasShadowBannedAttachmentKind(ShadowBannedAttachmentKinds.Sticker); } set { SaveShadowBannedAttachmentKind(ShadowBannedAttachmentKinds.Sticker, value, nameof(ShadowBanStickers)); } }
        public bool ShadowBanGraffiti { get { return HasShadowBannedAttachmentKind(ShadowBannedAttachmentKinds.Graffiti); } set { SaveShadowBannedAttachmentKind(ShadowBannedAttachmentKinds.Graffiti, value, nameof(ShadowBanGraffiti)); } }
        public bool ShadowBanForwardedMessages { get { return HasShadowBannedAttachmentKind(ShadowBannedAttachmentKinds.Forwarded); } set { SaveShadowBannedAttachmentKind(ShadowBannedAttachmentKinds.Forwarded, value, nameof(ShadowBanForwardedMessages)); } }
        public ObservableCollection<AppearanceOption> PeerChatBackgroundOptions { get; } = new ObservableCollection<AppearanceOption>(AppearanceManager.ChatBackgroundOptionsWithInherit);
        public ObservableCollection<AppearanceOption> PeerAccentOptions { get; } = new ObservableCollection<AppearanceOption>(AppearanceManager.AccentOptionsWithInherit);
        public ObservableCollection<AppearanceOption> PeerChatDensityOptions { get; } = new ObservableCollection<AppearanceOption>(AppearanceManager.ChatDensityOptionsWithInherit);
        public ObservableCollection<AppearanceOption> PeerChatFontOptions { get; } = new ObservableCollection<AppearanceOption>(AppearanceManager.ChatFontOptionsWithInherit);
        public ObservableCollection<AppearanceOption> PeerBubbleStyleOptions { get; } = new ObservableCollection<AppearanceOption>(AppearanceManager.BubbleStyleOptionsWithInherit);
        public ObservableCollection<AppearanceOption> PeerBubbleColorOptions { get; } = new ObservableCollection<AppearanceOption>(AppearanceManager.BubbleColorOptionsWithInherit);
        public ObservableCollection<TwoStringTuple> PeerEmojiPackOptions { get; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple(EmojiPackIds.Inherit, "Наследовать"),
            new TwoStringTuple(EmojiPackIds.System, "Системные"),
            new TwoStringTuple(EmojiPackIds.Vk, "ВКонтакте"),
            new TwoStringTuple(EmojiPackIds.TelegramLike, "Telegram-подобный"),
            new TwoStringTuple(EmojiPackIds.Noto, "Google Noto"),
            new TwoStringTuple(EmojiPackIds.Twemoji, "Twemoji"),
            new TwoStringTuple(EmojiPackIds.Fallback, "Запасной набор"),
            new TwoStringTuple(EmojiPackIds.Custom, "Свой manifest-файл")
        };
        public ObservableCollection<TwoStringTuple> PeerBackgroundDimOptions { get; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple("0", "0%"),
            new TwoStringTuple("15", "15%"),
            new TwoStringTuple("30", "30%"),
            new TwoStringTuple("45", "45%"),
            new TwoStringTuple("60", "60%")
        };
        public ObservableCollection<TwoStringTuple> PeerBackgroundBlurOptions { get; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple("0", "Нет"),
            new TwoStringTuple("4", "Легкий"),
            new TwoStringTuple("8", "Средний"),
            new TwoStringTuple("12", "Сильный"),
            new TwoStringTuple("16", "Максимум")
        };
        public ObservableCollection<TwoStringTuple> PeerBackgroundBrightnessOptions { get; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple("-20", "-20%"),
            new TwoStringTuple("0", "0%"),
            new TwoStringTuple("20", "+20%"),
            new TwoStringTuple("40", "+40%")
        };
        public AppearanceOption CurrentPeerChatBackground { get { return GetAppearanceOption(PeerChatBackgroundOptions, _localAppearanceTheme); } set { SaveAppearanceOption(value, ref _localAppearanceTheme, nameof(CurrentPeerChatBackground)); } }
        public AppearanceOption CurrentPeerAccent { get { return GetAppearanceOption(PeerAccentOptions, _localAppearanceAccent); } set { SaveAppearanceOption(value, ref _localAppearanceAccent, nameof(CurrentPeerAccent)); } }
        public AppearanceOption CurrentPeerChatDensity { get { return GetAppearanceOption(PeerChatDensityOptions, _localAppearanceDensity); } set { SaveAppearanceOption(value, ref _localAppearanceDensity, nameof(CurrentPeerChatDensity)); } }
        public AppearanceOption CurrentPeerChatFont { get { return GetAppearanceOption(PeerChatFontOptions, _localAppearanceFont); } set { SaveAppearanceOption(value, ref _localAppearanceFont, nameof(CurrentPeerChatFont)); } }
        public AppearanceOption CurrentPeerBubbleStyle { get { return GetAppearanceOption(PeerBubbleStyleOptions, _localAppearanceBubbleStyle); } set { SaveAppearanceOption(value, ref _localAppearanceBubbleStyle, nameof(CurrentPeerBubbleStyle)); } }
        public AppearanceOption CurrentPeerBubbleColor { get { return GetAppearanceOption(PeerBubbleColorOptions, _localAppearanceBubbleColor); } set { SaveAppearanceOption(value, ref _localAppearanceBubbleColor, nameof(CurrentPeerBubbleColor)); } }
        public TwoStringTuple CurrentPeerEmojiPack { get { return GetTupleOption(PeerEmojiPackOptions, _localEmojiPack); } set { SavePeerEmojiPack(value); } }
        public TwoStringTuple CurrentPeerBackgroundDim { get { return GetTupleOption(PeerBackgroundDimOptions, _localAppearanceDim.ToString()); } set { SavePeerBackgroundDim(value); } }
        public TwoStringTuple CurrentPeerBackgroundBlur { get { return GetTupleOption(PeerBackgroundBlurOptions, _localAppearanceBlur.ToString()); } set { SavePeerBackgroundBlur(value); } }
        public TwoStringTuple CurrentPeerBackgroundBrightness { get { return GetTupleOption(PeerBackgroundBrightnessOptions, _localAppearanceBrightness.ToString()); } set { SavePeerBackgroundBrightness(value); } }
        public Uri LocalBackgroundImageUri { get { return BuildLocalBackgroundImageUri(_localBackgroundImage); } }
        public IBrush LocalAppearancePreviewBackground { get { return GetLocalAppearancePreviewBackground(); } }
        public IBrush LocalAppearancePreviewOutgoing { get { return GetLocalAppearancePreviewOutgoing(); } }
        public double LocalAppearancePreviewImageOpacity { get { return LocalBackgroundImageUri == null ? 0 : 1; } }
        public double LocalAppearancePreviewDimOpacity { get { return LocalBackgroundImageUri == null ? 0 : Math.Clamp(_localAppearanceDim + Math.Max(0, -_localAppearanceBrightness), 0, 90) / 100d; } }
        public double LocalAppearancePreviewBrightnessOpacity { get { return LocalBackgroundImageUri == null ? 0 : Math.Max(0, _localAppearanceBrightness) / 100d; } }
        public int LocalAppearancePreviewBlurRadius { get { return LocalBackgroundImageUri == null ? 0 : _localAppearanceBlur; } }
        public bool HasLocalAppearanceDraftChanges { get { return HasLocalAppearanceChanges(); } }
        public bool IsE2ESectionVisible { get { return Id != 0 && CanUseE2EActions; } }
        public bool CanUseE2EActions { get { return GetE2EChat() != null; } }
        public bool E2EConfigured {
            get {
                E2EPeerState state = E2EManager.GetPeerState(Id);
                return state != null && E2EKeyStore.HasPeerKeys(Id, state.ProfileId);
            }
        }
        public string E2EStatusText {
            get {
                ChatViewModel chat = GetE2EChat();
                if (chat?.HasE2EStatus == true) return chat.E2EStatusText;
                return E2EConfigured ? "Laney E2E настроен" : "Для этого диалога E2E не настроен";
            }
        }
        public string E2EProfileText {
            get {
                E2EPeerState state = E2EManager.GetPeerState(Id);
                return state == null ? "Нет профиля" : E2ESecurityProfileIds.GetTitle(state.ProfileId);
            }
        }
        public string E2ESasText {
            get {
                E2EPeerState state = E2EManager.GetPeerState(Id);
                return String.IsNullOrWhiteSpace(state?.Sas) ? "SAS не создан" : $"SAS {state.Sas}";
            }
        }
        public string E2EFingerprintText {
            get {
                E2EPeerState state = E2EManager.GetPeerState(Id);
                return String.IsNullOrWhiteSpace(state?.Fingerprint) ? "Fingerprint появится после настройки." : state.Fingerprint;
            }
        }
        public bool E2EVerified {
            get { return E2EManager.GetPeerState(Id)?.IsVerified == true; }
            set { SetE2EVerified(value); }
        }
        public bool E2EAutoEncryptText {
            get { return E2EManager.GetPeerState(Id)?.AutoEncryptText == true; }
            set { SetE2EAutoEncryptText(value); }
        }
        public int MembersCount { get { return _membersCount; } private set { _membersCount = value; OnPropertyChanged(); } }
        public ObservableCollection<TwoStringTuple> Information { get { return _information; } private set { _information = value; OnPropertyChanged(); } }
        public ObservableCollection<TwoStringTuple> ChatStatistics { get { return _chatStatistics; } private set { _chatStatistics = value; OnPropertyChanged(); } }
        public ObservableCollection<ChatStatisticTile> ChatStatisticTiles { get { return _chatStatisticTiles; } private set { _chatStatisticTiles = value; OnPropertyChanged(); } }
        public ObservableCollection<ChatStatisticBar> ChatStatisticMix { get { return _chatStatisticMix; } private set { _chatStatisticMix = value; OnPropertyChanged(); } }
        public ObservableCollection<ChatStatisticBar> ChatStatisticAttachments { get { return _chatStatisticAttachments; } private set { _chatStatisticAttachments = value; OnPropertyChanged(); } }
        public bool HasChatStatisticDashboard { get { return ChatStatisticTiles.Count > 0; } }
        public bool HasChatStatisticMix { get { return ChatStatisticMix.Count > 0; } }
        public bool HasChatStatisticAttachments { get { return ChatStatisticAttachments.Count > 0; } }
        public ObservableCollection<LocalNoteHistoryEntry> LocalNoteHistory { get { return _localNoteHistory; } private set { _localNoteHistory = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasLocalNoteHistory)); } }
        public bool HasLocalNoteHistory { get { return LocalNoteHistory.Count > 0; } }
        public bool IsChatStatisticsVisible { get { return Id.IsChat(); } }
        public bool IsOwnUserProfile { get { return Id.IsUser() && session?.UserId == Id; } }
        public ChatMembersTabViewModel ChatMembers { get { return _chatMembers; } private set { _chatMembers = value; OnPropertyChanged(); } }

        public ConversationAttachmentsTabViewModel Photos { get { return _photos; } set { _photos = value; OnPropertyChanged(); } }
        public ConversationAttachmentsTabViewModel Videos { get { return _videos; } set { _videos = value; OnPropertyChanged(); } }
        public ConversationAttachmentsTabViewModel Audios { get { return _audios; } set { _audios = value; OnPropertyChanged(); } }
        public ConversationAttachmentsTabViewModel Documents { get { return _documents; } set { _documents = value; OnPropertyChanged(); } }
        public ConversationAttachmentsTabViewModel Share { get { return _share; } set { _share = value; OnPropertyChanged(); } }
        //public ConversationAttachmentsTabViewModel Graffities { get { return _graffities; } set { _graffities = value; OnPropertyChanged(); } }
        //public ConversationAttachmentsTabViewModel AudioMessages { get { return _audioMessages; } set { _audioMessages = value; OnPropertyChanged(); } }

        public Command FirstCommand { get { return _firstCommand; } private set { _firstCommand = value; OnPropertyChanged(); } }
        public Command SecondCommand { get { return _secondCommand; } private set { _secondCommand = value; OnPropertyChanged(); } }
        public Command ThirdCommand { get { return _thirdCommand; } private set { _thirdCommand = value; OnPropertyChanged(); } }
        public Command MoreCommand { get { return _moreCommand; } private set { _moreCommand = value; OnPropertyChanged(); } }
        public RelayCommand ClearMutedSendersCommand { get { return _clearMutedSendersCommand; } private set { _clearMutedSendersCommand = value; OnPropertyChanged(); } }
        public RelayCommand ClearQuietTextRulesCommand { get { return _clearQuietTextRulesCommand; } private set { _clearQuietTextRulesCommand = value; OnPropertyChanged(); } }
        public RelayCommand ClearShadowBannedSendersCommand { get { return _clearShadowBannedSendersCommand; } private set { _clearShadowBannedSendersCommand = value; OnPropertyChanged(); } }
        public RelayCommand ClearShadowBannedTextRulesCommand { get { return _clearShadowBannedTextRulesCommand; } private set { _clearShadowBannedTextRulesCommand = value; OnPropertyChanged(); } }
        public RelayCommand ClearShadowBannedAttachmentKindsCommand { get { return _clearShadowBannedAttachmentKindsCommand; } private set { _clearShadowBannedAttachmentKindsCommand = value; OnPropertyChanged(); } }
        public RelayCommand ClearLocalNoteHistoryCommand { get { return _clearLocalNoteHistoryCommand; } private set { _clearLocalNoteHistoryCommand = value; OnPropertyChanged(); } }
        public RelayCommand UpdateProfileInfoCommand { get { return _updateProfileInfoCommand; } private set { _updateProfileInfoCommand = value; OnPropertyChanged(); } }
        public RelayCommand UpdateProfilePhotoCommand { get { return _updateProfilePhotoCommand; } private set { _updateProfilePhotoCommand = value; OnPropertyChanged(); } }
        public RelayCommand UpdateProfileStatusCommand { get { return _updateProfileStatusCommand; } private set { _updateProfileStatusCommand = value; OnPropertyChanged(); } }
        public RelayCommand ClearProfileStatusCommand { get { return _clearProfileStatusCommand; } private set { _clearProfileStatusCommand = value; OnPropertyChanged(); } }
        public RelayCommand ApplyLocalAppearanceCommand { get { return _applyLocalAppearanceCommand; } private set { _applyLocalAppearanceCommand = value; OnPropertyChanged(); } }
        public RelayCommand ResetLocalAppearanceCommand { get { return _resetLocalAppearanceCommand; } private set { _resetLocalAppearanceCommand = value; OnPropertyChanged(); } }
        public RelayCommand ConfigureE2ECommand { get { return _configureE2ECommand; } private set { _configureE2ECommand = value; OnPropertyChanged(); } }
        public RelayCommand CreateE2EHandshakeCommand { get { return _createE2EHandshakeCommand; } private set { _createE2EHandshakeCommand = value; OnPropertyChanged(); } }
        public RelayCommand ImportE2EHandshakeCommand { get { return _importE2EHandshakeCommand; } private set { _importE2EHandshakeCommand = value; OnPropertyChanged(); } }
        public RelayCommand ShowE2EFingerprintCommand { get { return _showE2EFingerprintCommand; } private set { _showE2EFingerprintCommand = value; OnPropertyChanged(); } }
        public RelayCommand RotateE2EKeysCommand { get { return _rotateE2EKeysCommand; } private set { _rotateE2EKeysCommand = value; OnPropertyChanged(); } }
        public RelayCommand ExportE2EBackupCommand { get { return _exportE2EBackupCommand; } private set { _exportE2EBackupCommand = value; OnPropertyChanged(); } }
        public RelayCommand ImportE2EBackupCommand { get { return _importE2EBackupCommand; } private set { _importE2EBackupCommand = value; OnPropertyChanged(); } }
        public RelayCommand ResetE2ECommand { get { return _resetE2ECommand; } private set { _resetE2ECommand = value; OnPropertyChanged(); } }

        private int _lastCmid = 0;
        private VKSession session;
        private List<Entity> _allMembers = new List<Entity>();
        private ObservableCollection<Entity> _displayedMembers = new ObservableCollection<Entity>();
        public event EventHandler CloseWindowRequested;
        public event EventHandler FocusLocalNoteRequested;

        public PeerProfileViewModel(VKSession session, long peerId) {
            this.session = session;
            Id = peerId;
            _localNote = Settings.GetPeerLocalNote(peerId);
            _localAlias = Settings.GetPeerLocalAlias(peerId);
            _localAvatar = Settings.GetPeerLocalAvatar(peerId);
            _localTags = Settings.GetPeerLocalTagsText(peerId);
            LoadLocalAppearanceDraftFromSettings(false);
            _mutedSendersText = FormatSenderIds(Settings.GetMutedSenderIds(peerId));
            _quietTextRules = NormalizeShadowBannedTextRules(Settings.GetPeerQuietTextRules(peerId));
            _shadowBannedSendersText = FormatSenderIds(Settings.GetShadowBannedSenderIds(peerId));
            _shadowBannedTextRules = NormalizeShadowBannedTextRules(Settings.GetShadowBannedTextRules(peerId));
            ClearMutedSendersCommand = new RelayCommand((o) => ClearMutedSenders());
            ClearQuietTextRulesCommand = new RelayCommand((o) => ClearQuietTextRules());
            ClearShadowBannedSendersCommand = new RelayCommand((o) => ClearShadowBannedSenders());
            ClearShadowBannedTextRulesCommand = new RelayCommand((o) => ClearShadowBannedTextRules());
            ClearShadowBannedAttachmentKindsCommand = new RelayCommand((o) => ClearShadowBannedAttachmentKinds());
            ClearLocalNoteHistoryCommand = new RelayCommand((o) => ClearLocalNoteHistory());
            UpdateProfileInfoCommand = new RelayCommand((o) => new System.Action(async () => await UpdateProfileInfoAsync())());
            UpdateProfilePhotoCommand = new RelayCommand((o) => new System.Action(async () => await UpdateProfilePhotoAsync())());
            UpdateProfileStatusCommand = new RelayCommand((o) => new System.Action(async () => await UpdateProfileStatusAsync())());
            ClearProfileStatusCommand = new RelayCommand((o) => new System.Action(async () => await ClearProfileStatusAsync())());
            ApplyLocalAppearanceCommand = new RelayCommand((o) => ApplyLocalAppearanceDraft());
            ResetLocalAppearanceCommand = new RelayCommand((o) => LoadLocalAppearanceDraftFromSettings(true));
            ConfigureE2ECommand = new RelayCommand((o) => new System.Action(async () => await RunE2EActionAsync(o, (chat, target) => ContextMenuHelper.ShowE2ESetupDialogAsync(session, chat)))());
            CreateE2EHandshakeCommand = new RelayCommand((o) => new System.Action(async () => await RunE2EActionAsync(o, (chat, target) => ContextMenuHelper.ShowCreateX25519HandshakeDialogAsync(session, chat, target)))());
            ImportE2EHandshakeCommand = new RelayCommand((o) => new System.Action(async () => await RunE2EActionAsync(o, (chat, target) => ContextMenuHelper.ShowImportX25519HandshakeDialogAsync(session, chat, target)))());
            ShowE2EFingerprintCommand = new RelayCommand((o) => new System.Action(async () => await RunE2EActionAsync(o, (chat, target) => ContextMenuHelper.ShowE2EFingerprintDialogAsync(session, chat, target)))());
            RotateE2EKeysCommand = new RelayCommand((o) => new System.Action(async () => await RunE2EActionAsync(o, (chat, target) => ContextMenuHelper.RotateX25519KeysAsync(session, chat, target)))());
            ExportE2EBackupCommand = new RelayCommand((o) => new System.Action(async () => await RunE2EActionAsync(o, (chat, target) => ContextMenuHelper.ShowExportE2EBackupDialogAsync(session, chat, target)))());
            ImportE2EBackupCommand = new RelayCommand((o) => new System.Action(async () => await RunE2EActionAsync(o, (chat, target) => ContextMenuHelper.ShowImportE2EBackupDialogAsync(session, chat)))());
            ResetE2ECommand = new RelayCommand((o) => new System.Action(async () => await RunE2EActionAsync(o, (chat, target) => ContextMenuHelper.ResetE2EAsync(session, chat)))());
            RefreshLocalNoteHistory();
            new System.Action(async () => await SetupAsync())();
        }

        private ChatViewModel GetE2EChat() {
            if (session?.CurrentOpenedChat?.PeerId == Id) return session.CurrentOpenedChat;
            return session?.ImViewModel?.SortedChats?.FirstOrDefault(c => c.PeerId == Id);
        }

        private Control GetE2ECommandTarget(object target) {
            return target as Control ?? session?.ModalWindow as Control;
        }

        private async Task RunE2EActionAsync(object target, Func<ChatViewModel, Control, Task> action) {
            ChatViewModel chat = GetE2EChat();
            Control control = GetE2ECommandTarget(target);
            if (chat == null || control == null) {
                await new VKUIDialog("Laney E2E недоступен", "Открой конкретный диалог, а потом его профиль. Без chat VM тут нечего настраивать. Да, скучно, зато честно.", ["Понятно"], 1).ShowDialog(session.ModalWindow);
                return;
            }

            await action(chat, control);
            chat.RefreshE2EState();
            RefreshE2ESurface();
        }

        private void SetE2EVerified(bool value) {
            E2EPeerState state = E2EManager.GetPeerState(Id);
            if (state == null || state.IsVerified == value) return;

            E2EManager.SetPeerVerified(Id, value);
            GetE2EChat()?.RefreshE2EState();
            RefreshE2ESurface();
        }

        private void SetE2EAutoEncryptText(bool value) {
            E2EPeerState state = E2EManager.GetPeerState(Id);
            if (state == null || state.AutoEncryptText == value) return;

            E2EManager.SetAutoEncryptText(Id, value);
            GetE2EChat()?.RefreshE2EState();
            RefreshE2ESurface();
        }

        private void RefreshE2ESurface() {
            OnPropertyChanged(nameof(IsE2ESectionVisible));
            OnPropertyChanged(nameof(CanUseE2EActions));
            OnPropertyChanged(nameof(E2EConfigured));
            OnPropertyChanged(nameof(E2EStatusText));
            OnPropertyChanged(nameof(E2EProfileText));
            OnPropertyChanged(nameof(E2ESasText));
            OnPropertyChanged(nameof(E2EFingerprintText));
            OnPropertyChanged(nameof(E2EVerified));
            OnPropertyChanged(nameof(E2EAutoEncryptText));
        }

        private async Task SetupAsync() {
            Header = null;
            if (Id.IsChat()) {
                ChatMembers = null;
                await GetChatAsync(Id);
            } else if (Id.IsUser()) {
                await GetUserAsync(Id);
            } else if (Id.IsGroup()) {
                await GetGroupAsync(Id * -1);
            }

            _sourceHeader = Header;
            _sourceAvatar = Avatar;
            ApplyLocalIdentity();
        }

        private void SaveLocalNote(string value) {
            value ??= String.Empty;
            if (_localNote == value) return;

            _localNote = value;
            Settings.SetPeerLocalNoteWithHistory(Id, value);
            RefreshLocalNoteHistory();
            OnPropertyChanged(nameof(LocalNote));
        }

        private void RestoreLocalNote(string value) {
            if (String.IsNullOrWhiteSpace(value)) return;
            LocalNote = value;
        }

        private void ClearLocalNoteHistory() {
            Settings.ClearPeerLocalNoteHistory(Id);
            RefreshLocalNoteHistory();
        }

        private void RefreshLocalNoteHistory() {
            LocalNoteHistory = new ObservableCollection<LocalNoteHistoryEntry>(
                Settings.GetPeerLocalNoteHistory(Id).Select(i => new LocalNoteHistoryEntry(i, RestoreLocalNote)));
        }

        private void SaveLocalAlias(string value) {
            value ??= String.Empty;
            if (_localAlias == value) return;

            _localAlias = value;
            Settings.SetPeerLocalAlias(Id, value);
            ApplyLocalIdentity();
            ApplyLocalIdentityToOpenedChat();
            OnPropertyChanged(nameof(LocalAlias));
        }

        private void SaveLocalAvatar(string value) {
            value ??= String.Empty;
            if (_localAvatar == value) return;

            _localAvatar = value;
            Settings.SetPeerLocalAvatar(Id, value);
            ApplyLocalIdentity();
            ApplyLocalIdentityToOpenedChat();
            OnPropertyChanged(nameof(LocalAvatar));
        }

        private void SaveLocalTags(string value) {
            Settings.SetPeerLocalTags(Id, value);
            string normalized = Settings.GetPeerLocalTagsText(Id);
            if (_localTags == normalized) return;

            _localTags = normalized;
            OnPropertyChanged(nameof(LocalTags));
            RefreshLocalFolderStateForOpenedChat();
        }

        private void SaveLocalBackgroundImage(string value) {
            value ??= String.Empty;
            if (_localBackgroundImage == value) return;

            _localBackgroundImage = value.Trim();
            NotifyLocalAppearanceDraftChanged(nameof(LocalBackgroundImage));
        }

        private void SavePeerBackgroundDim(TwoStringTuple value) {
            if (value == null) return;
            if (!int.TryParse(value.Item1, out int dim)) return;
            dim = Math.Clamp(dim, 0, 80);
            if (_localAppearanceDim == dim) return;

            _localAppearanceDim = dim;
            NotifyLocalAppearanceDraftChanged(nameof(CurrentPeerBackgroundDim));
        }

        private void SavePeerBackgroundBlur(TwoStringTuple value) {
            if (value == null) return;
            if (!int.TryParse(value.Item1, out int blur)) return;
            blur = Math.Clamp(blur, 0, 16);
            if (_localAppearanceBlur == blur) return;

            _localAppearanceBlur = blur;
            NotifyLocalAppearanceDraftChanged(nameof(CurrentPeerBackgroundBlur));
        }

        private void SavePeerBackgroundBrightness(TwoStringTuple value) {
            if (value == null) return;
            if (!int.TryParse(value.Item1, out int brightness)) return;
            brightness = Math.Clamp(brightness, -40, 40);
            if (_localAppearanceBrightness == brightness) return;

            _localAppearanceBrightness = brightness;
            NotifyLocalAppearanceDraftChanged(nameof(CurrentPeerBackgroundBrightness));
        }

        private void SavePeerEmojiPack(TwoStringTuple value) {
            if (value == null) return;
            string packId = EmojiPackIds.Normalize(value.Item1, true);
            if (_localEmojiPack == packId) return;

            _localEmojiPack = packId;
            NotifyLocalAppearanceDraftChanged(nameof(CurrentPeerEmojiPack));
        }

        private void SaveMutedSenders(string value) {
            HashSet<long> ids = ParseSenderIds(value);
            string normalized = FormatSenderIds(ids);
            bool changed = _mutedSendersText != normalized;

            if (changed) {
                _mutedSendersText = normalized;
                Settings.SetMutedSenderIds(Id, ids);
            }

            OnPropertyChanged(nameof(MutedSendersText));
            OnPropertyChanged(nameof(HasMutedSenders));
        }

        private void SaveQuietTextRules(string value) {
            string normalized = NormalizeShadowBannedTextRules(value);
            bool changed = _quietTextRules != normalized;

            if (changed) {
                _quietTextRules = normalized;
                Settings.SetPeerQuietTextRules(Id, normalized);
            }

            OnPropertyChanged(nameof(QuietTextRules));
            OnPropertyChanged(nameof(HasQuietTextRules));
        }

        private void SaveAntiSpamEnabled(bool value) {
            Settings.SetPeerAntiSpamEnabled(Id, value);
            OnPropertyChanged(nameof(AntiSpamEnabled));
            ReloadOpenedChatAfterShadowBanChange();
        }

        private AppearanceOption GetAppearanceOption(ObservableCollection<AppearanceOption> options, string id) {
            if (String.IsNullOrWhiteSpace(id)) id = AppearanceManager.InheritChatBackgroundId;
            return options.Where(o => o.Id == id).FirstOrDefault() ?? options[0];
        }

        private TwoStringTuple GetTupleOption(ObservableCollection<TwoStringTuple> options, string id) {
            return options.Where(o => o.Item1 == id).FirstOrDefault() ?? options[0];
        }

        private void SaveAppearanceOption(AppearanceOption option, ref string target, string propertyName) {
            if (option == null) return;
            string id = option.Id ?? String.Empty;
            if (target == id) return;

            target = id;
            NotifyLocalAppearanceDraftChanged(propertyName);
        }

        private void RefreshLocalAppearancePreview() {
            OnPropertyChanged(nameof(LocalBackgroundImageUri));
            OnPropertyChanged(nameof(LocalAppearancePreviewBackground));
            OnPropertyChanged(nameof(LocalAppearancePreviewOutgoing));
            OnPropertyChanged(nameof(LocalAppearancePreviewImageOpacity));
            OnPropertyChanged(nameof(LocalAppearancePreviewDimOpacity));
            OnPropertyChanged(nameof(LocalAppearancePreviewBrightnessOpacity));
            OnPropertyChanged(nameof(LocalAppearancePreviewBlurRadius));
            OnPropertyChanged(nameof(HasLocalAppearanceDraftChanges));
        }

        private void NotifyLocalAppearanceDraftChanged(string propertyName) {
            OnPropertyChanged(propertyName);
            RefreshLocalAppearancePreview();
        }

        private void LoadLocalAppearanceDraftFromSettings(bool notify) {
            _localAppearanceTheme = NormalizeAppearanceDraft(Settings.GetPeerLocalTheme(Id));
            _localBackgroundImage = Settings.GetPeerLocalBackgroundImage(Id);
            _localAppearanceDim = Settings.GetPeerLocalBackgroundDim(Id);
            _localAppearanceBlur = Settings.GetPeerLocalBackgroundBlur(Id);
            _localAppearanceBrightness = Settings.GetPeerLocalBackgroundBrightness(Id);
            _localAppearanceAccent = NormalizeAppearanceDraft(Settings.GetPeerLocalAccent(Id));
            _localAppearanceDensity = NormalizeAppearanceDraft(Settings.GetPeerLocalDensity(Id));
            _localAppearanceFont = NormalizeAppearanceDraft(Settings.GetPeerLocalFont(Id));
            _localAppearanceBubbleStyle = NormalizeAppearanceDraft(Settings.GetPeerLocalBubbleStyle(Id));
            _localAppearanceBubbleColor = NormalizeAppearanceDraft(Settings.GetPeerLocalBubbleColor(Id));
            _localEmojiPack = EmojiPackIds.Normalize(Settings.GetPeerLocalEmojiPack(Id), true);

            if (!notify) return;
            OnPropertyChanged(nameof(CurrentPeerChatBackground));
            OnPropertyChanged(nameof(LocalBackgroundImage));
            OnPropertyChanged(nameof(CurrentPeerBackgroundDim));
            OnPropertyChanged(nameof(CurrentPeerBackgroundBlur));
            OnPropertyChanged(nameof(CurrentPeerBackgroundBrightness));
            OnPropertyChanged(nameof(CurrentPeerAccent));
            OnPropertyChanged(nameof(CurrentPeerChatDensity));
            OnPropertyChanged(nameof(CurrentPeerChatFont));
            OnPropertyChanged(nameof(CurrentPeerBubbleStyle));
            OnPropertyChanged(nameof(CurrentPeerBubbleColor));
            OnPropertyChanged(nameof(CurrentPeerEmojiPack));
            RefreshLocalAppearancePreview();
        }

        private void ApplyLocalAppearanceDraft() {
            Settings.SetPeerLocalTheme(Id, _localAppearanceTheme);
            Settings.SetPeerLocalBackgroundImage(Id, _localBackgroundImage);
            Settings.SetPeerLocalBackgroundDim(Id, _localAppearanceDim);
            Settings.SetPeerLocalBackgroundBlur(Id, _localAppearanceBlur);
            Settings.SetPeerLocalBackgroundBrightness(Id, _localAppearanceBrightness);
            Settings.SetPeerLocalAccent(Id, _localAppearanceAccent);
            Settings.SetPeerLocalDensity(Id, _localAppearanceDensity);
            Settings.SetPeerLocalFont(Id, _localAppearanceFont);
            Settings.SetPeerLocalBubbleStyle(Id, _localAppearanceBubbleStyle);
            Settings.SetPeerLocalBubbleColor(Id, _localAppearanceBubbleColor);
            Settings.SetPeerLocalEmojiPack(Id, _localEmojiPack);
            L2Emoji.ClearCache();
            OnPropertyChanged(nameof(HasLocalAppearanceDraftChanges));
        }

        private bool HasLocalAppearanceChanges() {
            return NormalizeStorageValue(_localAppearanceTheme) != NormalizeStorageValue(Settings.GetPeerLocalTheme(Id))
                || NormalizeStorageValue(_localBackgroundImage) != NormalizeStorageValue(Settings.GetPeerLocalBackgroundImage(Id))
                || _localAppearanceDim != Settings.GetPeerLocalBackgroundDim(Id)
                || _localAppearanceBlur != Settings.GetPeerLocalBackgroundBlur(Id)
                || _localAppearanceBrightness != Settings.GetPeerLocalBackgroundBrightness(Id)
                || NormalizeStorageValue(_localAppearanceAccent) != NormalizeStorageValue(Settings.GetPeerLocalAccent(Id))
                || NormalizeStorageValue(_localAppearanceDensity) != NormalizeStorageValue(Settings.GetPeerLocalDensity(Id))
                || NormalizeStorageValue(_localAppearanceFont) != NormalizeStorageValue(Settings.GetPeerLocalFont(Id))
                || NormalizeStorageValue(_localAppearanceBubbleStyle) != NormalizeStorageValue(Settings.GetPeerLocalBubbleStyle(Id))
                || NormalizeStorageValue(_localAppearanceBubbleColor) != NormalizeStorageValue(Settings.GetPeerLocalBubbleColor(Id))
                || EmojiPackIds.Normalize(_localEmojiPack, true) != EmojiPackIds.Normalize(Settings.GetPeerLocalEmojiPack(Id), true);
        }

        private static string NormalizeAppearanceDraft(string value) {
            return String.IsNullOrWhiteSpace(value) ? AppearanceManager.InheritChatBackgroundId : value.Trim();
        }

        private static string NormalizeStorageValue(string value) {
            if (String.IsNullOrWhiteSpace(value) || value == AppearanceManager.InheritChatBackgroundId) return String.Empty;
            return value.Trim();
        }

        private static Uri BuildLocalBackgroundImageUri(string value) {
            if (String.IsNullOrWhiteSpace(value)) return null;
            value = value.Trim();
            if (Uri.TryCreate(value, UriKind.Absolute, out Uri uri)) return uri;
            if (System.IO.Path.IsPathFullyQualified(value)) return new Uri(value);
            return null;
        }

        private IBrush GetLocalAppearancePreviewBackground() {
            string backgroundId = GetPreviewBackgroundId();
            if (String.IsNullOrWhiteSpace(backgroundId) || backgroundId == AppearanceManager.DefaultChatBackgroundId) {
                AppearanceOption defaultOption = AppearanceManager.GetChatBackgroundOption(AppearanceManager.DefaultChatBackgroundId);
                return new SolidColorBrush(defaultOption.GetActualColor());
            }

            AppearanceOption option = AppearanceManager.GetChatBackgroundOption(backgroundId);
            return new SolidColorBrush(option.GetActualColor());
        }

        private IBrush GetLocalAppearancePreviewOutgoing() {
            string colorId = _localAppearanceBubbleColor;
            if (String.IsNullOrWhiteSpace(colorId)
                || colorId == AppearanceManager.InheritChatBackgroundId
                || colorId == AppearanceManager.DefaultBubbleColorId) {
                if (Settings.MessageBubbleAutoColor) {
                    IBrush autoBrush = GetLocalAppearancePreviewAutoOutgoing();
                    if (autoBrush != null) return autoBrush;
                }

                return App.GetResource<IBrush>(AppearanceManager.MessageBubbleOutgoingBrushResourceKey)
                    ?? App.GetResource<IBrush>("VKImBubbleOutgoingBrush")
                    ?? new SolidColorBrush(Color.Parse("#DDEEFF"));
            }

            AppearanceOption option = AppearanceManager.BubbleColorOptionsWithInherit.FirstOrDefault(o => o.Id == colorId) ?? AppearanceManager.BubbleColorOptionsWithInherit[1];
            return new SolidColorBrush(option.GetActualColor());
        }

        private IBrush GetLocalAppearancePreviewAutoOutgoing() {
            string bubbleId = GetPreviewBackgroundId() switch {
                "mint" => "mint",
                "rose" => "rose",
                "violet" => "violet",
                "graphite" => "graphite",
                "paper" => "amber",
                _ => null
            };

            if (String.IsNullOrWhiteSpace(bubbleId)) return null;

            AppearanceOption option = AppearanceManager.BubbleColorOptionsWithInherit.FirstOrDefault(o => o.Id == bubbleId);
            return option == null ? null : new SolidColorBrush(option.GetActualColor());
        }

        private string GetPreviewBackgroundId() {
            string backgroundId = _localAppearanceTheme;
            if (String.IsNullOrWhiteSpace(backgroundId)
                || backgroundId == AppearanceManager.InheritChatBackgroundId
                || backgroundId == AppearanceManager.DefaultChatBackgroundId) {
                backgroundId = Settings.ChatBackground;
            }
            return String.IsNullOrWhiteSpace(backgroundId) ? AppearanceManager.DefaultChatBackgroundId : backgroundId;
        }

        private void SaveShadowBannedSenders(string value) {
            HashSet<long> ids = ParseSenderIds(value);
            string normalized = FormatSenderIds(ids);
            bool changed = _shadowBannedSendersText != normalized;

            if (changed) {
                _shadowBannedSendersText = normalized;
                Settings.SetShadowBannedSenderIds(Id, ids);
                ReloadOpenedChatAfterShadowBanChange();
            }

            OnPropertyChanged(nameof(ShadowBannedSendersText));
            OnPropertyChanged(nameof(HasShadowBannedSenders));
        }

        private void SaveShadowBannedTextRules(string value) {
            string normalized = NormalizeShadowBannedTextRules(value);
            bool changed = _shadowBannedTextRules != normalized;

            if (changed) {
                _shadowBannedTextRules = normalized;
                Settings.SetShadowBannedTextRules(Id, normalized);
                ReloadOpenedChatAfterShadowBanChange();
            }

            OnPropertyChanged(nameof(ShadowBannedTextRules));
            OnPropertyChanged(nameof(HasShadowBannedTextRules));
        }

        private void ClearMutedSenders() {
            if (!HasMutedSenders && String.IsNullOrWhiteSpace(_mutedSendersText)) return;

            _mutedSendersText = String.Empty;
            Settings.SetMutedSenderIds(Id, Array.Empty<long>());
            OnPropertyChanged(nameof(MutedSendersText));
            OnPropertyChanged(nameof(HasMutedSenders));
        }

        private void ClearQuietTextRules() {
            if (!HasQuietTextRules && String.IsNullOrWhiteSpace(_quietTextRules)) return;

            _quietTextRules = String.Empty;
            Settings.SetPeerQuietTextRules(Id, String.Empty);
            OnPropertyChanged(nameof(QuietTextRules));
            OnPropertyChanged(nameof(HasQuietTextRules));
        }

        private void ClearShadowBannedSenders() {
            if (!HasShadowBannedSenders && String.IsNullOrWhiteSpace(_shadowBannedSendersText)) return;

            _shadowBannedSendersText = String.Empty;
            Settings.SetShadowBannedSenderIds(Id, Array.Empty<long>());
            OnPropertyChanged(nameof(ShadowBannedSendersText));
            OnPropertyChanged(nameof(HasShadowBannedSenders));
            ReloadOpenedChatAfterShadowBanChange();
        }

        private void ClearShadowBannedTextRules() {
            if (!HasShadowBannedTextRules && String.IsNullOrWhiteSpace(_shadowBannedTextRules)) return;

            _shadowBannedTextRules = String.Empty;
            Settings.SetShadowBannedTextRules(Id, String.Empty);
            OnPropertyChanged(nameof(ShadowBannedTextRules));
            OnPropertyChanged(nameof(HasShadowBannedTextRules));
            ReloadOpenedChatAfterShadowBanChange();
        }

        private bool HasShadowBannedAttachmentKind(ShadowBannedAttachmentKinds kind) {
            return Settings.GetShadowBannedAttachmentKinds(Id).HasFlag(kind);
        }

        private void SaveShadowBannedAttachmentKind(ShadowBannedAttachmentKinds kind, bool enabled, string propertyName) {
            ShadowBannedAttachmentKinds current = Settings.GetShadowBannedAttachmentKinds(Id);
            ShadowBannedAttachmentKinds updated = enabled ? current | kind : current & ~kind;
            if (current == updated) return;

            Settings.SetShadowBannedAttachmentKinds(Id, updated);
            OnPropertyChanged(propertyName);
            OnPropertyChanged(nameof(HasShadowBannedAttachmentKinds));
            ReloadOpenedChatAfterShadowBanChange();
        }

        private void ClearShadowBannedAttachmentKinds() {
            if (!HasShadowBannedAttachmentKinds) return;

            Settings.SetShadowBannedAttachmentKinds(Id, ShadowBannedAttachmentKinds.None);
            OnPropertyChanged(nameof(ShadowBanVoiceMessages));
            OnPropertyChanged(nameof(ShadowBanLinks));
            OnPropertyChanged(nameof(ShadowBanStickers));
            OnPropertyChanged(nameof(ShadowBanGraffiti));
            OnPropertyChanged(nameof(ShadowBanForwardedMessages));
            OnPropertyChanged(nameof(HasShadowBannedAttachmentKinds));
            ReloadOpenedChatAfterShadowBanChange();
        }

        private void ReloadOpenedChatAfterShadowBanChange() {
            if (session.CurrentOpenedChat?.PeerId != Id) return;
            new System.Action(async () => await session.CurrentOpenedChat.ReloadMessagesAsync())();
        }

        private static HashSet<long> ParseSenderIds(string value) {
            HashSet<long> ids = new HashSet<long>();
            if (String.IsNullOrWhiteSpace(value)) return ids;

            foreach (string item in value.Split([',', ';', '\n', '\r', '\t', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
                if (long.TryParse(item, out long id) && id != 0) ids.Add(id);
            }

            return ids;
        }

        private static string FormatSenderIds(IEnumerable<long> ids) {
            return ids == null ? String.Empty : String.Join(", ", ids.Where(id => id != 0).Distinct().OrderBy(id => id));
        }

        private static string NormalizeShadowBannedTextRules(string value) {
            if (String.IsNullOrWhiteSpace(value)) return String.Empty;

            return String.Join(
                Environment.NewLine,
                value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(rule => rule.Length > 0)
                    .Distinct()
                    .Take(32));
        }

        private void ApplyLocalIdentity() {
            Header = String.IsNullOrWhiteSpace(_localAlias) ? _sourceHeader : _localAlias;
            Avatar = Settings.GetPeerLocalAvatarUri(Id) ?? _sourceAvatar;
        }

        private void ApplyLocalIdentityToOpenedChat() {
            if (session.CurrentOpenedChat?.PeerId == Id) session.CurrentOpenedChat.ApplyLocalPeerOverrides();
        }

        private void RefreshLocalFolderStateForOpenedChat() {
            ChatViewModel localChat = session.ImViewModel?.SortedChats?.FirstOrDefault(c => c.PeerId == Id);
            localChat?.RefreshLocalFolderState();
            if (session.CurrentOpenedChat?.PeerId == Id && session.CurrentOpenedChat != localChat) session.CurrentOpenedChat.RefreshLocalFolderState();
            session.ImViewModel?.RefreshLocalFilters();
        }

        #region User-specific

        private async Task GetUserAsync(long userId) {
            if (IsLoading) return;
            IsLoading = true;
            Placeholder = null;
            try {
                UserEx user = await session.API.GetUserCardAsync(userId, VKAPIHelper.UserFields);
                _lastCmid = user.LastCMID;
                Header = String.Join(" ", new string[2] { user.FirstName, user.LastName });
                if (user.Photo != null) Avatar = user.Photo;
                Subhead = VKAPIHelper.GetOnlineInfo(user.OnlineInfo, user.Sex).ToLowerInvariant();

                switch (user.Deactivated) {
                    case DeactivationState.Banned: Subhead = Assets.i18n.Resources.user_blocked; break;
                    case DeactivationState.Deleted: Subhead = Assets.i18n.Resources.user_deleted; break;
                    default: Subhead = VKAPIHelper.GetOnlineInfo(user.OnlineInfo, user.Sex).ToLowerInvariant(); break;
                }

                SetupInfo(user);
                SetupCommands(user);
                ProfileFirstNameDraft = user.FirstName ?? String.Empty;
                ProfileLastNameDraft = user.LastName ?? String.Empty;
                ProfileStatusDraft = user.Status ?? String.Empty;
                LoadSocialOverlap(user.Id);
            } catch (Exception ex) {
                Log.Error(ex, $"Error in PeerProfileViewModel.GetUser!");
                Header = null;
                Placeholder = PlaceholderViewModel.GetForException(ex, async (o) => await GetUserAsync(userId));
            }
            IsLoading = false;
        }

        private void SetupInfo(UserEx user) {
            Information.Clear();

            // Owner state (по умолчанию возвращается у забаненных юзеров, но мб и в других случаях, хз)
            if (user.OwnerState != null) {
                StringBuilder sb = new StringBuilder();
                if (!string.IsNullOrEmpty(user.OwnerState.Description)) sb.AppendLine(user.OwnerState.Description);
                if (user.OwnerState.UnbanDate > 0) sb.AppendLine($"{Assets.i18n.Resources.unban_date}: {DateTimeOffset.FromUnixTimeSeconds(user.OwnerState.UnbanDate).DateTime.ToHumanizedString()}");
                Information.Add(new TwoStringTuple(VKIconNames.Icon20InfoCircleOutline, sb.ToString().Trim()));
            }

            // Banned/deleted/blocked...
            if (user.Blacklisted == 1) {
                Information.Add(new TwoStringTuple(VKIconNames.Icon20BlockOutline, Localizer.Get("user_blacklisted", user.Sex)));
            }
            if (user.BlacklistedByMe == 1) {
                Information.Add(new TwoStringTuple(VKIconNames.Icon20BlockOutline, Localizer.Get("user_blacklisted_by_me", user.Sex)));
            }

            // Domain
            Information.Add(new TwoStringTuple(VKIconNames.Icon20MentionOutline, GetUserSlug(user)));

            // Private profile
            if (user.IsClosed && !user.CanAccessClosed)
                Information.Add(new TwoStringTuple(VKIconNames.Icon20LockOutline, Assets.i18n.Resources.user_private));

            // Status
            if (!String.IsNullOrEmpty(user.Status))
                Information.Add(new TwoStringTuple(VKIconNames.Icon20ArticleOutline, user.Status.Trim()));

            // Birthday
            if (!String.IsNullOrEmpty(user.BirthDate))
                Information.Add(new TwoStringTuple(VKIconNames.Icon20GiftOutline, VKAPIHelper.GetNormalizedBirthDate(user.BirthDate)));

            // Live in
            if (!String.IsNullOrWhiteSpace(user.LiveIn))
                Information.Add(new TwoStringTuple(VKIconNames.Icon20HomeOutline, user.LiveIn.Trim()));

            // Work
            if (user.CurrentCareer != null) {
                var c = user.CurrentCareer;
                string h = c.Company.Trim();
                Information.Add(new TwoStringTuple(VKIconNames.Icon20WorkOutline, String.IsNullOrWhiteSpace(c.Position) ? h : $"{h} — {c.Position.Trim()}"));
            }

            // Education
            if (!String.IsNullOrWhiteSpace(user.CurrentEducation))
                Information.Add(new TwoStringTuple(VKIconNames.Icon20EducationOutline, user.CurrentEducation.Trim()));

            // Site
            if (!String.IsNullOrWhiteSpace(user.Site))
                Information.Add(new TwoStringTuple(VKIconNames.Icon20LinkCircleOutline, user.Site.Trim()));

            // Followers
            if (user.Followers > 0)
                Information.Add(new TwoStringTuple(VKIconNames.Icon20FollowersOutline, Localizer.GetDeclensionFormatted(user.Followers, "follower")));

            Information.Add(new TwoStringTuple(VKIconNames.Icon20BugOutline, user.Id.ToString()));
        }

        private async Task ClearProfileStatusAsync() {
            ProfileStatusDraft = String.Empty;
            await UpdateProfileStatusAsync();
        }

        private async Task UpdateProfileInfoAsync() {
            if (!IsOwnUserProfile || IsLoading) return;

            string firstName = ProfileFirstNameDraft?.Trim() ?? String.Empty;
            string lastName = ProfileLastNameDraft?.Trim() ?? String.Empty;
            if (String.IsNullOrWhiteSpace(firstName) || String.IsNullOrWhiteSpace(lastName)) {
                await new VKUIDialog("Имя не сохранено", "VK хочет и имя, и фамилию. Одним пустым полем этот бюрократический аттракцион не пройти.", ["Понятно"], 1).ShowDialog(session.ModalWindow);
                return;
            }

            bool reload = false;
            IsLoading = true;
            try {
                using JsonDocument document = await session.API.CallMethodAsync("account.saveProfileInfo", new Dictionary<string, string> {
                    { "first_name", firstName },
                    { "last_name", lastName }
                });
                JsonElement response = GetRawResponseOrThrow(document, "account.saveProfileInfo");
                bool changed = ReadBoolLike(response, "changed");
                string nameRequest = FormatNameRequest(response);

                string text = changed
                    ? "Имя сохранено в VK."
                    : !String.IsNullOrWhiteSpace(nameRequest)
                        ? $"Заявка на имя ушла в VK: {nameRequest}"
                        : "VK принял запрос, но мгновенного изменения не подтвердил.";
                session.ShowNotification(new Notification("Профиль VK", text, NotificationType.Success));
                reload = true;
            } catch (Exception ex) {
                Log.Error(ex, "Error in PeerProfileViewModel.UpdateProfileInfoAsync!");
                await ExceptionHelper.ShowErrorDialogAsync(session.ModalWindow, ex);
            } finally {
                IsLoading = false;
            }

            if (reload) await GetUserAsync(Id);
        }

        private async Task UpdateProfileStatusAsync() {
            if (!IsOwnUserProfile || IsLoading) return;

            string status = ProfileStatusDraft?.Trim() ?? String.Empty;
            bool updated = false;
            IsLoading = true;
            try {
                int result = await session.API.CallMethodAsync<int>("status.set", new Dictionary<string, string> {
                    { "text", status }
                });
                if (result != 1) throw new InvalidOperationException("VK API не подтвердил сохранение статуса.");

                updated = true;
                session.ShowNotification(new Notification("Профиль обновлён", String.IsNullOrWhiteSpace(status) ? "Статус очищен." : "Статус сохранён в VK.", NotificationType.Success));
            } catch (Exception ex) {
                Log.Error(ex, "Error in PeerProfileViewModel.UpdateProfileStatusAsync!");
                await ExceptionHelper.ShowErrorDialogAsync(session.ModalWindow, ex);
            }
            IsLoading = false;

            if (updated) await GetUserAsync(Id);
        }

        private async Task UpdateProfilePhotoAsync() {
            if (!IsOwnUserProfile || IsLoading) return;

            Window window = session.ModalWindow;
            if (window?.StorageProvider?.CanOpen != true) {
                await new VKUIDialog("Фото не выбрать", "StorageProvider не дает открыть picker. Без файла фото профиля не обновить, внезапно.", ["Понятно"], 1).ShowDialog(session.ModalWindow);
                return;
            }

            IReadOnlyList<IStorageFile> files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
                AllowMultiple = false,
                Title = "Фото профиля VK",
                FileTypeFilter = new List<FilePickerFileType> { FilePickerFileTypes.ImageAll }
            });
            IStorageFile file = files.FirstOrDefault();
            if (file == null) return;

            bool reload = false;
            Exception uploadException = null;
            IsLoading = true;
            try {
                var server = await session.API.Photos.GetOwnerPhotoUploadServerAsync();
                if (server?.Uri == null) throw new InvalidOperationException("VK не вернул upload_url для фото профиля.");

                VKHttpClientFileUploader uploader = new VKHttpClientFileUploader("photo", server.Uri, file);
                uploader.UploadFailed += (a, ex) => uploadException = ex;
                string uploadResponse = await uploader.UploadAsync();
                if (String.IsNullOrWhiteSpace(uploadResponse)) throw uploadException ?? new InvalidOperationException("Upload server не вернул ответ.");

                using JsonDocument uploadDocument = JsonDocument.Parse(uploadResponse);
                JsonElement upload = uploadDocument.RootElement;
                string uploadServer = ReadStringOrNumber(upload, "server");
                string photo = ReadStringOrNumber(upload, "photo");
                string hash = ReadStringOrNumber(upload, "hash");
                if (String.IsNullOrWhiteSpace(uploadServer) || String.IsNullOrWhiteSpace(photo) || String.IsNullOrWhiteSpace(hash)) {
                    throw new InvalidOperationException("Upload server вернул ответ без server/photo/hash.");
                }

                using JsonDocument saveDocument = await session.API.Photos.SaveOwnerPhotoAsync(uploadServer, photo, hash);
                GetRawResponseOrThrow(saveDocument, "photos.saveOwnerPhoto");
                session.ShowNotification(new Notification("Профиль VK", "Фото профиля отправлено в VK.", NotificationType.Success));
                reload = true;
            } catch (Exception ex) {
                Log.Error(ex, "Error in PeerProfileViewModel.UpdateProfilePhotoAsync!");
                await ExceptionHelper.ShowErrorDialogAsync(session.ModalWindow, ex);
            } finally {
                IsLoading = false;
            }

            if (reload) await GetUserAsync(Id);
        }

        private void LoadSocialOverlap(long userId) {
            if (session.UserId == userId) return;

            _ = LoadSocialOverlapAsync(userId);
        }

        private async Task LoadSocialOverlapAsync(long userId) {
            await LoadCommonFriendsAsync(userId);
            await LoadCommonGroupsAsync(userId);
            await LoadCommonChatsAsync(userId);
        }

        private async Task LoadCommonFriendsAsync(long userId) {
            try {
                List<long> ids = await GetIdListFromRawMethodAsync("friends.getMutual", new Dictionary<string, string> {
                    { "target_uid", userId.ToString() },
                    { "count", "1000" }
                });
                if (Id != userId) return;

                List<string> names = new List<string>();
                if (ids.Count > 0) {
                    List<User> users = await session.API.Users.GetAsync(ids.Take(6).ToList(), VKAPIHelper.Fields);
                    CacheManager.Add(users);
                    names.AddRange(users.Select(u => $"{u.FirstName} {u.LastName}".Trim()).Where(n => !String.IsNullOrWhiteSpace(n)));
                }

                AddInformationIfCurrent(userId, VKIconNames.Icon20UserCheckOutline, FormatCommonItems("Общие друзья", ids.Count, names));
            } catch (Exception ex) {
                Log.Debug(ex, $"Unable to load mutual friends for user {userId}.");
            }
        }

        private async Task LoadCommonGroupsAsync(long userId) {
            try {
                List<long> ids = await GetIdListFromRawMethodAsync("groups.getMutual", new Dictionary<string, string> {
                    { "target_uid", userId.ToString() },
                    { "count", "1000" }
                });
                if (Id != userId) return;

                List<string> names = await GetGroupNamesAsync(ids.Take(6).ToList());
                AddInformationIfCurrent(userId, VKIconNames.Icon20FollowersOutline, FormatCommonItems("Общие группы", ids.Count, names));
            } catch (Exception ex) {
                Log.Debug(ex, $"Unable to load mutual groups for user {userId}.");
            }
        }

        private async Task LoadCommonChatsAsync(long userId) {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => {
                if (Id != userId) return;

                List<string> names = GetLocalCommonChatNames(userId);
                AddInformationIfCurrent(userId, VKIconNames.Icon20MessageOutline, FormatCommonItems("Общие чаты (локально)", names.Count, names.Take(6).ToList()));
            });
        }

        private List<string> GetLocalCommonChatNames(long userId) {
            if (session.ImViewModel?.SortedChats == null) return new List<string>();

            return session.ImViewModel.SortedChats
                .Where(chat => chat.PeerId.IsChat() && IsUserKnownInLocalChat(chat, userId))
                .OrderByDescending(chat => chat.SortIndex)
                .Select(chat => chat.DisplayTitle)
                .Where(title => !String.IsNullOrWhiteSpace(title))
                .Distinct()
                .ToList();
        }

        private static bool IsUserKnownInLocalChat(ChatViewModel chat, long userId) {
            if (chat.MembersUsers?.Any(u => u.Id == userId) == true) return true;
            if (chat.Members?.Any(m => m.MemberId == userId) == true) return true;
            return chat.ReceivedMessages?.Any(m => m.SenderId == userId) == true;
        }

        private async Task<List<long>> GetIdListFromRawMethodAsync(string method, Dictionary<string, string> parameters) {
            using JsonDocument document = await session.API.CallMethodAsync(method, parameters);
            JsonElement response = GetRawResponseOrThrow(document, method);
            return ReadIdList(response);
        }

        private async Task<List<string>> GetGroupNamesAsync(List<long> ids) {
            List<string> names = new List<string>();
            if (ids.Count == 0) return names;

            try {
                using JsonDocument document = await session.API.CallMethodAsync("groups.getById", new Dictionary<string, string> {
                    { "group_ids", String.Join(",", ids) },
                    { "fields", "activity" }
                });
                JsonElement response = GetRawResponseOrThrow(document, "groups.getById");
                JsonElement groups = response;
                if (response.ValueKind == JsonValueKind.Object) {
                    if (!response.TryGetProperty("groups", out groups) && !response.TryGetProperty("items", out groups)) return names;
                }

                if (groups.ValueKind != JsonValueKind.Array) return names;

                foreach (JsonElement group in groups.EnumerateArray()) {
                    if (group.TryGetProperty("id", out JsonElement idElement) && TryReadId(idElement, out long groupId)) {
                        string groupName = null;
                        if (group.TryGetProperty("name", out JsonElement nameElement) && nameElement.ValueKind == JsonValueKind.String) {
                            groupName = nameElement.GetString();
                            if (!String.IsNullOrWhiteSpace(groupName)) names.Add(groupName.Trim());
                        }

                        if (group.TryGetProperty("screen_name", out JsonElement screenNameElement)) {
                            CacheGroupNameFromRaw(groupId, groupName, screenNameElement.GetString());
                        }
                    }
                }
            } catch (Exception ex) {
                Log.Debug(ex, "Unable to load mutual group names.");
            }

            if (names.Count == 0) {
                foreach (long id in ids) {
                    Group group = CacheManager.GetGroup(id);
                    if (!String.IsNullOrWhiteSpace(group?.Name)) names.Add(group.Name.Trim());
                }
            }

            return names;
        }

        private static JsonElement GetRawResponseOrThrow(JsonDocument document, string method) {
            JsonElement root = document.RootElement;
            if (root.TryGetProperty("error", out JsonElement error)) {
                int code = error.TryGetProperty("error_code", out JsonElement codeElement) && codeElement.TryGetInt32(out int errorCode) ? errorCode : 0;
                string message = error.TryGetProperty("error_msg", out JsonElement messageElement) ? messageElement.GetString() : "VK API error";
                throw new APIException { Code = code, ErrorMessage = $"{method}: {message}" };
            }

            if (!root.TryGetProperty("response", out JsonElement response)) throw new InvalidOperationException($"Invalid response from {method}.");
            return response;
        }

        private static bool ReadBoolLike(JsonElement element, string propertyName) {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out JsonElement value)) return false;

            return value.ValueKind switch {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => value.TryGetInt32(out int number) && number != 0,
                JsonValueKind.String => Boolean.TryParse(value.GetString(), out bool parsed) ? parsed : value.GetString() == "1",
                _ => false
            };
        }

        private static string ReadStringOrNumber(JsonElement element, string propertyName) {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out JsonElement value)) return null;

            return value.ValueKind switch {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "1",
                JsonValueKind.False => "0",
                _ => null
            };
        }

        private static string FormatNameRequest(JsonElement response) {
            if (response.ValueKind != JsonValueKind.Object || !response.TryGetProperty("name_request", out JsonElement request) || request.ValueKind != JsonValueKind.Object) return null;

            List<string> parts = new List<string>();
            AddIfPresent("статус", "status");
            AddIfPresent("имя", "first_name");
            AddIfPresent("фамилия", "last_name");
            return parts.Count == 0 ? "ожидает модерации" : String.Join("; ", parts);

            void AddIfPresent(string title, string propertyName) {
                string value = ReadStringOrNumber(request, propertyName);
                if (!String.IsNullOrWhiteSpace(value)) parts.Add($"{title}: {value}");
            }
        }

        private static List<long> ReadIdList(JsonElement element) {
            List<long> ids = new List<long>();

            if (element.ValueKind == JsonValueKind.Object) {
                if (element.TryGetProperty("items", out JsonElement items)) {
                    element = items;
                } else if (element.TryGetProperty("groups", out JsonElement groups)) {
                    element = groups;
                }
            }

            if (element.ValueKind != JsonValueKind.Array) return ids;

            foreach (JsonElement item in element.EnumerateArray()) {
                if (TryReadId(item, out long id)) {
                    ids.Add(id);
                } else if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("id", out JsonElement idElement) && TryReadId(idElement, out id)) {
                    ids.Add(id);
                }
            }

            return ids.Distinct().ToList();
        }

        private static bool TryReadId(JsonElement element, out long id) {
            id = 0;
            if (element.ValueKind == JsonValueKind.Number) return element.TryGetInt64(out id);
            if (element.ValueKind == JsonValueKind.String) return Int64.TryParse(element.GetString(), out id);
            return false;
        }

        private static string FormatCommonItems(string title, int total, List<string> names) {
            if (total <= 0) return $"{title}: нет";

            string text = $"{title}: {total}";
            if (names.Count > 0) {
                text = $"{text}; {String.Join(", ", names)}";
                if (total > names.Count) text = $"{text} и ещё {total - names.Count}";
            }
            return text;
        }

        private void AddInformationIfCurrent(long userId, string icon, string text) {
            if (Id != userId || String.IsNullOrWhiteSpace(text)) return;

            Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                if (Id == userId) Information.Add(new TwoStringTuple(icon, text));
            });
        }

        private static void CacheGroupNameFromRaw(long id, string name, string screenName) {
            if (String.IsNullOrWhiteSpace(name)) return;

            CacheManager.Add(new Group {
                Id = id,
                Name = name,
                ScreenName = screenName
            });
        }

        private void SetupCommands(UserEx user) {
            FirstCommand = null;
            SecondCommand = null;
            ThirdCommand = null;
            MoreCommand = null;
            List<Command> commands = new List<Command>();
            List<Command> moreCommands = new List<Command>();

            // Если нет истории сообщений с этим юзером,
            // и ему нельзя писать сообщение,
            // или если открыт чат с этим юзером,
            // то не будем добавлять эту кнопку
            if ((user.CanWritePrivateMessage == 1 || user.MessagesCount > 0) && session.CurrentOpenedChat?.PeerId != user.Id) {
                Command messageCmd = new Command(VKIconNames.Icon20MessageOutline, Assets.i18n.Resources.message, false, (a) => {
                    CloseWindowRequested?.Invoke(this, null);
                    session.GoToChat(user.Id);
                });
                commands.Add(messageCmd);
            }

            if (session.UserId != user.Id && user.Deactivated == DeactivationState.No && (user.CanWritePrivateMessage == 1 || user.MessagesCount > 0)) {
                Command callCmd = new Command(VKIconNames.Icon24Phone, Assets.i18n.Resources.call, false, async (a) => await OpenVkCallAsync(GetUserCallUrl(user.Id)));
                commands.Add(callCmd);
            }

            // Friend
            if (session.UserId != user.Id && user.Blacklisted == 0 && user.BlacklistedByMe == 0
                && user.Deactivated == DeactivationState.No && user.CanSendFriendRequest == 1) {
                string ficon = VKIconNames.Icon20ServicesOutline;
                string flabel = "";

                switch (user.FriendStatus) {
                    case FriendStatus.None:
                        flabel = Assets.i18n.Resources.pp_friend_add;
                        ficon = VKIconNames.Icon20UserAddOutline;
                        break;
                    case FriendStatus.IsFriend:
                        flabel = Assets.i18n.Resources.pp_friend_your;
                        ficon = VKIconNames.Icon20UserCheckOutline;
                        break;
                    case FriendStatus.InboundRequest:
                        flabel = Assets.i18n.Resources.pp_friend_accept;
                        ficon = VKIconNames.Icon20UserAddOutline;
                        break;
                    case FriendStatus.RequestSent:
                        flabel = Assets.i18n.Resources.pp_friend_request;
                        ficon = VKIconNames.Icon20UserOutline;
                        break;
                }

                Command friendCmd = new Command(ficon, flabel, false, (a) => ExceptionHelper.ShowNotImplementedDialog(session.ModalWindow));
                commands.Add(friendCmd);
            }

            // Notifications
            if (session.UserId != user.Id) {
                string notifIcon = user.NotificationsDisabled ? VKIconNames.Icon20NotificationSlashOutline : VKIconNames.Icon20NotificationOutline;
                Command notifsCmd = new Command(notifIcon, user.NotificationsDisabled ? Assets.i18n.Resources.disabled : Assets.i18n.Resources.enabled, false, async (a) => await ToggleNotificationsAsync(!user.NotificationsDisabled, user.Id));
                commands.Add(notifsCmd);
            }

            // Open in browser
            string profileUrl = GetUserProfileUrl(user);
            Command openExternalCmd = new Command(VKIconNames.Icon20LinkCircleOutline, Assets.i18n.Resources.pp_profile, false, async (a) => await Launcher.LaunchUrl(profileUrl));
            commands.Add(openExternalCmd);

            AddCopyCommands(moreCommands, profileUrl, user.Id.ToString());
            moreCommands.Add(new Command(VKIconNames.Icon20WriteOutline, "К заметке", false, (a) => FocusLocalNoteRequested?.Invoke(this, EventArgs.Empty)));
            AddLocalSenderModerationCommands(moreCommands, user.Id);

            // Ban/unban
            if (session.UserId != user.Id && user.Blacklisted == 0) {
                string banIcon = user.BlacklistedByMe == 1 ? VKIconNames.Icon20UnlockOutline : VKIconNames.Icon20BlockOutline;
                string banLabel = user.BlacklistedByMe == 1 ? Assets.i18n.Resources.unblock : Assets.i18n.Resources.block;
                Command banCmd = new Command(banIcon, banLabel, true, async (a) => await ToggleBanAsync(user.Id, user.BlacklistedByMe == 1));
                moreCommands.Add(banCmd);
            }

            // Clear history
            if (user.MessagesCount > 0) {
                Command clearCmd = new Command(VKIconNames.Icon20DeleteOutline, Assets.i18n.Resources.chat_clear_history, true, (a) => ContextMenuHelper.TryClearChat(session, Id, async () => await SetupAsync()));
                moreCommands.Add(clearCmd);
            }

            Command moreCommand = new Command(VKIconNames.Icon20More, Assets.i18n.Resources.more, false, (a) => OpenContextMenu(a, commands, moreCommands));

            FirstCommand = commands[0];

            if (commands.Count < 2) {
                SecondCommand = moreCommand;
            } else if (commands.Count < 3) {
                SecondCommand = commands[1];
                ThirdCommand = moreCommand;
            } else {
                SecondCommand = commands[1];
                ThirdCommand = commands[2];
                MoreCommand = moreCommand;
            }
        }

        private void AddLocalSenderModerationCommands(List<Command> moreCommands, long senderId) {
            long peerId = GetCurrentChatPeerForSenderAction(senderId);
            if (peerId == 0) return;

            bool muted = Settings.IsSenderMutedLocally(peerId, senderId);
            bool shadowBanned = Settings.IsSenderShadowBanned(peerId, senderId);
            moreCommands.Add(new Command(
                muted ? VKIconNames.Icon20NotificationOutline : VKIconNames.Icon20NotificationSlashOutline,
                muted ? "Снять mute в текущем чате" : "Mute в текущем чате",
                false,
                (a) => ToggleSenderMuteInCurrentChat(peerId, senderId, muted)));
            moreCommands.Add(new Command(
                shadowBanned ? VKIconNames.Icon20UnlockOutline : VKIconNames.Icon20BlockOutline,
                shadowBanned ? "Снять shadow-ban в текущем чате" : "Shadow-ban в текущем чате",
                !shadowBanned,
                (a) => ToggleSenderShadowBanInCurrentChat(peerId, senderId, shadowBanned)));
        }

        private long GetCurrentChatPeerForSenderAction(long senderId) {
            ChatViewModel chat = session.CurrentOpenedChat;
            if (chat == null || senderId == session.Id || !chat.PeerId.IsChat()) return 0;
            return chat.PeerId;
        }

        private void ToggleSenderMuteInCurrentChat(long peerId, long senderId, bool currentlyMuted) {
            if (currentlyMuted) {
                Settings.UnmuteSenderLocally(peerId, senderId);
                session.ShowNotification(new Notification("Локальный mute", "Автор снова может присылать уведомления в этом чате.", NotificationType.Success));
            } else {
                if (session.CurrentOpenedChat?.PeerId == peerId) {
                    session.CurrentOpenedChat.MuteSenderLocally(senderId);
                } else {
                    Settings.MuteSenderLocally(peerId, senderId);
                }
                session.ShowNotification(new Notification("Локальный mute", "Уведомления автора отключены в текущем чате.", NotificationType.Success));
            }
        }

        private void ToggleSenderShadowBanInCurrentChat(long peerId, long senderId, bool currentlyShadowBanned) {
            if (currentlyShadowBanned) {
                Settings.UnshadowBanSenderLocally(peerId, senderId);
                ReloadCurrentChatAfterSenderModeration(peerId);
                session.ShowNotification(new Notification("Теневой бан", "Автор снова виден в текущем чате.", NotificationType.Success));
                return;
            }

            int removed = 0;
            if (session.CurrentOpenedChat?.PeerId == peerId) {
                removed = session.CurrentOpenedChat.ShadowBanSenderLocally(senderId);
            } else {
                Settings.ShadowBanSenderLocally(peerId, senderId);
            }
            session.ShowNotification(new Notification("Теневой бан", $"Автор скрыт локально. Убрано сообщений: {removed}.", NotificationType.Success));
        }

        private void ReloadCurrentChatAfterSenderModeration(long peerId) {
            if (session.CurrentOpenedChat?.PeerId != peerId) return;
            new System.Action(async () => await session.CurrentOpenedChat.ReloadMessagesAsync())();
        }

        private async Task ToggleBanAsync(long userId, bool unban) {
            IsLoading = true;
            try {
                bool result = unban ?
                await session.API.Account.UnbanAsync(userId) :
                await session.API.Account.BanAsync(userId);
                IsLoading = false;
                await SetupAsync(); // TODO: обновить кнопку, а не всё окно.
            } catch (Exception ex) {
                Log.Error(ex, $"Error in PeerProfileViewModel.ToggleBan!");
                IsLoading = false;
                await ExceptionHelper.ShowErrorDialogAsync(session.ModalWindow, ex);
            }
        }

        #endregion

        #region Group-specific

        private async Task GetGroupAsync(long groupId) {
            if (IsLoading) return;
            IsLoading = true;
            Placeholder = null;
            try {
                GroupEx group = await session.API.GetGroupCardAsync(groupId, VKAPIHelper.GroupFields);
                Header = group.Name;
                if (group.Photo != null) Avatar = group.Photo;
                Subhead = group.Activity;
                _lastCmid = group.LastCMID;
                SetupInfo(group);
                SetupCommands(group);
            } catch (Exception ex) {
                Log.Error(ex, $"Error in PeerProfileViewModel.GetGroup!");
                Header = null; // чтобы содержимое окна было скрыто
                Placeholder = PlaceholderViewModel.GetForException(ex, async (o) => await GetGroupAsync(groupId));
            }
            IsLoading = false;
        }

        private void SetupInfo(GroupEx group) {
            Information.Clear();

            Information.Add(new TwoStringTuple(VKIconNames.Icon20BugOutline, group.Id.ToString()));

            // Domain
            Information.Add(new TwoStringTuple(VKIconNames.Icon20MentionOutline, GetGroupSlug(group)));

            // Status
            if (!String.IsNullOrEmpty(group.Status))
                Information.Add(new TwoStringTuple(VKIconNames.Icon20ArticleOutline, group.Status.Trim()));

            // City
            string cc = null;
            if (group.City != null) cc = group.City.Title.Trim();
            if (group.Country != null) cc += !String.IsNullOrEmpty(cc) ? $", {group.Country.Title.Trim()}" : group.Country.Title.Trim();
            if (!String.IsNullOrEmpty(cc))
                Information.Add(new TwoStringTuple(VKIconNames.Icon20HomeOutline, cc));

            // Site
            if (!String.IsNullOrWhiteSpace(group.Site))
                Information.Add(new TwoStringTuple(VKIconNames.Icon20LinkCircleOutline, group.Site.Trim()));

            // Members
            if (group.Members > 0)
                Information.Add(new TwoStringTuple(VKIconNames.Icon20FollowersOutline, Localizer.GetDeclensionFormatted(group.Members, "members_sub")));
        }

        private void SetupCommands(GroupEx group) {
            FirstCommand = null;
            SecondCommand = null;
            ThirdCommand = null;
            MoreCommand = null;
            List<Command> commands = new List<Command>();
            List<Command> moreCommands = new List<Command>();

            if ((group.CanMessage == 1 || group.MessagesCount > 0) && session.CurrentOpenedChat?.PeerId != -group.Id) {
                Command messageCmd = new Command(VKIconNames.Icon20MessageOutline, Assets.i18n.Resources.message, false, (a) => {
                    CloseWindowRequested?.Invoke(this, null);
                    session.GoToChat(-group.Id);
                });
                commands.Add(messageCmd);
            }

            // Notifications
            string notifIcon = group.NotificationsDisabled ? VKIconNames.Icon20NotificationSlashOutline : VKIconNames.Icon20NotificationOutline;
            Command notifsCmd = new Command(notifIcon, group.NotificationsDisabled ? Assets.i18n.Resources.disabled : Assets.i18n.Resources.enabled, false, async (a) => await ToggleNotificationsAsync(!group.NotificationsDisabled, -group.Id));
            commands.Add(notifsCmd);

            // Open in browser
            string profileUrl = GetGroupProfileUrl(group);
            Command openExternalCmd = new Command(VKIconNames.Icon20LinkCircleOutline, Assets.i18n.Resources.pp_group, false, async (a) => await Launcher.LaunchUrl(profileUrl));
            commands.Add(openExternalCmd);

            AddCopyCommands(moreCommands, profileUrl, (-group.Id).ToString());

            // Allow/deny messages from group
            string banIcon = group.MessagesAllowed == 1 ? VKIconNames.Icon20BlockOutline : VKIconNames.Icon20Check;
            string banLabel = group.MessagesAllowed == 1 ? Assets.i18n.Resources.pp_deny : Assets.i18n.Resources.pp_allow;
            Command banCmd = new Command(banIcon, banLabel, group.MessagesAllowed == 1, async (a) => await ToggleMessagesFromGroupAsync(group.Id, group.MessagesAllowed == 1));
            moreCommands.Add(banCmd);

            // Clear history
            if (group.MessagesCount > 0) {
                Command clearCmd = new Command(VKIconNames.Icon20DeleteOutline, Assets.i18n.Resources.chat_clear_history, true, (a) => ContextMenuHelper.TryClearChat(session, Id, async () => await SetupAsync()));
                moreCommands.Add(clearCmd);
            }

            Command moreCommand = new Command(VKIconNames.Icon20More, Assets.i18n.Resources.more, false, (a) => OpenContextMenu(a, commands, moreCommands));

            FirstCommand = commands[0];

            if (commands.Count < 2) {
                SecondCommand = moreCommand;
            } else if (commands.Count < 3) {
                SecondCommand = commands[1];
                ThirdCommand = moreCommand;
            } else {
                SecondCommand = commands[1];
                ThirdCommand = commands[2];
                MoreCommand = moreCommand;
            }
        }

        private async Task ToggleMessagesFromGroupAsync(long groupId, bool allowed) {
            IsLoading = true;
            try {
                bool result = allowed ?
                    await session.API.Messages.DenyMessagesFromGroupAsync(groupId) :
                    await session.API.Messages.AllowMessagesFromGroupAsync(groupId);
                IsLoading = false;
                await SetupAsync(); // TODO: обновить кнопку, а не всё окно.
            } catch (Exception ex) {
                Log.Error(ex, $"Error in PeerProfileViewModel.ToggleMessageFromGroup!");
                IsLoading = false;
                await ExceptionHelper.ShowErrorDialogAsync(session.ModalWindow, ex);
            }
        }

        #endregion

        #region Chat-specific

        private async Task GetChatAsync(long peerId) {
            if (IsLoading) return;
            IsLoading = true;
            Placeholder = null;
            try {
                ChatInfoEx chat = await session.API.GetChatAsync(peerId - 2000000000, VKAPIHelper.Fields);
                _lastCmid = chat.LastCMID;
                Header = chat.Name;
                if (chat.PhotoUri != null) Avatar = chat.PhotoUri;

                if (chat.State == UserStateInChat.In) {
                    MembersCount = chat.MembersCount;
                    SetDescriprion(chat);
                    SetupChatInformation(chat);
                    SetupChatStatistics(chat);
                } else {
                    Subhead = chat.State == UserStateInChat.Left ? Assets.i18n.Resources.chat_left : Assets.i18n.Resources.chat_kicked.ToLowerInvariant();
                    ChatStatistics.Clear();
                }

                SetupCommands(chat);
                if (!chat.IsChannel && chat.State == UserStateInChat.In) {
                    if (ChatMembers == null) ChatMembers = new ChatMembersTabViewModel(_displayedMembers, () => _allMembers);
                    IsLoading = false; // required, see PeerProfile.axaml.cs > ViewModel_PropertyChanged
                    await LoadChatMembersAsync(chat);
                }
            } catch (Exception ex) {
                Log.Error(ex, $"Error in PeerProfileViewModel.GetChatAsync!");
                Header = null; // чтобы содержимое окна было скрыто
                Placeholder = PlaceholderViewModel.GetForException(ex, async (o) => await GetChatAsync(peerId));
            } finally {
                IsLoading = false;
            }
        }

        private void SetDescriprion(ChatInfoEx chat) {
            Subhead = string.Empty;

            if (string.IsNullOrEmpty(chat.Description)) {
                StringBuilder sb = new StringBuilder();
                if (chat.IsCasperChat) sb.Append($"{Assets.i18n.Resources.casper_chat.ToLowerInvariant()}, ");
                sb.Append(Localizer.GetDeclensionFormatted(chat.MembersCount, "members_sub"));
                Subhead = sb.ToString();
            } else {
                Subhead = chat.Description;
            }
        }

        private async Task LoadChatMembersAsync(ChatInfoEx chat) {
            if (ChatMembers.IsLoading) return;
            try {
                ChatMembers.IsLoading = true;
                _allMembers.Clear();
                _displayedMembers.Clear();

                var response = await session.API.Messages.GetConversationMembersAsync(session.GroupId, Id, extended: true, fields: VKAPIHelper.Fields);
                CacheManager.Add(response.Profiles);
                CacheManager.Add(response.Groups);
                SetupMembers(chat, response.Items);
                SetupChatInformation(chat, response.Items);
                SetupChatStatistics(chat);
                _displayedMembers.AddRange(_allMembers);
            } catch (Exception ex) {
                Log.Error(ex, $"Error in PeerProfileViewModel.LoadChatMembersAsync!");
                _displayedMembers.Clear(); // вдруг краш произойдёт при парсинге участников, а часть из них уже были добавлены в список/UI, их надо удалить.
                ChatMembers.Placeholder = PlaceholderViewModel.GetForException(ex, async (o) => await LoadChatMembersAsync(chat));
            } finally {
                ChatMembers.IsLoading = false;
            }
        }

        private void SetupChatInformation(ChatInfoEx chat, List<ChatMember> members = null) {
            Information.Clear();

            Information.Add(new TwoStringTuple(VKIconNames.Icon20BugOutline, $"peer_id: {chat.PeerId}, chat_id: {chat.ChatId}"));
            Information.Add(new TwoStringTuple(VKIconNames.Icon20LinkCircleOutline, GetChatDialogUrl(chat)));
            Information.Add(new TwoStringTuple(VKIconNames.Icon20InfoCircleOutline, FormatChatKind(chat)));
            if (chat.ACL != null) Information.Add(new TwoStringTuple(VKIconNames.Icon20CheckCircleOn, FormatChatAcl(chat.ACL)));
            string permissions = FormatChatPermissions(chat.Permissions);
            if (!String.IsNullOrWhiteSpace(permissions)) Information.Add(new TwoStringTuple(VKIconNames.Icon20ServicesOutline, permissions));
            if (chat.LastCMID > 0) Information.Add(new TwoStringTuple(VKIconNames.Icon20ArticleOutline, $"Сообщений минимум: {chat.LastCMID}"));
            if (chat.OnlineCount > 0) Information.Add(new TwoStringTuple(VKIconNames.Icon20UserCheckOutline, $"Онлайн сейчас: {chat.OnlineCount}"));

            if (members == null || members.Count == 0) return;

            int usersCount = members.Count(m => m.MemberId.IsUser());
            int groupsCount = members.Count(m => m.MemberId.IsGroup());
            int adminsCount = members.Count(m => m.IsAdmin || m.MemberId == chat.OwnerId);
            int invitedCount = members.Count(m => m.InvitedBy != 0 && m.InvitedBy != m.MemberId);

            Information.Add(new TwoStringTuple(VKIconNames.Icon20FollowersOutline, $"Участники: {members.Count}; пользователи: {usersCount}; сообщества: {groupsCount}"));
            Information.Add(new TwoStringTuple(VKIconNames.Icon20Stars, $"Админы: {adminsCount}; приглашены другими: {invitedCount}"));

            string owner = GetCachedPeerName(chat.OwnerId);
            if (!String.IsNullOrWhiteSpace(owner)) Information.Add(new TwoStringTuple(VKIconNames.Icon20UserOutline, $"Создатель: {owner}"));
        }

        private static string FormatChatKind(ChatInfoEx chat) {
            List<string> values = new List<string> {
                chat.IsChannel ? "канал" : "беседа"
            };
            if (chat.IsCasperChat) values.Add("фантом-чат");
            values.Add(chat.DisableServiceMessages ? "сервисные сообщения выключены" : "сервисные сообщения включены");
            return $"Тип: {String.Join("; ", values)}";
        }

        private static string FormatChatAcl(ChatACL acl) {
            List<string> values = new List<string>();
            if (acl.CanInvite) values.Add("приглашать");
            if (acl.CanSeeInviteLink) values.Add("видеть invite-link");
            if (acl.CanChangeInviteLink) values.Add("менять invite-link");
            if (acl.CanChangeInfo) values.Add("редактировать");
            if (acl.CanChangePin) values.Add("пин");
            if (acl.CanPromoteUsers) values.Add("админы");
            if (acl.CanModerate) values.Add("модерация");
            if (acl.CanCall) values.Add("звонки");
            if (acl.CanUseMassMentions) values.Add("mass mentions");
            if (acl.CanChangeStyle) values.Add("оформление");
            if (acl.CanSendReactions) values.Add("реакции");
            if (acl.CanForwardMessages) values.Add("форварды");
            if (acl.CanDisableServiceMessages) values.Add("сервисные");
            if (acl.CanChangeOwner) values.Add("владелец");

            return values.Count == 0 ? "Мои права: только читать/писать по базовым правилам" : $"Мои права: {String.Join(", ", values)}";
        }

        private static string FormatChatPermissions(Dictionary<string, string> permissions) {
            if (permissions == null || permissions.Count == 0) return null;

            string text = String.Join(
                "; ",
                permissions
                    .OrderBy(kv => kv.Key)
                    .Take(10)
                    .Select(kv => $"{FormatPermissionName(kv.Key)} — {FormatPermissionValue(kv.Value)}"));
            return String.IsNullOrWhiteSpace(text) ? null : $"Правила чата: {text}";
        }

        private static string FormatPermissionName(string key) {
            return key switch {
                "invite" => "приглашения",
                "change_info" => "инфо",
                "change_pin" => "пин",
                "use_mass_mentions" => "mass mentions",
                "see_invite_link" => "invite-link",
                "call" => "звонки",
                "change_admins" => "админы",
                "change_style" => "оформление",
                _ => key
            };
        }

        private static string FormatPermissionValue(string value) {
            return value switch {
                "owner" => "владелец",
                "admins" => "админы",
                "members" => "участники",
                "all" => "все",
                _ => String.IsNullOrWhiteSpace(value) ? "не задано" : value
            };
        }

        private void SetupChatStatistics(ChatInfoEx chat) {
            ChatStatistics.Clear();
            ChatStatisticTiles.Clear();
            ChatStatisticMix.Clear();
            ChatStatisticAttachments.Clear();

            ChatViewModel localChat = GetLocalChatForStatistics();
            List<MessageViewModel> messages = localChat?.ReceivedMessages?
                .Where(m => m != null)
                .DistinctBy(m => m.ConversationMessageId)
                .OrderBy(m => m.ConversationMessageId)
                .ToList();

            if (messages == null || messages.Count == 0) {
                ChatStatisticTiles.Add(new ChatStatisticTile(VKIconNames.Icon20InfoCircleOutline, "Выборка", "0", chat.LastCMID > 0 ? $"В VK минимум {chat.LastCMID}" : "Нет локальных сообщений"));
                AddStatistic(VKIconNames.Icon20InfoCircleOutline, "Локальная выборка: нет загруженных сообщений. Открой чат или пролистай историю, и статистика станет жирнее.");
                if (chat.LastCMID > 0) AddStatistic(VKIconNames.Icon20ArticleOutline, $"По VK известно минимум сообщений: {chat.LastCMID}");
                AddBackgroundIndexStatistics(chat);
                RefreshChatStatisticDashboardFlags();
                return;
            }

            List<MessageViewModel> normalMessages = messages.Where(m => m.Action == null && !m.IsExpired).ToList();
            DateTime first = messages.Min(m => m.SentTime);
            DateTime last = messages.Max(m => m.SentTime);
            int attachmentMessages = normalMessages.Count(m => m.Attachments?.Count > 0);
            int textMessages = normalMessages.Count(m => !String.IsNullOrWhiteSpace(m.Text));
            int serviceMessages = messages.Count - normalMessages.Count;
            int ownMessages = normalMessages.Count(m => m.SenderId == session.Id);
            int incomingMessages = normalMessages.Count - ownMessages;
            int uniqueSenders = normalMessages.Select(m => m.SenderId).Distinct().Count();
            Dictionary<string, int> attachmentCounters = BuildAttachmentCounters(normalMessages);
            Dictionary<string, int> wordCounters = BuildWordCounters(normalMessages, true);
            Dictionary<string, int> emojiCounters = BuildEmojiCounters(normalMessages);
            Dictionary<string, int> stickerCounters = BuildStickerCounters(normalMessages);
            ProfanityStats profanityStats = BuildProfanityCounters(normalMessages);
            int totalWords = wordCounters.Values.Sum();
            int uniqueWords = wordCounters.Count;
            int totalEmoji = emojiCounters.Values.Sum();
            int totalStickers = stickerCounters.Values.Sum();
            int emojiMessages = normalMessages.Count(m => ContainsEmoji(m.Text));
            int stickerMessages = normalMessages.Count(HasStickerAttachment);

            AddStatistic(VKIconNames.Icon20InfoCircleOutline, $"Локальная выборка: {messages.Count} загружено из минимум {Math.Max(chat.LastCMID, messages.Count)}; период {first:dd.MM.yyyy HH:mm} — {last:dd.MM.yyyy HH:mm}");
            AddStatistic(VKIconNames.Icon20MessageOutline, $"Сообщения: обычных {normalMessages.Count}; с текстом {textMessages}; с вложениями {attachmentMessages}; сервисных/истекших {serviceMessages}");
            AddStatistic(VKIconNames.Icon20UserOutline, $"Участники в выборке: {uniqueSenders}; мои {ownMessages}; чужие {incomingMessages}");
            AddBackgroundIndexStatistics(chat);

            ChatStatisticTiles.Add(new ChatStatisticTile(VKIconNames.Icon20MessageOutline, "Сообщения", FormatCompactNumber(messages.Count), $"из минимум {Math.Max(chat.LastCMID, messages.Count)}"));
            ChatStatisticTiles.Add(new ChatStatisticTile(VKIconNames.Icon20UserOutline, "Участники", FormatCompactNumber(uniqueSenders), $"{ownMessages} моих / {incomingMessages} чужих"));
            ChatStatisticTiles.Add(new ChatStatisticTile(VKIconNames.Icon20PictureOutline, "Вложения", FormatCompactNumber(attachmentCounters.Values.Sum()), $"{attachmentMessages} сообщений с вложениями"));
            ChatStatisticTiles.Add(new ChatStatisticTile(VKIconNames.Icon20ArticleOutline, "Слова", FormatCompactNumber(totalWords), $"{FormatCompactNumber(uniqueWords)} уникальных"));
            ChatStatisticTiles.Add(new ChatStatisticTile(VKIconNames.Icon20SmileOutline, "Эмодзи", FormatCompactNumber(totalEmoji), $"{emojiMessages} сообщений"));
            ChatStatisticTiles.Add(new ChatStatisticTile(VKIconNames.Icon20SmileOutline, "Стикеры", FormatCompactNumber(totalStickers), $"{stickerMessages} сообщений"));
            ChatStatisticTiles.Add(new ChatStatisticTile(VKIconNames.Icon20ReportOutline, "Мат", FormatCompactNumber(profanityStats.Hits), $"{profanityStats.Messages} сообщений"));
            ChatStatisticTiles.Add(new ChatStatisticTile(VKIconNames.Icon20RecentOutline, "Период", $"{(last - first).TotalDays:0.#} дн.", $"{first:dd.MM HH:mm} - {last:dd.MM HH:mm}"));

            AddStatisticBar(ChatStatisticMix, "Текстовые", textMessages, normalMessages.Count);
            AddStatisticBar(ChatStatisticMix, "С вложениями", attachmentMessages, normalMessages.Count);
            AddStatisticBar(ChatStatisticMix, "Мои", ownMessages, normalMessages.Count);
            AddStatisticBar(ChatStatisticMix, "Чужие", incomingMessages, normalMessages.Count);
            AddStatisticBar(ChatStatisticMix, "С emoji", emojiMessages, normalMessages.Count);
            AddStatisticBar(ChatStatisticMix, "Со стикерами", stickerMessages, normalMessages.Count);
            AddStatisticBar(ChatStatisticMix, "С матом", profanityStats.Messages, normalMessages.Count);
            AddStatisticBar(ChatStatisticMix, "Сервисные/истекшие", serviceMessages, messages.Count);

            foreach (KeyValuePair<string, int> item in attachmentCounters.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).Take(8)) {
                AddStatisticBar(ChatStatisticAttachments, item.Key, item.Value, attachmentCounters.Values.Sum());
            }

            AddTopSenders(normalMessages);
            AddAttachmentStatistics(attachmentCounters);
            AddReactionStatistics(normalMessages);
            AddMentionStatistics(normalMessages);
            AddActivityStatistics(normalMessages);
            AddWordStatistics(normalMessages, wordCounters);
            AddEmojiStatistics(emojiCounters, emojiMessages);
            AddStickerStatistics(stickerCounters, stickerMessages);
            AddProfanityStatistics(profanityStats);
            AddTopDomains(normalMessages);
            RefreshChatStatisticDashboardFlags();
        }

        private void AddBackgroundIndexStatistics(ChatInfoEx chat) {
            try {
                HistoryStatisticsState state = HistoryStatisticsStore.ReadState();
                PeerHistoryStatistics peer = state?.Peers?.FirstOrDefault(p => p.PeerId == Id);
                if (peer == null || peer.IndexedMessages == 0) return;

                long estimate = Math.Max(Math.Max(peer.TotalMessagesEstimate, chat.LastCMID), peer.IndexedMessages);
                string status = peer.IsComplete ? "полный" : "частичный";
                ChatStatisticTiles.Add(new ChatStatisticTile(VKIconNames.Icon20PollOutline, "Индекс", FormatCompactNumber(peer.IndexedMessages), $"{status}; из {FormatCompactNumber(estimate)}"));
                AddStatistic(VKIconNames.Icon20PollOutline, $"Фоновый индекс истории: {status}; сообщений {peer.IndexedMessages} из примерно {estimate}; текстовых {peer.TextMessages}; сервисных {peer.ServiceMessages}; вложений {peer.Attachments}; реакций {peer.Reactions}");
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot read chat background history statistics.");
            }
        }

        private ChatViewModel GetLocalChatForStatistics() {
            if (session.CurrentOpenedChat?.PeerId == Id) return session.CurrentOpenedChat;

            ChatViewModel cached = CacheManager.GetChat(session.Id, Id);
            if (cached != null) return cached;

            return session.ImViewModel?.SortedChats?.FirstOrDefault(c => c.PeerId == Id);
        }

        private void AddTopSenders(List<MessageViewModel> messages) {
            if (messages.Count == 0) return;

            string top = FormatTop(
                messages.GroupBy(m => m.SenderId)
                    .Select(g => new KeyValuePair<long, int>(g.Key, g.Count()))
                    .OrderByDescending(kv => kv.Value)
                    .ThenBy(kv => kv.Key),
                kv => $"{GetCachedPeerName(kv.Key) ?? kv.Key.ToString()} — {kv.Value} ({FormatPercent(kv.Value, messages.Count)})",
                5);

            if (!String.IsNullOrWhiteSpace(top)) AddStatistic(VKIconNames.Icon20FollowersOutline, $"Топ отправителей: {top}");
        }

        private static Dictionary<string, int> BuildAttachmentCounters(List<MessageViewModel> messages) {
            Dictionary<string, int> counters = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (MessageViewModel message in messages) {
                if (message.Attachments == null) continue;
                foreach (Attachment attachment in message.Attachments) {
                    string bucket = GetAttachmentBucket(attachment);
                    counters[bucket] = counters.TryGetValue(bucket, out int value) ? value + 1 : 1;
                }
            }

            return counters;
        }

        private static Dictionary<string, int> BuildWordCounters(List<MessageViewModel> messages, bool includeStopWords) {
            Dictionary<string, int> counters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (MessageViewModel message in messages) {
                foreach (string word in EnumerateWords(message.Text, includeStopWords)) {
                    counters[word] = counters.TryGetValue(word, out int value) ? value + 1 : 1;
                }
            }

            return counters;
        }

        private static Dictionary<string, int> BuildEmojiCounters(List<MessageViewModel> messages) {
            Dictionary<string, int> counters = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (MessageViewModel message in messages) {
                if (String.IsNullOrWhiteSpace(message.Text)) continue;
                foreach (Rune rune in message.Text.EnumerateRunes()) {
                    if (!IsEmojiRune(rune)) continue;
                    string emoji = rune.ToString();
                    counters[emoji] = counters.TryGetValue(emoji, out int value) ? value + 1 : 1;
                }
            }

            return counters;
        }

        private static Dictionary<string, int> BuildStickerCounters(List<MessageViewModel> messages) {
            Dictionary<string, int> counters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (MessageViewModel message in messages) {
                if (message.Attachments == null) continue;
                foreach (Attachment attachment in message.Attachments) {
                    string key = GetStickerKey(attachment);
                    if (String.IsNullOrWhiteSpace(key)) continue;
                    counters[key] = counters.TryGetValue(key, out int value) ? value + 1 : 1;
                }
            }

            return counters;
        }

        private static ProfanityStats BuildProfanityCounters(List<MessageViewModel> messages) {
            ProfanityStats stats = new ProfanityStats();

            foreach (MessageViewModel message in messages) {
                if (String.IsNullOrWhiteSpace(message.Text)) continue;

                string text = message.Text.ToLowerInvariant().Replace('ё', 'е');
                bool hasHit = false;
                foreach (string rawRoot in StatsProfanityRoots) {
                    string root = rawRoot.Replace('ё', 'е');
                    int hits = CountSubstring(text, root);
                    if (hits == 0) continue;

                    hasHit = true;
                    stats.Hits += hits;
                    stats.Counters[root] = stats.Counters.TryGetValue(root, out int value) ? value + hits : hits;
                }

                if (hasHit) stats.Messages++;
            }

            return stats;
        }

        private void AddAttachmentStatistics(Dictionary<string, int> counters) {
            int total = counters.Values.Sum();
            if (total == 0) {
                AddStatistic(VKIconNames.Icon20PictureOutline, "Вложения: в локальной выборке не найдены");
                return;
            }

            string top = FormatTop(
                counters.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key),
                kv => $"{kv.Key} — {kv.Value}",
                8);
            AddStatistic(VKIconNames.Icon20PictureOutline, $"Вложения: всего {total}; {top}");
        }

        private void AddReactionStatistics(List<MessageViewModel> messages) {
            Dictionary<int, int> counters = new Dictionary<int, int>();
            foreach (MessageViewModel message in messages) {
                if (message.Reactions == null) continue;
                foreach (MessageReaction reaction in message.Reactions) {
                    if (reaction == null || reaction.ReactionId == 0 || reaction.Count <= 0) continue;
                    counters[reaction.ReactionId] = counters.TryGetValue(reaction.ReactionId, out int value) ? value + reaction.Count : reaction.Count;
                }
            }

            int total = counters.Values.Sum();
            if (total == 0) {
                AddStatistic(VKIconNames.Icon20FavoriteOutline, "Реакции: в локальной выборке не найдены");
                return;
            }

            string top = FormatTop(
                counters.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key),
                kv => $"#{kv.Key} — {kv.Value}",
                6);
            AddStatistic(VKIconNames.Icon20FavoriteOutline, $"Реакции: всего {total}; {top}");
        }

        private void AddMentionStatistics(List<MessageViewModel> messages) {
            int myMentions = messages.Count(m => MentionsCurrentUser(m.Text));
            int anyMentions = messages.Count(m => HasAnyMention(m.Text));
            AddStatistic(VKIconNames.Icon20MentionOutline, $"Упоминания: меня {myMentions}; любые явные mention {anyMentions}");
        }

        private void AddActivityStatistics(List<MessageViewModel> messages) {
            if (messages.Count == 0) return;

            string days = FormatTop(
                messages.GroupBy(m => m.SentTime.Date)
                    .Select(g => new KeyValuePair<DateTime, int>(g.Key, g.Count()))
                    .OrderByDescending(kv => kv.Value)
                    .ThenByDescending(kv => kv.Key),
                kv => $"{kv.Key:dd.MM} — {kv.Value}",
                5);
            string hours = FormatTop(
                messages.GroupBy(m => m.SentTime.Hour)
                    .Select(g => new KeyValuePair<int, int>(g.Key, g.Count()))
                    .OrderByDescending(kv => kv.Value)
                    .ThenBy(kv => kv.Key),
                kv => $"{kv.Key:00}:00 — {kv.Value}",
                6);

            AddStatistic(VKIconNames.Icon20RecentOutline, $"Активность по дням: {days}");
            AddStatistic(VKIconNames.Icon16ClockOutline, $"Активность по часам: {hours}");
        }

        private void AddWordStatistics(List<MessageViewModel> messages, Dictionary<string, int> allWords) {
            int total = allWords.Values.Sum();
            int textMessages = messages.Count(m => !String.IsNullOrWhiteSpace(m.Text));
            double average = textMessages > 0 ? total / (double)textMessages : 0;

            AddStatistic(VKIconNames.Icon20ArticleOutline, $"Слова: всего {total}; уникальных {allWords.Count}; в среднем {average:0.#} на текстовое сообщение");

            Dictionary<string, int> counters = BuildWordCounters(messages, false);

            string top = FormatTop(
                counters.Where(kv => kv.Value > 1)
                    .OrderByDescending(kv => kv.Value)
                    .ThenBy(kv => kv.Key),
                kv => $"{kv.Key} — {kv.Value}",
                10);
            AddStatistic(VKIconNames.Icon20ArticleOutline, String.IsNullOrWhiteSpace(top) ? "Топ слов: мало текста для нормальной выборки" : $"Топ слов: {top}");
        }

        private void AddEmojiStatistics(Dictionary<string, int> counters, int messagesWithEmoji) {
            int total = counters.Values.Sum();
            if (total == 0) {
                AddStatistic(VKIconNames.Icon20SmileOutline, "Эмодзи: в локальной выборке не найдены");
                return;
            }

            string top = FormatTop(
                counters.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key),
                kv => $"{kv.Key} — {kv.Value}",
                12);
            AddStatistic(VKIconNames.Icon20SmileOutline, $"Эмодзи: всего {total}; сообщений {messagesWithEmoji}; топ {top}");
        }

        private void AddStickerStatistics(Dictionary<string, int> counters, int messagesWithStickers) {
            int total = counters.Values.Sum();
            if (total == 0) {
                AddStatistic(VKIconNames.Icon20SmileOutline, "Стикеры: в локальной выборке не найдены");
                return;
            }

            string top = FormatTop(
                counters.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key),
                kv => $"{kv.Key} — {kv.Value}",
                8);
            AddStatistic(VKIconNames.Icon20SmileOutline, $"Стикеры: всего {total}; сообщений {messagesWithStickers}; топ {top}");
        }

        private void AddProfanityStatistics(ProfanityStats stats) {
            if (stats.Hits == 0) {
                AddStatistic(VKIconNames.Icon20ReportOutline, "Мат: в локальной выборке не найден");
                return;
            }

            string top = FormatTop(
                stats.Counters.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key),
                kv => $"{kv.Key} — {kv.Value}",
                8);
            AddStatistic(VKIconNames.Icon20ReportOutline, $"Мат: сообщений {stats.Messages}; совпадений {stats.Hits}; топ {top}");
        }

        private void AddTopDomains(List<MessageViewModel> messages) {
            Dictionary<string, int> counters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (MessageViewModel message in messages) {
                if (message.Attachments == null) continue;
                foreach (Attachment attachment in message.Attachments) {
                    string domain = GetLinkDomain(attachment);
                    if (String.IsNullOrWhiteSpace(domain)) continue;
                    counters[domain] = counters.TryGetValue(domain, out int value) ? value + 1 : 1;
                }
            }

            string top = FormatTop(
                counters.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key),
                kv => $"{kv.Key} — {kv.Value}",
                8);
            AddStatistic(VKIconNames.Icon20LinkCircleOutline, String.IsNullOrWhiteSpace(top) ? "Домены: link-вложений в локальной выборке нет" : $"Домены: {top}");
        }

        private void AddStatistic(string iconId, string text) {
            if (!String.IsNullOrWhiteSpace(text)) ChatStatistics.Add(new TwoStringTuple(iconId, text));
        }

        private void AddStatisticBar(ObservableCollection<ChatStatisticBar> collection, string label, int value, int total) {
            if (collection == null || total <= 0) return;
            collection.Add(new ChatStatisticBar(label, $"{value} ({FormatPercent(value, total)})", value * 100d / total));
        }

        private void RefreshChatStatisticDashboardFlags() {
            OnPropertyChanged(nameof(HasChatStatisticDashboard));
            OnPropertyChanged(nameof(HasChatStatisticMix));
            OnPropertyChanged(nameof(HasChatStatisticAttachments));
        }

        private static string FormatCompactNumber(long value) {
            if (value >= 1_000_000) return $"{Math.Round(value / 1_000_000d, 1)}M";
            if (value >= 10_000) return $"{Math.Round(value / 1_000d, 1)}K";
            return value.ToString();
        }

        private static string GetAttachmentBucket(Attachment attachment) {
            if (attachment == null) return "прочее";

            return attachment.Type switch {
                AttachmentType.Photo => "фото",
                AttachmentType.Video => "видео",
                AttachmentType.Document => "документы",
                AttachmentType.Audio => "аудио",
                AttachmentType.AudioMessage => "голосовые",
                AttachmentType.Link => "ссылки",
                AttachmentType.Sticker or AttachmentType.UGCSticker => "стикеры",
                AttachmentType.Graffiti => "граффити",
                AttachmentType.Poll => "опросы",
                AttachmentType.Wall or AttachmentType.WallReply => "записи",
                AttachmentType.Story => "истории",
                _ => attachment.Type.ToString()
            };
        }

        private static string GetLinkDomain(Attachment attachment) {
            string url = attachment?.Type == AttachmentType.Link ? attachment.Link?.Url : null;
            if (String.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out Uri uri)) return null;

            string host = uri.Host?.ToLowerInvariant();
            if (String.IsNullOrWhiteSpace(host)) return null;
            return host.StartsWith("www.") ? host.Substring(4) : host;
        }

        private static bool HasStickerAttachment(MessageViewModel message) {
            return message?.Attachments != null && message.Attachments.Any(a => a?.Type == AttachmentType.Sticker || a?.Type == AttachmentType.UGCSticker);
        }

        private static string GetStickerKey(Attachment attachment) {
            if (attachment == null) return null;

            return attachment.Type switch {
                AttachmentType.Sticker when attachment.Sticker?.StickerId > 0 => $"VK #{attachment.Sticker.StickerId}",
                AttachmentType.Sticker => "VK sticker",
                AttachmentType.UGCSticker when attachment.UGCSticker?.Id > 0 => $"UGC #{attachment.UGCSticker.Id}",
                AttachmentType.UGCSticker => "UGC sticker",
                _ => null
            };
        }

        private bool MentionsCurrentUser(string text) {
            if (String.IsNullOrWhiteSpace(text) || session.UserId <= 0) return false;

            string userId = session.UserId.ToString();
            return text.IndexOf($"[id{userId}|", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf($"@id{userId}", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf($"club{userId}", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool HasAnyMention(string text) {
            return !String.IsNullOrWhiteSpace(text)
                && (text.IndexOf("[id", StringComparison.OrdinalIgnoreCase) >= 0
                    || text.IndexOf("[club", StringComparison.OrdinalIgnoreCase) >= 0
                    || text.IndexOf('@') >= 0);
        }

        private static bool ContainsEmoji(string text) {
            if (String.IsNullOrWhiteSpace(text)) return false;
            return text.EnumerateRunes().Any(IsEmojiRune);
        }

        private static bool IsEmojiRune(Rune rune) {
            int value = rune.Value;
            if (value >= 0x1F000 && value <= 0x1FAFF) return true;
            if (value >= 0x2600 && value <= 0x27BF) return true;

            UnicodeCategory category = Rune.GetUnicodeCategory(rune);
            return category == UnicodeCategory.OtherSymbol && value > 0x7F;
        }

        private static IEnumerable<string> EnumerateWords(string text, bool includeStopWords = false) {
            if (String.IsNullOrWhiteSpace(text)) yield break;

            StringBuilder word = new StringBuilder(24);
            foreach (char ch in text) {
                if (Char.IsLetterOrDigit(ch)) {
                    word.Append(Char.ToLowerInvariant(ch));
                    continue;
                }

                string current = FlushWord(word, includeStopWords);
                if (current != null) yield return current;
            }

            string last = FlushWord(word, includeStopWords);
            if (last != null) yield return last;
        }

        private static string FlushWord(StringBuilder word, bool includeStopWords) {
            if (word.Length == 0) return null;

            string value = word.ToString();
            word.Clear();
            if (includeStopWords) return value;
            if (value.Length < 4 || StatsStopWords.Contains(value)) return null;
            return value;
        }

        private static int CountSubstring(string text, string value) {
            if (String.IsNullOrWhiteSpace(text) || String.IsNullOrWhiteSpace(value)) return 0;

            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(value, index, StringComparison.OrdinalIgnoreCase)) >= 0) {
                count++;
                index += value.Length;
            }

            return count;
        }

        private static string FormatTop<T>(IEnumerable<T> items, Func<T, string> formatter, int limit) {
            return String.Join("; ", items.Take(limit).Select(formatter));
        }

        private static string FormatPercent(int value, int total) {
            if (total <= 0) return "0%";
            return $"{Math.Round(value * 100d / total, 1)}%";
        }

        private string GetCachedPeerName(long id) {
            if (id.IsUser()) {
                User user = CacheManager.GetUser(id);
                return user?.FullName;
            }

            if (id.IsGroup()) {
                Group group = CacheManager.GetGroup(id);
                return group?.Name;
            }

            return null;
        }

        private void SetupMembers(ChatInfoEx chat, List<ChatMember> members) {
            if (members == null || members.Count == 0) return;
            foreach (var member in CollectionsMarshal.AsSpan(members)) {
                string name = member.MemberId.ToString();
                string desc = String.Empty;
                long mid = member.MemberId;
                long iid = member.InvitedBy;
                Uri avatar = null;

                string joinDate = member.JoinDate.ToHumanizedTimeOrDateString();

                if (mid != iid) {
                    string invitedBy = String.Empty;
                    if (iid.IsUser()) {
                        var user = CacheManager.GetUser(iid);
                        if (user != null) {
                            invitedBy = Localizer.GetFormatted(user.Sex, "invited_by", user.NameWithFirstLetterSurname());
                        }
                    } else if (iid.IsGroup()) {
                        var group = CacheManager.GetGroup(iid);
                        if (group != null) {
                            invitedBy = Localizer.GetFormatted(Sex.Male, "invited_by", group.Name);
                        }
                    }
                    if (member.IsAdmin) desc = $"{Assets.i18n.Resources.admin}, ";
                    desc += $"{invitedBy} {joinDate}";
                } else if (mid == chat.OwnerId) {
                    desc = Localizer.Get("created_on", Sex.Male);
                }

                if (mid.IsUser()) {
                    var user = CacheManager.GetUser(member.MemberId);
                    if (user != null) {
                        name = user.FullName;
                        avatar = user.Photo;
                    }
                } else if (mid.IsGroup()) {
                    var group = CacheManager.GetGroup(member.MemberId);
                    if (group != null) {
                        name = group.Name;
                        avatar = group.Photo;
                    }
                }

                Command command = SetUpMemberCommand(chat, member);
                _allMembers.Add(new Entity(mid, avatar, name, desc, command));
            }
        }

        private Command SetUpMemberCommand(ChatInfoEx chat, ChatMember member) {
            ActionSheet ash = new ActionSheet {
                Placement = PlacementMode.BottomEdgeAlignedRight
            };

            var profile = new ActionSheetItem {
                Header = Assets.i18n.Resources.open_profile
            };
            profile.Click += (a, b) => {
                // TODO: открывать профиль в текущем окне PeerProfile с возможностью вернуться назад.
                CloseWindowRequested?.Invoke(this, null);
                new System.Action(async () => await Router.OpenPeerProfileAsync(session, member.MemberId))();
            };
            ash.Items.Add(profile);

            // TODO: админы (не создатель), которые тоже имеют права менять админов.
            bool canChangeAdmin = chat.OwnerId == session.Id;

            if (member.MemberId != session.Id && canChangeAdmin && !member.IsAdmin) ash.Items.Add(new ActionSheetItem {
                Header = Assets.i18n.Resources.pp_member_admin_add
            });

            if (member.MemberId != session.Id && canChangeAdmin && member.IsAdmin) ash.Items.Add(new ActionSheetItem {
                Header = Assets.i18n.Resources.pp_member_admin_remove
            });

            if (member.MemberId != session.Id && member.CanKick) ash.Items.Add(new ActionSheetItem {
                Header = Assets.i18n.Resources.pp_member_kick
            });

            if (ash.Items.Count > 0) {
                return new Command(VKIconNames.Icon24MoreHorizontal, Assets.i18n.Resources.more, false, (c) => {
                    ash.ShowAt(c as Control);
                });
            }

            return null;
        }

        private void SetupCommands(ChatInfoEx chat) {
            FirstCommand = null;
            SecondCommand = null;
            ThirdCommand = null;
            MoreCommand = null;
            List<Command> commands = new List<Command>();
            List<Command> moreCommands = new List<Command>();

            // Edit
            if (chat.ACL.CanChangeInfo) {
                var act = new Action<object>(async (o) => {
                    ChatEditor modal = new ChatEditor(session, chat.ChatId, chat.Name, chat.Description, chat.Photo, chat.Permissions, chat.ACL, chat.DisableServiceMessages);
                    var result = await modal.ShowDialog<ChatEditorResult>(session.ModalWindow);

                    // фото меняется сразу, а не при нажатии на кнопку "save" в ChatEditor, 
                    // и это приводит к тому, что закрытие CE крестиком не обновит фото в PeerProfile,
                    // т. е. не прилетит в result. Поэтому у CE есть свойство Photo, где хранится актуальное фото
                    chat.Photo = modal.Photo;

                    if (result != null) {
                        chat.Name = result.Name;
                        chat.Description = result.Description;
                        chat.Permissions = result.Permissions;
                        chat.ACL = result.ACL;
                        chat.DisableServiceMessages = result.ServiceMessagesDisabled;

                        Header = chat.Name;
                        Avatar = chat.PhotoUri != null ? chat.PhotoUri : null;
                        SetDescriprion(chat);
                    }
                });

                Command editCmd = new Command(VKIconNames.Icon20WriteOutline, Assets.i18n.Resources.edit, false, act);
                commands.Add(editCmd);
            }

            // Add member
            if (chat.ACL.CanInvite) {
                Command addCmd = new Command(VKIconNames.Icon20UserAddOutline, Assets.i18n.Resources.add, false, (a) => ExceptionHelper.ShowNotImplementedDialog(session.ModalWindow));
                commands.Add(addCmd);
            }

            // Notifications
            bool notifsDisabled = chat.PushSettings != null && chat.PushSettings.DisabledForever;
            string notifIcon = notifsDisabled ? VKIconNames.Icon20NotificationSlashOutline : VKIconNames.Icon20NotificationOutline;
            Command notifsCmd = new Command(notifIcon, notifsDisabled ? Assets.i18n.Resources.disabled : Assets.i18n.Resources.enabled, false, async (a) => await ToggleNotificationsAsync(!notifsDisabled, chat.PeerId));
            commands.Add(notifsCmd);

            // Link
            if (chat.ACL.CanSeeInviteLink) {
                var act = new System.Action<object>((o) => {
                    ChatLinkViewer modal = new ChatLinkViewer(session, Id);
                    modal.ShowDialog(session.ModalWindow);
                });

                Command chatLinkCmd = new Command(VKIconNames.Icon20LinkCircleOutline, Assets.i18n.Resources.link, false, act);
                commands.Add(chatLinkCmd);
            }

            if (chat.ACL.CanCall) {
                Command callCmd = new Command(VKIconNames.Icon24Phone, Assets.i18n.Resources.call, false, async (a) => await OpenVkCallAsync(GetChatCallUrl(chat)));
                commands.Add(callCmd);
            }

            // Unpin message
            if (chat.ACL.CanChangePin && chat.PinnedMessage != null) {
                Command unpinCmd = new Command(VKIconNames.Icon20PinSlashOutline, Assets.i18n.Resources.pp_unpin_message, false, async (a) => {
                    if (await ContextMenuHelper.UnpinMessageAsync(session, Id)) chat.PinnedMessage = null;
                });
                commands.Add(unpinCmd);
            }

            // Clear history
            Command clearCmd = new Command(VKIconNames.Icon20DeleteOutline, Assets.i18n.Resources.chat_clear_history, true, (a) => ContextMenuHelper.TryClearChat(session, Id, async () => await SetupAsync()));
            moreCommands.Add(clearCmd);

            AddCopyCommands(moreCommands, GetChatDialogUrl(chat), chat.PeerId.ToString());

            // Exit or return to chat/channel
            if (chat.State != UserStateInChat.Kicked) {
                string exitLabel = chat.IsChannel ? Assets.i18n.Resources.pp_exit_channel : Assets.i18n.Resources.pp_exit_chat;
                string returnLabel = chat.IsChannel ? Assets.i18n.Resources.pp_return_channel : Assets.i18n.Resources.pp_return_chat;
                string icon = chat.State == UserStateInChat.In ? VKIconNames.Icon20DoorArrowRightOutline : VKIconNames.Icon20DoorEnterArrowRightOutline;
                Command exitRetCmd = new Command(icon, chat.State == UserStateInChat.In ? exitLabel : returnLabel, true, (a) => {
                    if (chat.State == UserStateInChat.In) {
                        ContextMenuHelper.TryLeaveChat(session, Id, async () => await SetupAsync());
                    } else {
                        ContextMenuHelper.ReturnToChat(session, Id, async () => await SetupAsync());
                    }
                });
                moreCommands.Add(exitRetCmd);
            }

            Command moreCommand = new Command(VKIconNames.Icon20More, Assets.i18n.Resources.more, false, (a) => OpenContextMenu(a, commands, moreCommands));

            FirstCommand = commands[0];

            if (commands.Count < 2) {
                SecondCommand = moreCommand;
            } else if (commands.Count < 3) {
                SecondCommand = commands[1];
                ThirdCommand = moreCommand;
            } else {
                SecondCommand = commands[1];
                ThirdCommand = commands[2];
                MoreCommand = moreCommand;
            }
        }

        #endregion

        #region General commands

        private void OpenContextMenu(object target, List<Command> commands, List<Command> moreCommands) {
            ActionSheet ash = new ActionSheet();

            if (commands.Count > 3) {
                commands = commands.GetRange(3, commands.Count - 3);
                foreach (var item in CollectionsMarshal.AsSpan(commands)) {
                    ActionSheetItem asi = new ActionSheetItem {
                        Before = new VKIcon {
                            Id = item.IconId
                        },
                        Header = item.Label
                    };
                    asi.Click += (a, b) => item.Action.Execute(asi);
                    if (item.IsDestructive) asi.Classes.Add("Destructive");
                    ash.Items.Add(asi);
                }
            }

            if (ash.Items.Count > 0) ash.Items.Add(new ActionSheetItem());

            foreach (var item in CollectionsMarshal.AsSpan(moreCommands)) {
                ActionSheetItem asi = new ActionSheetItem {
                    Before = new VKIcon {
                        Id = item.IconId
                    },
                    Header = item.Label
                };
                asi.Click += (a, b) => item.Action.Execute(asi);
                if (item.IsDestructive) asi.Classes.Add("Destructive");
                ash.Items.Add(asi);
            }

            ash.ShowAt(target as Control, true);

        }

        private void AddCopyCommands(List<Command> moreCommands, string profileUrl, string peerId) {
            if (!String.IsNullOrWhiteSpace(profileUrl)) {
                moreCommands.Add(new Command(VKIconNames.Icon20ShareOutline, "Скопировать ссылку", false, async (a) => await CopyToClipboardAsync(a, profileUrl)));
            }

            if (!String.IsNullOrWhiteSpace(peerId)) {
                moreCommands.Add(new Command(VKIconNames.Icon20InfoCircleOutline, "Скопировать ID", false, async (a) => await CopyToClipboardAsync(a, peerId)));
            }
        }

        private async Task CopyToClipboardAsync(object target, string text) {
            if (String.IsNullOrWhiteSpace(text)) return;

            Control control = target as Control;
            TopLevel topLevel = control == null ? null : TopLevel.GetTopLevel(control);
            topLevel ??= TopLevel.GetTopLevel(session.ModalWindow);
            if (topLevel?.Clipboard == null) return;

            await topLevel.Clipboard.SetTextAsync(text);
            session.ShowNotification(new Notification("Laney", "Скопировано", NotificationType.Success));
        }

        private static string GetUserSlug(UserEx user) {
            return String.IsNullOrWhiteSpace(user.Domain) ? $"id{user.Id}" : user.Domain;
        }

        private static string GetUserProfileUrl(UserEx user) {
            return $"https://vk.com/{GetUserSlug(user)}";
        }

        private static string GetUserCallUrl(long userId) {
            return $"https://vk.com/im?sel={userId}&call=1";
        }

        private static string GetGroupSlug(GroupEx group) {
            return !String.IsNullOrWhiteSpace(group.ScreenName) ? group.ScreenName : $"club{group.Id}";
        }

        private static string GetGroupProfileUrl(GroupEx group) {
            return $"https://vk.com/{GetGroupSlug(group)}";
        }

        private static string GetChatDialogUrl(ChatInfoEx chat) {
            return $"https://vk.com/im?sel=c{chat.ChatId}";
        }

        private static string GetChatCallUrl(ChatInfoEx chat) {
            return $"https://vk.com/im?sel=c{chat.ChatId}&call=1";
        }

        private static async Task OpenVkCallAsync(string url) {
            if (String.IsNullOrWhiteSpace(url)) return;
            await Launcher.LaunchUrl(url);
        }

        private async Task ToggleNotificationsAsync(bool enabled, long id) {
            IsLoading = true;
            try {
                var result = await session.API.Account.SetSilenceModeAsync(!enabled ? 0 : -1, id, true);
                IsLoading = false;
                await SetupAsync(); // TODO: обновить кнопку, а не всё окно.
            } catch (Exception ex) {
                Log.Error(ex, $"Error in PeerProfileViewModel.ToggleNotifications!");
                IsLoading = false;
                await ExceptionHelper.ShowErrorDialogAsync(session.ModalWindow, ex);
            }
        }

        #endregion

        #region Conversation attachments

        public async Task LoadPhotosAsync() {
            await LoadConvAttachmentsAsync(Photos, HistoryAttachmentMediaType.Photo);
        }

        public async Task LoadVideosAsync() {
            await LoadConvAttachmentsAsync(Videos, HistoryAttachmentMediaType.Video);
        }

        public async Task LoadAudiosAsync() {
            await LoadConvAttachmentsAsync(Audios, HistoryAttachmentMediaType.Audio);
        }

        public async Task LoadDocsAsync() {
            await LoadConvAttachmentsAsync(Documents, HistoryAttachmentMediaType.Doc);
        }

        public async Task LoadLinksAsync() {
            await LoadConvAttachmentsAsync(Share, HistoryAttachmentMediaType.Share);
        }

        //public void LoadGraffities() {
        //    LoadVM(Graffities, HistoryAttachmentMediaType.Graffiti);
        //}

        //public void LoadAudioMessages() {
        //    LoadVM(AudioMessages, HistoryAttachmentMediaType.AudioMessage);
        //}

        private async Task LoadConvAttachmentsAsync(ConversationAttachmentsTabViewModel ivm, HistoryAttachmentMediaType type) {
            if (ivm.IsLoading || ivm.End) return;

            // Т. к. в пустых чатах, разумеется, нет cmid, то мы не будем отправлять запрос API,
            // а сразу отобразим плейсхолдер об отсутствующих вложениях.
            if (_lastCmid == 0) {
                ivm.Placeholder = new PlaceholderViewModel {
                    Text = Localizer.Get($"pp_attachments_{type}".ToLower()),
                    ActionButton = null
                };
                ivm.End = true;
                return;
            }

            ivm.Placeholder = null;
            ivm.IsLoading = true;
            try {
                ConversationAttachmentsResponse resp = await session.API.Messages.GetHistoryAttachmentsAsync(session.GroupId, Id, type, _lastCmid, ivm.Items.Count, Constants.AttachmentsCountPerRequest, true, fields: VKAPIHelper.Fields);
                CacheManager.Add(resp.Profiles);
                CacheManager.Add(resp.Groups);
                foreach (var item in CollectionsMarshal.AsSpan(resp.Items)) {
                    ivm.Items.Add(item);
                }
                if (resp.Items.Count < Constants.AttachmentsCountPerRequest) ivm.End = true;

                if (ivm.Items.Count == 0) {
                    ivm.Placeholder = new PlaceholderViewModel {
                        Text = Localizer.Get($"pp_attachments_{type}".ToLower()),
                        ActionButton = null
                    };
                    ivm.End = true;
                }
            } catch (Exception ex) {
                Log.Error(ex, $"Error in PeerProfileViewModel.LoadConvAttachmentsAsync!");
                if (ivm.Items.Count == 0) {
                    ivm.Placeholder = PlaceholderViewModel.GetForException(ex, async (o) => await LoadConvAttachmentsAsync(ivm, type));
                } else {
                    if (await ExceptionHelper.ShowErrorDialogAsync(session.ModalWindow, ex)) await LoadConvAttachmentsAsync(ivm, type);
                }
            }
            ivm.IsLoading = false;
        }

        #endregion
    }
}
