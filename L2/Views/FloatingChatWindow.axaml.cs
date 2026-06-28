using Avalonia;
using Avalonia.Controls;
using ELOR.Laney.Core;
using ELOR.Laney.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using VKUI.Windows;

namespace ELOR.Laney.Views {
    public sealed partial class FloatingChatWindow : DialogWindow {
        private static readonly Dictionary<string, FloatingChatWindow> OpenedWindows = new Dictionary<string, FloatingChatWindow>();

        private readonly string key;
        private readonly VKSession session;
        private readonly ChatViewModel chat;

        public FloatingChatWindow() {
            InitializeComponent();
            if (!Design.IsDesignMode) throw new ArgumentException();
        }

        private FloatingChatWindow(VKSession session, ChatViewModel chat, string key) {
            InitializeComponent();
            this.session = session;
            this.chat = chat;
            this.key = key;

            DataContext = chat;
            Title = chat.DisplayTitle;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = GetInitialPosition(session);

            ChatView.ChangeBackButtonVisibility(true);
            ChatView.BackButtonClick += ChatView_BackButtonClick;
            Closed += FloatingChatWindow_Closed;
            chat.PropertyChanged += Chat_PropertyChanged;
            if (session?.Window != null) session.Window.Closing += Owner_Closing;
        }

        public static void ShowFor(VKSession session, ChatViewModel chat) {
            if (session == null || chat == null) return;

            string key = $"{session.Id}:{chat.PeerId}";
            if (OpenedWindows.TryGetValue(key, out FloatingChatWindow existing)) {
                existing.Show();
                existing.Activate();
                return;
            }

            FloatingChatWindow window = new FloatingChatWindow(session, chat, key);
            OpenedWindows[key] = window;
            window.Show();
            window.Activate();
        }

        private static PixelPoint GetInitialPosition(VKSession session) {
            PixelPoint ownerPosition = session?.Window?.Position ?? new PixelPoint(64, 64);
            return new PixelPoint(ownerPosition.X + 96, ownerPosition.Y + 96);
        }

        private void Chat_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(ChatViewModel.DisplayTitle)) Title = chat.DisplayTitle;
        }

        private void Owner_Closing(object sender, CancelEventArgs e) {
            Close();
        }

        private void ChatView_BackButtonClick(object? sender, EventArgs e) {
            Close();
        }

        private void FloatingChatWindow_Closed(object? sender, EventArgs e) {
            if (!String.IsNullOrWhiteSpace(key)) OpenedWindows.Remove(key);
            if (session?.Window != null) session.Window.Closing -= Owner_Closing;
            if (chat != null) chat.PropertyChanged -= Chat_PropertyChanged;
            ChatView.BackButtonClick -= ChatView_BackButtonClick;
            Closed -= FloatingChatWindow_Closed;
        }
    }
}
