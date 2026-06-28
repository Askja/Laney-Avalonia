using Avalonia.Threading;
using ELOR.Laney.Core;
using ELOR.Laney.Execute.Objects;
using ELOR.Laney.DataModels;
using ELOR.Laney.ViewModels;
using ELOR.Laney.ViewModels.Controls;
using ELOR.VKAPILib.Objects;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ELOR.Laney.Helpers {
    public sealed class HistoryStatisticsSnapshot {
        public string State { get; set; } = "idle";
        public bool IsRunning { get; set; }
        public bool IsPaused { get; set; }
        public int TotalPeers { get; set; }
        public int IndexedPeers { get; set; }
        public long MessagesScanned { get; set; }
        public long TotalMessagesEstimate { get; set; }
        public long TextMessages { get; set; }
        public long ServiceMessages { get; set; }
        public long Attachments { get; set; }
        public long Reactions { get; set; }
        public long ApiCalls { get; set; }
        public int ErrorCount { get; set; }
        public string CurrentPeerTitle { get; set; }
        public long CurrentPeerId { get; set; }
        public string LastError { get; set; }
        public DateTime LastUpdatedUtc { get; set; }
        public int ApiDelayMs { get; set; }
        public int PageSize { get; set; }
        public double ProgressPercent { get; set; }
    }

    public sealed class BackgroundHistoryStatisticsIndexer : IDisposable {
        private const int PageSize = 100;
        private const int ApiDelayMs = 450;
        private const int PeerDelayMs = 900;
        private const int MaxApiCallsPerRun = 120;
        private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(45);
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(20);
        private static readonly object SnapshotLock = new object();
        private static HistoryStatisticsSnapshot lastSnapshot = HistoryStatisticsStore.ReadSnapshot();

        private readonly VKSession session;
        private readonly DispatcherTimer timer;
        private CancellationTokenSource cts;
        private bool isRunning;
        private bool isDisposed;

        public BackgroundHistoryStatisticsIndexer(VKSession session) {
            this.session = session;
            timer = new DispatcherTimer {
                Interval = InitialDelay
            };
            timer.Tick += Timer_Tick;
        }

        public static HistoryStatisticsSnapshot GetSnapshot() {
            lock (SnapshotLock) return Clone(lastSnapshot);
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
            cts?.Cancel();
            cts?.Dispose();
        }

        private async void Timer_Tick(object sender, EventArgs e) {
            timer.Interval = Interval;
            await RefreshAsync();
        }

        private async Task RefreshAsync() {
            if (isDisposed || isRunning || session?.ImViewModel?.SortedChats == null) return;

            if (PowerState.IsOnBattery()) {
                await UpdatePausedOnBatteryAsync();
                return;
            }

            cts?.Dispose();
            cts = new CancellationTokenSource();
            CancellationToken ct = cts.Token;

            try {
                isRunning = true;
                if (DemoMode.IsEnabled) {
                    await RefreshDemoAsync(ct);
                } else {
                    await RefreshVkAsync(ct);
                }
            } catch (OperationCanceledException) {
                Log.Information("Background history statistics indexing cancelled.");
            } catch (Exception ex) {
                await UpdateErrorAsync(ex);
                Log.Warning(ex, "Background history statistics indexing failed.");
            } finally {
                isRunning = false;
            }
        }

        private async Task RefreshVkAsync(CancellationToken ct) {
            HistoryStatisticsState state = HistoryStatisticsStore.ReadState();
            List<ChatViewModel> chats = session.ImViewModel.SortedChats
                .Where(c => c != null && c.PeerId != 0)
                .ToList();

            state.TotalPeers = chats.Count;
            state.State = "running";
            state.IsPaused = false;
            state.StartedAtUtc = DateTime.UtcNow;
            state.LastError = null;
            state.PageSize = PageSize;
            state.ApiDelayMs = ApiDelayMs;
            int apiCalls = 0;
            await SaveAndPublishAsync(state);

            foreach (ChatViewModel chat in chats) {
                ct.ThrowIfCancellationRequested();

                if (PowerState.IsOnBattery()) {
                    state.State = "paused_battery";
                    state.IsPaused = true;
                    await SaveAndPublishAsync(state);
                    Log.Information("Background history statistics indexing paused: device is on battery.");
                    return;
                }

                PeerHistoryStatistics peer = state.GetPeer(chat.PeerId);
                peer.PeerTitle = chat.Title;
                peer.PeerAvatar = chat.Avatar?.ToString();
                peer.LastKnownMaxConversationMessageId = Math.Max(peer.LastKnownMaxConversationMessageId, GetKnownMaxCmid(chat));
                IndexLoadedMessages(peer, chat);

                if (peer.IsComplete) {
                    await SaveAndPublishPeerAsync(state, peer, chat);
                    continue;
                }

                await SaveAndPublishPeerAsync(state, peer, chat);

                while (!peer.IsComplete && apiCalls < MaxApiCallsPerRun) {
                    ct.ThrowIfCancellationRequested();

                    if (PowerState.IsOnBattery()) {
                        state.State = "paused_battery";
                        state.IsPaused = true;
                        await SaveAndPublishAsync(state);
                        Log.Information("Background history statistics indexing paused: device is on battery.");
                        return;
                    }

                    MessagesHistoryResponse response = await LoadHistoryPageAsync(chat.PeerId, peer.NextBeforeConversationMessageId);
                    apiCalls++;
                    state.ApiCalls++;
                    peer.TotalMessagesEstimate = Math.Max(peer.TotalMessagesEstimate, response?.Count ?? 0);
                    List<Message> messages = response?.Items?
                        .Where(m => m != null && m.PeerId == chat.PeerId && m.ConversationMessageId > 0)
                        .GroupBy(m => m.ConversationMessageId)
                        .Select(g => g.First())
                        .ToList() ?? new List<Message>();

                    if (messages.Count == 0) {
                        peer.IsComplete = true;
                        peer.NextBeforeConversationMessageId = 0;
                        peer.LastUpdatedUtc = DateTime.UtcNow;
                        await SaveAndPublishPeerAsync(state, peer, chat);
                        break;
                    }

                    foreach (Message message in messages) {
                        AddMessage(peer, message);
                    }

                    int minCmid = messages.Min(m => m.ConversationMessageId);
                    peer.NextBeforeConversationMessageId = minCmid;
                    peer.LastUpdatedUtc = DateTime.UtcNow;
                    if (messages.Count < PageSize || minCmid <= 1) {
                        peer.IsComplete = true;
                        peer.NextBeforeConversationMessageId = 0;
                    }

                    await SaveAndPublishPeerAsync(state, peer, chat);
                    await Task.Delay(ApiDelayMs, ct);
                }

                if (apiCalls >= MaxApiCallsPerRun) break;
                await Task.Delay(PeerDelayMs, ct);
            }

            state.State = state.Peers.Any(p => !p.IsComplete) ? "idle" : "completed";
            state.IsPaused = false;
            state.CurrentPeerId = 0;
            state.CurrentPeerTitle = null;
            state.FinishedAtUtc = DateTime.UtcNow;
            await SaveAndPublishAsync(state);
        }

        private async Task RefreshDemoAsync(CancellationToken ct) {
            DemoModeSession demoSession = DemoMode.GetDemoSessionById(session.Id);
            HistoryStatisticsState state = new HistoryStatisticsState {
                State = "completed",
                StartedAtUtc = DateTime.UtcNow,
                FinishedAtUtc = DateTime.UtcNow,
                TotalPeers = session.ImViewModel.SortedChats.Count,
                PageSize = PageSize,
                ApiDelayMs = 0
            };

            Dictionary<long, ChatViewModel> chats = session.ImViewModel.SortedChats
                .Where(c => c != null)
                .GroupBy(c => c.PeerId)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (IGrouping<long, Message> group in (demoSession?.Messages ?? new List<Message>())
                .Where(m => m != null && m.PeerId != 0 && m.ConversationMessageId > 0)
                .GroupBy(m => m.PeerId)) {
                ct.ThrowIfCancellationRequested();

                PeerHistoryStatistics peer = state.GetPeer(group.Key);
                if (chats.TryGetValue(group.Key, out ChatViewModel chat)) {
                    peer.PeerTitle = chat.Title;
                    peer.PeerAvatar = chat.Avatar?.ToString();
                }

                foreach (Message message in group.OrderByDescending(m => m.ConversationMessageId)) {
                    AddMessage(peer, message);
                }

                peer.TotalMessagesEstimate = Math.Max(peer.TotalMessagesEstimate, peer.IndexedMessages);
                peer.IsComplete = true;
                peer.LastUpdatedUtc = DateTime.UtcNow;
            }

            await SaveAndPublishAsync(state);
        }

        private async Task<MessagesHistoryResponse> LoadHistoryPageAsync(long peerId, int beforeConversationMessageId) {
            if (beforeConversationMessageId > 1) {
                return await session.API.Messages.GetHistoryAsync(session.GroupId, peerId, 1, PageSize, beforeConversationMessageId, true, null, false, 0);
            }

            return await session.API.Messages.GetHistoryAsync(session.GroupId, peerId, 0, PageSize, 0, true, null, false, 0);
        }

        private static void IndexLoadedMessages(PeerHistoryStatistics peer, ChatViewModel chat) {
            if (chat?.ReceivedMessages == null) return;

            int previousMax = peer.LastKnownMaxConversationMessageId;
            foreach (var message in chat.ReceivedMessages
                .Where(m => m != null && m.ConversationMessageId > previousMax)
                .OrderBy(m => m.ConversationMessageId)) {
                AddMessage(peer, message);
            }
        }

        private static void AddMessage(PeerHistoryStatistics peer, MessageViewModel message) {
            if (message == null) return;

            peer.LastKnownMaxConversationMessageId = Math.Max(peer.LastKnownMaxConversationMessageId, message.ConversationMessageId);
            peer.IndexedMessages++;
            UpdateDateRange(peer, message.SentTime);

            bool service = message.Action != null || message.IsExpired;
            if (service) {
                peer.ServiceMessages++;
            } else if (!String.IsNullOrWhiteSpace(message.Text)) {
                peer.TextMessages++;
            }

            peer.Attachments += message.Attachments?.Count ?? 0;
            peer.Reactions += message.Reactions?.Sum(r => Math.Max(0, r?.Count ?? 0)) ?? 0;
        }

        private static void AddMessage(PeerHistoryStatistics peer, Message message) {
            if (message == null) return;

            peer.LastKnownMaxConversationMessageId = Math.Max(peer.LastKnownMaxConversationMessageId, message.ConversationMessageId);
            peer.IndexedMessages++;
            UpdateDateRange(peer, message.DateTime);

            bool service = message.Action != null || message.IsExpired;
            if (service) {
                peer.ServiceMessages++;
            } else if (!String.IsNullOrWhiteSpace(message.Text)) {
                peer.TextMessages++;
            }

            peer.Attachments += message.Attachments?.Count ?? 0;
            peer.Reactions += message.Reactions?.Sum(r => Math.Max(0, r?.Count ?? 0)) ?? 0;
        }

        private static void UpdateDateRange(PeerHistoryStatistics peer, DateTime value) {
            if (value == default) return;
            if (peer.FirstMessageUtc == default || value.ToUniversalTime() < peer.FirstMessageUtc) peer.FirstMessageUtc = value.ToUniversalTime();
            if (peer.LastMessageUtc == default || value.ToUniversalTime() > peer.LastMessageUtc) peer.LastMessageUtc = value.ToUniversalTime();
        }

        private static int GetKnownMaxCmid(ChatViewModel chat) {
            return Math.Max(
                chat.LastMessage?.ConversationMessageId ?? 0,
                Math.Max(chat.InRead, chat.OutRead));
        }

        private async Task SaveAndPublishPeerAsync(HistoryStatisticsState state, PeerHistoryStatistics peer, ChatViewModel chat) {
            state.CurrentPeerId = chat.PeerId;
            state.CurrentPeerTitle = chat.Title;
            await SaveAndPublishAsync(state);
        }

        private async Task SaveAndPublishAsync(HistoryStatisticsState state) {
            state.LastUpdatedUtc = DateTime.UtcNow;
            await HistoryStatisticsStore.SaveStateAsync(state);
            Publish(HistoryStatisticsStore.BuildSnapshot(state));
        }

        private async Task UpdatePausedOnBatteryAsync() {
            HistoryStatisticsState state = HistoryStatisticsStore.ReadState();
            state.State = "paused_battery";
            state.IsPaused = true;
            state.LastUpdatedUtc = DateTime.UtcNow;
            state.PageSize = PageSize;
            state.ApiDelayMs = ApiDelayMs;
            await SaveAndPublishAsync(state);
            Log.Information("Background history statistics indexing skipped: device is on battery.");
        }

        private async Task UpdateErrorAsync(Exception ex) {
            HistoryStatisticsState state = HistoryStatisticsStore.ReadState();
            state.State = "error";
            state.ErrorCount++;
            state.LastError = ex.Message;
            state.LastUpdatedUtc = DateTime.UtcNow;
            await SaveAndPublishAsync(state);
        }

        private static void Publish(HistoryStatisticsSnapshot snapshot) {
            lock (SnapshotLock) lastSnapshot = Clone(snapshot);
        }

        private static HistoryStatisticsSnapshot Clone(HistoryStatisticsSnapshot source) {
            if (source == null) return new HistoryStatisticsSnapshot();

            return new HistoryStatisticsSnapshot {
                State = source.State,
                IsRunning = source.IsRunning,
                IsPaused = source.IsPaused,
                TotalPeers = source.TotalPeers,
                IndexedPeers = source.IndexedPeers,
                MessagesScanned = source.MessagesScanned,
                TotalMessagesEstimate = source.TotalMessagesEstimate,
                TextMessages = source.TextMessages,
                ServiceMessages = source.ServiceMessages,
                Attachments = source.Attachments,
                Reactions = source.Reactions,
                ApiCalls = source.ApiCalls,
                ErrorCount = source.ErrorCount,
                CurrentPeerTitle = source.CurrentPeerTitle,
                CurrentPeerId = source.CurrentPeerId,
                LastError = source.LastError,
                LastUpdatedUtc = source.LastUpdatedUtc,
                ApiDelayMs = source.ApiDelayMs,
                PageSize = source.PageSize,
                ProgressPercent = source.ProgressPercent
            };
        }
    }

    public sealed class HistoryStatisticsState {
        public string State { get; set; } = "idle";
        public bool IsPaused { get; set; }
        public int TotalPeers { get; set; }
        public long ApiCalls { get; set; }
        public int ErrorCount { get; set; }
        public long CurrentPeerId { get; set; }
        public string CurrentPeerTitle { get; set; }
        public string LastError { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public DateTime FinishedAtUtc { get; set; }
        public DateTime LastUpdatedUtc { get; set; }
        public int ApiDelayMs { get; set; }
        public int PageSize { get; set; }
        public List<PeerHistoryStatistics> Peers { get; set; } = new List<PeerHistoryStatistics>();

        public PeerHistoryStatistics GetPeer(long peerId) {
            PeerHistoryStatistics peer = Peers.FirstOrDefault(p => p.PeerId == peerId);
            if (peer != null) return peer;

            peer = new PeerHistoryStatistics {
                PeerId = peerId
            };
            Peers.Add(peer);
            return peer;
        }
    }

    public sealed class PeerHistoryStatistics {
        public long PeerId { get; set; }
        public string PeerTitle { get; set; }
        public string PeerAvatar { get; set; }
        public int NextBeforeConversationMessageId { get; set; }
        public int LastKnownMaxConversationMessageId { get; set; }
        public long IndexedMessages { get; set; }
        public long TotalMessagesEstimate { get; set; }
        public long TextMessages { get; set; }
        public long ServiceMessages { get; set; }
        public long Attachments { get; set; }
        public long Reactions { get; set; }
        public bool IsComplete { get; set; }
        public DateTime FirstMessageUtc { get; set; }
        public DateTime LastMessageUtc { get; set; }
        public DateTime LastUpdatedUtc { get; set; }
    }

    internal static class HistoryStatisticsStore {
        private static string DirectoryPath => LocalDataProfile.GetCurrentAccountDirectory("history-statistics");
        private static string StatePath => Path.Combine(DirectoryPath, "index.json");

        public static HistoryStatisticsState ReadState() {
            if (!File.Exists(StatePath)) return new HistoryStatisticsState();

            try {
                HistoryStatisticsState state = (HistoryStatisticsState)JsonSerializer.Deserialize(File.ReadAllText(StatePath), typeof(HistoryStatisticsState), L2JsonSerializerContext.Default);
                return state ?? new HistoryStatisticsState();
            } catch {
                return new HistoryStatisticsState();
            }
        }

        public static HistoryStatisticsSnapshot ReadSnapshot() {
            return BuildSnapshot(ReadState());
        }

        public static async Task SaveStateAsync(HistoryStatisticsState state) {
            Directory.CreateDirectory(DirectoryPath);
            await File.WriteAllTextAsync(StatePath, JsonSerializer.Serialize(state, typeof(HistoryStatisticsState), L2JsonSerializerContext.Default));
        }

        public static HistoryStatisticsSnapshot BuildSnapshot(HistoryStatisticsState state) {
            state ??= new HistoryStatisticsState();

            long messages = state.Peers.Sum(p => p.IndexedMessages);
            long estimate = state.Peers.Sum(p => Math.Max(p.TotalMessagesEstimate, p.IndexedMessages));
            double progress = estimate > 0 ? Math.Min(100d, messages * 100d / estimate) : 0d;

            return new HistoryStatisticsSnapshot {
                State = state.State ?? "idle",
                IsRunning = String.Equals(state.State, "running", StringComparison.Ordinal),
                IsPaused = state.IsPaused || String.Equals(state.State, "paused_battery", StringComparison.Ordinal),
                TotalPeers = Math.Max(state.TotalPeers, state.Peers.Count),
                IndexedPeers = state.Peers.Count(p => p.IndexedMessages > 0 || p.IsComplete),
                MessagesScanned = messages,
                TotalMessagesEstimate = estimate,
                TextMessages = state.Peers.Sum(p => p.TextMessages),
                ServiceMessages = state.Peers.Sum(p => p.ServiceMessages),
                Attachments = state.Peers.Sum(p => p.Attachments),
                Reactions = state.Peers.Sum(p => p.Reactions),
                ApiCalls = state.ApiCalls,
                ErrorCount = state.ErrorCount,
                CurrentPeerId = state.CurrentPeerId,
                CurrentPeerTitle = state.CurrentPeerTitle,
                LastError = state.LastError,
                LastUpdatedUtc = state.LastUpdatedUtc,
                ApiDelayMs = state.ApiDelayMs,
                PageSize = state.PageSize,
                ProgressPercent = progress
            };
        }
    }
}
