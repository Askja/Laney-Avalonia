using Avalonia.Controls;
using ELOR.Laney.Core;
using ELOR.Laney.DataModels;
using ELOR.Laney.Helpers;
using ELOR.VKAPILib.Objects;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using VKUI.Controls;

namespace ELOR.Laney.ViewModels.SettingsCategories {
    public sealed class PrivacyViewModel : CommonViewModel {
        private readonly VKSession _session;
        private RelayCommand _panicLockCommand;

        public ObservableCollection<PrivacySectionViewModel> Sections { get; } = new ObservableCollection<PrivacySectionViewModel>();
        public ObservableCollection<TwoStringTuple> AutoLockMinuteOptions { get; } = new ObservableCollection<TwoStringTuple> {
            new TwoStringTuple("1", "1 мин"),
            new TwoStringTuple("5", "5 мин"),
            new TwoStringTuple("10", "10 мин"),
            new TwoStringTuple("15", "15 мин"),
            new TwoStringTuple("30", "30 мин"),
            new TwoStringTuple("60", "1 час")
        };

        public bool HasSections { get { return Sections.Count > 0; } }
        public bool StreamerMode {
            get { return Settings.StreamerMode; }
            set {
                if (Settings.StreamerMode == value) return;

                Settings.StreamerMode = value;
                OnPropertyChanged();
            }
        }

        public bool AutoBlockBlacklistedUsers {
            get { return Settings.AutoBlockBlacklistedUsers; }
            set {
                if (Settings.AutoBlockBlacklistedUsers == value) return;

                Settings.AutoBlockBlacklistedUsers = value;
                OnPropertyChanged();
                ReloadOpenedChat();
            }
        }

        public bool AutoLockEnabled {
            get { return Settings.AutoLockEnabled; }
            set {
                if (Settings.AutoLockEnabled == value) return;

                Settings.AutoLockEnabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanConfigureAutoLock));
            }
        }

        public bool CanConfigureAutoLock { get { return AutoLockEnabled; } }
        public bool PanicLockClearClipboard { get { return Settings.PanicLockClearClipboard; } set { Settings.PanicLockClearClipboard = value; OnPropertyChanged(); } }
        public bool AutoLockClearClipboard { get { return Settings.AutoLockClearClipboard; } set { Settings.AutoLockClearClipboard = value; OnPropertyChanged(); } }
        public string KeymapPanicLock { get { return Settings.KeymapPanicLock; } set { Settings.KeymapPanicLock = value; OnPropertyChanged(); } }
        public RelayCommand PanicLockCommand { get { return _panicLockCommand; } private set { _panicLockCommand = value; OnPropertyChanged(); } }

        public TwoStringTuple CurrentAutoLockIdleMinutes {
            get { return GetAutoLockMinutes(Settings.AutoLockIdleMinutes); }
            set {
                ChangeAutoLockMinutes(value);
                OnPropertyChanged();
            }
        }

        public PrivacyViewModel() {
            _session = VKSession.Main;
            PanicLockCommand = new RelayCommand((o) => new System.Action(async () => await PanicLockAsync())());
            new System.Action(async () => await LoadAsync())();
        }

        private async Task PanicLockAsync() {
            if (_session?.Window == null) return;
            await _session.Window.PanicLockAsync();
        }

        public async Task LoadAsync() {
            if (IsLoading) return;

            IsLoading = true;
            Placeholder = null;
            Sections.Clear();
            OnPropertyChanged(nameof(HasSections));

            try {
                if (DemoMode.IsEnabled || _session?.API == null) {
                    return;
                }

                PrivacyResponse response = await _session.API.Account.GetPrivacySettingsAsync();
                Dictionary<string, PrivacySection> sectionsByName = response.Sections?
                    .Where(s => !String.IsNullOrWhiteSpace(s.Name))
                    .GroupBy(s => s.Name)
                    .ToDictionary(g => g.Key, g => g.First()) ?? new Dictionary<string, PrivacySection>();
                Dictionary<string, PrivacyCategory> categoriesByValue = response.SupportedCategories?
                    .Where(c => !String.IsNullOrWhiteSpace(c.Value))
                    .GroupBy(c => c.Value)
                    .ToDictionary(g => g.Key, g => g.First()) ?? new Dictionary<string, PrivacyCategory>();

                foreach (var group in (response.Settings ?? new List<PrivacySetting>()).GroupBy(s => s.Section)) {
                    sectionsByName.TryGetValue(group.Key ?? String.Empty, out PrivacySection section);
                    PrivacySectionViewModel sectionVm = new PrivacySectionViewModel(section?.Title ?? group.Key ?? Assets.i18n.Resources.settings_privacy, section?.Description);

                    foreach (PrivacySetting setting in group) {
                        sectionVm.Settings.Add(new PrivacySettingViewModel(_session, setting, BuildOptions(setting, response.SupportedCategories, categoriesByValue)));
                    }

                    Sections.Add(sectionVm);
                }

                if (Sections.Count == 0) {
                    Placeholder = new PlaceholderViewModel {
                        Icon = new VKIcon { Id = VKIconNames.Icon56InfoOutline },
                        Header = Assets.i18n.Resources.settings_privacy,
                        Text = Assets.i18n.Resources.privacy_no_settings
                    };
                }
            } catch (Exception ex) {
                Log.Error(ex, "Cannot load privacy settings.");
                Placeholder = PlaceholderViewModel.GetForException(ex, async (o) => await LoadAsync());
            } finally {
                IsLoading = false;
                OnPropertyChanged(nameof(HasSections));
            }
        }

        private static ObservableCollection<TwoStringTuple> BuildOptions(PrivacySetting setting, List<PrivacyCategory> allCategories, Dictionary<string, PrivacyCategory> categoriesByValue) {
            ObservableCollection<TwoStringTuple> options = new ObservableCollection<TwoStringTuple>();
            if (setting.Type == PrivacySettingValueType.Binary) return options;

            IEnumerable<string> values = setting.SupportedCategories;
            if (values == null || !values.Any()) values = allCategories?.Select(c => c.Value) ?? Enumerable.Empty<string>();

            foreach (string value in values.Where(v => !String.IsNullOrWhiteSpace(v)).Distinct()) {
                string title = categoriesByValue.TryGetValue(value, out PrivacyCategory category) ? category.Title : value;
                options.Add(new TwoStringTuple(value, title));
            }

            string current = setting.Value?.Category;
            if (!String.IsNullOrWhiteSpace(current) && !options.Any(o => o.Item1 == current)) {
                string title = categoriesByValue.TryGetValue(current, out PrivacyCategory category) ? category.Title : current;
                options.Add(new TwoStringTuple(current, title));
            }

            return options;
        }

        private TwoStringTuple GetAutoLockMinutes(int minutes) {
            string id = minutes.ToString();
            return AutoLockMinuteOptions.Where(m => m.Item1 == id).FirstOrDefault() ?? AutoLockMinuteOptions.FirstOrDefault();
        }

        private void ChangeAutoLockMinutes(TwoStringTuple value) {
            if (value == null) return;

            Settings.AutoLockIdleMinutes = int.Parse(value.Item1);
            OnPropertyChanged(nameof(CurrentAutoLockIdleMinutes));
        }

        private void ReloadOpenedChat() {
            if (_session?.CurrentOpenedChat == null) return;
            new System.Action(async () => await _session.CurrentOpenedChat.ReloadMessagesAsync())();
        }
    }

    public sealed class PrivacySectionViewModel : ViewModelBase {
        public string Title { get; }
        public string Description { get; }
        public bool HasDescription { get { return !String.IsNullOrWhiteSpace(Description); } }
        public ObservableCollection<PrivacySettingViewModel> Settings { get; } = new ObservableCollection<PrivacySettingViewModel>();

        public PrivacySectionViewModel(string title, string description) {
            Title = title;
            Description = description;
        }
    }

    public sealed class PrivacySettingViewModel : ViewModelBase {
        private readonly VKSession _session;
        private bool _isApplyingValue;
        private bool _isSaving;
        private bool _isEnabledValue;
        private string _errorText;
        private TwoStringTuple _currentOption;

        public string Key { get; }
        public string Title { get; }
        public PrivacySettingValueType Type { get; }
        public ObservableCollection<TwoStringTuple> Options { get; }

        public bool IsBinary { get { return Type == PrivacySettingValueType.Binary; } }
        public bool IsList { get { return Type == PrivacySettingValueType.List; } }
        public bool IsSaving { get { return _isSaving; } private set { _isSaving = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanEdit)); } }
        public bool CanEdit { get { return !IsSaving && (IsBinary || Options.Count > 0); } }
        public string Subtitle { get { return ErrorText ?? Key; } }
        public string ErrorText { get { return _errorText; } private set { _errorText = value; OnPropertyChanged(); OnPropertyChanged(nameof(Subtitle)); } }

        public bool IsEnabledValue {
            get { return _isEnabledValue; }
            set {
                if (_isEnabledValue == value) return;

                bool oldValue = _isEnabledValue;
                _isEnabledValue = value;
                OnPropertyChanged();

                if (_isApplyingValue) return;
                new System.Action(async () => await SaveBinaryAsync(value, oldValue))();
            }
        }

        public TwoStringTuple CurrentOption {
            get { return _currentOption; }
            set {
                if (value == null || _currentOption?.Item1 == value.Item1) return;

                TwoStringTuple oldValue = _currentOption;
                _currentOption = value;
                OnPropertyChanged();

                if (_isApplyingValue) return;
                new System.Action(async () => await SaveListAsync(value, oldValue))();
            }
        }

        public PrivacySettingViewModel(VKSession session, PrivacySetting setting, ObservableCollection<TwoStringTuple> options) {
            _session = session;
            Key = setting.Key;
            Title = setting.Title;
            Type = setting.Type;
            Options = options ?? new ObservableCollection<TwoStringTuple>();
            ApplyValue(setting.Value);
        }

        private async Task SaveBinaryAsync(bool value, bool oldValue) {
            await SaveAsync(value ? "1" : "0", oldValue, null);
        }

        private async Task SaveListAsync(TwoStringTuple value, TwoStringTuple oldValue) {
            await SaveAsync(value.Item1, null, oldValue);
        }

        private async Task SaveAsync(string value, bool? oldBinaryValue, TwoStringTuple oldOption) {
            if (_session?.API == null || IsSaving) return;

            IsSaving = true;
            ErrorText = null;
            try {
                PrivacySettingValue response = await _session.API.Account.SetPrivacyAsync(Key, value);
                ApplyValue(response);
            } catch (Exception ex) {
                Log.Error(ex, "Cannot save privacy setting {Key}.", Key);
                ErrorText = ExceptionHelper.GetDefaultErrorInfo(ex).Item2;
                Rollback(oldBinaryValue, oldOption);
            } finally {
                IsSaving = false;
            }
        }

        private void ApplyValue(PrivacySettingValue value) {
            _isApplyingValue = true;
            try {
                if (IsBinary) {
                    _isEnabledValue = value?.IsEnabled == true;
                    OnPropertyChanged(nameof(IsEnabledValue));
                } else {
                    _currentOption = Options.FirstOrDefault(o => o.Item1 == value?.Category) ?? Options.FirstOrDefault();
                    OnPropertyChanged(nameof(CurrentOption));
                }
            } finally {
                _isApplyingValue = false;
            }
        }

        private void Rollback(bool? oldBinaryValue, TwoStringTuple oldOption) {
            _isApplyingValue = true;
            try {
                if (oldBinaryValue.HasValue) {
                    _isEnabledValue = oldBinaryValue.Value;
                    OnPropertyChanged(nameof(IsEnabledValue));
                } else {
                    _currentOption = oldOption;
                    OnPropertyChanged(nameof(CurrentOption));
                }
            } finally {
                _isApplyingValue = false;
            }
        }
    }
}
