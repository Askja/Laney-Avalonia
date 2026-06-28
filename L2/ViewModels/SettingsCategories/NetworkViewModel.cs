using ELOR.Laney.Core;
using System;

namespace ELOR.Laney.ViewModels.SettingsCategories {
    public sealed class NetworkViewModel : CommonViewModel {
        public bool LowTrafficMode { get { return Settings.LowTrafficMode; } set { Settings.LowTrafficMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(GroupsBackgroundLongPollLimitText)); } }
        public string GroupsBackgroundLongPollLimitText { get { return Settings.GroupsBackgroundLongPollLimit.ToString(); } set { SetNonNegativeInt(value, v => Settings.GroupsBackgroundLongPollLimit = v, nameof(GroupsBackgroundLongPollLimitText)); } }
        public bool EnableLongPollLogs { get { return Settings.EnableLongPollLogs; } set { Settings.EnableLongPollLogs = value; OnPropertyChanged(); } }
        public bool LNetLogs { get { return Settings.LNetLogs; } set { Settings.LNetLogs = value; OnPropertyChanged(); } }
        public bool ApiDebugMonitorEnabled { get { return Settings.ApiDebugMonitorEnabled; } set { Settings.ApiDebugMonitorEnabled = value; OnPropertyChanged(); } }

        private void SetNonNegativeInt(string value, Action<int> setter, string propertyName) {
            if (!Int32.TryParse(value, out int parsed)) return;
            setter(Math.Max(0, parsed));
            OnPropertyChanged(propertyName);
        }
    }
}
