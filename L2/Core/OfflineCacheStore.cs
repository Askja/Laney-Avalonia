using ELOR.Laney.ViewModels;
using ELOR.Laney.ViewModels.Controls;
using ELOR.Laney.Execute.Objects;
using ELOR.VKAPILib.Objects;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ELOR.Laney.Core {
    public sealed class OfflineDownloadedAttachmentRecord {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string Kind { get; set; }
        public string OriginalName { get; set; }
        public string SourceUrl { get; set; }
        public long PeerId { get; set; }
        public int ConversationMessageId { get; set; }
        public int ParentConversationMessageId { get; set; }
        public long SenderId { get; set; }
        public DateTime SentTime { get; set; }
        public long Bytes { get; set; }
        public string Sha256 { get; set; }
        public string Tags { get; set; }
        public DateTime SavedAt { get; set; }
    }

    public static class OfflineCacheStore {
        private const int MaxMessagesPerChat = 1000;
        private const int MaxDownloadedAttachments = 5000;

        private static string DirectoryPath => LocalDataProfile.GetCurrentAccountDirectory("offline");
        private static string ChatsDirectoryPath => Path.Combine(DirectoryPath, "chats");
        private static string DownloadedAttachmentsPath => Path.Combine(DirectoryPath, "downloaded-attachments.json");

        public static async Task SaveChatSnapshotAsync(ChatViewModel chat) {
            if (chat == null || chat.PeerId == 0) return;

            try {
                OfflineChatSnapshot snapshot = BuildSnapshot(chat);
                if (snapshot.Messages.Count == 0) return;

                Directory.CreateDirectory(ChatsDirectoryPath);
                string path = GetChatSnapshotPath(chat.PeerId);
                await File.WriteAllTextAsync(path, Serialize(snapshot, typeof(OfflineChatSnapshot)));
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot save offline chat snapshot. Peer={PeerId}", chat?.PeerId);
            }
        }

        public static async Task<List<MessageViewModel>> LoadChatMessagesAsync(VKSession session, ChatViewModel chat, int startMessageId, int count, Action<MessageViewModel> afterBuild = null) {
            if (session == null || chat == null || count <= 0) return new List<MessageViewModel>();

            try {
                string path = GetChatSnapshotPath(chat.PeerId);
                if (!File.Exists(path)) return new List<MessageViewModel>();

                OfflineChatSnapshot snapshot = (OfflineChatSnapshot)Deserialize(await File.ReadAllTextAsync(path), typeof(OfflineChatSnapshot));
                if (snapshot?.Messages == null || snapshot.Messages.Count == 0) return new List<MessageViewModel>();

                List<OfflineMessageSnapshot> messages = SelectWindow(snapshot.Messages, startMessageId, count);
                List<MessageViewModel> result = new List<MessageViewModel>(messages.Count);
                foreach (OfflineMessageSnapshot item in messages) {
                    MessageViewModel message = MessageViewModel.Create(item.ToMessage(), session, true);
                    afterBuild?.Invoke(message);
                    result.Add(message);
                }
                return result;
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot load offline chat snapshot. Peer={PeerId}", chat?.PeerId);
                return new List<MessageViewModel>();
            }
        }

        public static async Task RegisterDownloadedAttachmentAsync(string targetPath, string kind, string originalName, string sourceUrl, long peerId, int conversationMessageId, int parentConversationMessageId, long senderId, DateTime sentTime, long bytes, string sha256) {
            if (String.IsNullOrWhiteSpace(targetPath)) return;

            try {
                List<OfflineDownloadedAttachmentRecord> records = await ReadDownloadedAttachmentsAsync();
                string existingTags = records
                    .Where(r => String.Equals(r.FilePath, targetPath, StringComparison.OrdinalIgnoreCase)
                        || (!String.IsNullOrWhiteSpace(sha256) && String.Equals(r.Sha256, sha256, StringComparison.OrdinalIgnoreCase)))
                    .Select(r => r.Tags)
                    .FirstOrDefault(t => !String.IsNullOrWhiteSpace(t));
                records.RemoveAll(r => String.Equals(r.FilePath, targetPath, StringComparison.OrdinalIgnoreCase));
                records.Insert(0, new OfflineDownloadedAttachmentRecord {
                    FilePath = targetPath,
                    FileName = Path.GetFileName(targetPath),
                    Kind = kind,
                    OriginalName = originalName,
                    SourceUrl = sourceUrl,
                    PeerId = peerId,
                    ConversationMessageId = conversationMessageId,
                    ParentConversationMessageId = parentConversationMessageId,
                    SenderId = senderId,
                    SentTime = sentTime,
                    Bytes = bytes,
                    Sha256 = sha256,
                    Tags = existingTags,
                    SavedAt = DateTime.Now
                });

                Directory.CreateDirectory(DirectoryPath);
                await File.WriteAllTextAsync(DownloadedAttachmentsPath, Serialize(records.Take(MaxDownloadedAttachments).ToList(), typeof(List<OfflineDownloadedAttachmentRecord>)));
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot register offline downloaded attachment. Path={Path}", targetPath);
            }
        }

        public static async Task<IReadOnlyList<OfflineDownloadedAttachmentRecord>> GetDownloadedAttachmentsAsync() {
            return await ReadDownloadedAttachmentsAsync();
        }

        public static async Task<IReadOnlyList<OfflineDownloadedAttachmentRecord>> SearchDownloadedAttachmentsAsync(string query, int maxCount = 200) {
            List<OfflineDownloadedAttachmentRecord> records = await ReadDownloadedAttachmentsAsync();
            string[] terms = NormalizeSearchQuery(query);
            IEnumerable<OfflineDownloadedAttachmentRecord> result = records;

            if (terms.Length > 0) {
                result = result.Where(r => terms.All(term => BuildDownloadedAttachmentSearchText(r).Contains(term, StringComparison.OrdinalIgnoreCase)));
            }

            return result
                .OrderByDescending(r => r.SavedAt)
                .Take(Math.Clamp(maxCount, 1, MaxDownloadedAttachments))
                .ToList();
        }

        public static async Task SetDownloadedAttachmentTagsAsync(string filePath, string tags) {
            if (String.IsNullOrWhiteSpace(filePath)) return;

            List<OfflineDownloadedAttachmentRecord> records = await ReadDownloadedAttachmentsAsync();
            OfflineDownloadedAttachmentRecord record = records.FirstOrDefault(r => String.Equals(r.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
            if (record == null) return;

            record.Tags = NormalizeTags(tags);
            Directory.CreateDirectory(DirectoryPath);
            await File.WriteAllTextAsync(DownloadedAttachmentsPath, Serialize(records.Take(MaxDownloadedAttachments).ToList(), typeof(List<OfflineDownloadedAttachmentRecord>)));
        }

        public static async Task<int> CleanupMissingDownloadedAttachmentsAsync() {
            List<OfflineDownloadedAttachmentRecord> records = await ReadDownloadedAttachmentsAsync();
            int before = records.Count;
            records = records.Where(r => File.Exists(r.FilePath)).ToList();
            Directory.CreateDirectory(DirectoryPath);
            await File.WriteAllTextAsync(DownloadedAttachmentsPath, Serialize(records.Take(MaxDownloadedAttachments).ToList(), typeof(List<OfflineDownloadedAttachmentRecord>)));
            return before - records.Count;
        }

        private static OfflineChatSnapshot BuildSnapshot(ChatViewModel chat) {
            List<MessageViewModel> messages = new List<MessageViewModel>();
            HashSet<int> ids = new HashSet<int>();
            Add(chat.DisplayedMessages);
            Add(chat.ReceivedMessages);

            return new OfflineChatSnapshot {
                PeerId = chat.PeerId,
                Title = chat.Title,
                Avatar = chat.Avatar?.ToString(),
                CapturedAt = DateTime.Now,
                Messages = messages
                    .OrderByDescending(m => m.SentTime)
                    .ThenByDescending(m => m.ConversationMessageId)
                    .Take(MaxMessagesPerChat)
                    .OrderBy(m => m.ConversationMessageId)
                    .Select(OfflineMessageSnapshot.FromViewModel)
                    .ToList()
            };

            void Add(IEnumerable<MessageViewModel> source) {
                if (source == null) return;
                foreach (MessageViewModel message in source) {
                    if (message == null || message.IsExpired) continue;
                    int id = message.ConversationMessageId != 0 ? message.ConversationMessageId : message.GlobalId;
                    if (!ids.Add(id)) continue;
                    messages.Add(message);
                }
            }
        }

        private static List<OfflineMessageSnapshot> SelectWindow(List<OfflineMessageSnapshot> messages, int startMessageId, int count) {
            List<OfflineMessageSnapshot> ordered = messages
                .Where(m => m != null && m.ConversationMessageId > 0)
                .OrderBy(m => m.ConversationMessageId)
                .ToList();

            if (ordered.Count <= count) return ordered;
            if (startMessageId <= 0) return ordered.Skip(Math.Max(0, ordered.Count - count)).Take(count).ToList();

            int center = ordered.FindIndex(m => m.ConversationMessageId >= startMessageId);
            if (center < 0) center = ordered.Count - 1;
            int start = Math.Max(0, center - count / 2);
            if (start + count > ordered.Count) start = Math.Max(0, ordered.Count - count);
            return ordered.Skip(start).Take(count).ToList();
        }

        private static async Task<List<OfflineDownloadedAttachmentRecord>> ReadDownloadedAttachmentsAsync() {
            if (!File.Exists(DownloadedAttachmentsPath)) return new List<OfflineDownloadedAttachmentRecord>();

            try {
                List<OfflineDownloadedAttachmentRecord> records = (List<OfflineDownloadedAttachmentRecord>)Deserialize(await File.ReadAllTextAsync(DownloadedAttachmentsPath), typeof(List<OfflineDownloadedAttachmentRecord>)) ?? new List<OfflineDownloadedAttachmentRecord>();
                return records.Where(r => !String.IsNullOrWhiteSpace(r?.FilePath)).ToList();
            } catch {
                return new List<OfflineDownloadedAttachmentRecord>();
            }
        }

        private static string Serialize(object value, Type type) {
            return JsonSerializer.Serialize(value, type, L2JsonSerializerContext.Default);
        }

        private static object Deserialize(string json, Type type) {
            return JsonSerializer.Deserialize(json, type, L2JsonSerializerContext.Default);
        }

        private static string GetChatSnapshotPath(long peerId) {
            return Path.Combine(ChatsDirectoryPath, $"{peerId}.json");
        }

        private static string[] NormalizeSearchQuery(string query) {
            return (query ?? String.Empty)
                .Split([' ', ',', ';', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(t => !String.IsNullOrWhiteSpace(t))
                .Take(8)
                .ToArray();
        }

        private static string BuildDownloadedAttachmentSearchText(OfflineDownloadedAttachmentRecord record) {
            if (record == null) return String.Empty;
            return $"{record.FileName} {record.Kind} {record.OriginalName} {record.SourceUrl} {record.PeerId} {record.ConversationMessageId} {record.ParentConversationMessageId} {record.SenderId} {record.Sha256} {record.Tags}";
        }

        private static string NormalizeTags(string tags) {
            if (String.IsNullOrWhiteSpace(tags)) return String.Empty;
            return String.Join(", ", tags
                .Split([',', ';', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => t.Trim().Trim('#').ToLowerInvariant())
                .Where(t => !String.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(16));
        }

        public sealed class OfflineChatSnapshot {
            public long PeerId { get; set; }
            public string Title { get; set; }
            public string Avatar { get; set; }
            public DateTime CapturedAt { get; set; }
            public List<OfflineMessageSnapshot> Messages { get; set; } = new List<OfflineMessageSnapshot>();
        }

        public sealed class OfflineMessageSnapshot {
            public int GlobalId { get; set; }
            public int ConversationMessageId { get; set; }
            public int RandomId { get; set; }
            public long PeerId { get; set; }
            public long SenderId { get; set; }
            public long AdminAuthorId { get; set; }
            public DateTime SentTime { get; set; }
            public DateTime? EditTime { get; set; }
            public bool Important { get; set; }
            public string Text { get; set; }
            public List<Attachment> Attachments { get; set; }
            public List<Message> ForwardedMessages { get; set; }
            public Message ReplyMessage { get; set; }
            public Geo Geo { get; set; }
            public ELOR.VKAPILib.Objects.Action Action { get; set; }
            public BotKeyboard Keyboard { get; set; }
            public BotTemplate Template { get; set; }
            public string Payload { get; set; }
            public int TTL { get; set; }
            public bool IsUnavailable { get; set; }
            public int ReactionId { get; set; }
            public List<MessageReaction> Reactions { get; set; }
            public bool NestedMessagesHasMore { get; set; }

            public static OfflineMessageSnapshot FromViewModel(MessageViewModel message) {
                return new OfflineMessageSnapshot {
                    GlobalId = message.GlobalId,
                    ConversationMessageId = message.ConversationMessageId,
                    RandomId = message.RandomId,
                    PeerId = message.PeerId,
                    SenderId = message.SenderId,
                    AdminAuthorId = message.AdminAuthorId,
                    SentTime = message.SentTime,
                    EditTime = message.EditTime,
                    Important = message.IsImportant,
                    Text = message.Text,
                    Attachments = message.Attachments,
                    ForwardedMessages = message.ForwardedMessages,
                    ReplyMessage = message.ReplyMessage,
                    Geo = message.Location,
                    Action = message.Action,
                    Keyboard = message.Keyboard,
                    Template = message.Template,
                    Payload = message.Payload,
                    TTL = message.TTL,
                    IsUnavailable = message.IsUnavailable,
                    ReactionId = message.SelectedReactionId,
                    Reactions = message.Reactions?.ToList(),
                    NestedMessagesHasMore = message.HasMoreNestedMessage
                };
            }

            public Message ToMessage() {
                return new Message {
                    Id = GlobalId,
                    ConversationMessageId = ConversationMessageId,
                    RandomId = RandomId,
                    PeerId = PeerId,
                    FromId = SenderId,
                    AdminAuthorId = AdminAuthorId,
                    DateUnix = new DateTimeOffset(SentTime).ToUnixTimeSeconds(),
                    UpdateTimeUnix = EditTime == null ? 0 : new DateTimeOffset(EditTime.Value).ToUnixTimeSeconds(),
                    Important = Important,
                    Text = Text,
                    Attachments = Attachments,
                    ForwardedMessages = ForwardedMessages,
                    ReplyMessage = ReplyMessage,
                    Geo = Geo,
                    Action = Action,
                    Keyboard = Keyboard,
                    Template = Template,
                    PayLoad = Payload,
                    TTL = TTL,
                    IsUnavailable = IsUnavailable,
                    ReactionId = ReactionId,
                    Reactions = Reactions,
                    NestedMessagesHasMore = NestedMessagesHasMore
                };
            }
        }
    }
}
