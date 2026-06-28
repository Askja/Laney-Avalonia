using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using System;
using System.Runtime.InteropServices;

namespace VKUI.Controls {
    public class WindowTitleBar : TemplatedControl {
        #region Properties

        public static readonly StyledProperty<bool> CanShowTitleProperty =
            AvaloniaProperty.Register<WindowTitleBar, bool>(nameof(CanShowTitle));

        public bool CanShowTitle {
            get => GetValue(CanShowTitleProperty);
            set => SetValue(CanShowTitleProperty, value);
        }

        public static readonly StyledProperty<bool> CanMoveProperty =
            AvaloniaProperty.Register<WindowTitleBar, bool>(nameof(CanMove));

        public bool CanMove {
            get => GetValue(CanMoveProperty);
            set => SetValue(CanMoveProperty, value);
        }

        #endregion

        #region Template elements

        Grid TitleBar;
        TextBlock WindowTitle;
        Border DragArea;
        Button MinimizeButton;
        Button MaximizeButton;
        Button CloseButton;
        TextBlock MaximizeIcon;
        Window OwnerWindow;

        bool isTemplateLoaded = false;
        protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
            base.OnApplyTemplate(e);

            TitleBar = e.NameScope.Find<Grid>(nameof(TitleBar));
            WindowTitle = e.NameScope.Find<TextBlock>(nameof(WindowTitle));
            DragArea = e.NameScope.Find<Border>(nameof(DragArea));
            MinimizeButton = e.NameScope.Find<Button>(nameof(MinimizeButton));
            MaximizeButton = e.NameScope.Find<Button>(nameof(MaximizeButton));
            CloseButton = e.NameScope.Find<Button>(nameof(CloseButton));
            MaximizeIcon = e.NameScope.Find<TextBlock>(nameof(MaximizeIcon));

            MinimizeButton.Click += MinimizeButton_Click;
            MaximizeButton.Click += MaximizeButton_Click;
            CloseButton.Click += CloseButton_Click;
            Unloaded += WindowTitleBar_Unloaded;

            isTemplateLoaded = true;
            Setup();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
            base.OnPropertyChanged(change);
            if (!isTemplateLoaded) return;

            if (change.Property == CanShowTitleProperty) {
                WindowTitle.IsVisible = CanShowTitle;
            }
        }

        #endregion

        private void CloseButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e) {
            OwnerWindow.Close();
        }

        private void MinimizeButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e) {
            OwnerWindow.WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e) {
            OwnerWindow.WindowState = OwnerWindow.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void WindowTitleBar_Unloaded(object sender, Avalonia.Interactivity.RoutedEventArgs e) {
            if (OwnerWindow != null) OwnerWindow.PropertyChanged -= OwnerWindow_PropertyChanged;
            MinimizeButton.Click -= MinimizeButton_Click;
            MaximizeButton.Click -= MaximizeButton_Click;
            CloseButton.Click -= CloseButton_Click;
            DragArea.PointerPressed -= DragArea_PointerPressed;
            Unloaded -= WindowTitleBar_Unloaded;
        }

        private void Setup() {
            if (!isTemplateLoaded) return;

            // Finding window
            // TODO: чекать, это DialogWindow или обычный Window
            // и менять стиль кнопок в зависимости от этого.
            Control control = (Control)Parent;
            do {
                if (control is Window window) {
                    OwnerWindow = window;
                } else {
                    control = (Control)control.Parent;
                }
            } while (OwnerWindow == null && control.GetType() != typeof(Window));
            if (OwnerWindow == null) throw new ArgumentNullException("Unable to find a parent Window!");

            // Appearance
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                TitleBar.Height = 48;
                WindowTitle.Classes.Add("Default");
                MinimizeButton.IsVisible = true;
                MaximizeButton.IsVisible = OwnerWindow.CanResize;
                CloseButton.IsVisible = true;
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                TitleBar.Height = 27; // 22 for old macos...
                WindowTitle.Classes.Add("Mac");
            }

            // Window
            WindowTitle.IsVisible = CanShowTitle;
            WindowTitle.Text = OwnerWindow.Title;
            OwnerWindow.PropertyChanged += OwnerWindow_PropertyChanged;
            DragArea.PointerPressed += DragArea_PointerPressed;
            UpdateMaximizeIcon();
        }

        private void OwnerWindow_PropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e) {
            if (e.Property.Name == nameof(Window.Title)) {
                WindowTitle.Text = OwnerWindow.Title;
            } else if (e.Property.Name == nameof(Window.WindowState)) {
                UpdateMaximizeIcon();
            }
        }

        private void UpdateMaximizeIcon() {
            if (MaximizeIcon == null || OwnerWindow == null) return;
            MaximizeIcon.Text = OwnerWindow.WindowState == WindowState.Maximized ? "❐" : "□";
        }

        private void DragArea_PointerPressed(object sender, Avalonia.Input.PointerPressedEventArgs e) {
            if (CanMove) OwnerWindow.BeginMoveDrag(e);
        }
    }
}
