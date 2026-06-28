using ELOR.Laney.Core;
using ELOR.Laney.DataModels;
using System.Collections.ObjectModel;
using System.Linq;

namespace ELOR.Laney.ViewModels.SettingsCategories {
    public class NotificationsViewModel : CommonViewModel {
        public ObservableCollection<TwoStringTuple> Hours { get; private set; } = BuildHours();
        public ObservableCollection<TwoStringTuple> AutoStatuses { get; private set; } = BuildAutoStatuses();
        public ObservableCollection<TwoStringTuple> IdleMinuteOptions { get; private set; } = BuildIdleMinuteOptions();

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
    }
}
