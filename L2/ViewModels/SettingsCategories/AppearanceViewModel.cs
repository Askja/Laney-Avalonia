using ELOR.Laney.Core;
using ELOR.Laney.DataModels;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace ELOR.Laney.ViewModels.SettingsCategories {
    public class AppearanceViewModel : CommonViewModel {
        private long _accountId;

        public ObservableCollection<Tuple<int, string>> AppThemes { get; private set; } = new ObservableCollection<Tuple<int, string>> {
            new Tuple<int, string>(0, Assets.i18n.Resources.st_theme_system),
            new Tuple<int, string>(1, Assets.i18n.Resources.st_theme_light),
            new Tuple<int, string>(2, Assets.i18n.Resources.st_theme_dark)
        };
        public ObservableCollection<AppearanceOption> AccentOptions { get; private set; } = new ObservableCollection<AppearanceOption>(AppearanceManager.AccentOptions);
        public ObservableCollection<AppearanceOption> AccountAccentOptions { get; private set; } = new ObservableCollection<AppearanceOption>(AppearanceManager.AccentOptionsWithInherit);
        public ObservableCollection<AppearanceOption> ChatBackgroundOptions { get; private set; } = new ObservableCollection<AppearanceOption>(AppearanceManager.ChatBackgroundOptions);
        public ObservableCollection<TwoStringTuple> ChatListDensityOptions { get; private set; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple(ChatListDensityIds.Small, "Компактный"),
            new TwoStringTuple(ChatListDensityIds.Medium, "Стандартный"),
            new TwoStringTuple(ChatListDensityIds.Large, "Крупный")
        };
        public ObservableCollection<TwoStringTuple> ChatListLayoutOptions { get; private set; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple(ChatListLayoutIds.Classic, "Классический"),
            new TwoStringTuple(ChatListLayoutIds.Compact, "Плотный"),
            new TwoStringTuple(ChatListLayoutIds.Telegram, "Telegram-like"),
            new TwoStringTuple(ChatListLayoutIds.MediaRich, "Media-rich"),
            new TwoStringTuple(ChatListLayoutIds.SplitFolder, "Split-folder")
        };
        public ObservableCollection<TwoStringTuple> ChatListAvatarShapeOptions { get; private set; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple(AvatarShapeIds.Circle, "Круг"),
            new TwoStringTuple(AvatarShapeIds.Squircle, "Сквиркл"),
            new TwoStringTuple(AvatarShapeIds.Rounded, "Скругленный квадрат"),
            new TwoStringTuple(AvatarShapeIds.Square, "Sharp")
        };

        public Tuple<int, string> CurrentAppTheme { get { return GetTheme(); } set { ChangeTheme(value); OnPropertyChanged(); } }
        public AppearanceOption CurrentAccent { get { return GetAccent(); } set { ChangeAccent(value); OnPropertyChanged(); } }
        public AppearanceOption CurrentAccountAccent { get { return GetAccountAccent(); } set { ChangeAccountAccent(value); OnPropertyChanged(); } }
        public bool HasAccountAccent { get { return _accountId != 0; } }
        public AppearanceOption CurrentChatBackground { get { return GetChatBackground(); } set { ChangeChatBackground(value); OnPropertyChanged(); } }
        public TwoStringTuple CurrentChatListDensity { get { return GetChatListDensity(); } set { ChangeChatListDensity(value); OnPropertyChanged(); } }
        public TwoStringTuple CurrentChatListLayout { get { return GetChatListLayout(); } set { ChangeChatListLayout(value); OnPropertyChanged(); } }
        public TwoStringTuple CurrentChatListAvatarShape { get { return GetChatListAvatarShape(); } set { ChangeChatListAvatarShape(value); OnPropertyChanged(); } }
        public bool ChatItemMoreRows { get { return Settings.ChatItemMoreRows; } set { Settings.ChatItemMoreRows = value; MarkCustomProfile(); OnPropertyChanged(); } }

        private Tuple<int, string> GetTheme() {
            return AppThemes.Where(l => l.Item1 == Settings.AppTheme).FirstOrDefault();
        }

        private void ChangeTheme(Tuple<int, string> value) {
            Settings.Set(Settings.THEME, value.Item1);
            App.ChangeTheme(value.Item1);
            OnPropertyChanged(nameof(CurrentAppTheme));
        }

        private AppearanceOption GetAccent() {
            return AccentOptions.Where(o => o.Id == Settings.AccentColor).FirstOrDefault() ?? AccentOptions[0];
        }

        private void ChangeAccent(AppearanceOption value) {
            if (value == null) return;
            Settings.AccentColor = value.Id;
            MarkCustomProfile();
            OnPropertyChanged(nameof(CurrentAccent));
        }

        public void SetAccountId(long accountId) {
            if (_accountId == accountId) return;

            _accountId = accountId;
            OnPropertyChanged(nameof(HasAccountAccent));
            OnPropertyChanged(nameof(CurrentAccountAccent));
        }

        private AppearanceOption GetAccountAccent() {
            string id = _accountId == 0 ? AppearanceManager.InheritChatBackgroundId : Settings.GetAccountAccent(_accountId);
            if (String.IsNullOrWhiteSpace(id)) id = AppearanceManager.InheritChatBackgroundId;
            return AccountAccentOptions.Where(o => o.Id == id).FirstOrDefault() ?? AccountAccentOptions[0];
        }

        private void ChangeAccountAccent(AppearanceOption value) {
            if (value == null || _accountId == 0) return;
            Settings.SetAccountAccent(_accountId, value.Id);
            OnPropertyChanged(nameof(CurrentAccountAccent));
        }

        private AppearanceOption GetChatBackground() {
            return ChatBackgroundOptions.Where(o => o.Id == Settings.ChatBackground).FirstOrDefault() ?? ChatBackgroundOptions[0];
        }

        private void ChangeChatBackground(AppearanceOption value) {
            if (value == null) return;
            Settings.ChatBackground = value.Id;
            MarkCustomProfile();
            OnPropertyChanged(nameof(CurrentChatBackground));
        }

        private TwoStringTuple GetChatListDensity() {
            return ChatListDensityOptions.Where(o => o.Item1 == Settings.ChatListDensity).FirstOrDefault() ?? ChatListDensityOptions[1];
        }

        private void ChangeChatListDensity(TwoStringTuple value) {
            if (value == null) return;
            Settings.ChatListDensity = value.Item1;
            MarkCustomProfile();
            OnPropertyChanged(nameof(CurrentChatListDensity));
        }

        private TwoStringTuple GetChatListLayout() {
            return ChatListLayoutOptions.Where(o => o.Item1 == Settings.ChatListLayout).FirstOrDefault() ?? ChatListLayoutOptions[0];
        }

        private void ChangeChatListLayout(TwoStringTuple value) {
            if (value == null) return;
            Settings.ChatListLayout = value.Item1;
            MarkCustomProfile();
            OnPropertyChanged(nameof(CurrentChatListLayout));
        }

        private TwoStringTuple GetChatListAvatarShape() {
            return ChatListAvatarShapeOptions.Where(o => o.Item1 == Settings.ChatListAvatarShape).FirstOrDefault() ?? ChatListAvatarShapeOptions[0];
        }

        private void ChangeChatListAvatarShape(TwoStringTuple value) {
            if (value == null) return;
            Settings.ChatListAvatarShape = value.Item1;
            MarkCustomProfile();
            OnPropertyChanged(nameof(CurrentChatListAvatarShape));
        }

        private void MarkCustomProfile() {
            if (Settings.InterfaceProfile != InterfaceProfileIds.Custom) Settings.InterfaceProfile = InterfaceProfileIds.Custom;
        }
    }
}
