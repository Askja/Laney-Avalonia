using ELOR.Laney.Core;
using ELOR.Laney.DataModels;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace ELOR.Laney.ViewModels.SettingsCategories {
    public sealed class AppFontFamilyOption : CommonViewModel {
        private bool _isSelected;

        public AppFontFamilyOption(string familyName, string title, string sample) {
            FamilyName = familyName;
            Family = AppearanceManager.CreateAppFontFamily(familyName);
            Title = title;
            Sample = sample;
        }

        public string FamilyName { get; }
        public FontFamily Family { get; }
        public string Title { get; }
        public string Sample { get; }
        public bool IsSelected { get { return _isSelected; } set { _isSelected = value; OnPropertyChanged(); } }
    }

    public sealed class AppIconVariantOption : CommonViewModel {
        private bool _isSelected;

        public AppIconVariantOption(string id, string title, string subtitle, string previewUri) {
            Id = id;
            Title = title;
            Subtitle = subtitle;
            PreviewUri = new Uri(previewUri);
            PreviewBitmap = LoadPreviewBitmap(PreviewUri);
        }

        public string Id { get; }
        public string Title { get; }
        public string Subtitle { get; }
        public Uri PreviewUri { get; }
        public Bitmap PreviewBitmap { get; }
        public bool IsSelected { get { return _isSelected; } set { _isSelected = value; OnPropertyChanged(); } }

        private static Bitmap LoadPreviewBitmap(Uri uri) {
            try {
                return AssetsManager.GetBitmapFromUri(uri);
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot load app icon preview {PreviewUri}", uri);
                return null;
            }
        }
    }

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
        public ObservableCollection<AppFontFamilyOption> AppFontFamilyOptions { get; private set; } = new ObservableCollection<AppFontFamilyOption> {
            new AppFontFamilyOption("Segoe UI", "Системный", "Съешь ещё этих мягких французских булок"),
            new AppFontFamilyOption("avares://VKUI/Fonts#VK Sans Text", "VK Sans", "Плотный интерфейс без каши в списках"),
            new AppFontFamilyOption("Arial", "Arial", "Классика Windows для быстрых рабочих окон"),
            new AppFontFamilyOption("Tahoma", "Tahoma", "Компактные подписи и сухие таблицы"),
            new AppFontFamilyOption("Verdana", "Verdana", "Чуть шире, зато читаемо на мелком кегле"),
            new AppFontFamilyOption("Consolas", "Consolas", "Моноширинный режим для логов и дебага"),
            new AppFontFamilyOption("Georgia", "Georgia", "Антиква для тех, кто любит странности")
        };
        public ObservableCollection<AppIconVariantOption> AppIconVariantCards { get; private set; } = new ObservableCollection<AppIconVariantOption> {
            new AppIconVariantOption(AppIconVariantIds.Auto, "Авто", "Laney под тему системы", "avares://laney/Assets/Logo/Laney.png"),
            new AppIconVariantOption(AppIconVariantIds.VkColor, "VK круг", "Icons8, новый знак", "avares://laney/Assets/Logo/vk_modern.png"),
            new AppIconVariantOption(AppIconVariantIds.VkClassic, "VK квадрат", "Icons8, классический вид", "avares://laney/Assets/Logo/vk_classic.png"),
            new AppIconVariantOption(AppIconVariantIds.VkBlue, "VK светлый", "Icons8, светлый синий", "avares://laney/Assets/Logo/vk_blue.png"),
            new AppIconVariantOption(AppIconVariantIds.VkWhite, "VK моно", "Icons8, контрастный знак", "avares://laney/Assets/Logo/vk_white.png"),
            new AppIconVariantOption(AppIconVariantIds.AnimeStar, "Ай розовая", "Локальная картинка", "avares://laney/Assets/Logo/anime_star.png"),
            new AppIconVariantOption(AppIconVariantIds.AnimeAi, "Ай Хошино", "Локальная картинка", "avares://laney/Assets/Logo/anime_ai.png"),
            new AppIconVariantOption(AppIconVariantIds.AnimeAkane, "Акане", "Локальная картинка", "avares://laney/Assets/Logo/anime_akane.png")
        };
        public ObservableCollection<TwoStringTuple> ChatListDensityOptions { get; private set; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple(ChatListDensityIds.Small, "Компактный"),
            new TwoStringTuple(ChatListDensityIds.Medium, "Стандартный"),
            new TwoStringTuple(ChatListDensityIds.Large, "Крупный")
        };
        public ObservableCollection<TwoStringTuple> ChatListLayoutOptions { get; private set; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple(ChatListLayoutIds.Classic, "Классический"),
            new TwoStringTuple(ChatListLayoutIds.Compact, "Плотный"),
            new TwoStringTuple(ChatListLayoutIds.Telegram, "В стиле Telegram"),
            new TwoStringTuple(ChatListLayoutIds.MediaRich, "Медиа"),
            new TwoStringTuple(ChatListLayoutIds.SplitFolder, "Папки и теги")
        };
        public ObservableCollection<TwoStringTuple> ChatListAvatarShapeOptions { get; private set; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple(AvatarShapeIds.Circle, "Круг"),
            new TwoStringTuple(AvatarShapeIds.Squircle, "Сквиркл"),
            new TwoStringTuple(AvatarShapeIds.Rounded, "Скругленный квадрат"),
            new TwoStringTuple(AvatarShapeIds.Square, "Без скругления")
        };

        public Tuple<int, string> CurrentAppTheme { get { return GetTheme(); } set { ChangeTheme(value); OnPropertyChanged(); } }
        public AppearanceOption CurrentAccent { get { return GetAccent(); } set { ChangeAccent(value); OnPropertyChanged(); } }
        public AppearanceOption CurrentAccountAccent { get { return GetAccountAccent(); } set { ChangeAccountAccent(value); OnPropertyChanged(); } }
        public bool HasAccountAccent { get { return _accountId != 0; } }
        public AppearanceOption CurrentChatBackground { get { return GetChatBackground(); } set { ChangeChatBackground(value); OnPropertyChanged(); } }
        public AppFontFamilyOption CurrentAppFontFamily { get { return GetAppFontFamily(); } set { ChangeAppFontFamily(value); OnPropertyChanged(); } }
        public AppIconVariantOption CurrentAppIconVariant { get { return GetAppIconVariant(); } set { ChangeAppIconVariant(value); OnPropertyChanged(); } }
        public bool ShowAccountRail { get { return Settings.ShowAccountRail; } set { Settings.ShowAccountRail = value; MarkCustomProfile(); OnPropertyChanged(); } }
        public TwoStringTuple CurrentChatListDensity { get { return GetChatListDensity(); } set { ChangeChatListDensity(value); OnPropertyChanged(); } }
        public TwoStringTuple CurrentChatListLayout { get { return GetChatListLayout(); } set { ChangeChatListLayout(value); OnPropertyChanged(); } }
        public TwoStringTuple CurrentChatListAvatarShape { get { return GetChatListAvatarShape(); } set { ChangeChatListAvatarShape(value); OnPropertyChanged(); } }
        public bool ChatItemMoreRows { get { return Settings.ChatItemMoreRows; } set { Settings.ChatItemMoreRows = value; MarkCustomProfile(); OnPropertyChanged(); } }

        public AppearanceViewModel() {
            RefreshFontSelection();
            RefreshIconSelection();
        }

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

        private AppFontFamilyOption GetAppFontFamily() {
            string font = Settings.AppFontFamily;
            return AppFontFamilyOptions.Where(o => String.Equals(o.FamilyName, font, StringComparison.OrdinalIgnoreCase)).FirstOrDefault()
                ?? AppFontFamilyOptions[0];
        }

        private void ChangeAppFontFamily(AppFontFamilyOption value) {
            if (value == null) return;
            Settings.AppFontFamily = value.FamilyName;
            MarkCustomProfile();
            RefreshFontSelection();
            OnPropertyChanged(nameof(CurrentAppFontFamily));
        }

        public void SelectAppFontFamily(AppFontFamilyOption value) {
            ChangeAppFontFamily(value);
        }

        private void RefreshFontSelection() {
            string current = Settings.AppFontFamily;
            foreach (AppFontFamilyOption option in AppFontFamilyOptions) {
                option.IsSelected = String.Equals(option.FamilyName, current, StringComparison.OrdinalIgnoreCase);
            }
        }

        private AppIconVariantOption GetAppIconVariant() {
            string icon = Settings.AppIconVariant;
            return AppIconVariantCards.Where(o => o.Id == icon).FirstOrDefault() ?? AppIconVariantCards[0];
        }

        private void ChangeAppIconVariant(AppIconVariantOption value) {
            if (value == null) return;
            Settings.AppIconVariant = value.Id;
            MarkCustomProfile();
            RefreshIconSelection();
            OnPropertyChanged(nameof(CurrentAppIconVariant));
        }

        public void SelectAppIconVariant(AppIconVariantOption value) {
            ChangeAppIconVariant(value);
        }

        private void RefreshIconSelection() {
            string current = Settings.AppIconVariant;
            foreach (AppIconVariantOption option in AppIconVariantCards) {
                option.IsSelected = option.Id == current;
            }
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
