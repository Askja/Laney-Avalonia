using Avalonia.Controls;
using Avalonia.Interactivity;
using ELOR.Laney.Core;
using ELOR.Laney.Extensions;
using ELOR.Laney.Helpers;
using ELOR.Laney.ViewModels;
using AudioSettingsView = ELOR.Laney.Views.SettingsCategories.Audio;

namespace ELOR.Laney.Views {
    public partial class MusicView : VKUI.Controls.Page {
        private bool incrementalLoadingRegistered;
        private MusicViewModel ViewModel => DataContext as MusicViewModel;
        public event System.EventHandler BackRequested;

        public MusicView() {
            InitializeComponent();
            BackButton.Click += BackButton_Click;
            Unloaded += MusicView_Unloaded;
        }

        private async void BackButton_Click(object sender, RoutedEventArgs e) {
            if (BackRequested != null) {
                BackRequested.Invoke(this, System.EventArgs.Empty);
                return;
            }

            if (NavigationRouter != null) await NavigationRouter.BackAsync();
        }

        private async void MusicView_Loaded(object sender, RoutedEventArgs e) {
            if (Design.IsDesignMode) return;
            if (DataContext is not MusicViewModel) DataContext = new MusicViewModel(VKSession.GetByDataContext(this));
            if (!incrementalLoadingRegistered) {
                TracksScroll.RegisterIncrementalLoadingEvent(async () => await ViewModel.LoadNextAsync());
                incrementalLoadingRegistered = true;
            }

            await ViewModel.InitializeAsync();
        }

        private void MusicView_Unloaded(object sender, RoutedEventArgs e) {
            ViewModel?.Dispose();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e) {
            if (ViewModel == null) return;
            await ViewModel.RefreshAsync();
        }

        private async void LoadMoreButton_Click(object sender, RoutedEventArgs e) {
            if (ViewModel == null) return;
            await ViewModel.LoadNextAsync();
        }

        private void PlayTrackButton_Click(object sender, RoutedEventArgs e) {
            if (ViewModel == null || sender is not Control control || control.DataContext is not MusicTrackViewModel track) return;
            ViewModel.PlayTrack(track);
        }

        private void EnqueueTrackButton_Click(object sender, RoutedEventArgs e) {
            if (ViewModel == null || sender is not Control control || control.DataContext is not MusicTrackViewModel track) return;
            ViewModel.EnqueueTrack(track);
        }

        private async void DownloadTrackButton_Click(object sender, RoutedEventArgs e) {
            if (ViewModel == null || sender is not Control control || control.DataContext is not MusicTrackViewModel track) return;
            await ViewModel.DownloadTrackAsync(track);
        }

        private async void OpenTrackButton_Click(object sender, RoutedEventArgs e) {
            if (sender is not Control control || control.DataContext is not MusicTrackViewModel track) return;
            await Launcher.LaunchUrl(track.Link);
        }

        private async void SetStatusButton_Click(object sender, RoutedEventArgs e) {
            if (ViewModel == null) return;
            await ViewModel.SetVkStatusFromCurrentAsync();
        }

        private async void ScrobbleButton_Click(object sender, RoutedEventArgs e) {
            if (ViewModel == null) return;
            await ViewModel.ScrobbleCurrentAsync();
        }

        private async void ExportScrobblesButton_Click(object sender, RoutedEventArgs e) {
            if (ViewModel == null) return;
            await ViewModel.ExportScrobblesAsync();
        }

        private void OpenIntegrationsFolderButton_Click(object sender, RoutedEventArgs e) {
            ViewModel?.OpenIntegrationsFolder();
        }

        private void ClearScrobblesButton_Click(object sender, RoutedEventArgs e) {
            ViewModel?.ClearScrobbles();
        }

        private void ClearQueueButton_Click(object sender, RoutedEventArgs e) {
            ViewModel?.ClearQueue();
        }

        private void ResetEqualizerButton_Click(object sender, RoutedEventArgs e) {
            ViewModel?.ResetCustomEqualizer();
        }

        private async void OpenAudioSettingsButton_Click(object sender, RoutedEventArgs e) {
            Window owner = TopLevel.GetTopLevel(this) as Window;
            if (owner == null) return;
            SettingsWindow settings = new SettingsWindow(typeof(AudioSettingsView));
            await settings.ShowDialog(owner);
        }
    }
}
