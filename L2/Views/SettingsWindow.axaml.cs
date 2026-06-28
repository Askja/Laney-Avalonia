using Avalonia.Controls;
using Avalonia.Input;
using ELOR.Laney.Core;
using ELOR.Laney.Core.Localization;
using ELOR.Laney.DataModels;
using ELOR.Laney.ViewModels;
using ELOR.Laney.Views.SettingsCategories;
using System;
using System.Linq;
using VKUI.Windows;

namespace ELOR.Laney.Views {
    public partial class SettingsWindow : DialogWindow {
        SettingsViewModel ViewModel { get => DataContext as SettingsViewModel; }
        private readonly Type initialCategoryViewType;

        public SettingsWindow() : this(null) { }

        public SettingsWindow(Type initialCategoryViewType) {
            InitializeComponent();
#if LINUX
            TitleBar.IsVisible = false;
#endif

            this.initialCategoryViewType = initialCategoryViewType;
            DataContext = new SettingsViewModel();
            SettingsSearchBox.PlaceholderText = Localizer.Get("settings_search_placeholder");
            NoSearchResultsPlaceholder.Header = Localizer.Get("settings_search_empty_title");
            NoSearchResultsPlaceholder.Text = Localizer.Get("settings_search_empty_text");
            Loaded += SettingsWindow_Loaded;
            KeyDown += SettingsWindow_KeyDown;
        }

        private void SettingsWindow_Loaded(object sender, Avalonia.Interactivity.RoutedEventArgs e) {
            Loaded -= SettingsWindow_Loaded;
            if (Owner is MainWindow ownerWindow && ownerWindow.Session != null) {
                ViewModel.SetAccountId(ownerWindow.Session.Id);
            }

            SettingsCategory category = ViewModel.SelectedCategory;
            if (initialCategoryViewType != null) {
                category = ViewModel.Categories.FirstOrDefault(c => c.View?.GetType() == initialCategoryViewType) ?? category;
            }

            if (DemoMode.IsEnabled) {
                category = GetPerfCategory(App.GetCmdLineValue("perf-settings-category")) ?? category;
            }

            ShowCategory(category);
        }

        private SettingsCategory GetPerfCategory(string categoryId) {
            return categoryId switch {
                "general" => ViewModel.Categories.FirstOrDefault(c => c.View is General),
                "interface" or "appearance" => ViewModel.Categories.FirstOrDefault(c => c.View is Appearance),
                "chats" => ViewModel.Categories.FirstOrDefault(c => c.View is Chats),
                "messages" => ViewModel.Categories.FirstOrDefault(c => c.View is Messages),
                "notifications" => ViewModel.Categories.FirstOrDefault(c => c.View is NotificationsPage),
                "privacy" => ViewModel.Categories.FirstOrDefault(c => c.View is Privacy),
                "stickers" => ViewModel.Categories.FirstOrDefault(c => c.View is Stickers),
                "audio" => ViewModel.Categories.FirstOrDefault(c => c.View is Audio),
                "automation" => ViewModel.Categories.FirstOrDefault(c => c.View is Automation),
                "memory" or "performance" => ViewModel.Categories.FirstOrDefault(c => c.View is Memory),
                "network" => ViewModel.Categories.FirstOrDefault(c => c.View is Network),
                "e2e" or "security" => ViewModel.Categories.FirstOrDefault(c => c.View is E2E),
                "experiments" => ViewModel.Categories.FirstOrDefault(c => c.View is Experiments),
                "debug" => ViewModel.Categories.FirstOrDefault(c => c.View is DebugPage),
                _ => null
            };
        }

        private void CategoriesList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            SettingsCategory sc = CategoriesList.SelectedItem as SettingsCategory;
            if (sc == null) {
                ViewModel.SelectedCategory = e.RemovedItems[0] as SettingsCategory;
                return;
            }
            ShowCategory(sc);
        }

        private void SearchResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (e.AddedItems.Count == 0) return;

            SettingsSearchResult result = e.AddedItems[0] as SettingsSearchResult;
            if (result == null) return;

            ShowCategory(result.Category);
            SearchResultsList.SelectedItem = null;
        }

        private void SettingsWindow_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control)) {
                SettingsSearchBox.Focus();
                SettingsSearchBox.SelectAll();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape && ViewModel?.IsSearchActive == true && SettingsSearchBox.IsFocused) {
                ViewModel.SearchQuery = String.Empty;
                e.Handled = true;
            }
        }

        private void ShowCategory(SettingsCategory sc) {
            if (sc == null) return;
            if (CategoriesList.SelectedItem != sc) CategoriesList.SelectedItem = sc;
            ViewModel.SelectedCategory = sc;
            ContentPanel.Content = sc.View;
            ContentPanel.DataContext = sc.ViewModel;
        }
    }
}
