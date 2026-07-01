using Avalonia.Controls;
using Avalonia.Interactivity;
using ELOR.Laney.Core;
using ELOR.Laney.Extensions;
using ELOR.Laney.ViewModels;

namespace ELOR.Laney.Views {
    public partial class NewsFeedView : VKUI.Controls.Page {
        private bool incrementalLoadingRegistered;
        private NewsFeedViewModel ViewModel { get { return DataContext as NewsFeedViewModel; } }

        public NewsFeedView() {
            InitializeComponent();
            BackButton.Click += async (a, b) => await NavigationRouter.BackAsync();
        }

        private async void NewsFeedView_Loaded(object sender, RoutedEventArgs e) {
            if (Design.IsDesignMode) return;
            if (DataContext is not NewsFeedViewModel) DataContext = new NewsFeedViewModel(VKSession.GetByDataContext(this));
            if (!incrementalLoadingRegistered) {
                FeedScroll.RegisterIncrementalLoadingEvent(async () => await ViewModel.LoadNextAsync());
                incrementalLoadingRegistered = true;
            }

            await ViewModel.InitializeAsync();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e) {
            if (ViewModel == null) return;
            await ViewModel.RefreshAsync();
        }

        private async void ApplyRulesButton_Click(object sender, RoutedEventArgs e) {
            if (ViewModel == null) return;
            await ViewModel.ApplyRulesAsync();
        }

        private async void LoadMoreButton_Click(object sender, RoutedEventArgs e) {
            if (ViewModel == null) return;
            await ViewModel.LoadNextAsync();
        }

        private async void OpenPostButton_Click(object sender, RoutedEventArgs e) {
            if (sender is not Control control || control.DataContext is not NewsFeedPostViewModel item) return;
            await Launcher.LaunchUrl(item.Link);
        }
    }
}
