using Avalonia.Labs.Notifications;
using Avalonia.Threading;
using Serilog;
using System;
using System.Collections.Generic;
using ToastNotifications.Avalonia;

namespace ELOR.Laney.Core {
    public static class NativeNotificationService {
        private static readonly Dictionary<uint, ToastNotification> ActiveNotifications = new Dictionary<uint, ToastNotification>();
        private static INativeNotificationManager subscribedManager;

        public static bool Show(ToastNotification notification) {
            INativeNotificationManager manager = NativeNotificationManager.Current;
            if (manager == null || notification == null) return false;

            try {
                EnsureSubscribed(manager);

                INativeNotification native = manager.CreateNotification(null);
                if (native == null) return false;

                native.Title = notification.Title;
                native.Message = GetBody(notification);
                native.Tag = notification.AssociatedObject?.GetHashCode().ToString();
                native.Icon = notification.Avatar;
                if (notification.Expiration > TimeSpan.Zero) native.Expiration = notification.Expiration;
                native.Show();

                ActiveNotifications[native.Id] = notification;
                return true;
            } catch (Exception ex) {
                Log.Warning(ex, "Unable to show native notification. Falling back to Laney toast.");
                return false;
            }
        }

        private static void EnsureSubscribed(INativeNotificationManager manager) {
            if (ReferenceEquals(subscribedManager, manager)) return;
            if (subscribedManager != null) subscribedManager.NotificationCompleted -= OnNotificationCompleted;

            subscribedManager = manager;
            subscribedManager.NotificationCompleted += OnNotificationCompleted;
        }

        private static void OnNotificationCompleted(object sender, NativeNotificationCompletedEventArgs e) {
            if (!e.NotificationId.HasValue) return;
            if (!ActiveNotifications.TryGetValue(e.NotificationId.Value, out ToastNotification notification)) return;

            ActiveNotifications.Remove(e.NotificationId.Value);
            if (e.IsActivated) {
                Dispatcher.UIThread.Post(() => notification.OnClick?.Invoke());
            } else if (e.IsCancelled) {
                Dispatcher.UIThread.Post(() => notification.OnClose?.Invoke());
            }
        }

        private static string GetBody(ToastNotification notification) {
            return String.IsNullOrEmpty(notification.Footnote)
                ? notification.Message
                : $"{notification.Message}\n{notification.Footnote}";
        }
    }
}
