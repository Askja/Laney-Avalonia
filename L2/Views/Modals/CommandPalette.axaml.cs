using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ELOR.Laney.Extensions;
using ELOR.Laney.ViewModels.Modals;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VKUI.Windows;

namespace ELOR.Laney.Views.Modals {
    public sealed partial class CommandPalette : DialogWindow {
        private readonly List<CommandPaletteAction> actions;
        private readonly ObservableCollection<CommandPaletteAction> filteredActions = new ObservableCollection<CommandPaletteAction>();

        public CommandPalette() : this(Array.Empty<CommandPaletteAction>()) { }

        public CommandPalette(IEnumerable<CommandPaletteAction> actions) {
            InitializeComponent();
            this.FixDialogWindows(TitleBar, Root);

            this.actions = actions?.ToList() ?? new List<CommandPaletteAction>();
            ActionsList.ItemsSource = filteredActions;
            FilterActions();

            Opened += (a, b) => QueryBox.Focus();
        }

        private void QueryBox_PropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e) {
            if (e.Property == TextBox.TextProperty) FilterActions();
        }

        private void QueryBox_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape) {
                Close();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter) {
                CloseSelectedAction();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Down) {
                MoveSelection(1);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Up) {
                MoveSelection(-1);
                e.Handled = true;
            }
        }

        private void Action_Click(object sender, RoutedEventArgs e) {
            if (sender is Button button && button.DataContext is CommandPaletteAction action) {
                Close(action);
            }
        }

        private void FilterActions() {
            string query = QueryBox?.Text ?? String.Empty;
            filteredActions.Clear();

            foreach (CommandPaletteAction action in actions.Where(a => a.Matches(query))) {
                filteredActions.Add(action);
            }

            EmptyPlaceholder.IsVisible = filteredActions.Count == 0;
            ActionsList.IsVisible = filteredActions.Count > 0;
            ActionsList.SelectedIndex = filteredActions.Count > 0 ? 0 : -1;
        }

        private void MoveSelection(int delta) {
            if (filteredActions.Count == 0) return;

            int next = ActionsList.SelectedIndex + delta;
            if (next < 0) next = filteredActions.Count - 1;
            if (next >= filteredActions.Count) next = 0;
            ActionsList.SelectedIndex = next;
            ActionsList.ScrollIntoView(ActionsList.SelectedItem);
        }

        private void CloseSelectedAction() {
            if (ActionsList.SelectedItem is CommandPaletteAction action) Close(action);
        }
    }
}
