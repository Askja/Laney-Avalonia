using ELOR.Laney.Core;
using ELOR.Laney.Core.Localization;
using ELOR.VKAPILib;
using System;

namespace ELOR.Laney.ViewModels.SettingsCategories {
    public sealed class NetworkViewModel : CommonViewModel {
        public bool LowTrafficMode { get { return Settings.LowTrafficMode; } set { Settings.LowTrafficMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(GroupsBackgroundLongPollLimitText)); } }
        public string GroupsBackgroundLongPollLimitText { get { return Settings.GroupsBackgroundLongPollLimit.ToString(); } set { SetNonNegativeInt(value, v => Settings.GroupsBackgroundLongPollLimit = v, nameof(GroupsBackgroundLongPollLimitText)); } }
        public bool EnableLongPollLogs { get { return Settings.EnableLongPollLogs; } set { Settings.EnableLongPollLogs = value; OnPropertyChanged(); } }
        public bool LNetLogs { get { return Settings.LNetLogs; } set { Settings.LNetLogs = value; OnPropertyChanged(); } }
        public bool ApiDebugMonitorEnabled { get { return Settings.ApiDebugMonitorEnabled; } set { Settings.ApiDebugMonitorEnabled = value; OnPropertyChanged(); } }
        public bool ProxyEnabled { get { return Settings.ProxyEnabled; } set { Settings.ProxyEnabled = value; OnPropertyChanged(); } }
        public string ProxyUri { get { return Settings.ProxyUri; } set { Settings.ProxyUri = value; OnPropertyChanged(); } }
        public bool ProxyBypassLocal { get { return Settings.ProxyBypassLocal; } set { Settings.ProxyBypassLocal = value; OnPropertyChanged(); } }
        public string ApiSectionTitle { get { return Localizer.Get("settings_api_section"); } }
        public string ApiDomainTitle { get { return Localizer.Get("settings_api_domain"); } }
        public string ApiDomainSubtitle { get { return Localizer.Get("settings_api_domain_desc"); } }
        public string ApiVersionTitle { get { return Localizer.Get("settings_api_version"); } }
        public string ApiVersionSubtitle { get { return Localizer.Get("settings_api_version_desc"); } }
        public string ApiResetTitle { get { return Localizer.Get("settings_api_reset"); } }
        public string ApiResetSubtitle { get { return Localizer.Get("settings_api_reset_desc"); } }
        public string ApiDomain {
            get { return Settings.ApiDomain; }
            set {
                Settings.ApiDomain = value;
                OnPropertyChanged();
            }
        }

        public string ApiVersion {
            get { return Settings.ApiVersion; }
            set {
                Settings.ApiVersion = value;
                OnPropertyChanged();
            }
        }

        public void ResetApiDefaults() {
            Settings.ApiDomain = VKAPI.DefaultDomain;
            Settings.ApiVersion = VKAPI.BundledVersion;
            OnPropertyChanged(nameof(ApiDomain));
            OnPropertyChanged(nameof(ApiVersion));
        }

        private void SetNonNegativeInt(string value, Action<int> setter, string propertyName) {
            if (!Int32.TryParse(value, out int parsed)) return;
            setter(Math.Max(0, parsed));
            OnPropertyChanged(propertyName);
        }
    }
}
