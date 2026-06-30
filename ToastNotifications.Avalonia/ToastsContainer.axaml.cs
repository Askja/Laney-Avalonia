using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using System.Linq;
using System.Runtime.InteropServices;

namespace ToastNotifications.Avalonia {
    internal partial class ToastsContainer : Window {
        internal ToastsContainer() {
            InitializeComponent();
            SetPosition();
        }

        internal ToastsContainer(Action<string> log = null) {
            InitializeComponent();
            Log = log;
            SetPosition();
        }

        Action<string> Log;
        ToastNotificationOptions Options = new ToastNotificationOptions();

        bool topAligned = false;
        bool leftAligned = false;
        Screen lastScreen = null;

        internal void ApplyOptions(ToastNotificationOptions options) {
            Options = options ?? new ToastNotificationOptions();
            TrimOverflow();
            SetPosition();
        }

        private void SetPosition(bool cycle = false) {
            var screen = Screens.ScreenFromWindow(this);
            if (screen == null) {
                if (lastScreen == null) {
                    Log?.Invoke("SetPosition: Cannot get screen!");
                    return;
                } else {
                    // В macOS после того, как мы получили окно в первый раз, в дальнейшем возвращает null.
                    Log?.Invoke("SetPosition: Cannot get screen now, but last time we got this...");
                    screen = lastScreen;
                }
            } else {
                lastScreen = screen;
            }

            var working = screen.WorkingArea;
            double scale = screen.Scaling;
            bool needBigMargin = false;

            double sw = (double)screen.Bounds.Width / scale;
            double sh = (double)screen.Bounds.Height / scale;
            double ww = working.Width / scale;
            double wh = working.Height / scale;
            double wx = working.X / scale;
            double wy = working.Y / scale;

            if (wh > sh) {
                needBigMargin = true;
                Log?.Invoke("SetPosition: Working area's height is GREATER than screen height! WTF?!");
            }

            NotificationItems.Measure(new Size(MaxWidth, wh));
            double height = NotificationItems.DesiredSize.Height;

            bool topRequested = Options.Position == ToastStackPosition.TopLeft || Options.Position == ToastStackPosition.TopRight;
            bool leftRequested = Options.Position == ToastStackPosition.TopLeft || Options.Position == ToastStackPosition.BottomLeft;
            topAligned = topRequested;
            leftAligned = leftRequested;

            const double margin = 12;
            int posx = Convert.ToInt32((leftRequested ? wx + margin : wx + ww - MaxWidth - margin) * scale);
            int posy = Convert.ToInt32((topRequested ? wy + margin : wy + wh - height - margin) * scale);

            Position = new PixelPoint(posx, needBigMargin ? posy + 48 : posy);
            Height = needBigMargin ? height - 96 : height;

            IsVisible = height > 0;
            Log?.Invoke($"SetPosition: topAligned={topAligned}; leftAligned={leftAligned}; sw={sw}; sh={sh}; wx={wx}; wy={wy}; ww={ww}; wh={wh}; tx={posx}; ty={posy}; th={height}; isVisible={IsVisible}");
        }

        internal void AddToastToContainer(ToastNotification notification, Bitmap appLogo) {
            TrimOverflow();
            Toast toast = new Toast() {
                Header = notification.Header,
                Title = notification.Title,
                Body = notification.Message,
                Footnote = notification.Footnote,
                AppLogo = appLogo,
                Avatar = Options.ShowAvatars ? notification.Avatar : null,
                Image = Options.ShowImages ? notification.Image : null,
                Actions = Options.FastActionsEnabled ? notification.Actions.ToArray() : Array.Empty<ToastNotificationAction>(),
                IsWriteBarVisible = notification.OnSendClick != null,
                Margin = new Thickness(12, 3, 12, 9)
            };

            // Linux DE moment... (maybe also macOS?)
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                toast.Loaded += async (a, b) => {
                    await Task.Delay(20);
                    SetPosition();
                };
            }

            IsVisible = true;
            NotificationItems.Children.Add(toast);
            Log?.Invoke($"AddToastToContainer: toast {toast.GetHashCode()} added in window!");

            SetPosition();

            DispatcherTimer timer = new DispatcherTimer {
                Interval = notification.Expiration,
            };
            timer.Tick += (a, b) => {
                timer.Stop();
                RemoveToast(toast);
            };
            timer.Start();

            toast.GotFocus += (a, b) => {
                timer.Stop();
            };
            toast.LostFocus += (a, b) => {
                timer.Start();
            };

            toast.CloseButtonClick += (a, b) => {
                timer.Stop();
                notification.OnClose?.Invoke();
                RemoveToast(toast);
            };
            toast.SendButtonClick += (a, b) => {
                timer.Stop();
                notification.OnSendClick?.Invoke(b);
                RemoveToast(toast);
            };
            toast.ActionButtonClick += (a, b) => {
                timer.Stop();
                b.Invoke();
                if (b.DismissAfterClick) RemoveToast(toast);
            };
            toast.Click += (a, b) => {
                timer.Stop();
                notification.OnClick?.Invoke();
                RemoveToast(toast);
            };
        }

        private void TrimOverflow() {
            int stackLimit = Math.Clamp(Options.StackLimit, 1, 10);
            while (NotificationItems.Children.Count >= stackLimit) {
                NotificationItems.Children.RemoveAt(0);
            }
        }

        private void RemoveToast(Toast toast) {
            if (!NotificationItems.Children.Contains(toast)) return;
            NotificationItems.Children.Remove(toast);
            Log?.Invoke($"AddToastToContainer: toast {toast.GetHashCode()} removed!");
            SetPosition();
        }
    }
}
