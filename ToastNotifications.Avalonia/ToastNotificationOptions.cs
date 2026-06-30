namespace ToastNotifications.Avalonia {
    public enum ToastStackPosition {
        BottomRight = 0,
        BottomLeft = 1,
        TopRight = 2,
        TopLeft = 3
    }

    public sealed class ToastNotificationOptions {
        public ToastStackPosition Position { get; set; } = ToastStackPosition.BottomRight;
        public int StackLimit { get; set; } = 4;
        public TimeSpan Expiration { get; set; } = TimeSpan.FromSeconds(8);
        public bool FastActionsEnabled { get; set; } = true;
        public bool ShowAvatars { get; set; } = true;
        public bool ShowImages { get; set; } = true;
    }

    public sealed class ToastNotificationAction {
        public ToastNotificationAction(string title, Action handler, bool dismissAfterClick = true) {
            Title = title;
            Handler = handler;
            DismissAfterClick = dismissAfterClick;
        }

        public string Title { get; }
        public Action Handler { get; }
        public bool DismissAfterClick { get; }

        public void Invoke() {
            Handler?.Invoke();
        }
    }
}
