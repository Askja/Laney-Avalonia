using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ELOR.Laney.Core {
    public static class ScheduledMessageManager {
        private static readonly Dictionary<long, VKSession> Sessions = new Dictionary<long, VKSession>();
        private static readonly object Sync = new object();
        private static DispatcherTimer timer;
        private static bool isSending;

        public static void EnsureStarted(VKSession session) {
            if (session == null || session.Id == 0) return;

            lock (Sync) {
                Sessions[session.Id] = session;
            }

            if (timer != null) {
                if (!timer.IsEnabled) timer.Start();
                return;
            }

            timer = new DispatcherTimer {
                Interval = TimeSpan.FromSeconds(30)
            };
            timer.Tick += async (a, b) => await ProcessDueMessagesAsync();
            timer.Start();
        }

        private static async System.Threading.Tasks.Task ProcessDueMessagesAsync() {
            if (isSending) return;
            isSending = true;

            try {
                long nowUnix = DateTimeOffset.Now.ToUnixTimeSeconds();
                List<ScheduledMessageItem> due = Settings.GetScheduledMessages()
                    .Where(item => item.NextSendUnix <= nowUnix)
                    .OrderBy(item => item.NextSendUnix)
                    .ToList();

                foreach (ScheduledMessageItem item in due) {
                    VKSession session;
                    lock (Sync) {
                        Sessions.TryGetValue(item.SessionId, out session);
                    }

                    if (session == null) continue;
                    await SendScheduledMessageAsync(session, item);
                }
            } finally {
                isSending = false;
            }
        }

        private static async System.Threading.Tasks.Task SendScheduledMessageAsync(VKSession session, ScheduledMessageItem item) {
            try {
                int randomId = Random.Shared.Next(Int32.MinValue, Int32.MaxValue);
                await session.API.Messages.SendAsync(
                    item.GroupId,
                    item.PeerId,
                    randomId,
                    item.Text,
                    0,
                    0,
                    new List<string>(),
                    String.Empty,
                    0,
                    dontParseLinks: Settings.DontParseLinks,
                    disableMentions: Settings.DisableMentions);

                MoveOrRemove(item);
                session.ShowNotification(new Notification("Отложенное отправлено", $"peer {item.PeerId}", NotificationType.Success));
            } catch (Exception ex) {
                item.NextSendUnix = DateTimeOffset.Now.AddMinutes(5).ToUnixTimeSeconds();
                Settings.AddScheduledMessage(item);
                Log.Warning(ex, "Scheduled message send failed. Peer={PeerId}; id={Id}", item.PeerId, item.Id);
            }
        }

        private static void MoveOrRemove(ScheduledMessageItem item) {
            if (item.RepeatIntervalMinutes <= 0) {
                Settings.RemoveScheduledMessage(item.Id);
                return;
            }

            long intervalSeconds = item.RepeatIntervalMinutes * 60L;
            long nowUnix = DateTimeOffset.Now.ToUnixTimeSeconds();
            do {
                item.NextSendUnix += intervalSeconds;
            } while (item.NextSendUnix <= nowUnix);

            Settings.AddScheduledMessage(item);
        }
    }
}
