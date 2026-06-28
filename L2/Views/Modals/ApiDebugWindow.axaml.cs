using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using ELOR.Laney.Core;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace ELOR.Laney.Views.Modals {
    public partial class ApiDebugWindow : Window {
        private readonly ObservableCollection<ApiDebugCallEntry> _entries = new ObservableCollection<ApiDebugCallEntry>();

        public ApiDebugWindow() {
            InitializeComponent();
            CallsList.ItemsSource = _entries;
            RefreshList();

            if (FeatureFlags.IsEnabled(FeatureFlags.ApiDebugLiveWindow)) {
                ApiDebugMonitor.EntryAdded += ApiDebugMonitor_EntryAdded;
                Closed += (a, b) => ApiDebugMonitor.EntryAdded -= ApiDebugMonitor_EntryAdded;
            }
        }

        private void ApiDebugMonitor_EntryAdded(object sender, ApiDebugCallEntry entry) {
            Dispatcher.UIThread.Post(() => {
                _entries.Insert(0, entry);
                while (_entries.Count > 300) _entries.RemoveAt(_entries.Count - 1);
                if (CallsList.SelectedItem == null) CallsList.SelectedItem = entry;
            });
        }

        private void Refresh_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e) {
            RefreshList();
        }

        private void Clear_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e) {
            ApiDebugMonitor.Clear();
            _entries.Clear();
            Details.Text = String.Empty;
        }

        private async void Copy_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e) {
            if (CallsList.SelectedItem is not ApiDebugCallEntry entry) return;
            await TopLevel.GetTopLevel(this).Clipboard.SetTextAsync(entry.ToDetailsText());
        }

        private void CallsList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            Details.Text = CallsList.SelectedItem is ApiDebugCallEntry entry ? entry.ToDetailsText() : String.Empty;
        }

        private void RefreshList() {
            ApiDebugCallEntry selected = CallsList.SelectedItem as ApiDebugCallEntry;
            _entries.Clear();
            foreach (ApiDebugCallEntry entry in ApiDebugMonitor.GetSnapshot()) {
                _entries.Add(entry);
            }

            CallsList.SelectedItem = selected != null && _entries.Contains(selected) ? selected : _entries.FirstOrDefault();
        }
    }
}
