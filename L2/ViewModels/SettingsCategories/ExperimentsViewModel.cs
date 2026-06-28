using ELOR.Laney.Core;
using ELOR.Laney.ViewModels;
using System.Collections.ObjectModel;

namespace ELOR.Laney.ViewModels.SettingsCategories {
    public sealed class FeatureFlagOption : CommonViewModel {
        private readonly FeatureFlagDefinition _definition;

        public string Id => _definition.Id;
        public string Title => _definition.Title;
        public string Subtitle => _definition.RequiresRestart ? $"{_definition.Description} Нужен restart." : _definition.Description;
        public bool IsEnabled { get { return FeatureFlags.IsEnabled(_definition.Id); } set { FeatureFlags.SetEnabled(_definition.Id, value); OnPropertyChanged(); } }

        public FeatureFlagOption(FeatureFlagDefinition definition) {
            _definition = definition;
        }
    }

    public sealed class ExperimentsViewModel : CommonViewModel {
        public ObservableCollection<FeatureFlagOption> FeatureFlags { get; private set; } = new ObservableCollection<FeatureFlagOption>();

        public bool StreamerMode { get { return Settings.StreamerMode; } set { Settings.StreamerMode = value; OnPropertyChanged(); } }
        public bool ShowFPS { get { return Settings.ShowFPS; } set { Settings.ShowFPS = value; OnPropertyChanged(); } }
        public bool ShowDebugCounters { get { return Settings.ShowDebugCounters; } set { Settings.ShowDebugCounters = value; OnPropertyChanged(); } }
        public bool ShowDevItemsInContextMenus { get { return Settings.ShowDevItemsInContextMenus; } set { Settings.ShowDevItemsInContextMenus = value; OnPropertyChanged(); } }
        public bool DisableMarkingMessagesAsRead { get { return Settings.DisableMarkingMessagesAsRead; } set { Settings.DisableMarkingMessagesAsRead = value; OnPropertyChanged(); } }
        public bool ShowDebugInfoInGallery { get { return Settings.ShowDebugInfoInGallery; } set { Settings.ShowDebugInfoInGallery = value; OnPropertyChanged(); } }
        public bool EnableLogs { get { return Settings.EnableLogs; } set { Settings.EnableLogs = value; OnPropertyChanged(); } }
        public bool BitmapManagerLogs { get { return Settings.BitmapManagerLogs; } set { Settings.BitmapManagerLogs = value; OnPropertyChanged(); } }
        public bool MessageRenderingLogs { get { return Settings.MessageRenderingLogs; } set { Settings.MessageRenderingLogs = value; OnPropertyChanged(); } }

        public ExperimentsViewModel() {
            foreach (FeatureFlagDefinition definition in ELOR.Laney.Core.FeatureFlags.Definitions) {
                FeatureFlags.Add(new FeatureFlagOption(definition));
            }
        }
    }
}
