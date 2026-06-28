using ELOR.Laney.Core;
using ELOR.Laney.Core.Localization;
using ELOR.Laney.DataModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ELOR.Laney.ViewModels.SettingsCategories {
    public class GeneralViewModel : CommonViewModel {
        public ObservableCollection<TwoStringTuple> Languages { get; private set; } = Localizer.SupportedLanguages;
        public ObservableCollection<TwoStringTuple> StickerAnimationModes { get; private set; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple(((int)StickerAnimationMode.Always).ToString(), "Всегда"),
            new TwoStringTuple(((int)StickerAnimationMode.Hover).ToString(), "При наведении"),
            new TwoStringTuple(((int)StickerAnimationMode.Click).ToString(), "По клику"),
            new TwoStringTuple(((int)StickerAnimationMode.Never).ToString(), "Никогда")
        };
        public ObservableCollection<TwoStringTuple> InterfaceProfiles { get; private set; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple(InterfaceProfileIds.Custom, "Custom"),
            new TwoStringTuple(InterfaceProfileIds.Compact, "Compact"),
            new TwoStringTuple(InterfaceProfileIds.Balanced, "Balanced"),
            new TwoStringTuple(InterfaceProfileIds.Touch, "Touch"),
            new TwoStringTuple(InterfaceProfileIds.Work, "Work"),
            new TwoStringTuple(InterfaceProfileIds.Night, "Night"),
            new TwoStringTuple(InterfaceProfileIds.LowRam, "Low RAM"),
            new TwoStringTuple(InterfaceProfileIds.Streamer, "Streamer")
        };

        public TwoStringTuple CurrentLanguage { get { return GetLang(); } set { ChangeLang(value); OnPropertyChanged(); } }
        public TwoStringTuple CurrentInterfaceProfile { get { return GetInterfaceProfile(); } set { ChangeInterfaceProfile(value); OnPropertyChanged(); } }
        public TwoStringTuple CurrentStickerAnimationMode { get { return GetStickerAnimationMode(); } set { ChangeStickerAnimationMode(value); OnPropertyChanged(); } }
        public bool SentViaEnter { get { return Settings.SentViaEnter; } set { Settings.SentViaEnter = value; OnPropertyChanged(); } }
        public bool DontParseLinks { get { return Settings.DontParseLinks; } set { Settings.DontParseLinks = value; MarkCustomProfile(); OnPropertyChanged(); } }
        public bool DisableMentions { get { return Settings.DisableMentions; } set { Settings.DisableMentions = value; MarkCustomProfile(); OnPropertyChanged(); } }

        public bool SuggestStickers { get { return Settings.SuggestStickers; } set { Settings.SuggestStickers = value; MarkCustomProfile(); OnPropertyChanged(); } }
        public bool LocalVoiceTranscriptionEnabled { get { return Settings.LocalVoiceTranscriptionEnabled; } set { Settings.LocalVoiceTranscriptionEnabled = value; MarkCustomProfile(); OnPropertyChanged(); } }
        public string LocalVoiceTranscriptionModelPath { get { return Settings.LocalVoiceTranscriptionModelPath; } set { Settings.LocalVoiceTranscriptionModelPath = value; MarkCustomProfile(); OnPropertyChanged(); } }
        public string LocalVoiceTranscriptionLanguage { get { return Settings.LocalVoiceTranscriptionLanguage; } set { Settings.LocalVoiceTranscriptionLanguage = value; MarkCustomProfile(); OnPropertyChanged(); } }
        public bool LocalOcrEnabled { get { return Settings.LocalOcrEnabled; } set { Settings.LocalOcrEnabled = value; MarkCustomProfile(); OnPropertyChanged(); } }
        public string LocalOcrTesseractPath { get { return Settings.LocalOcrTesseractPath; } set { Settings.LocalOcrTesseractPath = value; MarkCustomProfile(); OnPropertyChanged(); } }
        public string LocalOcrLanguage { get { return Settings.LocalOcrLanguage; } set { Settings.LocalOcrLanguage = value; MarkCustomProfile(); OnPropertyChanged(); } }
        public string LocalBackupDirectory { get { return String.IsNullOrWhiteSpace(Settings.LocalBackupDirectory) ? "Не выбрана" : Settings.LocalBackupDirectory; } }
        public string LocalDataMode { get { return App.IsPortableMode ? "Portable: ./data рядом с приложением" : "System: LocalAppData"; } }
        public string LocalDataPath { get { return App.LocalDataPath; } }

        public GeneralViewModel() {

        }

        public void RefreshLocalBackupDirectory() {
            OnPropertyChanged(nameof(LocalBackupDirectory));
        }

        private TwoStringTuple GetLang() {
            var id = Settings.Get(Settings.LANGUAGE, Constants.DefaultLang);
            return Languages.Where(l => l.Item1 == id).FirstOrDefault();
        }

        private void ChangeLang(TwoStringTuple value) {
            Settings.Set(Settings.LANGUAGE, value.Item1);
            Localizer.LoadLanguage(value.Item1);
            OnPropertyChanged(nameof(CurrentLanguage));
        }

        private TwoStringTuple GetInterfaceProfile() {
            return InterfaceProfiles.Where(p => p.Item1 == Settings.InterfaceProfile).FirstOrDefault() ?? InterfaceProfiles[0];
        }

        private void ChangeInterfaceProfile(TwoStringTuple value) {
            if (value == null) return;

            var settings = GetProfileSettings(value.Item1);
            Settings.SetBatch(settings);

            OnPropertyChanged(nameof(CurrentInterfaceProfile));
            OnPropertyChanged(nameof(CurrentStickerAnimationMode));
            OnPropertyChanged(nameof(SuggestStickers));
            OnPropertyChanged(nameof(DontParseLinks));
            OnPropertyChanged(nameof(DisableMentions));
            OnPropertyChanged(nameof(LocalVoiceTranscriptionEnabled));
            OnPropertyChanged(nameof(LocalVoiceTranscriptionModelPath));
            OnPropertyChanged(nameof(LocalVoiceTranscriptionLanguage));
            OnPropertyChanged(nameof(LocalOcrEnabled));
            OnPropertyChanged(nameof(LocalOcrTesseractPath));
            OnPropertyChanged(nameof(LocalOcrLanguage));
        }

        private TwoStringTuple GetStickerAnimationMode() {
            string id = ((int)Settings.StickerAnimation).ToString();
            return StickerAnimationModes.Where(m => m.Item1 == id).FirstOrDefault();
        }

        private void ChangeStickerAnimationMode(TwoStringTuple value) {
            if (value == null) return;
            Settings.StickerAnimation = (StickerAnimationMode)int.Parse(value.Item1);
            MarkCustomProfile();
            OnPropertyChanged(nameof(CurrentStickerAnimationMode));
        }

        private void MarkCustomProfile() {
            if (Settings.InterfaceProfile == InterfaceProfileIds.Custom) return;
            Settings.InterfaceProfile = InterfaceProfileIds.Custom;
            OnPropertyChanged(nameof(CurrentInterfaceProfile));
        }

        private Dictionary<string, object> GetProfileSettings(string profileId) {
            Dictionary<string, object> settings = new Dictionary<string, object> {
                { Settings.INTERFACE_PROFILE, profileId }
            };

            switch (profileId) {
                case InterfaceProfileIds.Compact:
                    settings.Add(Settings.THEME, 0);
                    settings.Add(Settings.STREAMER_MODE, false);
                    settings.Add(Settings.LOW_MOTION_MODE, false);
                    settings.Add(Settings.LOW_MEMORY_MODE, false);
                    settings.Add(Settings.LOW_TRAFFIC_MODE, false);
                    settings.Add(Settings.STICKERS_ANIMATION_MODE, (int)StickerAnimationMode.Hover);
                    settings.Add(Settings.STICKERS_ANIMATE, true);
                    settings.Add(Settings.STICKERS_SUGGEST, true);
                    settings.Add(Settings.DONT_PARSE_LINKS, false);
                    settings.Add(Settings.DISABLE_MENTIONS, false);
                    settings.Add(Settings.CHAT_ITEM_MORE_ROWS, false);
                    settings.Add(Settings.CHAT_LIST_DENSITY, ChatListDensityIds.Small);
                    settings.Add(Settings.CHAT_LIST_LAYOUT, ChatListLayoutIds.Compact);
                    settings.Add(Settings.CHAT_LIST_AVATAR_SIZE, AvatarSizeIds.Auto);
                    settings.Add(Settings.CHAT_LIST_AVATAR_SHAPE, AvatarShapeIds.Squircle);
                    settings.Add(Settings.CHAT_LIST_FONT_SIZE, TextSizeIds.Auto);
                    settings.Add(Settings.MESSAGE_AVATAR_SIZE, AvatarSizeIds.Small);
                    settings.Add(Settings.MESSAGE_FONT_SIZE, TextSizeIds.Small);
                    settings.Add(Settings.MESSAGE_BUBBLE_WIDTH, BubbleWidthIds.Narrow);
                    settings.Add(Settings.MESSAGE_BUBBLE_DENSITY, BubbleDensityIds.Compact);
                    settings.Add(Settings.MESSAGE_BUBBLE_STYLE, BubbleStyleIds.Minimal);
                    settings.Add(Settings.MESSAGE_BUBBLE_OPACITY, 100);
                    settings.Add(Settings.MESSAGE_BUBBLE_AUTO_COLOR, false);
                    break;
                case InterfaceProfileIds.Balanced:
                    settings.Add(Settings.THEME, 0);
                    settings.Add(Settings.STREAMER_MODE, false);
                    settings.Add(Settings.LOW_MOTION_MODE, false);
                    settings.Add(Settings.LOW_MEMORY_MODE, false);
                    settings.Add(Settings.LOW_TRAFFIC_MODE, false);
                    settings.Add(Settings.STICKERS_ANIMATION_MODE, (int)StickerAnimationMode.Hover);
                    settings.Add(Settings.STICKERS_ANIMATE, true);
                    settings.Add(Settings.STICKERS_SUGGEST, true);
                    settings.Add(Settings.DONT_PARSE_LINKS, false);
                    settings.Add(Settings.DISABLE_MENTIONS, false);
                    settings.Add(Settings.CHAT_ITEM_MORE_ROWS, false);
                    settings.Add(Settings.CHAT_LIST_DENSITY, ChatListDensityIds.Medium);
                    settings.Add(Settings.CHAT_LIST_LAYOUT, ChatListLayoutIds.Classic);
                    settings.Add(Settings.CHAT_LIST_AVATAR_SIZE, AvatarSizeIds.Auto);
                    settings.Add(Settings.CHAT_LIST_AVATAR_SHAPE, AvatarShapeIds.Circle);
                    settings.Add(Settings.CHAT_LIST_FONT_SIZE, TextSizeIds.Auto);
                    settings.Add(Settings.MESSAGE_AVATAR_SIZE, AvatarSizeIds.Medium);
                    settings.Add(Settings.MESSAGE_FONT_SIZE, TextSizeIds.Medium);
                    settings.Add(Settings.MESSAGE_BUBBLE_WIDTH, BubbleWidthIds.Medium);
                    settings.Add(Settings.MESSAGE_BUBBLE_DENSITY, BubbleDensityIds.Normal);
                    settings.Add(Settings.MESSAGE_BUBBLE_STYLE, BubbleStyleIds.Vk);
                    settings.Add(Settings.MESSAGE_BUBBLE_OPACITY, 100);
                    settings.Add(Settings.MESSAGE_BUBBLE_AUTO_COLOR, true);
                    break;
                case InterfaceProfileIds.Touch:
                    settings.Add(Settings.THEME, 0);
                    settings.Add(Settings.STREAMER_MODE, false);
                    settings.Add(Settings.LOW_MOTION_MODE, false);
                    settings.Add(Settings.LOW_MEMORY_MODE, false);
                    settings.Add(Settings.LOW_TRAFFIC_MODE, false);
                    settings.Add(Settings.STICKERS_ANIMATION_MODE, (int)StickerAnimationMode.Click);
                    settings.Add(Settings.STICKERS_ANIMATE, true);
                    settings.Add(Settings.STICKERS_SUGGEST, true);
                    settings.Add(Settings.DONT_PARSE_LINKS, false);
                    settings.Add(Settings.DISABLE_MENTIONS, false);
                    settings.Add(Settings.CHAT_ITEM_MORE_ROWS, true);
                    settings.Add(Settings.CHAT_LIST_DENSITY, ChatListDensityIds.Large);
                    settings.Add(Settings.CHAT_LIST_LAYOUT, ChatListLayoutIds.Telegram);
                    settings.Add(Settings.CHAT_LIST_AVATAR_SIZE, AvatarSizeIds.Large);
                    settings.Add(Settings.CHAT_LIST_AVATAR_SHAPE, AvatarShapeIds.Rounded);
                    settings.Add(Settings.CHAT_LIST_FONT_SIZE, TextSizeIds.Large);
                    settings.Add(Settings.MESSAGE_AVATAR_SIZE, AvatarSizeIds.Large);
                    settings.Add(Settings.MESSAGE_FONT_SIZE, TextSizeIds.Large);
                    settings.Add(Settings.MESSAGE_BUBBLE_WIDTH, BubbleWidthIds.Wide);
                    settings.Add(Settings.MESSAGE_BUBBLE_DENSITY, BubbleDensityIds.Air);
                    settings.Add(Settings.MESSAGE_BUBBLE_STYLE, BubbleStyleIds.Telegram);
                    settings.Add(Settings.MESSAGE_BUBBLE_OPACITY, 92);
                    settings.Add(Settings.MESSAGE_BUBBLE_AUTO_COLOR, true);
                    break;
                case InterfaceProfileIds.Work:
                    settings.Add(Settings.THEME, 0);
                    settings.Add(Settings.STREAMER_MODE, false);
                    settings.Add(Settings.LOW_MOTION_MODE, false);
                    settings.Add(Settings.LOW_MEMORY_MODE, false);
                    settings.Add(Settings.LOW_TRAFFIC_MODE, false);
                    settings.Add(Settings.STICKERS_ANIMATION_MODE, (int)StickerAnimationMode.Hover);
                    settings.Add(Settings.STICKERS_ANIMATE, true);
                    settings.Add(Settings.STICKERS_SUGGEST, true);
                    settings.Add(Settings.DONT_PARSE_LINKS, false);
                    settings.Add(Settings.DISABLE_MENTIONS, false);
                    settings.Add(Settings.NOTIF_PRIVATE_SOUND, true);
                    settings.Add(Settings.NOTIF_GCHAT_SOUND, true);
                    settings.Add(Settings.GROUPS_BACKGROUND_LONGPOLL_LIMIT, 2);
                    settings.Add(Settings.CHAT_LIST_DENSITY, ChatListDensityIds.Small);
                    settings.Add(Settings.CHAT_LIST_LAYOUT, ChatListLayoutIds.Compact);
                    settings.Add(Settings.CHAT_LIST_AVATAR_SIZE, AvatarSizeIds.Auto);
                    settings.Add(Settings.CHAT_LIST_AVATAR_SHAPE, AvatarShapeIds.Squircle);
                    settings.Add(Settings.CHAT_LIST_FONT_SIZE, TextSizeIds.Auto);
                    settings.Add(Settings.MESSAGE_AVATAR_SIZE, AvatarSizeIds.Small);
                    settings.Add(Settings.MESSAGE_FONT_SIZE, TextSizeIds.Small);
                    settings.Add(Settings.MESSAGE_BUBBLE_WIDTH, BubbleWidthIds.Narrow);
                    settings.Add(Settings.MESSAGE_BUBBLE_DENSITY, BubbleDensityIds.Compact);
                    settings.Add(Settings.MESSAGE_BUBBLE_STYLE, BubbleStyleIds.Minimal);
                    settings.Add(Settings.MESSAGE_BUBBLE_OPACITY, 100);
                    settings.Add(Settings.MESSAGE_BUBBLE_AUTO_COLOR, false);
                    break;
                case InterfaceProfileIds.Night:
                    settings.Add(Settings.THEME, 2);
                    settings.Add(Settings.STREAMER_MODE, false);
                    settings.Add(Settings.LOW_MOTION_MODE, false);
                    settings.Add(Settings.LOW_MEMORY_MODE, false);
                    settings.Add(Settings.LOW_TRAFFIC_MODE, false);
                    settings.Add(Settings.STICKERS_ANIMATION_MODE, (int)StickerAnimationMode.Click);
                    settings.Add(Settings.STICKERS_ANIMATE, true);
                    settings.Add(Settings.NOTIF_PRIVATE_SOUND, false);
                    settings.Add(Settings.NOTIF_GCHAT_SOUND, false);
                    settings.Add(Settings.NOTIF_DONT_ANNOY_ME, true);
                    settings.Add(Settings.NOTIF_DONT_ANNOY_ME_START_HOUR, 23);
                    settings.Add(Settings.NOTIF_DONT_ANNOY_ME_END_HOUR, 8);
                    settings.Add(Settings.CHAT_LIST_DENSITY, ChatListDensityIds.Medium);
                    settings.Add(Settings.CHAT_LIST_LAYOUT, ChatListLayoutIds.Telegram);
                    settings.Add(Settings.CHAT_LIST_AVATAR_SIZE, AvatarSizeIds.Auto);
                    settings.Add(Settings.CHAT_LIST_AVATAR_SHAPE, AvatarShapeIds.Circle);
                    settings.Add(Settings.CHAT_LIST_FONT_SIZE, TextSizeIds.Auto);
                    settings.Add(Settings.MESSAGE_AVATAR_SIZE, AvatarSizeIds.Medium);
                    settings.Add(Settings.MESSAGE_FONT_SIZE, TextSizeIds.Medium);
                    settings.Add(Settings.MESSAGE_BUBBLE_WIDTH, BubbleWidthIds.Medium);
                    settings.Add(Settings.MESSAGE_BUBBLE_DENSITY, BubbleDensityIds.Normal);
                    settings.Add(Settings.MESSAGE_BUBBLE_STYLE, BubbleStyleIds.Telegram);
                    settings.Add(Settings.MESSAGE_BUBBLE_OPACITY, 92);
                    settings.Add(Settings.MESSAGE_BUBBLE_AUTO_COLOR, true);
                    break;
                case InterfaceProfileIds.LowRam:
                    settings.Add(Settings.THEME, 0);
                    settings.Add(Settings.STREAMER_MODE, false);
                    settings.Add(Settings.LOW_MOTION_MODE, true);
                    settings.Add(Settings.LOW_MEMORY_MODE, true);
                    settings.Add(Settings.LOW_TRAFFIC_MODE, true);
                    settings.Add(Settings.STICKERS_ANIMATION_MODE, (int)StickerAnimationMode.Never);
                    settings.Add(Settings.STICKERS_ANIMATE, false);
                    settings.Add(Settings.STICKERS_SUGGEST, false);
                    settings.Add(Settings.DEBUG_LOAD_IMAGES_SEQUENTIAL, true);
                    settings.Add(Settings.GROUPS_BACKGROUND_LONGPOLL_LIMIT, 0);
                    settings.Add(Settings.IMAGE_CACHE_DEFAULT_TTL_MINUTES, 60);
                    settings.Add(Settings.IMAGE_CACHE_AVATAR_TTL_MINUTES, 240);
                    settings.Add(Settings.IMAGE_CACHE_ATTACHMENT_TTL_MINUTES, 30);
                    settings.Add(Settings.IMAGE_CACHE_E2E_TTL_MINUTES, 5);
                    settings.Add(Settings.IMAGE_CACHE_RAM_LIMIT_MB, 64);
                    settings.Add(Settings.DEBUG_FPS, false);
                    settings.Add(Settings.DEBUG_COUNTERS_CHAT, false);
                    settings.Add(Settings.DEBUG_COUNTERS_RAM, false);
                    settings.Add(Settings.CHAT_LIST_DENSITY, ChatListDensityIds.Small);
                    settings.Add(Settings.CHAT_LIST_LAYOUT, ChatListLayoutIds.Compact);
                    settings.Add(Settings.CHAT_LIST_AVATAR_SIZE, AvatarSizeIds.Small);
                    settings.Add(Settings.CHAT_LIST_AVATAR_SHAPE, AvatarShapeIds.Rounded);
                    settings.Add(Settings.CHAT_LIST_FONT_SIZE, TextSizeIds.Small);
                    settings.Add(Settings.MESSAGE_AVATAR_SIZE, AvatarSizeIds.Small);
                    settings.Add(Settings.MESSAGE_FONT_SIZE, TextSizeIds.Small);
                    settings.Add(Settings.MESSAGE_BUBBLE_WIDTH, BubbleWidthIds.Narrow);
                    settings.Add(Settings.MESSAGE_BUBBLE_DENSITY, BubbleDensityIds.Compact);
                    settings.Add(Settings.MESSAGE_BUBBLE_STYLE, BubbleStyleIds.Flat);
                    settings.Add(Settings.MESSAGE_BUBBLE_OPACITY, 100);
                    settings.Add(Settings.MESSAGE_BUBBLE_AUTO_COLOR, false);
                    break;
                case InterfaceProfileIds.Streamer:
                    settings.Add(Settings.THEME, 2);
                    settings.Add(Settings.STREAMER_MODE, true);
                    settings.Add(Settings.LOW_MOTION_MODE, true);
                    settings.Add(Settings.LOW_MEMORY_MODE, false);
                    settings.Add(Settings.LOW_TRAFFIC_MODE, false);
                    settings.Add(Settings.STICKERS_ANIMATION_MODE, (int)StickerAnimationMode.Never);
                    settings.Add(Settings.STICKERS_ANIMATE, false);
                    settings.Add(Settings.STICKERS_SUGGEST, false);
                    settings.Add(Settings.DONT_PARSE_LINKS, true);
                    settings.Add(Settings.DISABLE_MENTIONS, true);
                    settings.Add(Settings.NOTIF_PRIVATE_SOUND, false);
                    settings.Add(Settings.NOTIF_GCHAT_SOUND, false);
                    settings.Add(Settings.CHAT_LIST_DENSITY, ChatListDensityIds.Large);
                    settings.Add(Settings.CHAT_LIST_LAYOUT, ChatListLayoutIds.Telegram);
                    settings.Add(Settings.CHAT_LIST_AVATAR_SIZE, AvatarSizeIds.Large);
                    settings.Add(Settings.CHAT_LIST_AVATAR_SHAPE, AvatarShapeIds.Square);
                    settings.Add(Settings.CHAT_LIST_FONT_SIZE, TextSizeIds.Large);
                    settings.Add(Settings.MESSAGE_AVATAR_SIZE, AvatarSizeIds.Large);
                    settings.Add(Settings.MESSAGE_FONT_SIZE, TextSizeIds.Large);
                    settings.Add(Settings.MESSAGE_BUBBLE_WIDTH, BubbleWidthIds.Wide);
                    settings.Add(Settings.MESSAGE_BUBBLE_DENSITY, BubbleDensityIds.Air);
                    settings.Add(Settings.MESSAGE_BUBBLE_STYLE, BubbleStyleIds.Outline);
                    settings.Add(Settings.MESSAGE_BUBBLE_OPACITY, 100);
                    settings.Add(Settings.MESSAGE_BUBBLE_AUTO_COLOR, false);
                    break;
                default:
                    settings[Settings.INTERFACE_PROFILE] = InterfaceProfileIds.Custom;
                    settings.Add(Settings.STREAMER_MODE, false);
                    settings.Add(Settings.LOW_MOTION_MODE, false);
                    settings.Add(Settings.LOW_MEMORY_MODE, false);
                    settings.Add(Settings.LOW_TRAFFIC_MODE, false);
                    settings.Add(Settings.CHAT_LIST_DENSITY, ChatListDensityIds.Medium);
                    settings.Add(Settings.CHAT_LIST_LAYOUT, ChatListLayoutIds.Classic);
                    settings.Add(Settings.CHAT_LIST_AVATAR_SIZE, AvatarSizeIds.Auto);
                    settings.Add(Settings.CHAT_LIST_AVATAR_SHAPE, AvatarShapeIds.Circle);
                    settings.Add(Settings.CHAT_LIST_FONT_SIZE, TextSizeIds.Auto);
                    settings.Add(Settings.MESSAGE_AVATAR_SIZE, AvatarSizeIds.Medium);
                    settings.Add(Settings.MESSAGE_FONT_SIZE, TextSizeIds.Medium);
                    settings.Add(Settings.MESSAGE_BUBBLE_WIDTH, BubbleWidthIds.Medium);
                    settings.Add(Settings.MESSAGE_BUBBLE_DENSITY, BubbleDensityIds.Normal);
                    settings.Add(Settings.MESSAGE_BUBBLE_STYLE, BubbleStyleIds.Vk);
                    settings.Add(Settings.MESSAGE_BUBBLE_OPACITY, 100);
                    settings.Add(Settings.MESSAGE_BUBBLE_AUTO_COLOR, false);
                    break;
            }

            return settings;
        }
    }
}
