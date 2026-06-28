using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VKUI.Controls;
using VKUI.Utils;

namespace VKUI.Popups {
    public sealed class ActionSheet : PopupFlyoutBase {
        public ActionSheet() {
            ShowMode = FlyoutShowMode.Standard;
            OverlayDismissEventPassThrough = false;
            _items = new List<ActionSheetItem>();
        }

        private List<ActionSheetItem> _items;

        public List<ActionSheetItem> Items {
            get => _items;
        }

        public bool CloseAfterClick { get; set; } = true;
        public bool IsSearchEnabled { get; set; }
        public int SearchThreshold { get; set; } = 12;
        public string SearchWatermark { get; set; } = "Найти действие";
        public Control Above { get; set; }
        public object Tag { get; set; }

        StackPanel rootPanel;
        StackPanel itemsPanel;
        TextBox searchBox;
        VKUIFlyoutPresenter presenter;
        TopLevel ownerTopLevel;
        private List<Button> itemsButtons = new List<Button>();
        protected override Control CreatePresenter() {
            rootPanel = new StackPanel();

            if (ShouldShowSearch()) {
                searchBox = new TextBox {
                    PlaceholderText = SearchWatermark,
                    Margin = new Thickness(10, 8, 10, 4),
                    MinWidth = 220
                };
                searchBox.Classes.Add("Search");
                searchBox.PropertyChanged += SearchBox_PropertyChanged;
                rootPanel.Children.Add(searchBox);
            }

            itemsPanel = new StackPanel {
                Margin = new Thickness(0, 4, 0, 4),
            };
            rootPanel.Children.Add(itemsPanel);

            RebuildItems(null);

            presenter = new VKUIFlyoutPresenter {
                Above = Above,
                Content = rootPanel,
                ParentFlyout = this
            };
            presenter.Classes.Add("ActionSheet");
            return presenter;
        }

        private void RebuildItems(string query) {
            itemsPanel.Children.Clear();

            bool hasQuery = !String.IsNullOrWhiteSpace(query);
            ActionSheetItem section = null;
            int actionCount = 0;

            foreach (ActionSheetItem item in _items) {
                if (item.Before == null && item.Header == null) { // Экстравагатным образом добавляем сепаратор
                    if (hasQuery || itemsPanel.Children.Count == 0) continue;

                    Rectangle separator = new Rectangle();
                    separator.Classes.Add("ActionSheetSeparator");
                    itemsPanel.Children.Add(separator);
                    continue;
                }

                if (item.IsSectionHeader) {
                    section = item;
                    continue;
                }

                if (hasQuery && !MatchesQuery(item, query)) continue;

                if (section != null) {
                    AddSection(section.Header);
                    section = null;
                }

                item.Click -= Item_Click;
                item.Click += Item_Click;
                itemsPanel.Children.Add(item);
                actionCount++;
            }

            if (actionCount == 0) AddEmptyState();
        }

        private void AddSection(string header) {
            TextBlock section = new TextBlock {
                Text = header,
                FontSize = 12,
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                Opacity = 0.72,
                Margin = new Thickness(12, 8, 12, 3)
            };
            itemsPanel.Children.Add(section);
        }

        private void AddEmptyState() {
            TextBlock empty = new TextBlock {
                Text = "Ничего не найдено",
                FontSize = 13,
                Opacity = 0.68,
                Margin = new Thickness(14, 10, 14, 12)
            };
            itemsPanel.Children.Add(empty);
        }

        private static bool MatchesQuery(ActionSheetItem item, string query) {
            string needle = query.Trim();
            return Contains(item.Header, needle) || Contains(item.Subtitle, needle);
        }

        private static bool Contains(string source, string query) {
            return !String.IsNullOrWhiteSpace(source) && source.Contains(query, StringComparison.OrdinalIgnoreCase);
        }

        private bool ShouldShowSearch() {
            return IsSearchEnabled || (SearchThreshold > 0 && CountActionItems() >= SearchThreshold);
        }

        private int CountActionItems() {
            return _items.Count(i => i.Before != null || (!String.IsNullOrWhiteSpace(i.Header) && !i.IsSectionHeader));
        }

        protected override void OnOpened() {
            base.OnOpened();
            RefreshNavigationTargets();
            if (searchBox != null) {
                searchBox.Focus(NavigationMethod.Tab);
            } else {
                itemsButtons.FirstOrDefault()?.Focus(NavigationMethod.Tab);
            }

            rootPanel.KeyDown += Items_KeyDown;
            rootPanel.AddHandler(InputElement.LostFocusEvent, Items_LostFocus, RoutingStrategies.Bubble, true);

            ownerTopLevel = TopLevel.GetTopLevel(presenter);
            ownerTopLevel?.AddHandler(InputElement.PointerPressedEvent, OwnerTopLevel_PointerPressed, RoutingStrategies.Tunnel, true);

        }

        protected override void OnClosed() {
            base.OnClosed();
            rootPanel.KeyDown -= Items_KeyDown;
            rootPanel.RemoveHandler(InputElement.LostFocusEvent, Items_LostFocus);
            if (searchBox != null) searchBox.PropertyChanged -= SearchBox_PropertyChanged;
            ownerTopLevel?.RemoveHandler(InputElement.PointerPressedEvent, OwnerTopLevel_PointerPressed);
            ownerTopLevel = null;
            itemsButtons.Clear();
        }

        private void Item_Click(object? sender, RoutedEventArgs e) {
            if (CloseAfterClick) Hide();
        }

        private void Items_KeyDown(object sender, KeyEventArgs e) {
            Debug.WriteLine($"Action sheet navigation: {e.Key}");
            if (e.Key == Key.Escape) {
                Hide();
                e.Handled = true;
                return;
            }

            if (itemsButtons.Count == 0) return;

            bool searchIsFocused = searchBox != null && searchBox.IsFocused;
            Button current = searchIsFocused ? itemsButtons.First() : itemsButtons.FirstOrDefault(b => b.IsFocused) ?? itemsButtons.First();
            int index = itemsButtons.IndexOf(current);
            if (index < 0) index = 0;

            if (e.Key == Key.Up) {
                int nextIndex = index == 0 ? itemsButtons.Count - 1 : index - 1;
                itemsButtons[nextIndex].Focus(NavigationMethod.Directional);
                e.Handled = true;
            } else if (e.Key == Key.Down) {
                int nextIndex = index == itemsButtons.Count - 1 ? 0 : index + 1;
                itemsButtons[nextIndex].Focus(NavigationMethod.Directional);
                e.Handled = true;
            }
        }

        private void SearchBox_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e) {
            if (e.Property != TextBox.TextProperty) return;

            RebuildItems(searchBox.Text);
            RefreshNavigationTargets();
        }

        private void RefreshNavigationTargets() {
            itemsButtons.Clear();
            itemsPanel.FindVisualChildrenByType(itemsButtons);
        }

        private void OwnerTopLevel_PointerPressed(object? sender, PointerPressedEventArgs e) {
            if (IsInsidePresenter(e.Source as Visual)) return;
            Hide();
        }

        private void Items_LostFocus(object? sender, FocusChangedEventArgs e) {
            if (IsInsidePresenter(e.NewFocusedElement as Visual)) return;

            Dispatcher.UIThread.Post(() => {
                IInputElement focused = ownerTopLevel?.FocusManager?.GetFocusedElement();
                if (!IsInsidePresenter(focused as Visual)) Hide();
            }, DispatcherPriority.Background);
        }

        private bool IsInsidePresenter(Visual visual) {
            if (presenter == null || visual == null) return false;
            return visual == presenter || presenter.IsVisualAncestorOf(visual);
        }
    }
}
