using ELOR.Laney.Core;
using ELOR.Laney.DataModels;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace ELOR.Laney.ViewModels.SettingsCategories {
    public sealed class ChatsViewModel : CommonViewModel {
        public ObservableCollection<TwoStringTuple> ChatListDensityOptions { get; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple(ChatListDensityIds.Small, "Компактный"),
            new TwoStringTuple(ChatListDensityIds.Medium, "Стандартный"),
            new TwoStringTuple(ChatListDensityIds.Large, "Крупный")
        };
        public ObservableCollection<TwoStringTuple> ChatListLayoutOptions { get; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple(ChatListLayoutIds.Classic, "Классический"),
            new TwoStringTuple(ChatListLayoutIds.Compact, "Плотный"),
            new TwoStringTuple(ChatListLayoutIds.Telegram, "Telegram-like"),
            new TwoStringTuple(ChatListLayoutIds.MediaRich, "Media-rich"),
            new TwoStringTuple(ChatListLayoutIds.SplitFolder, "Split-folder")
        };
        public ObservableCollection<TwoStringTuple> ChatListAvatarShapeOptions { get; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple(AvatarShapeIds.Circle, "Круг"),
            new TwoStringTuple(AvatarShapeIds.Squircle, "Сквиркл"),
            new TwoStringTuple(AvatarShapeIds.Rounded, "Скругленный квадрат"),
            new TwoStringTuple(AvatarShapeIds.Square, "Sharp")
        };
        public ObservableCollection<TwoStringTuple> ChatListAvatarSizeOptions { get; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple(AvatarSizeIds.Auto, "По плотности"),
            new TwoStringTuple(AvatarSizeIds.Small, "Маленькие"),
            new TwoStringTuple(AvatarSizeIds.Medium, "Средние"),
            new TwoStringTuple(AvatarSizeIds.Large, "Крупные")
        };
        public ObservableCollection<TwoStringTuple> MessageAvatarSizeOptions { get; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple(AvatarSizeIds.Small, "Маленькие"),
            new TwoStringTuple(AvatarSizeIds.Medium, "Средние"),
            new TwoStringTuple(AvatarSizeIds.Large, "Крупные")
        };
        public ObservableCollection<TwoStringTuple> ChatListFontSizeOptions { get; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple(TextSizeIds.Auto, "По плотности"),
            new TwoStringTuple(TextSizeIds.Small, "Мелкий"),
            new TwoStringTuple(TextSizeIds.Medium, "Стандартный"),
            new TwoStringTuple(TextSizeIds.Large, "Крупный")
        };
        public ObservableCollection<TwoStringTuple> MessageFontSizeOptions { get; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple(TextSizeIds.Small, "Мелкий"),
            new TwoStringTuple(TextSizeIds.Medium, "Стандартный"),
            new TwoStringTuple(TextSizeIds.Large, "Крупный")
        };
        public ObservableCollection<TwoStringTuple> MessageBubbleWidthOptions { get; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple(BubbleWidthIds.Narrow, "Узкие"),
            new TwoStringTuple(BubbleWidthIds.Medium, "Стандартные"),
            new TwoStringTuple(BubbleWidthIds.Wide, "Широкие"),
            new TwoStringTuple(BubbleWidthIds.Full, "На всю ленту")
        };
        public ObservableCollection<TwoStringTuple> MessageBubbleDensityOptions { get; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple(BubbleDensityIds.Compact, "Compact"),
            new TwoStringTuple(BubbleDensityIds.Normal, "Normal"),
            new TwoStringTuple(BubbleDensityIds.Air, "Air")
        };
        public ObservableCollection<TwoStringTuple> MessageBubbleStyleOptions { get; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple(BubbleStyleIds.Vk, "VK"),
            new TwoStringTuple(BubbleStyleIds.Telegram, "Telegram"),
            new TwoStringTuple(BubbleStyleIds.Minimal, "Minimal"),
            new TwoStringTuple(BubbleStyleIds.Outline, "Outline"),
            new TwoStringTuple(BubbleStyleIds.Flat, "Flat")
        };
        public ObservableCollection<TwoStringTuple> MessageBubbleOpacityOptions { get; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple("100", "100%"),
            new TwoStringTuple("92", "92%"),
            new TwoStringTuple("84", "84%"),
            new TwoStringTuple("76", "76%")
        };

        public bool SentViaEnter { get { return Settings.SentViaEnter; } set { Settings.SentViaEnter = value; OnPropertyChanged(); } }
        public bool DontParseLinks { get { return Settings.DontParseLinks; } set { Settings.DontParseLinks = value; MarkCustomProfile(); OnPropertyChanged(); } }
        public bool DisableMentions { get { return Settings.DisableMentions; } set { Settings.DisableMentions = value; MarkCustomProfile(); OnPropertyChanged(); } }
        public TwoStringTuple CurrentChatListDensity { get { return GetChatListDensity(); } set { ChangeChatListDensity(value); OnPropertyChanged(); } }
        public TwoStringTuple CurrentChatListLayout { get { return GetChatListLayout(); } set { ChangeChatListLayout(value); OnPropertyChanged(); } }
        public string ChatListWidthText { get { return Math.Round(Settings.ChatListWidth).ToString(); } set { ChangeChatListWidth(value); OnPropertyChanged(); } }
        public TwoStringTuple CurrentChatListAvatarShape { get { return GetChatListAvatarShape(); } set { ChangeChatListAvatarShape(value); OnPropertyChanged(); } }
        public TwoStringTuple CurrentChatListAvatarSize { get { return GetChatListAvatarSize(); } set { ChangeChatListAvatarSize(value); OnPropertyChanged(); } }
        public TwoStringTuple CurrentMessageAvatarSize { get { return GetMessageAvatarSize(); } set { ChangeMessageAvatarSize(value); OnPropertyChanged(); } }
        public TwoStringTuple CurrentChatListFontSize { get { return GetChatListFontSize(); } set { ChangeChatListFontSize(value); OnPropertyChanged(); } }
        public TwoStringTuple CurrentMessageFontSize { get { return GetMessageFontSize(); } set { ChangeMessageFontSize(value); OnPropertyChanged(); } }
        public TwoStringTuple CurrentMessageBubbleWidth { get { return GetMessageBubbleWidth(); } set { ChangeMessageBubbleWidth(value); OnPropertyChanged(); } }
        public TwoStringTuple CurrentMessageBubbleDensity { get { return GetMessageBubbleDensity(); } set { ChangeMessageBubbleDensity(value); OnPropertyChanged(); } }
        public TwoStringTuple CurrentMessageBubbleStyle { get { return GetMessageBubbleStyle(); } set { ChangeMessageBubbleStyle(value); OnPropertyChanged(); } }
        public TwoStringTuple CurrentMessageBubbleOpacity { get { return GetMessageBubbleOpacity(); } set { ChangeMessageBubbleOpacity(value); OnPropertyChanged(); } }
        public bool MessageBubbleAutoColor { get { return Settings.MessageBubbleAutoColor; } set { Settings.MessageBubbleAutoColor = value; MarkCustomProfile(); OnPropertyChanged(); } }
        public bool ChatItemMoreRows { get { return Settings.ChatItemMoreRows; } set { Settings.ChatItemMoreRows = value; MarkCustomProfile(); OnPropertyChanged(); } }
        public bool LocalOcrEnabled { get { return Settings.LocalOcrEnabled; } set { Settings.LocalOcrEnabled = value; MarkCustomProfile(); OnPropertyChanged(); } }
        public string LocalOcrTesseractPath { get { return Settings.LocalOcrTesseractPath; } set { Settings.LocalOcrTesseractPath = value; MarkCustomProfile(); OnPropertyChanged(); } }
        public string LocalOcrLanguage { get { return Settings.LocalOcrLanguage; } set { Settings.LocalOcrLanguage = value; MarkCustomProfile(); OnPropertyChanged(); } }
        public bool ComposerActionFormatting { get { return Settings.ComposerActionFormatting; } set { Settings.ComposerActionFormatting = value; MarkCustomProfile(); OnPropertyChanged(); } }
        public bool ComposerActionQuick { get { return Settings.ComposerActionQuick; } set { Settings.ComposerActionQuick = value; MarkCustomProfile(); OnPropertyChanged(); } }
        public bool ComposerActionGroupTemplates { get { return Settings.ComposerActionGroupTemplates; } set { Settings.ComposerActionGroupTemplates = value; MarkCustomProfile(); OnPropertyChanged(); } }
        public bool ComposerActionStickers { get { return Settings.ComposerActionStickers; } set { Settings.ComposerActionStickers = value; MarkCustomProfile(); OnPropertyChanged(); } }
        public bool ChatHeaderActionSearch { get { return Settings.ChatHeaderActionSearch; } set { Settings.ChatHeaderActionSearch = value; MarkCustomProfile(); OnPropertyChanged(); } }
        public bool ChatHeaderActionProfile { get { return Settings.ChatHeaderActionProfile; } set { Settings.ChatHeaderActionProfile = value; MarkCustomProfile(); OnPropertyChanged(); } }
        public bool ChatHeaderActionMore { get { return Settings.ChatHeaderActionMore; } set { Settings.ChatHeaderActionMore = value; MarkCustomProfile(); OnPropertyChanged(); } }
        public string KeymapCommandPalette { get { return Settings.KeymapCommandPalette; } set { Settings.KeymapCommandPalette = value; OnPropertyChanged(); } }
        public string KeymapGlobalSearch { get { return Settings.KeymapGlobalSearch; } set { Settings.KeymapGlobalSearch = value; OnPropertyChanged(); } }
        public string KeymapChatSearch { get { return Settings.KeymapChatSearch; } set { Settings.KeymapChatSearch = value; OnPropertyChanged(); } }
        public string KeymapFocusComposer { get { return Settings.KeymapFocusComposer; } set { Settings.KeymapFocusComposer = value; OnPropertyChanged(); } }
        public string KeymapAttachments { get { return Settings.KeymapAttachments; } set { Settings.KeymapAttachments = value; OnPropertyChanged(); } }
        public string KeymapStickers { get { return Settings.KeymapStickers; } set { Settings.KeymapStickers = value; OnPropertyChanged(); } }
        public string KeymapSettings { get { return Settings.KeymapSettings; } set { Settings.KeymapSettings = value; OnPropertyChanged(); } }
        public string KeymapPanicLock { get { return Settings.KeymapPanicLock; } set { Settings.KeymapPanicLock = value; OnPropertyChanged(); } }
        public string KeymapBack { get { return Settings.KeymapBack; } set { Settings.KeymapBack = value; OnPropertyChanged(); } }

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

        private void ChangeChatListWidth(string value) {
            if (!Double.TryParse(value, out double width)) return;
            Settings.ChatListWidth = width;
            MarkCustomProfile();
            OnPropertyChanged(nameof(ChatListWidthText));
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

        private TwoStringTuple GetChatListAvatarSize() {
            return ChatListAvatarSizeOptions.Where(o => o.Item1 == Settings.ChatListAvatarSize).FirstOrDefault() ?? ChatListAvatarSizeOptions[0];
        }

        private void ChangeChatListAvatarSize(TwoStringTuple value) {
            if (value == null) return;
            Settings.ChatListAvatarSize = value.Item1;
            MarkCustomProfile();
            OnPropertyChanged(nameof(CurrentChatListAvatarSize));
        }

        private TwoStringTuple GetMessageAvatarSize() {
            return MessageAvatarSizeOptions.Where(o => o.Item1 == Settings.MessageAvatarSize).FirstOrDefault() ?? MessageAvatarSizeOptions[1];
        }

        private void ChangeMessageAvatarSize(TwoStringTuple value) {
            if (value == null) return;
            Settings.MessageAvatarSize = value.Item1;
            MarkCustomProfile();
            OnPropertyChanged(nameof(CurrentMessageAvatarSize));
        }

        private TwoStringTuple GetChatListFontSize() {
            return ChatListFontSizeOptions.Where(o => o.Item1 == Settings.ChatListFontSize).FirstOrDefault() ?? ChatListFontSizeOptions[0];
        }

        private void ChangeChatListFontSize(TwoStringTuple value) {
            if (value == null) return;
            Settings.ChatListFontSize = value.Item1;
            MarkCustomProfile();
            OnPropertyChanged(nameof(CurrentChatListFontSize));
        }

        private TwoStringTuple GetMessageFontSize() {
            return MessageFontSizeOptions.Where(o => o.Item1 == Settings.MessageFontSize).FirstOrDefault() ?? MessageFontSizeOptions[1];
        }

        private void ChangeMessageFontSize(TwoStringTuple value) {
            if (value == null) return;
            Settings.MessageFontSize = value.Item1;
            MarkCustomProfile();
            OnPropertyChanged(nameof(CurrentMessageFontSize));
        }

        private TwoStringTuple GetMessageBubbleWidth() {
            return MessageBubbleWidthOptions.Where(o => o.Item1 == Settings.MessageBubbleWidth).FirstOrDefault() ?? MessageBubbleWidthOptions[1];
        }

        private void ChangeMessageBubbleWidth(TwoStringTuple value) {
            if (value == null) return;
            Settings.MessageBubbleWidth = value.Item1;
            MarkCustomProfile();
            OnPropertyChanged(nameof(CurrentMessageBubbleWidth));
        }

        private TwoStringTuple GetMessageBubbleDensity() {
            return MessageBubbleDensityOptions.Where(o => o.Item1 == Settings.MessageBubbleDensity).FirstOrDefault() ?? MessageBubbleDensityOptions[1];
        }

        private void ChangeMessageBubbleDensity(TwoStringTuple value) {
            if (value == null) return;
            Settings.MessageBubbleDensity = value.Item1;
            MarkCustomProfile();
            OnPropertyChanged(nameof(CurrentMessageBubbleDensity));
        }

        private TwoStringTuple GetMessageBubbleStyle() {
            return MessageBubbleStyleOptions.Where(o => o.Item1 == Settings.MessageBubbleStyle).FirstOrDefault() ?? MessageBubbleStyleOptions[0];
        }

        private void ChangeMessageBubbleStyle(TwoStringTuple value) {
            if (value == null) return;
            Settings.MessageBubbleStyle = value.Item1;
            MarkCustomProfile();
            OnPropertyChanged(nameof(CurrentMessageBubbleStyle));
        }

        private TwoStringTuple GetMessageBubbleOpacity() {
            string opacity = Settings.MessageBubbleOpacity.ToString();
            return MessageBubbleOpacityOptions.Where(o => o.Item1 == opacity).FirstOrDefault() ?? MessageBubbleOpacityOptions[0];
        }

        private void ChangeMessageBubbleOpacity(TwoStringTuple value) {
            if (value == null) return;
            if (!int.TryParse(value.Item1, out int opacity)) return;
            Settings.MessageBubbleOpacity = opacity;
            MarkCustomProfile();
            OnPropertyChanged(nameof(CurrentMessageBubbleOpacity));
        }

        private void MarkCustomProfile() {
            if (Settings.InterfaceProfile != InterfaceProfileIds.Custom) Settings.InterfaceProfile = InterfaceProfileIds.Custom;
        }
    }
}
