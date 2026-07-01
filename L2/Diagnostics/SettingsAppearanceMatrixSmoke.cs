using Avalonia;
using Avalonia.Media;
using ELOR.Laney.Core;
using System;
using System.Collections.Generic;

namespace ELOR.Laney.Diagnostics {
    public static class SettingsAppearanceMatrixSmoke {
        public static SettingsAppearanceMatrixSmokeReport Run(long peerId = 0) {
            long matrixPeerId = peerId != 0 ? peerId : 2000000102;
            List<SettingsAppearanceMatrixCase> cases = BuildCases();
            SettingsAppearanceMatrixSmokeReport report = new SettingsAppearanceMatrixSmokeReport(cases.Count);
            SettingsAppearanceSnapshot snapshot = SettingsAppearanceSnapshot.Capture(matrixPeerId);

            try {
                foreach (SettingsAppearanceMatrixCase item in cases) {
                    report.CasesChecked++;
                    try {
                        ApplyCase(item, matrixPeerId);
                        ValidateCase(item, matrixPeerId, report);
                    } catch (Exception ex) {
                        report.AddFailure(item.Name, "case_exception", $"{ex.GetType().Name}:{ex.InnerException?.Message ?? ex.Message}");
                    }
                }
            } finally {
                snapshot.Restore(matrixPeerId);
                RefreshAppearanceResources();
            }

            return report;
        }

        private static List<SettingsAppearanceMatrixCase> BuildCases() {
            return new List<SettingsAppearanceMatrixCase> {
                new SettingsAppearanceMatrixCase("light_classic_vk") {
                    Theme = 1,
                    Accent = AppearanceManager.DefaultAccentId,
                    AppFont = "Segoe UI",
                    ChatBackground = AppearanceManager.DefaultChatBackgroundId,
                    ChatListDensity = ChatListDensityIds.Medium,
                    ChatListLayout = ChatListLayoutIds.Classic,
                    ChatListAvatarSize = AvatarSizeIds.Auto,
                    ChatListAvatarShape = AvatarShapeIds.Circle,
                    ChatListFontSize = TextSizeIds.Auto,
                    MessageAvatarSize = AvatarSizeIds.Medium,
                    MessageFontSize = TextSizeIds.Medium,
                    BubbleWidth = BubbleWidthIds.Medium,
                    BubbleDensity = BubbleDensityIds.Normal,
                    BubbleStyle = BubbleStyleIds.Vk,
                    BubbleAutoColor = false,
                    CheckmarkStyle = MessageCheckmarkStyleIds.Vk,
                    ChatOpenBehavior = ChatOpenBehaviorIds.Bottom,
                    PeerTheme = AppearanceManager.InheritChatBackgroundId,
                    PeerDensity = AppearanceManager.InheritChatBackgroundId,
                    PeerFont = AppearanceManager.InheritChatBackgroundId,
                    PeerBubbleStyle = AppearanceManager.InheritChatBackgroundId,
                    PeerBubbleColor = AppearanceManager.InheritChatBackgroundId
                },
                new SettingsAppearanceMatrixCase("dark_compact_telegram") {
                    Theme = 2,
                    Accent = "raspberry",
                    AppFont = "Verdana",
                    ChatBackground = "graphite",
                    ChatListDensity = ChatListDensityIds.Small,
                    ChatListLayout = ChatListLayoutIds.Telegram,
                    ChatListAvatarSize = AvatarSizeIds.Small,
                    ChatListAvatarShape = AvatarShapeIds.Squircle,
                    ChatListFontSize = TextSizeIds.Small,
                    MessageAvatarSize = AvatarSizeIds.Small,
                    MessageFontSize = TextSizeIds.Small,
                    BubbleWidth = BubbleWidthIds.Narrow,
                    BubbleDensity = BubbleDensityIds.Compact,
                    BubbleStyle = BubbleStyleIds.Telegram,
                    BubbleAutoColor = true,
                    CheckmarkStyle = MessageCheckmarkStyleIds.Compact,
                    ChatOpenBehavior = ChatOpenBehaviorIds.FirstUnread,
                    PeerTheme = "mint",
                    PeerDensity = BubbleDensityIds.Compact,
                    PeerFont = "small",
                    PeerBubbleStyle = BubbleStyleIds.Telegram,
                    PeerBubbleColor = "mint"
                },
                new SettingsAppearanceMatrixCase("light_media_air") {
                    Theme = 1,
                    Accent = "emerald",
                    AppFont = "Arial",
                    ChatBackground = "paper",
                    ChatListDensity = ChatListDensityIds.Large,
                    ChatListLayout = ChatListLayoutIds.MediaRich,
                    ChatListAvatarSize = AvatarSizeIds.Large,
                    ChatListAvatarShape = AvatarShapeIds.Rounded,
                    ChatListFontSize = TextSizeIds.Large,
                    MessageAvatarSize = AvatarSizeIds.Large,
                    MessageFontSize = TextSizeIds.Large,
                    BubbleWidth = BubbleWidthIds.Wide,
                    BubbleDensity = BubbleDensityIds.Air,
                    BubbleStyle = BubbleStyleIds.Minimal,
                    BubbleAutoColor = true,
                    CheckmarkStyle = MessageCheckmarkStyleIds.Minimal,
                    ChatOpenBehavior = ChatOpenBehaviorIds.Bottom,
                    PeerTheme = AppearanceManager.DefaultChatBackgroundId,
                    PeerDensity = BubbleDensityIds.Air,
                    PeerFont = "large",
                    PeerBubbleStyle = BubbleStyleIds.Minimal,
                    PeerBubbleColor = "amber"
                },
                new SettingsAppearanceMatrixCase("dark_split_outline_peer") {
                    Theme = 2,
                    Accent = "violet",
                    AppFont = "Segoe UI",
                    ChatBackground = "violet",
                    ChatListDensity = ChatListDensityIds.Medium,
                    ChatListLayout = ChatListLayoutIds.SplitFolder,
                    ChatListAvatarSize = AvatarSizeIds.Auto,
                    ChatListAvatarShape = AvatarShapeIds.Square,
                    ChatListFontSize = TextSizeIds.Auto,
                    MessageAvatarSize = AvatarSizeIds.Medium,
                    MessageFontSize = TextSizeIds.Medium,
                    BubbleWidth = BubbleWidthIds.Full,
                    BubbleDensity = BubbleDensityIds.Normal,
                    BubbleStyle = BubbleStyleIds.Outline,
                    BubbleAutoColor = false,
                    CheckmarkStyle = MessageCheckmarkStyleIds.Hidden,
                    ChatOpenBehavior = ChatOpenBehaviorIds.FirstUnread,
                    PeerTheme = "rose",
                    PeerBackgroundDim = 35,
                    PeerBackgroundBrightness = -20,
                    PeerDensity = AppearanceManager.DefaultChatDensityId,
                    PeerFont = AppearanceManager.DefaultChatFontId,
                    PeerBubbleStyle = BubbleStyleIds.Outline,
                    PeerBubbleColor = "rose"
                },
                new SettingsAppearanceMatrixCase("system_flat_legacy_inputs") {
                    Theme = 0,
                    Accent = "amber",
                    AppFont = "Tahoma",
                    ChatBackground = "mint",
                    ChatListDensity = ChatListDensityIds.Small,
                    ChatListLayout = ChatListLayoutIds.Compact,
                    ChatListAvatarSize = AvatarSizeIds.Medium,
                    ChatListAvatarShape = AvatarShapeIds.Circle,
                    ChatListFontSize = TextSizeIds.Medium,
                    MessageAvatarSize = AvatarSizeIds.Small,
                    MessageFontSize = TextSizeIds.Small,
                    BubbleWidth = BubbleWidthIds.Narrow,
                    BubbleDensity = BubbleDensityIds.LegacyRelaxed,
                    BubbleStyle = BubbleStyleIds.LegacySharp,
                    BubbleAutoColor = false,
                    CheckmarkStyle = MessageCheckmarkStyleIds.Vk,
                    ChatOpenBehavior = ChatOpenBehaviorIds.Bottom,
                    PeerTheme = "graphite",
                    PeerDensity = BubbleDensityIds.LegacyRelaxed,
                    PeerFont = "small",
                    PeerBubbleStyle = BubbleStyleIds.LegacyRound,
                    PeerBubbleColor = "graphite"
                },
                new SettingsAppearanceMatrixCase("dark_large_flat_auto") {
                    Theme = 2,
                    Accent = "graphite",
                    AppFont = "Segoe UI Semibold",
                    ChatBackground = "rose",
                    ChatListDensity = ChatListDensityIds.Large,
                    ChatListLayout = ChatListLayoutIds.Classic,
                    ChatListAvatarSize = AvatarSizeIds.Large,
                    ChatListAvatarShape = AvatarShapeIds.Squircle,
                    ChatListFontSize = TextSizeIds.Large,
                    MessageAvatarSize = AvatarSizeIds.Large,
                    MessageFontSize = TextSizeIds.Large,
                    BubbleWidth = BubbleWidthIds.Full,
                    BubbleDensity = BubbleDensityIds.Air,
                    BubbleStyle = BubbleStyleIds.Flat,
                    BubbleAutoColor = true,
                    CheckmarkStyle = MessageCheckmarkStyleIds.Compact,
                    ChatOpenBehavior = ChatOpenBehaviorIds.FirstUnread,
                    PeerTheme = AppearanceManager.InheritChatBackgroundId,
                    PeerDensity = AppearanceManager.InheritChatBackgroundId,
                    PeerFont = AppearanceManager.InheritChatBackgroundId,
                    PeerBubbleStyle = AppearanceManager.InheritChatBackgroundId,
                    PeerBubbleColor = AppearanceManager.DefaultBubbleColorId
                }
            };
        }

        private static void ApplyCase(SettingsAppearanceMatrixCase item, long peerId) {
            Settings.AppTheme = item.Theme;
            Settings.AccentColor = item.Accent;
            Settings.AppFontFamily = item.AppFont;
            Settings.ChatBackground = item.ChatBackground;
            Settings.ChatListDensity = item.ChatListDensity;
            Settings.ChatListLayout = item.ChatListLayout;
            Settings.ChatListAvatarSize = item.ChatListAvatarSize;
            Settings.ChatListAvatarShape = item.ChatListAvatarShape;
            Settings.ChatListFontSize = item.ChatListFontSize;
            Settings.MessageAvatarSize = item.MessageAvatarSize;
            Settings.MessageFontSize = item.MessageFontSize;
            Settings.MessageBubbleWidth = item.BubbleWidth;
            Settings.MessageBubbleDensity = item.BubbleDensity;
            Settings.MessageBubbleStyle = item.BubbleStyle;
            Settings.MessageBubbleAutoColor = item.BubbleAutoColor;
            Settings.MessageCheckmarkStyle = item.CheckmarkStyle;
            Settings.ChatOpenBehavior = item.ChatOpenBehavior;

            Settings.SetPeerLocalTheme(peerId, item.PeerTheme);
            Settings.SetPeerLocalBackgroundDim(peerId, item.PeerBackgroundDim);
            Settings.SetPeerLocalBackgroundBrightness(peerId, item.PeerBackgroundBrightness);
            Settings.SetPeerLocalDensity(peerId, item.PeerDensity);
            Settings.SetPeerLocalFont(peerId, item.PeerFont);
            Settings.SetPeerLocalBubbleStyle(peerId, item.PeerBubbleStyle);
            Settings.SetPeerLocalBubbleColor(peerId, item.PeerBubbleColor);

            RefreshAppearanceResources();
        }

        private static void RefreshAppearanceResources() {
            if (App.Current != null) App.ChangeTheme(Settings.AppTheme);
            if (Application.Current?.Resources != null) AppearanceManager.ApplyAppearanceSettings();
        }

        private static void ValidateCase(SettingsAppearanceMatrixCase item, long peerId, SettingsAppearanceMatrixSmokeReport report) {
            ValidateFiniteRange(item.Name, "MessageTextFontSize", AppearanceManager.GetMessageTextFontSize(peerId), 10, 32, report);
            ValidateFiniteRange(item.Name, "MessageTextLineHeight", AppearanceManager.GetMessageTextLineHeight(peerId), 12, 40, report);
            ValidateFiniteRange(item.Name, "MessageAvatarSize", AppearanceManager.GetMessageAvatarSize(), 20, 64, report);
            ValidateFiniteRange(item.Name, "MessageBubbleMaxWidth", AppearanceManager.GetMessageBubbleMaxWidth(), 320, 1200, report);
            ValidateFiniteRange(item.Name, "ChatListItemHeight2Row", AppearanceManager.GetChatListItemHeight(false), 44, 96, report);
            ValidateFiniteRange(item.Name, "ChatListItemHeight3Row", AppearanceManager.GetChatListItemHeight(true), 52, 108, report);
            ValidateFiniteRange(item.Name, "ChatListAvatarSize", AppearanceManager.GetChatListAvatarSize(), 28, 64, report);
            ValidateFiniteRange(item.Name, "ChatListTitleFontSize", AppearanceManager.GetChatListTitleFontSize(), 11, 22, report);
            ValidateFiniteRange(item.Name, "ChatListSubtitleFontSize", AppearanceManager.GetChatListSubtitleFontSize(), 11, 22, report);
            ValidateFiniteRange(item.Name, "ChatListTimeFontSize", AppearanceManager.GetChatListTimeFontSize(), 10, 20, report);
            ValidateFiniteRange(item.Name, "ChatBackgroundDimOpacity", AppearanceManager.GetChatBackgroundDimOpacity(peerId), 0, 1, report);
            ValidateFiniteRange(item.Name, "ChatBackgroundBrightnessOpacity", AppearanceManager.GetChatBackgroundBrightnessOpacity(peerId), 0, 1, report);

            ValidateThickness(item.Name, "MessageOuterMargin", AppearanceManager.GetMessageOuterMargin(peerId), report);
            ValidateThickness(item.Name, "MessageTextHostMargin", AppearanceManager.GetMessageTextHostMargin(peerId), report);
            ValidateThickness(item.Name, "MessageBubbleBorderThickness", AppearanceManager.GetMessageBubbleBorderThickness(peerId), report);
            ValidateThickness(item.Name, "ChatListAvatarMargin2Row", AppearanceManager.GetChatListAvatarMargin(false), report);
            ValidateThickness(item.Name, "ChatListAvatarMargin3Row", AppearanceManager.GetChatListAvatarMargin(true), report);
            ValidateThickness(item.Name, "ChatListInfoMargin2Row", AppearanceManager.GetChatListInfoMargin(false), report);
            ValidateThickness(item.Name, "ChatListInfoMargin3Row", AppearanceManager.GetChatListInfoMargin(true), report);
            ValidateCornerRadius(item.Name, "MessageBubbleCornerRadius", AppearanceManager.GetMessageBubbleCornerRadius(peerId), report);
            ValidateCornerRadius(item.Name, "ChatListAvatarCornerRadius", AppearanceManager.GetChatListAvatarCornerRadius(), report);
            ValidateBrush(item.Name, "ChatBackgroundBrush", AppearanceManager.GetChatBackgroundBrush(peerId), report);
            ValidateBrush(item.Name, "OutgoingBubbleBrush", AppearanceManager.GetOutgoingBubbleBrush(peerId), report);
            ValidateBrush(item.Name, "MessageBubbleBorderBrush", AppearanceManager.GetMessageBubbleBorderBrush(peerId), report);

            ValidateResource<FontFamily>(item.Name, AppearanceManager.AppFontFamilyResourceKey, report);
            ValidateResource<IBrush>(item.Name, AppearanceManager.ChatBackgroundResourceKey, report);
            ValidateResource<double>(item.Name, AppearanceManager.MessageTextFontSizeResourceKey, report);
            ValidateResource<double>(item.Name, AppearanceManager.MessageTextLineHeightResourceKey, report);
            ValidateResource<double>(item.Name, AppearanceManager.MessageAvatarSizeResourceKey, report);
            ValidateResource<double>(item.Name, AppearanceManager.MessageBubbleMaxWidthResourceKey, report);
            ValidateResource<CornerRadius>(item.Name, AppearanceManager.MessageBubbleCornerRadiusResourceKey, report);
            ValidateResource<Thickness>(item.Name, AppearanceManager.MessageBubbleBorderThicknessResourceKey, report);
            ValidateResource<IBrush>(item.Name, AppearanceManager.MessageBubbleBorderBrushResourceKey, report);
            ValidateResource<double>(item.Name, AppearanceManager.ChatListItemHeight2RowResourceKey, report);
            ValidateResource<double>(item.Name, AppearanceManager.ChatListItemHeight3RowResourceKey, report);
            ValidateResource<double>(item.Name, AppearanceManager.ChatListAvatarSizeResourceKey, report);
            ValidateResource<double>(item.Name, AppearanceManager.ChatListTitleFontSizeResourceKey, report);
            ValidateResource<double>(item.Name, AppearanceManager.ChatListSubtitleFontSizeResourceKey, report);
            ValidateResource<double>(item.Name, AppearanceManager.ChatListTimeFontSizeResourceKey, report);
        }

        private static void ValidateFiniteRange(string caseName, string field, double value, double min, double max, SettingsAppearanceMatrixSmokeReport report) {
            report.ValueChecks++;
            if (Double.IsNaN(value) || Double.IsInfinity(value) || value < min || value > max) {
                report.AddFailure(caseName, field, $"invalid_range:{value}:{min}-{max}");
            }
        }

        private static void ValidateThickness(string caseName, string field, Thickness value, SettingsAppearanceMatrixSmokeReport report) {
            report.ValueChecks++;
            if (!IsFiniteNonNegative(value.Left)
                || !IsFiniteNonNegative(value.Top)
                || !IsFiniteNonNegative(value.Right)
                || !IsFiniteNonNegative(value.Bottom)) {
                report.AddFailure(caseName, field, $"invalid_thickness:{value}");
            }
        }

        private static void ValidateCornerRadius(string caseName, string field, CornerRadius value, SettingsAppearanceMatrixSmokeReport report) {
            report.ValueChecks++;
            if (!IsFiniteNonNegative(value.TopLeft)
                || !IsFiniteNonNegative(value.TopRight)
                || !IsFiniteNonNegative(value.BottomLeft)
                || !IsFiniteNonNegative(value.BottomRight)) {
                report.AddFailure(caseName, field, $"invalid_corner_radius:{value}");
            }
        }

        private static void ValidateBrush(string caseName, string field, IBrush brush, SettingsAppearanceMatrixSmokeReport report) {
            report.ValueChecks++;
            if (brush == null) report.AddFailure(caseName, field, "brush_missing");
        }

        private static void ValidateResource<T>(string caseName, string key, SettingsAppearanceMatrixSmokeReport report) {
            if (Application.Current == null) return;

            report.ResourceChecks++;
            if (Application.Current.TryGetResource(key, null, out object value) != true) {
                report.AddFailure(caseName, key, "resource_missing");
                return;
            }

            if (value is not T) report.AddFailure(caseName, key, $"resource_type:{value?.GetType().Name ?? "null"}!={typeof(T).Name}");
        }

        private static bool IsFiniteNonNegative(double value) {
            return !Double.IsNaN(value) && !Double.IsInfinity(value) && value >= 0;
        }
    }

    public sealed class SettingsAppearanceMatrixSmokeReport {
        private readonly List<SettingsAppearanceMatrixSmokeIssue> _issues = new List<SettingsAppearanceMatrixSmokeIssue>();

        public int CasesTotal { get; }
        public int CasesChecked { get; set; }
        public int ValueChecks { get; set; }
        public int ResourceChecks { get; set; }
        public int FailedChecks { get; private set; }
        public IReadOnlyList<SettingsAppearanceMatrixSmokeIssue> Issues => _issues;
        public bool Passed => FailedChecks == 0;

        public SettingsAppearanceMatrixSmokeReport(int casesTotal) {
            CasesTotal = casesTotal;
        }

        public void AddFailure(string caseName, string field, string reason) {
            FailedChecks++;
            _issues.Add(new SettingsAppearanceMatrixSmokeIssue(caseName, field, reason));
        }
    }

    public sealed class SettingsAppearanceMatrixSmokeIssue {
        public string CaseName { get; }
        public string Field { get; }
        public string Reason { get; }

        public SettingsAppearanceMatrixSmokeIssue(string caseName, string field, string reason) {
            CaseName = caseName;
            Field = field;
            Reason = reason;
        }
    }

    internal sealed class SettingsAppearanceMatrixCase {
        public string Name { get; }
        public int Theme { get; set; }
        public string Accent { get; set; }
        public string AppFont { get; set; }
        public string ChatBackground { get; set; }
        public string ChatListDensity { get; set; }
        public string ChatListLayout { get; set; }
        public string ChatListAvatarSize { get; set; }
        public string ChatListAvatarShape { get; set; }
        public string ChatListFontSize { get; set; }
        public string MessageAvatarSize { get; set; }
        public string MessageFontSize { get; set; }
        public string BubbleWidth { get; set; }
        public string BubbleDensity { get; set; }
        public string BubbleStyle { get; set; }
        public bool BubbleAutoColor { get; set; }
        public string CheckmarkStyle { get; set; }
        public string ChatOpenBehavior { get; set; }
        public string PeerTheme { get; set; }
        public int PeerBackgroundDim { get; set; }
        public int PeerBackgroundBrightness { get; set; }
        public string PeerDensity { get; set; }
        public string PeerFont { get; set; }
        public string PeerBubbleStyle { get; set; }
        public string PeerBubbleColor { get; set; }

        public SettingsAppearanceMatrixCase(string name) {
            Name = name;
        }
    }

    internal sealed class SettingsAppearanceSnapshot {
        private int Theme { get; set; }
        private string Accent { get; set; }
        private string AppFont { get; set; }
        private string ChatBackground { get; set; }
        private string ChatListDensity { get; set; }
        private string ChatListLayout { get; set; }
        private string ChatListAvatarSize { get; set; }
        private string ChatListAvatarShape { get; set; }
        private string ChatListFontSize { get; set; }
        private string MessageAvatarSize { get; set; }
        private string MessageFontSize { get; set; }
        private string BubbleWidth { get; set; }
        private string BubbleDensity { get; set; }
        private string BubbleStyle { get; set; }
        private bool BubbleAutoColor { get; set; }
        private string CheckmarkStyle { get; set; }
        private string ChatOpenBehavior { get; set; }
        private string PeerTheme { get; set; }
        private int PeerBackgroundDim { get; set; }
        private int PeerBackgroundBrightness { get; set; }
        private string PeerDensity { get; set; }
        private string PeerFont { get; set; }
        private string PeerBubbleStyle { get; set; }
        private string PeerBubbleColor { get; set; }

        public static SettingsAppearanceSnapshot Capture(long peerId) {
            return new SettingsAppearanceSnapshot {
                Theme = Settings.AppTheme,
                Accent = Settings.AccentColor,
                AppFont = Settings.AppFontFamily,
                ChatBackground = Settings.ChatBackground,
                ChatListDensity = Settings.ChatListDensity,
                ChatListLayout = Settings.ChatListLayout,
                ChatListAvatarSize = Settings.ChatListAvatarSize,
                ChatListAvatarShape = Settings.ChatListAvatarShape,
                ChatListFontSize = Settings.ChatListFontSize,
                MessageAvatarSize = Settings.MessageAvatarSize,
                MessageFontSize = Settings.MessageFontSize,
                BubbleWidth = Settings.MessageBubbleWidth,
                BubbleDensity = Settings.MessageBubbleDensity,
                BubbleStyle = Settings.MessageBubbleStyle,
                BubbleAutoColor = Settings.MessageBubbleAutoColor,
                CheckmarkStyle = Settings.MessageCheckmarkStyle,
                ChatOpenBehavior = Settings.ChatOpenBehavior,
                PeerTheme = Settings.GetPeerLocalTheme(peerId),
                PeerBackgroundDim = Settings.GetPeerLocalBackgroundDim(peerId),
                PeerBackgroundBrightness = Settings.GetPeerLocalBackgroundBrightness(peerId),
                PeerDensity = Settings.GetPeerLocalDensity(peerId),
                PeerFont = Settings.GetPeerLocalFont(peerId),
                PeerBubbleStyle = Settings.GetPeerLocalBubbleStyle(peerId),
                PeerBubbleColor = Settings.GetPeerLocalBubbleColor(peerId)
            };
        }

        public void Restore(long peerId) {
            Settings.AppTheme = Theme;
            Settings.AccentColor = Accent;
            Settings.AppFontFamily = AppFont;
            Settings.ChatBackground = ChatBackground;
            Settings.ChatListDensity = ChatListDensity;
            Settings.ChatListLayout = ChatListLayout;
            Settings.ChatListAvatarSize = ChatListAvatarSize;
            Settings.ChatListAvatarShape = ChatListAvatarShape;
            Settings.ChatListFontSize = ChatListFontSize;
            Settings.MessageAvatarSize = MessageAvatarSize;
            Settings.MessageFontSize = MessageFontSize;
            Settings.MessageBubbleWidth = BubbleWidth;
            Settings.MessageBubbleDensity = BubbleDensity;
            Settings.MessageBubbleStyle = BubbleStyle;
            Settings.MessageBubbleAutoColor = BubbleAutoColor;
            Settings.MessageCheckmarkStyle = CheckmarkStyle;
            Settings.ChatOpenBehavior = ChatOpenBehavior;
            Settings.SetPeerLocalTheme(peerId, PeerTheme);
            Settings.SetPeerLocalBackgroundDim(peerId, PeerBackgroundDim);
            Settings.SetPeerLocalBackgroundBrightness(peerId, PeerBackgroundBrightness);
            Settings.SetPeerLocalDensity(peerId, PeerDensity);
            Settings.SetPeerLocalFont(peerId, PeerFont);
            Settings.SetPeerLocalBubbleStyle(peerId, PeerBubbleStyle);
            Settings.SetPeerLocalBubbleColor(peerId, PeerBubbleColor);
        }
    }
}
