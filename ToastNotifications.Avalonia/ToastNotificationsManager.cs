using Avalonia.Controls.Notifications;
using Avalonia.Media.Imaging;

namespace ToastNotifications.Avalonia {
    public class ToastNotificationsManager : WindowNotificationManager {
        internal static ToastsContainer Container { get; private set; }
        public static double ExpirationMilliseconds { get; set; } = 7000;
        public static ToastNotificationOptions Options { get; private set; } = new ToastNotificationOptions();
        internal Bitmap AppLogo { get; private set; }
        internal Action<string> Log { get; private set; }

        public ToastNotificationsManager(Bitmap appLogo = null, Action<string> log = null) {
            AppLogo = appLogo;
            Log = log;
        }

        public static void Configure(ToastNotificationOptions options) {
            Options = NormalizeOptions(options);
            ExpirationMilliseconds = Options.Expiration.TotalMilliseconds;
            Container?.ApplyOptions(Options);
        }

        public new void Show(INotification notification) {
            if (Container == null) {
                Container = new ToastsContainer(Log);
                Container.ApplyOptions(Options);
            }

            if (notification is ToastNotification tn) {
                tn.Expiration = Options.Expiration;
                Container.AddToastToContainer(tn, AppLogo);
            } else {
                throw new ArgumentException($"ToastNotification required!");
            }
        }

        private static ToastNotificationOptions NormalizeOptions(ToastNotificationOptions options) {
            options ??= new ToastNotificationOptions();
            options.StackLimit = Math.Clamp(options.StackLimit, 1, 10);
            double seconds = Math.Clamp(options.Expiration.TotalSeconds, 2, 60);
            options.Expiration = TimeSpan.FromSeconds(seconds);
            return options;
        }
    }
}
