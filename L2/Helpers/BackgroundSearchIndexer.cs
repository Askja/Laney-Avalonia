using Avalonia.Threading;
using ELOR.Laney.Core;
using Serilog;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ELOR.Laney.Helpers {
    public sealed class BackgroundSearchIndexer : IDisposable {
        private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

        private readonly VKSession session;
        private readonly DispatcherTimer timer;
        private bool isRunning;
        private bool isDisposed;

        public BackgroundSearchIndexer(VKSession session) {
            this.session = session;
            timer = new DispatcherTimer {
                Interval = InitialDelay
            };
            timer.Tick += Timer_Tick;
        }

        public void Start() {
            if (isDisposed) return;
            timer.Start();
        }

        public void Dispose() {
            if (isDisposed) return;
            isDisposed = true;
            timer.Stop();
            timer.Tick -= Timer_Tick;
        }

        private async void Timer_Tick(object sender, EventArgs e) {
            timer.Interval = Interval;
            await RefreshAsync();
        }

        private async Task RefreshAsync() {
            if (isDisposed || isRunning || session?.ImViewModel?.SortedChats == null) return;
            if (PowerState.IsOnBattery()) {
                Log.Information("Background search index refresh skipped: device is on battery.");
                return;
            }

            try {
                isRunning = true;
                await LocalSearchIndex.RefreshFromChatsAsync(session.ImViewModel.SortedChats);
            } catch (Exception ex) {
                Log.Warning(ex, "Background search index refresh failed.");
            } finally {
                isRunning = false;
            }
        }
    }

    internal static class PowerState {
        public static bool IsOnBattery() {
#if WIN
            if (GetSystemPowerStatus(out SystemPowerStatus status)) return status.ACLineStatus == 0;
#endif
            return false;
        }

#if WIN
        [DllImport("kernel32.dll")]
        private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

        [StructLayout(LayoutKind.Sequential)]
        private struct SystemPowerStatus {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;
            public uint BatteryLifeTime;
            public uint BatteryFullLifeTime;
        }
#endif
    }
}
