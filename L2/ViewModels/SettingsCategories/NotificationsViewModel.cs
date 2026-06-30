using ELOR.Laney.Core;
using ELOR.Laney.Core.Localization;
using ELOR.Laney.DataModels;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace ELOR.Laney.ViewModels.SettingsCategories {
    public class NotificationsViewModel : CommonViewModel {
        public ObservableCollection<TwoStringTuple> Hours { get; private set; } = BuildHours();
        public ObservableCollection<TwoStringTuple> AutoStatuses { get; private set; } = BuildAutoStatuses();
        public ObservableCollection<TwoStringTuple> IdleMinuteOptions { get; private set; } = BuildIdleMinuteOptions();
        public ObservableCollection<TwoStringTuple> DeliveryModes { get; private set; } = BuildDeliveryModes();
        public ObservableCollection<TwoStringTuple> Positions { get; private set; } = BuildPositions();

        public bool Private { get { return Settings.NotificationsPrivate; } set { Settings.NotificationsPrivate = value; OnPropertyChanged(); } }
        public bool PrivateSound { get { return Settings.NotificationsPrivateSound; } set { Settings.NotificationsPrivateSound = value; OnPropertyChanged(); } }
        public bool GroupChat { get { return Settings.NotificationsGroupChat; } set { Settings.NotificationsGroupChat = value; OnPropertyChanged(); } }
        public bool GroupChatSound { get { return Settings.NotificationsGroupChatSound; } set { Settings.NotificationsGroupChatSound = value; OnPropertyChanged(); } }
        public bool DontAnnoyMeMode { get { return Settings.DontAnnoyMeMode; } set { Settings.DontAnnoyMeMode = value; OnPropertyChanged(); } }
        public bool DontAnnoyMeAllowMentions { get { return Settings.DontAnnoyMeAllowMentions; } set { Settings.DontAnnoyMeAllowMentions = value; OnPropertyChanged(); } }
        public bool DontAnnoyMeAllowImportant { get { return Settings.DontAnnoyMeAllowImportant; } set { Settings.DontAnnoyMeAllowImportant = value; OnPropertyChanged(); } }
        public string DontAnnoyMeKeywords { get { return Settings.DontAnnoyMeKeywords; } set { Settings.DontAnnoyMeKeywords = value; OnPropertyChanged(); } }
        public TwoStringTuple CurrentDontAnnoyMeStartHour { get { return GetHour(Settings.DontAnnoyMeStartHour); } set { ChangeStartHour(value); OnPropertyChanged(); } }
        public TwoStringTuple CurrentDontAnnoyMeEndHour { get { return GetHour(Settings.DontAnnoyMeEndHour); } set { ChangeEndHour(value); OnPropertyChanged(); } }
        public bool AutoStatusEnabled { get { return Settings.AutoStatusEnabled; } set { Settings.AutoStatusEnabled = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanConfigureAutoStatusSchedule)); OnPropertyChanged(nameof(CanConfigureAutoStatusIdle)); } }
        public TwoStringTuple CurrentAutoStatusMode { get { return GetAutoStatus(Settings.AutoStatusMode); } set { ChangeAutoStatusMode(value); OnPropertyChanged(); } }
        public bool AutoStatusScheduleEnabled { get { return Settings.AutoStatusScheduleEnabled; } set { Settings.AutoStatusScheduleEnabled = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanConfigureAutoStatusSchedule)); } }
        public TwoStringTuple CurrentAutoStatusScheduleStartHour { get { return GetHour(Settings.AutoStatusScheduleStartHour); } set { ChangeAutoStatusScheduleStartHour(value); OnPropertyChanged(); } }
        public TwoStringTuple CurrentAutoStatusScheduleEndHour { get { return GetHour(Settings.AutoStatusScheduleEndHour); } set { ChangeAutoStatusScheduleEndHour(value); OnPropertyChanged(); } }
        public TwoStringTuple CurrentAutoStatusScheduleMode { get { return GetAutoStatus(Settings.AutoStatusScheduleMode); } set { ChangeAutoStatusScheduleMode(value); OnPropertyChanged(); } }
        public bool AutoStatusIdleEnabled { get { return Settings.AutoStatusIdleEnabled; } set { Settings.AutoStatusIdleEnabled = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanConfigureAutoStatusIdle)); } }
        public TwoStringTuple CurrentAutoStatusIdleMinutes { get { return GetIdleMinutes(Settings.AutoStatusIdleMinutes); } set { ChangeAutoStatusIdleMinutes(value); OnPropertyChanged(); } }
        public TwoStringTuple CurrentAutoStatusIdleMode { get { return GetAutoStatus(Settings.AutoStatusIdleMode); } set { ChangeAutoStatusIdleMode(value); OnPropertyChanged(); } }
        public bool CanConfigureAutoStatusSchedule { get { return AutoStatusEnabled && AutoStatusScheduleEnabled; } }
        public bool CanConfigureAutoStatusIdle { get { return AutoStatusEnabled && AutoStatusIdleEnabled; } }
        public string StyleSectionTitle { get { return Localizer.Get("settings_notifications_style_section"); } }
        public string DeliveryModeTitle { get { return Localizer.Get("settings_notifications_delivery"); } }
        public string DeliveryModeSubtitle { get { return Localizer.Get("settings_notifications_delivery_desc"); } }
        public string PositionTitle { get { return Localizer.Get("settings_notifications_position"); } }
        public string PositionSubtitle { get { return Localizer.Get("settings_notifications_position_desc"); } }
        public string StackLimitTitle { get { return Localizer.Get("settings_notifications_stack_limit"); } }
        public string StackLimitSubtitle { get { return Localizer.Get("settings_notifications_stack_limit_desc"); } }
        public string TimeoutTitle { get { return Localizer.Get("settings_notifications_timeout"); } }
        public string TimeoutSubtitle { get { return Localizer.Get("settings_notifications_timeout_desc"); } }
        public string FastActionsTitle { get { return Localizer.Get("settings_notifications_fast_actions"); } }
        public string FastActionsSubtitle { get { return Localizer.Get("settings_notifications_fast_actions_desc"); } }
        public string ShowAvatarsTitle { get { return Localizer.Get("settings_notifications_show_avatars"); } }
        public string ShowAvatarsSubtitle { get { return Localizer.Get("settings_notifications_show_avatars_desc"); } }
        public string ShowImagesTitle { get { return Localizer.Get("settings_notifications_show_images"); } }
        public string ShowImagesSubtitle { get { return Localizer.Get("settings_notifications_show_images_desc"); } }
        public TwoStringTuple CurrentDeliveryMode { get { return GetDeliveryMode(Settings.NotificationDeliveryMode); } set { ChangeDeliveryMode(value); OnPropertyChanged(); } }
        public TwoStringTuple CurrentPosition { get { return GetPosition(Settings.CustomNotificationPosition); } set { ChangePosition(value); OnPropertyChanged(); } }
        public string CustomNotificationStackLimitText { get { return Settings.CustomNotificationStackLimit.ToString(); } set { SetRangeInt(value, 1, 10, v => Settings.CustomNotificationStackLimit = v, nameof(CustomNotificationStackLimitText)); } }
        public string CustomNotificationTimeoutText { get { return Settings.CustomNotificationTimeoutSeconds.ToString(); } set { SetRangeInt(value, 2, 60, v => Settings.CustomNotificationTimeoutSeconds = v, nameof(CustomNotificationTimeoutText)); } }
        public bool CustomNotificationFastActions { get { return Settings.CustomNotificationFastActions; } set { Settings.CustomNotificationFastActions = value; OnPropertyChanged(); } }
        public bool CustomNotificationShowAvatars { get { return Settings.CustomNotificationShowAvatars; } set { Settings.CustomNotificationShowAvatars = value; OnPropertyChanged(); } }
        public bool CustomNotificationShowImages { get { return Settings.CustomNotificationShowImages; } set { Settings.CustomNotificationShowImages = value; OnPropertyChanged(); } }

        public NotificationsViewModel() { }

        private static ObservableCollection<TwoStringTuple> BuildHours() {
            ObservableCollection<TwoStringTuple> hours = new ObservableCollection<TwoStringTuple>();
            for (int hour = 0; hour < 24; hour++) {
                string value = hour.ToString();
                hours.Add(new TwoStringTuple(value, $"{hour:00}:00"));
            }

            return hours;
        }

        private static ObservableCollection<TwoStringTuple> BuildAutoStatuses() {
            return new ObservableCollection<TwoStringTuple>(
                AutoStatusManager.Modes.Select(mode => new TwoStringTuple(mode, AutoStatusManager.GetTitle(mode))));
        }

        private static ObservableCollection<TwoStringTuple> BuildIdleMinuteOptions() {
            return new ObservableCollection<TwoStringTuple> {
                new TwoStringTuple("5", "5 мин"),
                new TwoStringTuple("10", "10 мин"),
                new TwoStringTuple("15", "15 мин"),
                new TwoStringTuple("30", "30 мин"),
                new TwoStringTuple("60", "1 час")
            };
        }

        private static ObservableCollection<TwoStringTuple> BuildDeliveryModes() {
            return new ObservableCollection<TwoStringTuple> {
                new TwoStringTuple(NotificationDeliveryModeIds.Custom, Localizer.Get("settings_notifications_delivery_custom")),
                new TwoStringTuple(NotificationDeliveryModeIds.System, Localizer.Get("settings_notifications_delivery_system")),
                new TwoStringTuple(NotificationDeliveryModeIds.Both, Localizer.Get("settings_notifications_delivery_both"))
            };
        }

        private static ObservableCollection<TwoStringTuple> BuildPositions() {
            return new ObservableCollection<TwoStringTuple> {
                new TwoStringTuple(NotificationPositionIds.BottomRight, Localizer.Get("settings_notifications_position_bottom_right")),
                new TwoStringTuple(NotificationPositionIds.BottomLeft, Localizer.Get("settings_notifications_position_bottom_left")),
                new TwoStringTuple(NotificationPositionIds.TopRight, Localizer.Get("settings_notifications_position_top_right")),
                new TwoStringTuple(NotificationPositionIds.TopLeft, Localizer.Get("settings_notifications_position_top_left"))
            };
        }

        private TwoStringTuple GetHour(int hour) {
            string id = hour.ToString();
            return Hours.Where(h => h.Item1 == id).FirstOrDefault();
        }

        private TwoStringTuple GetAutoStatus(string mode) {
            string id = AutoStatusModeIds.Normalize(mode);
            return AutoStatuses.Where(s => s.Item1 == id).FirstOrDefault();
        }

        private TwoStringTuple GetIdleMinutes(int minutes) {
            string id = minutes.ToString();
            return IdleMinuteOptions.Where(m => m.Item1 == id).FirstOrDefault() ?? IdleMinuteOptions.FirstOrDefault();
        }

        private TwoStringTuple GetDeliveryMode(string mode) {
            string id = NotificationDeliveryModeIds.Normalize(mode);
            return DeliveryModes.Where(m => m.Item1 == id).FirstOrDefault() ?? DeliveryModes.FirstOrDefault();
        }

        private TwoStringTuple GetPosition(string position) {
            string id = NotificationPositionIds.Normalize(position);
            return Positions.Where(p => p.Item1 == id).FirstOrDefault() ?? Positions.FirstOrDefault();
        }

        private void ChangeStartHour(TwoStringTuple value) {
            if (value == null) return;
            Settings.DontAnnoyMeStartHour = int.Parse(value.Item1);
            OnPropertyChanged(nameof(CurrentDontAnnoyMeStartHour));
        }

        private void ChangeEndHour(TwoStringTuple value) {
            if (value == null) return;
            Settings.DontAnnoyMeEndHour = int.Parse(value.Item1);
            OnPropertyChanged(nameof(CurrentDontAnnoyMeEndHour));
        }

        private void ChangeAutoStatusMode(TwoStringTuple value) {
            if (value == null) return;
            Settings.AutoStatusMode = value.Item1;
            OnPropertyChanged(nameof(CurrentAutoStatusMode));
        }

        private void ChangeAutoStatusScheduleStartHour(TwoStringTuple value) {
            if (value == null) return;
            Settings.AutoStatusScheduleStartHour = int.Parse(value.Item1);
            OnPropertyChanged(nameof(CurrentAutoStatusScheduleStartHour));
        }

        private void ChangeAutoStatusScheduleEndHour(TwoStringTuple value) {
            if (value == null) return;
            Settings.AutoStatusScheduleEndHour = int.Parse(value.Item1);
            OnPropertyChanged(nameof(CurrentAutoStatusScheduleEndHour));
        }

        private void ChangeAutoStatusScheduleMode(TwoStringTuple value) {
            if (value == null) return;
            Settings.AutoStatusScheduleMode = value.Item1;
            OnPropertyChanged(nameof(CurrentAutoStatusScheduleMode));
        }

        private void ChangeAutoStatusIdleMinutes(TwoStringTuple value) {
            if (value == null) return;
            Settings.AutoStatusIdleMinutes = int.Parse(value.Item1);
            OnPropertyChanged(nameof(CurrentAutoStatusIdleMinutes));
        }

        private void ChangeAutoStatusIdleMode(TwoStringTuple value) {
            if (value == null) return;
            Settings.AutoStatusIdleMode = value.Item1;
            OnPropertyChanged(nameof(CurrentAutoStatusIdleMode));
        }

        private void ChangeDeliveryMode(TwoStringTuple value) {
            if (value == null) return;
            Settings.NotificationDeliveryMode = value.Item1;
            OnPropertyChanged(nameof(CurrentDeliveryMode));
        }

        private void ChangePosition(TwoStringTuple value) {
            if (value == null) return;
            Settings.CustomNotificationPosition = value.Item1;
            OnPropertyChanged(nameof(CurrentPosition));
        }

        private void SetRangeInt(string value, int min, int max, Action<int> setter, string propertyName) {
            if (!Int32.TryParse(value, out int parsed)) return;
            setter(Math.Clamp(parsed, min, max));
            OnPropertyChanged(propertyName);
        }
    }
}
