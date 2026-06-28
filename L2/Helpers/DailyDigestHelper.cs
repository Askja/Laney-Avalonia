using ELOR.Laney.Core;
using ELOR.Laney.ViewModels;
using ELOR.Laney.ViewModels.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELOR.Laney.Helpers {
    public static class DailyDigestHelper {
        private const int MaxSectionItems = 10;

        public static async Task<string> BuildAsync(IEnumerable<ChatViewModel> chats) {
            List<ChatViewModel> safeChats = chats?.ToList() ?? new List<ChatViewModel>();
            DateTime today = DateTime.Today;
            DateTimeOffset now = DateTimeOffset.Now;

            List<ChatViewModel> todayChats = safeChats
                .Where(c => c.LastMessage?.SentTime.Date == today)
                .OrderByDescending(c => c.LastMessage.SentTime)
                .Take(MaxSectionItems)
                .ToList();

            List<ChatViewModel> importantChats = safeChats
                .Where(c => c.IsImportant || c.IsPinned || c.HasMention || c.LastMessage?.IsImportant == true)
                .OrderByDescending(c => c.SortIndex)
                .Take(MaxSectionItems)
                .ToList();

            List<ChatViewModel> waitingChats = safeChats
                .Where(IsWaitingForReply)
                .OrderByDescending(c => c.SortIndex)
                .Take(MaxSectionItems)
                .ToList();

            List<string> overdueReminders = (await QuickActionStore.ReadReminderLinesAsync())
                .Where(line => TryGetDue(line, out DateTimeOffset due) && due <= now)
                .Take(MaxSectionItems)
                .ToList();

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"# Сводка дня: {DateTime.Now:dd.MM.yyyy HH:mm}");
            sb.AppendLine();
            AppendChatSection(sb, "Кто писал сегодня", todayChats, includeSender: true);
            AppendChatSection(sb, "Важное", importantChats, includeSender: false);
            AppendTextSection(sb, "Просроченные reminder", overdueReminders);
            AppendChatSection(sb, "Ждут ответа", waitingChats, includeSender: false);
            sb.AppendLine();
            sb.AppendLine("Источник: загруженный локальный список чатов и quick-actions/reminders.md. Полную историю VK не сканировал, без фокусов и прожига API.");

            string digest = sb.ToString().TrimEnd();
            await QuickActionStore.SaveDailyDigestAsync(digest);
            return digest;
        }

        private static bool IsWaitingForReply(ChatViewModel chat) {
            MessageViewModel message = chat.LastMessage;
            if (chat.IsUnanswered) return true;
            if (message == null || message.IsOutgoing) return false;
            return chat.UnreadMessagesCount > 0 || chat.IsMarkedAsUnread || chat.HasMention;
        }

        private static void AppendChatSection(StringBuilder sb, string title, List<ChatViewModel> chats, bool includeSender) {
            sb.AppendLine($"## {title}");
            if (chats.Count == 0) {
                sb.AppendLine("- Пусто.");
                sb.AppendLine();
                return;
            }

            foreach (ChatViewModel chat in chats) {
                MessageViewModel message = chat.LastMessage;
                string time = message?.SentTime.ToString("HH:mm") ?? "--:--";
                string sender = includeSender && message != null ? $"{GetSender(message)}: " : String.Empty;
                sb.AppendLine($"- {time} {chat.DisplayTitle}: {sender}{GetPreview(message)}");
            }
            sb.AppendLine();
        }

        private static void AppendTextSection(StringBuilder sb, string title, List<string> lines) {
            sb.AppendLine($"## {title}");
            if (lines.Count == 0) {
                sb.AppendLine("- Пусто.");
                sb.AppendLine();
                return;
            }

            foreach (string line in lines) {
                sb.AppendLine($"- {Shorten(line, 180)}");
            }
            sb.AppendLine();
        }

        private static string GetSender(MessageViewModel message) {
            if (message.IsOutgoing) return "ты";
            return String.IsNullOrWhiteSpace(message.DisplaySenderName) ? "собеседник" : message.DisplaySenderName;
        }

        private static string GetPreview(MessageViewModel message) {
            if (message == null) return "без сообщений";
            return Shorten(message.DisplayPreviewText, 140);
        }

        private static string Shorten(string text, int maxLength) {
            if (String.IsNullOrWhiteSpace(text)) return "(пусто)";
            string clean = text.Replace("\r", " ").Replace("\n", " ").Trim();
            if (clean.Length <= maxLength) return clean;
            return clean.Substring(0, maxLength - 1).TrimEnd() + "…";
        }

        private static bool TryGetDue(string line, out DateTimeOffset due) {
            due = default;
            if (String.IsNullOrWhiteSpace(line)) return false;

            int dueIndex = line.IndexOf(" due:", StringComparison.OrdinalIgnoreCase);
            if (dueIndex < 0) return false;

            int valueStart = dueIndex + " due:".Length;
            int valueEnd = line.IndexOf(' ', valueStart);
            string value = valueEnd < 0 ? line.Substring(valueStart) : line.Substring(valueStart, valueEnd - valueStart);
            return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out due);
        }
    }
}
