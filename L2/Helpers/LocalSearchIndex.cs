using ELOR.Laney.Core;
using ELOR.Laney.ViewModels;
using ELOR.Laney.ViewModels.Controls;
using ELOR.VKAPILib.Objects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ELOR.Laney.Helpers {
    public sealed class LocalSearchIndexEntry {
        public long PeerId { get; set; }
        public int ConversationMessageId { get; set; }
        public string PeerName { get; set; }
        public string PeerAvatar { get; set; }
        public string Text { get; set; }
        public string AttachmentText { get; set; }
        public string AttachmentKinds { get; set; }
        public DateTime SentDate { get; set; }
    }

    public static class LocalSearchIndex {
        private const int MaxEntries = 20000;
        private static string DirectoryPath => LocalDataProfile.GetCurrentAccountDirectory("search-index");
        private static string MessagesPath => Path.Combine(DirectoryPath, "messages.json");

        public static async Task RefreshFromChatsAsync(IEnumerable<ChatViewModel> chats) {
            if (chats == null) return;

            Dictionary<string, LocalSearchIndexEntry> entries = (await ReadEntriesAsync())
                .GroupBy(e => GetKey(e.PeerId, e.ConversationMessageId))
                .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.SentDate).First());

            foreach (ChatViewModel chat in chats) {
                foreach (MessageViewModel message in chat.ReceivedMessages) {
                    LocalSearchIndexEntry entry = BuildEntry(chat, message);
                    if (entry == null) continue;
                    entries[GetKey(entry.PeerId, entry.ConversationMessageId)] = entry;
                }
            }

            List<LocalSearchIndexEntry> compacted = entries.Values
                .OrderByDescending(e => e.SentDate)
                .Take(MaxEntries)
                .ToList();

            Directory.CreateDirectory(DirectoryPath);
            await using FileStream fs = File.Create(MessagesPath);
            WriteEntries(fs, compacted);
            await fs.FlushAsync();
        }

        public static async Task<IReadOnlyList<LocalSearchIndexEntry>> SearchMessagesAsync(string query, int count) {
            if (String.IsNullOrWhiteSpace(query)) return Array.Empty<LocalSearchIndexEntry>();

            string needle = query.Trim();
            return (await ReadEntriesAsync())
                .Where(e => Contains(e.Text, needle)
                    || Contains(e.PeerName, needle)
                    || Contains(e.AttachmentText, needle)
                    || Contains(e.AttachmentKinds, needle))
                .OrderByDescending(e => e.SentDate)
                .Take(count)
                .ToList();
        }

        private static LocalSearchIndexEntry BuildEntry(ChatViewModel chat, MessageViewModel message) {
            if (chat == null || message == null || message.Action != null || message.IsExpired) return null;

            string text = message.Text;
            if (String.IsNullOrWhiteSpace(text)) text = message.ToString();
            string attachmentText = BuildAttachmentText(message.Attachments);
            string attachmentKinds = BuildAttachmentKinds(message.Attachments);
            if (String.IsNullOrWhiteSpace(text) && String.IsNullOrWhiteSpace(attachmentText)) return null;

            return new LocalSearchIndexEntry {
                PeerId = chat.PeerId,
                ConversationMessageId = message.ConversationMessageId,
                PeerName = chat.Title,
                PeerAvatar = chat.Avatar?.ToString(),
                Text = text,
                AttachmentText = attachmentText,
                AttachmentKinds = attachmentKinds,
                SentDate = message.SentTime
            };
        }

        private static string BuildAttachmentText(IReadOnlyList<Attachment> attachments) {
            if (attachments == null || attachments.Count == 0) return String.Empty;

            List<string> parts = new List<string>();
            foreach (Attachment attachment in attachments) {
                switch (attachment.Type) {
                    case AttachmentType.Photo:
                        Add(parts, "фото photo image");
                        Add(parts, attachment.Photo?.Text);
                        Add(parts, LocalOcrService.GetImageSource(attachment));
                        AddOcrText(parts, attachment);
                        break;
                    case AttachmentType.Document:
                        Add(parts, "документ файл doc file");
                        Add(parts, attachment.Document?.Title);
                        Add(parts, attachment.Document?.Extension);
                        Add(parts, attachment.Document?.Url);
                        AddOcrText(parts, attachment);
                        break;
                    case AttachmentType.Link:
                        Add(parts, "ссылка link url");
                        Add(parts, attachment.Link?.Title);
                        Add(parts, attachment.Link?.Caption);
                        Add(parts, attachment.Link?.Description);
                        Add(parts, attachment.Link?.Url);
                        break;
                    case AttachmentType.AudioMessage:
                        Add(parts, "голосовое voice audio transcript расшифровка");
                        Add(parts, attachment.AudioMessage?.Transcript);
                        break;
                }
            }

            return String.Join(" ", parts);
        }

        private static string BuildAttachmentKinds(IReadOnlyList<Attachment> attachments) {
            if (attachments == null || attachments.Count == 0) return String.Empty;

            return String.Join(" ", attachments.Select(a => a.Type switch {
                AttachmentType.Photo => "photo фото image картинка",
                AttachmentType.Document => "document doc файл документ",
                AttachmentType.Link => "link url ссылка",
                AttachmentType.AudioMessage => "voice audio голосовое transcript расшифровка",
                _ => a.Type.ToString()
            }));
        }

        private static void Add(List<string> parts, string text) {
            if (!String.IsNullOrWhiteSpace(text)) parts.Add(text.Trim());
        }

        private static void AddOcrText(List<string> parts, Attachment attachment) {
            string source = LocalOcrService.GetImageSource(attachment);
            string text = LocalOcrService.GetCachedText(source);
            if (!String.IsNullOrWhiteSpace(text)) Add(parts, $"ocr распознавание текст {text}");
        }

        private static Task<List<LocalSearchIndexEntry>> ReadEntriesAsync() {
            if (!File.Exists(MessagesPath)) return Task.FromResult(new List<LocalSearchIndexEntry>());

            try {
                using FileStream fs = File.OpenRead(MessagesPath);
                using JsonDocument document = JsonDocument.Parse(fs);
                if (document.RootElement.ValueKind != JsonValueKind.Array) return Task.FromResult(new List<LocalSearchIndexEntry>());

                List<LocalSearchIndexEntry> entries = new List<LocalSearchIndexEntry>();
                foreach (JsonElement element in document.RootElement.EnumerateArray()) {
                    LocalSearchIndexEntry entry = ReadEntry(element);
                    if (entry != null) entries.Add(entry);
                }

                return Task.FromResult(entries);
            } catch {
                return Task.FromResult(new List<LocalSearchIndexEntry>());
            }
        }

        private static void WriteEntries(Stream stream, List<LocalSearchIndexEntry> entries) {
            using Utf8JsonWriter writer = new Utf8JsonWriter(stream);
            writer.WriteStartArray();
            foreach (LocalSearchIndexEntry entry in entries) {
                writer.WriteStartObject();
                writer.WriteNumber("peerId", entry.PeerId);
                writer.WriteNumber("conversationMessageId", entry.ConversationMessageId);
                writer.WriteString("peerName", entry.PeerName);
                writer.WriteString("peerAvatar", entry.PeerAvatar);
                writer.WriteString("text", entry.Text);
                writer.WriteString("attachmentText", entry.AttachmentText);
                writer.WriteString("attachmentKinds", entry.AttachmentKinds);
                writer.WriteString("sentDate", entry.SentDate);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        private static LocalSearchIndexEntry ReadEntry(JsonElement element) {
            if (element.ValueKind != JsonValueKind.Object) return null;

            return new LocalSearchIndexEntry {
                PeerId = ReadInt64(element, "peerId"),
                ConversationMessageId = ReadInt32(element, "conversationMessageId"),
                PeerName = ReadString(element, "peerName"),
                PeerAvatar = ReadString(element, "peerAvatar"),
                Text = ReadString(element, "text"),
                AttachmentText = ReadString(element, "attachmentText"),
                AttachmentKinds = ReadString(element, "attachmentKinds"),
                SentDate = ReadDateTime(element, "sentDate")
            };
        }

        private static string ReadString(JsonElement element, string propertyName) {
            return element.TryGetProperty(propertyName, out JsonElement property) && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
        }

        private static int ReadInt32(JsonElement element, string propertyName) {
            return element.TryGetProperty(propertyName, out JsonElement property) && property.TryGetInt32(out int value)
                ? value
                : 0;
        }

        private static long ReadInt64(JsonElement element, string propertyName) {
            return element.TryGetProperty(propertyName, out JsonElement property) && property.TryGetInt64(out long value)
                ? value
                : 0;
        }

        private static DateTime ReadDateTime(JsonElement element, string propertyName) {
            string value = ReadString(element, propertyName);
            return DateTime.TryParse(value, out DateTime parsed) ? parsed : DateTime.MinValue;
        }

        private static string GetKey(long peerId, int cmid) {
            return $"{peerId}:{cmid}";
        }

        private static bool Contains(string source, string query) {
            return !String.IsNullOrWhiteSpace(source) && source.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
