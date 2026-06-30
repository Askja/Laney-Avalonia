using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using ELOR.Laney.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ELOR.Laney.Core {
    public sealed class AppearanceOption {
        public string Id { get; }
        public string Title { get; }
        public string Subtitle { get; }
        public IBrush PreviewBrush { get; }

        private readonly Color lightColor;
        private readonly Color darkColor;

        public AppearanceOption(string id, string title, string subtitle, string lightColor, string darkColor) {
            Id = id;
            Title = title;
            Subtitle = subtitle;
            this.lightColor = Color.Parse(lightColor);
            this.darkColor = Color.Parse(darkColor);
            PreviewBrush = new SolidColorBrush(this.lightColor);
        }

        public Color GetActualColor() {
            return AppearanceManager.IsDarkTheme() ? darkColor : lightColor;
        }

        public Color GetActualColor(bool isDarkTheme) {
            return isDarkTheme ? darkColor : lightColor;
        }
    }

    public static class AppearanceManager {
        public const string DefaultAccentId = "vk";
        public const string DefaultChatBackgroundId = "default";
        public const string InheritChatBackgroundId = "inherit";
        public const string DefaultChatDensityId = "default";
        public const string DefaultChatFontId = "default";
        public const string DefaultBubbleStyleId = BubbleStyleIds.Vk;
        public const string DefaultBubbleColorId = "default";
        public const string AppFontFamilyResourceKey = "LaneyAppFontFamily";
        public const string ChatBackgroundResourceKey = "LaneyChatBackgroundBrush";
        public const string MessageOuterMarginResourceKey = "LaneyMessageOuterMargin";
        public const string MessageTextHostMarginResourceKey = "LaneyMessageTextHostMargin";
        public const string MessageTextFontSizeResourceKey = "LaneyMessageTextFontSize";
        public const string MessageTextLineHeightResourceKey = "LaneyMessageTextLineHeight";
        public const string MessageAvatarSizeResourceKey = "LaneyMessageAvatarSize";
        public const string MessageBubbleMaxWidthResourceKey = "LaneyMessageBubbleMaxWidth";
        public const string MessageBubbleCornerRadiusResourceKey = "LaneyMessageBubbleCornerRadius";
        public const string MessageBubbleBorderThicknessResourceKey = "LaneyMessageBubbleBorderThickness";
        public const string MessageBubbleBorderBrushResourceKey = "LaneyMessageBubbleBorderBrush";
        public const string MessageBubbleBackgroundOpacityResourceKey = "LaneyMessageBubbleBackgroundOpacity";
        public const string MessageBubbleOutgoingBrushResourceKey = "MessageBubbleDefaultOutgoingBrush";
        public const string ChatListItemHeight2RowResourceKey = "LaneyChatListItemHeight2Row";
        public const string ChatListItemHeight3RowResourceKey = "LaneyChatListItemHeight3Row";
        public const string ChatListAvatarSizeResourceKey = "LaneyChatListAvatarSize";
        public const string ChatListAvatarMargin2RowResourceKey = "LaneyChatListAvatarMargin2Row";
        public const string ChatListAvatarMargin3RowResourceKey = "LaneyChatListAvatarMargin3Row";
        public const string ChatListOnlineMargin2RowResourceKey = "LaneyChatListOnlineMargin2Row";
        public const string ChatListOnlineMargin3RowResourceKey = "LaneyChatListOnlineMargin3Row";
        public const string ChatListInfoMargin2RowResourceKey = "LaneyChatListInfoMargin2Row";
        public const string ChatListInfoMargin3RowResourceKey = "LaneyChatListInfoMargin3Row";
        public const string ChatListAvatarCornerRadiusResourceKey = "LaneyChatListAvatarCornerRadius";
        public const string ChatListTitleFontSizeResourceKey = "LaneyChatListTitleFontSize";
        public const string ChatListSubtitleFontSizeResourceKey = "LaneyChatListSubtitleFontSize";
        public const string ChatListTimeFontSizeResourceKey = "LaneyChatListTimeFontSize";
        public const string ChatListTitleLineHeightResourceKey = "LaneyChatListTitleLineHeight";
        public const string ChatListSubtitleLineHeightResourceKey = "LaneyChatListSubtitleLineHeight";
        public const string ChatListFirstLineHeightResourceKey = "LaneyChatListFirstLineHeight";
        public const string ChatListSecondLineHeightResourceKey = "LaneyChatListSecondLineHeight";

        private static readonly string[] AccentBrushKeys = {
            "VKAccentBrush",
            "VKAccentAlternateBrush",
            "VKActionSheetActionForegroundBrush",
            "VKButtonOutlineBorderBrush",
            "VKButtonOutlineForegroundBrush",
            "VKButtonPrimaryBackgroundBrush",
            "VKButtonSecondaryForegroundBrush",
            "VKButtonTertiaryForegroundBrush",
            "VKCellButtonForegroundBrush",
            "VKCounterPrimaryBackgroundBrush",
            "VKHeaderAlternateTabActiveIndicatorBrush",
            "VKHeaderTabActiveIndicatorBrush",
            "VKHeaderTintBrush",
            "VKHeaderTintAlternateBrush",
            "VKImAttachTintBrush",
            "VKImReplySeparatorBrush",
            "VKImTextNameBrush",
            "VKLinkAlternateBrush",
            "VKLoaderTrackValueFillBrush",
            "VKPollOptionBackgroundBrush",
            "VKTextLinkBrush",
            "VKWritebarIconBrush"
        };

        private static readonly IReadOnlyDictionary<string, string> VkChatThemeBackgroundMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            ["default"] = DefaultChatBackgroundId,
            ["classic"] = DefaultChatBackgroundId,
            ["vk"] = DefaultChatBackgroundId,
            ["blue"] = DefaultChatBackgroundId,
            ["green"] = "mint",
            ["mint"] = "mint",
            ["forest"] = "mint",
            ["sea"] = "mint",
            ["ocean"] = "mint",
            ["red"] = "rose",
            ["pink"] = "rose",
            ["rose"] = "rose",
            ["sunset"] = "rose",
            ["purple"] = "violet",
            ["violet"] = "violet",
            ["unicorn"] = "violet",
            ["space"] = "graphite",
            ["dark"] = "graphite",
            ["night"] = "graphite",
            ["graphite"] = "graphite",
            ["black"] = "graphite",
            ["yellow"] = "paper",
            ["orange"] = "paper",
            ["beach"] = "paper",
            ["brown"] = "paper",
            ["coffee"] = "paper",
            ["caramel"] = "paper",
            ["sepia"] = "paper",
            ["paper"] = "paper"
        };

        public static IReadOnlyList<AppearanceOption> AccentOptions { get; } = new List<AppearanceOption> {
            new AppearanceOption(DefaultAccentId, "VK", "Стандартный акцент темы", "#2688EB", "#529EF4"),
            new AppearanceOption("emerald", "Изумруд", "Спокойный рабочий зеленый", "#1B9E5A", "#55C98A"),
            new AppearanceOption("raspberry", "Малина", "Контрастный розово-красный", "#E03E72", "#FF6A9A"),
            new AppearanceOption("violet", "Фиолетовый", "Плотный, но не кислотный", "#7A4DDB", "#A68BFF"),
            new AppearanceOption("amber", "Янтарь", "Теплый акцент без кофейни", "#C97800", "#F2A93B"),
            new AppearanceOption("graphite", "Графит", "Минимум цвета, максимум дела", "#56616D", "#A6AFB8")
        };

        public static IReadOnlyList<AppearanceOption> AccentOptionsWithInherit { get; } =
            new List<AppearanceOption> {
                new AppearanceOption(InheritChatBackgroundId, "Наследовать", "Взять цвет уровнем выше", "#D7DEE7", "#303743")
            }.Concat(AccentOptions.Where(o => o.Id != DefaultAccentId)).ToList();

        public static IReadOnlyList<AppearanceOption> ChatBackgroundOptions { get; } = new List<AppearanceOption> {
            new AppearanceOption(DefaultChatBackgroundId, "Авто", "Под тему приложения и оформление VK-чата", "#F4F6F8", "#0F1115"),
            new AppearanceOption("paper", "Бумага", "Светлее и тише", "#F4F6F8", "#111418"),
            new AppearanceOption("mint", "Мята", "Холодный спокойный фон", "#EAF7F1", "#102018"),
            new AppearanceOption("rose", "Пыльная роза", "Теплый фон без сахарной комы", "#FFF0F3", "#251218"),
            new AppearanceOption("violet", "Туман", "Мягкий фиолетовый оттенок", "#F3F0FF", "#181326"),
            new AppearanceOption("graphite", "Графит", "Темнее и контрастнее", "#E3E7EA", "#07090C")
        };

        public static IReadOnlyList<AppearanceOption> ChatBackgroundOptionsWithInherit { get; } =
            new List<AppearanceOption> {
                new AppearanceOption(InheritChatBackgroundId, "Как в настройках", "Убрать личную тему этого чата", "#D7DEE7", "#303743")
            }.Concat(ChatBackgroundOptions).ToList();

        public static IReadOnlyList<AppearanceOption> ChatDensityOptionsWithInherit { get; } = new List<AppearanceOption> {
            new AppearanceOption(InheritChatBackgroundId, "Как обычно", "Сбросить плотность этого чата", "#D7DEE7", "#303743"),
            new AppearanceOption(DefaultChatDensityId, "Стандарт", "Баланс воздуха и плотности", "#EDEEF0", "#0A0A0A"),
            new AppearanceOption("compact", "Плотно", "Больше сообщений на экране", "#DBEAFE", "#172554"),
            new AppearanceOption("relaxed", "Свободно", "Крупнее отступы, меньше каши", "#ECFDF5", "#052E1A")
        };

        public static IReadOnlyList<AppearanceOption> ChatFontOptionsWithInherit { get; } = new List<AppearanceOption> {
            new AppearanceOption(InheritChatBackgroundId, "Как обычно", "Сбросить шрифт этого чата", "#D7DEE7", "#303743"),
            new AppearanceOption(DefaultChatFontId, "Стандарт", "15px, как в теме", "#EDEEF0", "#0A0A0A"),
            new AppearanceOption("small", "Мелкий", "14px для плотных рабочих чатов", "#EEF2FF", "#1E1B4B"),
            new AppearanceOption("large", "Крупный", "16px, когда глаза не железные", "#FFF7ED", "#431407")
        };

        public static IReadOnlyList<AppearanceOption> BubbleStyleOptionsWithInherit { get; } = new List<AppearanceOption> {
            new AppearanceOption(InheritChatBackgroundId, "Как обычно", "Сбросить форму пузырей", "#D7DEE7", "#303743"),
            new AppearanceOption(DefaultBubbleStyleId, "VK", "Обычные округлые bubble", "#EDEEF0", "#0A0A0A"),
            new AppearanceOption(BubbleStyleIds.Telegram, "Telegram", "Чище, плотнее и без лишней ваты", "#DBEAFE", "#172554"),
            new AppearanceOption(BubbleStyleIds.Minimal, "Минимализм", "Меньше скруглений, больше текста", "#F1F5F9", "#111827"),
            new AppearanceOption(BubbleStyleIds.Outline, "Контур", "Контур вместо тяжелого пятна", "#F8FAFC", "#1F2937"),
            new AppearanceOption(BubbleStyleIds.Flat, "Плоский", "Почти без радиуса, максимально сухо", "#E5E7EB", "#111827")
        };

        public static IReadOnlyList<AppearanceOption> BubbleColorOptionsWithInherit { get; } = new List<AppearanceOption> {
            new AppearanceOption(InheritChatBackgroundId, "Как обычно", "Сбросить цвет исходящих", "#D7DEE7", "#303743"),
            new AppearanceOption(DefaultBubbleColorId, "VK", "Стандартный цвет исходящих", "#DDEEFF", "#24435F"),
            new AppearanceOption("mint", "Мята", "Зеленоватый акцент", "#DDF8EA", "#174332"),
            new AppearanceOption("amber", "Янтарь", "Теплый спокойный цвет", "#FFF0CC", "#463315"),
            new AppearanceOption("rose", "Роза", "Мягкий розовый тон", "#FFE1EA", "#4A1B2A"),
            new AppearanceOption("violet", "Туман", "Мягкий фиолетовый тон", "#EDE7FF", "#30224C"),
            new AppearanceOption("graphite", "Графит", "Сдержанный серый", "#E4E8ED", "#2B323A")
        };

        public static void ApplyAppearanceSettings() {
            ApplyAccent(Settings.AccentColor);
            Application.Current.Resources[AppFontFamilyResourceKey] = CreateAppFontFamily(Settings.AppFontFamily);
            SetResourceBrush(ChatBackgroundResourceKey, GetChatBackgroundBrush(0));
            Application.Current.Resources[MessageOuterMarginResourceKey] = GetMessageOuterMargin(0);
            Application.Current.Resources[MessageTextHostMarginResourceKey] = GetMessageTextHostMargin(0);
            Application.Current.Resources[MessageTextFontSizeResourceKey] = GetMessageTextFontSize(0);
            Application.Current.Resources[MessageTextLineHeightResourceKey] = GetMessageTextLineHeight(0);
            Application.Current.Resources[MessageAvatarSizeResourceKey] = GetMessageAvatarSize();
            Application.Current.Resources[MessageBubbleMaxWidthResourceKey] = GetMessageBubbleMaxWidth();
            Application.Current.Resources[MessageBubbleCornerRadiusResourceKey] = GetMessageBubbleCornerRadius(0);
            Application.Current.Resources[MessageBubbleBorderThicknessResourceKey] = GetMessageBubbleBorderThickness(0);
            Application.Current.Resources[MessageBubbleBorderBrushResourceKey] = GetMessageBubbleBorderBrush(0);
            Application.Current.Resources[MessageBubbleBackgroundOpacityResourceKey] = GetMessageBubbleBackgroundOpacity();
            ApplyChatListResources();
        }

        public static void ApplyAccountAppearanceSettings(VKSession session) {
            if (session?.Window?.Resources == null) return;
            ApplyAccent(session.Window.Resources, Settings.GetAccountAccent(session.Id));
        }

        public static void ApplyChatAccentResources(IResourceDictionary resources, long peerId) {
            if (resources == null) return;

            string accentId = Settings.GetPeerLocalAccent(peerId);
            ApplyAccent(resources, accentId);
        }

        private static void ApplyChatListResources() {
            Application.Current.Resources[ChatListItemHeight2RowResourceKey] = GetChatListItemHeight(false);
            Application.Current.Resources[ChatListItemHeight3RowResourceKey] = GetChatListItemHeight(true);
            Application.Current.Resources[ChatListAvatarSizeResourceKey] = GetChatListAvatarSize();
            Application.Current.Resources[ChatListAvatarMargin2RowResourceKey] = GetChatListAvatarMargin(false);
            Application.Current.Resources[ChatListAvatarMargin3RowResourceKey] = GetChatListAvatarMargin(true);
            Application.Current.Resources[ChatListOnlineMargin2RowResourceKey] = GetChatListOnlineMargin(false);
            Application.Current.Resources[ChatListOnlineMargin3RowResourceKey] = GetChatListOnlineMargin(true);
            Application.Current.Resources[ChatListInfoMargin2RowResourceKey] = GetChatListInfoMargin(false);
            Application.Current.Resources[ChatListInfoMargin3RowResourceKey] = GetChatListInfoMargin(true);
            Application.Current.Resources[ChatListAvatarCornerRadiusResourceKey] = GetChatListAvatarCornerRadius();
            Application.Current.Resources[ChatListTitleFontSizeResourceKey] = GetChatListTitleFontSize();
            Application.Current.Resources[ChatListSubtitleFontSizeResourceKey] = GetChatListSubtitleFontSize();
            Application.Current.Resources[ChatListTimeFontSizeResourceKey] = GetChatListTimeFontSize();
            Application.Current.Resources[ChatListTitleLineHeightResourceKey] = GetChatListTitleLineHeight();
            Application.Current.Resources[ChatListSubtitleLineHeightResourceKey] = GetChatListSubtitleLineHeight();
            Application.Current.Resources[ChatListFirstLineHeightResourceKey] = GetChatListFirstLineHeight();
            Application.Current.Resources[ChatListSecondLineHeightResourceKey] = GetChatListSecondLineHeight();
        }

        public static IBrush GetChatBackgroundBrush(long peerId) {
            string backgroundId = GetEffectiveChatBackgroundId(peerId);
            return GetChatBackgroundBrushById(backgroundId);
        }

        public static IBrush GetChatBackgroundBrush(ChatViewModel chat) {
            string backgroundId = GetEffectiveChatBackgroundId(chat);
            return GetChatBackgroundBrushById(backgroundId);
        }

        public static IBrush GetChatBackgroundBrush(ChatViewModel chat, bool isDarkTheme) {
            string backgroundId = GetEffectiveChatBackgroundId(chat);
            return GetChatBackgroundBrushById(backgroundId, isDarkTheme);
        }

        private static IBrush GetChatBackgroundBrushById(string backgroundId) {
            return GetChatBackgroundBrushById(backgroundId, IsDarkTheme());
        }

        private static IBrush GetChatBackgroundBrushById(string backgroundId, bool isDarkTheme) {
            if (String.IsNullOrWhiteSpace(backgroundId) || backgroundId == DefaultChatBackgroundId) {
                return new SolidColorBrush(GetDefaultChatBackgroundColor(isDarkTheme));
            }

            AppearanceOption option = GetChatBackgroundOption(backgroundId);
            return new SolidColorBrush(option.GetActualColor(isDarkTheme));
        }

        public static Thickness GetMessageOuterMargin(long peerId) {
            string density = GetEffectiveMessageDensity(peerId);
            return density switch {
                BubbleDensityIds.Compact => new Thickness(8, 2),
                BubbleDensityIds.Air => new Thickness(14, 6),
                _ => new Thickness(12, 4)
            };
        }

        public static Thickness GetMessageTextHostMargin(long peerId) {
            string density = GetEffectiveMessageDensity(peerId);
            return density switch {
                BubbleDensityIds.Compact => new Thickness(10, 0, 10, 6),
                BubbleDensityIds.Air => new Thickness(14, 2, 14, 10),
                _ => new Thickness(12, 0, 12, 8)
            };
        }

        public static double GetMessageTextFontSize(long peerId) {
            string font = Settings.GetPeerLocalFont(peerId);
            if (String.IsNullOrWhiteSpace(font) || font == InheritChatBackgroundId || font == DefaultChatFontId) {
                font = Settings.MessageFontSize;
            }

            return font switch {
                "small" => 14,
                "large" => 16,
                _ => 15
            };
        }

        public static double GetMessageTextLineHeight(long peerId) {
            string font = Settings.GetPeerLocalFont(peerId);
            if (String.IsNullOrWhiteSpace(font) || font == InheritChatBackgroundId || font == DefaultChatFontId) {
                font = Settings.MessageFontSize;
            }

            return font switch {
                "small" => 19,
                "large" => 22,
                _ => 20
            };
        }

        public static CornerRadius GetMessageBubbleCornerRadius(long peerId) {
            string style = GetEffectiveBubbleStyle(peerId);
            return style switch {
                BubbleStyleIds.Telegram => new CornerRadius(16),
                BubbleStyleIds.Minimal => new CornerRadius(8),
                BubbleStyleIds.Outline => new CornerRadius(14),
                BubbleStyleIds.Flat => new CornerRadius(4),
                _ => new CornerRadius(18)
            };
        }

        public static Thickness GetMessageBubbleBorderThickness(long peerId) {
            return GetEffectiveBubbleStyle(peerId) == BubbleStyleIds.Outline ? new Thickness(1) : new Thickness(0.75);
        }

        public static IBrush GetMessageBubbleBorderBrush(long peerId) {
            if (GetEffectiveBubbleStyle(peerId) == BubbleStyleIds.Outline) {
                return App.GetResource<IBrush>("VKImageBorderBrush") ?? new SolidColorBrush(Color.Parse("#D0D7DE"));
            }

            return new SolidColorBrush(Color.Parse(IsDarkTheme() ? "#42FFFFFF" : "#66B7C5D6"));
        }

        public static double GetMessageBubbleMaxWidth() {
            return Settings.MessageBubbleWidth switch {
                BubbleWidthIds.Narrow => 448,
                BubbleWidthIds.Wide => 720,
                BubbleWidthIds.Full => 984,
                _ => 576
            };
        }

        public static double GetMessageBubbleBackgroundOpacity() {
            return Settings.MessageBubbleOpacity / 100d;
        }

        public static IBrush GetOutgoingBubbleBrush(long peerId) {
            return GetOutgoingBubbleBrush(peerId, GetEffectiveChatBackgroundId(peerId));
        }

        public static IBrush GetOutgoingBubbleBrush(ChatViewModel chat) {
            long peerId = chat?.PeerId ?? 0;
            return GetOutgoingBubbleBrush(peerId, GetEffectiveChatBackgroundId(chat));
        }

        public static IBrush GetOutgoingBubbleBrush(ChatViewModel chat, bool isDarkTheme) {
            long peerId = chat?.PeerId ?? 0;
            return GetOutgoingBubbleBrush(peerId, GetEffectiveChatBackgroundId(chat), isDarkTheme);
        }

        private static IBrush GetOutgoingBubbleBrush(long peerId, string backgroundId) {
            return GetOutgoingBubbleBrush(peerId, backgroundId, IsDarkTheme());
        }

        private static IBrush GetOutgoingBubbleBrush(long peerId, string backgroundId, bool isDarkTheme) {
            string colorId = Settings.GetPeerLocalBubbleColor(peerId);
            if (String.IsNullOrWhiteSpace(colorId) || colorId == InheritChatBackgroundId || colorId == DefaultBubbleColorId) {
                if (Settings.MessageBubbleAutoColor) {
                    IBrush autoBrush = GetAutoOutgoingBubbleBrush(backgroundId, isDarkTheme);
                    if (autoBrush != null) return autoBrush;
                }

                return App.GetResource<IBrush>(MessageBubbleOutgoingBrushResourceKey)
                    ?? App.GetResource<IBrush>("VKImBubbleOutgoingBrush")
                    ?? new SolidColorBrush(Color.Parse("#DDEEFF"));
            }

            AppearanceOption option = BubbleColorOptionsWithInherit.FirstOrDefault(o => o.Id == colorId) ?? BubbleColorOptionsWithInherit[1];
            return CreateGlassBubbleBrush(option.GetActualColor(isDarkTheme));
        }

        private static IBrush GetAutoOutgoingBubbleBrush(string backgroundId, bool isDarkTheme) {
            string bubbleId = backgroundId switch {
                "mint" => "mint",
                "rose" => isDarkTheme ? "graphite" : "rose",
                "violet" => "violet",
                "graphite" => "graphite",
                "paper" => isDarkTheme ? "graphite" : "amber",
                _ => null
            };

            if (String.IsNullOrWhiteSpace(bubbleId)) return null;

            AppearanceOption option = BubbleColorOptionsWithInherit.FirstOrDefault(o => o.Id == bubbleId);
            return option == null ? null : CreateGlassBubbleBrush(option.GetActualColor(isDarkTheme));
        }

        private static SolidColorBrush CreateGlassBubbleBrush(Color color) {
            return new SolidColorBrush(color, IsDarkTheme() ? 0.98 : 0.96);
        }

        public static Uri GetChatBackgroundImageUri(long peerId) {
            return Settings.GetPeerLocalBackgroundImageUri(peerId) ?? Settings.ChatBackgroundImageUri;
        }

        public static double GetChatBackgroundImageOpacity(long peerId) {
            return GetChatBackgroundImageUri(peerId) == null ? 0 : 1;
        }

        public static double GetChatBackgroundDimOpacity(long peerId) {
            if (GetChatBackgroundImageUri(peerId) == null) return 0;

            int dim = Settings.GetPeerLocalBackgroundDim(peerId);
            int negativeBrightness = Math.Max(0, -Settings.GetPeerLocalBackgroundBrightness(peerId));
            if (dim == 0 && negativeBrightness == 0 && IsDarkTheme()) return 0.28;
            return Math.Clamp(dim + negativeBrightness, 0, 90) / 100d;
        }

        public static int GetChatBackgroundBlurRadius(long peerId) {
            if (GetChatBackgroundImageUri(peerId) == null) return 0;
            return Settings.GetPeerLocalBackgroundBlur(peerId);
        }

        public static double GetChatBackgroundBrightnessOpacity(long peerId) {
            if (GetChatBackgroundImageUri(peerId) == null) return 0;
            return Math.Max(0, Settings.GetPeerLocalBackgroundBrightness(peerId)) / 100d;
        }

        private static string GetEffectiveMessageDensity(long peerId) {
            if (peerId == 0) return Settings.MessageBubbleDensity;

            string density = Settings.GetPeerLocalDensity(peerId);
            if (String.IsNullOrWhiteSpace(density) || density == InheritChatBackgroundId) return Settings.MessageBubbleDensity;

            return density.Trim().ToLowerInvariant() switch {
                BubbleDensityIds.Compact => BubbleDensityIds.Compact,
                BubbleDensityIds.Air => BubbleDensityIds.Air,
                BubbleDensityIds.LegacyRelaxed => BubbleDensityIds.Air,
                _ => BubbleDensityIds.Normal
            };
        }

        private static string GetEffectiveChatBackgroundId(long peerId) {
            string backgroundId = peerId != 0 ? Settings.GetPeerLocalTheme(peerId) : null;
            return GetEffectiveChatBackgroundId(backgroundId, null);
        }

        private static string GetEffectiveChatBackgroundId(ChatViewModel chat) {
            if (chat == null) return GetEffectiveChatBackgroundId(0);

            string backgroundId = Settings.GetPeerLocalTheme(chat.PeerId);
            string vkThemeBackgroundId = MapVkChatThemeToBackgroundId(chat.ChatSettings?.Theme);
            return GetEffectiveChatBackgroundId(backgroundId, vkThemeBackgroundId);
        }

        private static string GetEffectiveChatBackgroundId(string localBackgroundId, string vkThemeBackgroundId) {
            string backgroundId = localBackgroundId;
            if (String.IsNullOrWhiteSpace(backgroundId) || backgroundId == InheritChatBackgroundId || backgroundId == DefaultChatBackgroundId) {
                string globalBackgroundId = Settings.ChatBackground;
                backgroundId = !String.IsNullOrWhiteSpace(vkThemeBackgroundId)
                    && (String.IsNullOrWhiteSpace(globalBackgroundId) || globalBackgroundId == DefaultChatBackgroundId)
                    ? vkThemeBackgroundId
                    : globalBackgroundId;
            }

            return String.IsNullOrWhiteSpace(backgroundId) ? DefaultChatBackgroundId : backgroundId;
        }

        public static bool IsDarkTheme() {
            if (Settings.AppTheme == 2) return true;
            if (Settings.AppTheme == 1) return false;

            if (TryIsDarkResource("LaneySurfacePanelBrush", out bool isDark)) return isDark;
            if (TryIsDarkResource("LaneySurfaceSidebarBrush", out isDark)) return isDark;
            if (TryIsDarkResource("LaneySurfaceRootBrush", out isDark)) return isDark;
            if (TryIsDarkResource("VKBackgroundContentBrush", out isDark)) return isDark;
            if (TryIsDarkResource("VKBackgroundPageBrush", out isDark)) return isDark;

            ThemeVariant requested = App.Current?.RequestedThemeVariant;
            if (requested == ThemeVariant.Dark) return true;
            if (requested == ThemeVariant.Light) return false;

            ThemeVariant actual = App.Current?.ActualThemeVariant;
            if (actual == ThemeVariant.Dark) return true;

            string actualName = actual?.ToString();
            if (!String.IsNullOrWhiteSpace(actualName) && actualName.Contains("Dark", StringComparison.OrdinalIgnoreCase)) return true;

            PlatformThemeVariant platformTheme = App.Current?.PlatformSettings?.GetColorValues().ThemeVariant ?? PlatformThemeVariant.Light;
            if (platformTheme == PlatformThemeVariant.Dark) return true;

            return false;
        }

        public static IBrush GetSkeletonBrush(bool isDarkTheme) {
            return new SolidColorBrush(Color.Parse(isDarkTheme ? "#28313D" : "#E5ECF3"));
        }

        public static IBrush GetReadableTextBrush(IBrush background, bool secondary = false) {
            bool isDarkBackground = true;
            if (background is ISolidColorBrush solidBrush) {
                isDarkBackground = GetColorLuminance(solidBrush.Color) < 0.56;
            }

            string color = isDarkBackground
                ? secondary ? "#D2DCE8" : "#FFFFFF"
                : secondary ? "#314C67" : "#07111F";
            return new SolidColorBrush(Color.Parse(color));
        }

        public static FontFamily CreateAppFontFamily(string fontFamily) {
            if (String.IsNullOrWhiteSpace(fontFamily)) return new FontFamily("Segoe UI");

            try {
                return new FontFamily(fontFamily);
            } catch {
                return new FontFamily("Segoe UI");
            }
        }

        private static Color GetDefaultChatBackgroundColor(bool isDarkTheme) {
            return Color.Parse(isDarkTheme ? "#0F1115" : "#F4F6F8");
        }

        private static bool TryIsDarkResource(string resourceKey, out bool isDark) {
            return TryIsDarkBrush(App.GetResource<IBrush>(resourceKey), out isDark);
        }

        public static bool TryIsDarkBrush(IBrush brush, out bool isDark) {
            isDark = false;
            if (brush is not ISolidColorBrush solidBrush) return false;

            Color color = solidBrush.Color;
            double luminance = GetColorLuminance(color);
            isDark = luminance < 0.45;
            return true;
        }

        private static double GetColorLuminance(Color color) {
            return (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255d;
        }

        public static string MapVkChatThemeToBackgroundId(string theme) {
            if (String.IsNullOrWhiteSpace(theme)) return null;

            string normalized = theme.Trim().ToLowerInvariant();
            if (VkChatThemeBackgroundMap.TryGetValue(normalized, out string mapped)) return mapped;

            foreach (KeyValuePair<string, string> item in VkChatThemeBackgroundMap) {
                if (normalized.Contains(item.Key, StringComparison.OrdinalIgnoreCase)) return item.Value;
            }

            return DefaultChatBackgroundId;
        }

        private static string GetEffectiveBubbleStyle(long peerId) {
            if (peerId == 0) return Settings.MessageBubbleStyle;

            string style = Settings.GetPeerLocalBubbleStyle(peerId);
            if (String.IsNullOrWhiteSpace(style) || style == InheritChatBackgroundId) return Settings.MessageBubbleStyle;

            return style.Trim().ToLowerInvariant() switch {
                BubbleStyleIds.Telegram => BubbleStyleIds.Telegram,
                BubbleStyleIds.Minimal => BubbleStyleIds.Minimal,
                BubbleStyleIds.Outline => BubbleStyleIds.Outline,
                BubbleStyleIds.Flat => BubbleStyleIds.Flat,
                BubbleStyleIds.LegacyRound => BubbleStyleIds.Telegram,
                BubbleStyleIds.LegacySharp => BubbleStyleIds.Minimal,
                _ => BubbleStyleIds.Vk
            };
        }

        public static double GetChatListItemHeight(bool moreRows) {
            return Settings.ChatListDensity switch {
                ChatListDensityIds.Small => moreRows ? 66 : 56,
                ChatListDensityIds.Large => moreRows ? 84 : 72,
                _ => moreRows ? 72 : 64
            };
        }

        public static double GetChatListAvatarSize() {
            if (Settings.ChatListAvatarSize != AvatarSizeIds.Auto) return GetChatListExplicitAvatarSize(Settings.ChatListAvatarSize);

            return Settings.ChatListDensity switch {
                ChatListDensityIds.Small => 40,
                ChatListDensityIds.Large => 56,
                _ => 48
            };
        }

        public static double GetMessageAvatarSize() {
            return GetAvatarSize(Settings.MessageAvatarSize, 32);
        }

        public static Thickness GetChatListAvatarMargin(bool moreRows) {
            return Settings.ChatListDensity switch {
                ChatListDensityIds.Small => moreRows ? new Thickness(10, 6, 0, 6) : new Thickness(10, 8, 0, 8),
                ChatListDensityIds.Large => moreRows ? new Thickness(14, 8, 0, 8) : new Thickness(14, 8, 0, 8),
                _ => moreRows ? new Thickness(12, 6, 0, 6) : new Thickness(12, 8, 0, 8)
            };
        }

        public static Thickness GetChatListOnlineMargin(bool moreRows) {
            return Settings.ChatListDensity switch {
                ChatListDensityIds.Small => moreRows ? new Thickness(0, 40, 3, 0) : new Thickness(0, 0, 3, 8),
                ChatListDensityIds.Large => moreRows ? new Thickness(0, 54, 3, 0) : new Thickness(0, 0, 3, 8),
                _ => moreRows ? new Thickness(0, 44, 2, 0) : new Thickness(0, 0, 2, 8)
            };
        }

        public static Thickness GetChatListInfoMargin(bool moreRows) {
            return Settings.ChatListDensity switch {
                ChatListDensityIds.Small => moreRows ? new Thickness(10, 5, 0, 0) : new Thickness(10, 8, 0, 8),
                ChatListDensityIds.Large => moreRows ? new Thickness(14, 8, 0, 0) : new Thickness(14, 13, 0, 13),
                _ => moreRows ? new Thickness(12, 6, 0, 0) : new Thickness(12, 11, 0, 11)
            };
        }

        public static CornerRadius GetChatListAvatarCornerRadius() {
            return Settings.ChatListAvatarShape switch {
                AvatarShapeIds.Square => new CornerRadius(0),
                AvatarShapeIds.Rounded => new CornerRadius(10),
                AvatarShapeIds.Squircle => new CornerRadius(16),
                _ => new CornerRadius(999)
            };
        }

        public static double GetChatListTitleFontSize() {
            string size = GetEffectiveChatListFontSize();
            return size switch {
                TextSizeIds.Small => 14,
                TextSizeIds.Large => 16,
                _ => 15
            };
        }

        public static double GetChatListSubtitleFontSize() {
            string size = GetEffectiveChatListFontSize();
            return size switch {
                TextSizeIds.Small => 13,
                TextSizeIds.Large => 15,
                _ => 14
            };
        }

        public static double GetChatListTimeFontSize() {
            string size = GetEffectiveChatListFontSize();
            return size switch {
                TextSizeIds.Small => 12,
                TextSizeIds.Large => 14,
                _ => 13
            };
        }

        public static double GetChatListTitleLineHeight() {
            string size = GetEffectiveChatListFontSize();
            return size switch {
                TextSizeIds.Small => 18,
                TextSizeIds.Large => 22,
                _ => 20
            };
        }

        public static double GetChatListSubtitleLineHeight() {
            string size = GetEffectiveChatListFontSize();
            return size switch {
                TextSizeIds.Small => 17,
                TextSizeIds.Large => 20,
                _ => 18
            };
        }

        public static double GetChatListFirstLineHeight() {
            string size = GetEffectiveChatListFontSize();
            return size switch {
                TextSizeIds.Small => 20,
                TextSizeIds.Large => 24,
                _ => 22
            };
        }

        public static double GetChatListSecondLineHeight() {
            string size = GetEffectiveChatListFontSize();
            return size switch {
                TextSizeIds.Small => 17,
                TextSizeIds.Large => 20,
                _ => 18
            };
        }

        private static string GetEffectiveChatListFontSize() {
            if (Settings.ChatListFontSize != TextSizeIds.Auto) return Settings.ChatListFontSize;

            return Settings.ChatListDensity switch {
                ChatListDensityIds.Small => TextSizeIds.Small,
                ChatListDensityIds.Large => TextSizeIds.Large,
                _ => TextSizeIds.Medium
            };
        }

        private static double GetAvatarSize(string sizeId, double fallback) {
            return sizeId switch {
                AvatarSizeIds.Small => 28,
                AvatarSizeIds.Medium => 32,
                AvatarSizeIds.Large => 40,
                _ => fallback
            };
        }

        private static double GetChatListExplicitAvatarSize(string sizeId) {
            return sizeId switch {
                AvatarSizeIds.Small => 40,
                AvatarSizeIds.Medium => 48,
                AvatarSizeIds.Large => 56,
                _ => 48
            };
        }

        public static AppearanceOption GetAccentOption(string id) {
            return AccentOptions.FirstOrDefault(o => o.Id == id) ?? AccentOptions[0];
        }

        public static AppearanceOption GetChatBackgroundOption(string id) {
            return ChatBackgroundOptions.FirstOrDefault(o => o.Id == id) ?? ChatBackgroundOptions[0];
        }

        private static void ApplyAccent(string accentId) {
            ApplyAccent(Application.Current.Resources, accentId);
        }

        private static void ApplyAccent(IResourceDictionary resources, string accentId) {
            if (resources == null) return;
            if (String.IsNullOrWhiteSpace(accentId) || accentId == InheritChatBackgroundId) {
                RemoveAccentOverrides(resources);
                return;
            }

            AppearanceOption option = GetAccentOption(accentId);
            if (option.Id == DefaultAccentId) {
                RemoveAccentOverrides(resources);
                return;
            }

            Color color = option.GetActualColor();
            foreach (string key in AccentBrushKeys) {
                SetResourceBrush(resources, key, color);
            }

            SetResourceBrush(resources, "VKBackgroundTextHighlightedBrush", color, 0.2);
            SetResourceBrush(resources, "VKButtonPrimaryForegroundBrush", Colors.White);
            SetResourceBrush(resources, "VKCounterPrimaryTextBrush", Colors.White);
        }

        private static void RemoveAccentOverrides() {
            RemoveAccentOverrides(Application.Current.Resources);
        }

        private static void RemoveAccentOverrides(IResourceDictionary resources) {
            foreach (string key in AccentBrushKeys) {
                RemoveResource(resources, key);
            }

            RemoveResource(resources, "VKBackgroundTextHighlightedBrush");
            RemoveResource(resources, "VKButtonPrimaryForegroundBrush");
            RemoveResource(resources, "VKCounterPrimaryTextBrush");
        }

        private static void SetResourceBrush(string key, IBrush brush) {
            SetResourceBrush(Application.Current.Resources, key, brush);
        }

        private static void SetResourceBrush(IResourceDictionary resources, string key, IBrush brush) {
            resources[key] = brush;
        }

        private static void SetResourceBrush(string key, Color color, double opacity = 1) {
            SetResourceBrush(key, new SolidColorBrush(color, opacity));
        }

        private static void SetResourceBrush(IResourceDictionary resources, string key, Color color, double opacity = 1) {
            SetResourceBrush(resources, key, new SolidColorBrush(color, opacity));
        }

        private static void RemoveResource(string key) {
            RemoveResource(Application.Current.Resources, key);
        }

        private static void RemoveResource(IResourceDictionary resources, string key) {
            if (resources.ContainsKey(key)) {
                resources.Remove(key);
            }
        }
    }
}
