using ELOR.Laney.Core;
using ELOR.Laney.ViewModels;
using ELOR.Laney.ViewModels.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ELOR.Laney.Helpers {
    public static class TaskExtractionHelper {
        private const int MaxCandidates = 30;

        private static readonly string[] TaskMarkers = [
            "надо", "нужно", "сделай", "сделать", "проверь", "посмотри", "скинь", "отправь",
            "напомни", "не забудь", "дедлайн", "срок", "todo", "fixme", "asap"
        ];

        public static async Task<string> ExtractAsync(IEnumerable<ChatViewModel> chats, ChatViewModel currentChat) {
            List<MessageViewModel> messages = BuildSourceMessages(chats, currentChat)
                .Where(IsTaskCandidate)
                .GroupBy(message => $"{message.PeerId}:{message.ConversationMessageId}")
                .Select(group => group.First())
                .OrderByDescending(message => message.SentTime)
                .Take(MaxCandidates)
                .ToList();

            foreach (MessageViewModel message in messages) {
                await QuickActionStore.AddTodoAsync(message.PeerId, BuildTodoText(message));
            }

            return BuildReport(messages);
        }

        private static IEnumerable<MessageViewModel> BuildSourceMessages(IEnumerable<ChatViewModel> chats, ChatViewModel currentChat) {
            if (currentChat?.DisplayedMessages != null) {
                foreach (MessageViewModel message in currentChat.DisplayedMessages) {
                    if (message != null) yield return message;
                }
            }

            foreach (ChatViewModel chat in chats ?? Array.Empty<ChatViewModel>()) {
                if (chat?.LastMessage != null) yield return chat.LastMessage;
            }
        }

        private static bool IsTaskCandidate(MessageViewModel message) {
            if (message == null || message.IsOutgoing) return false;

            string text = message.DisplayText;
            if (String.IsNullOrWhiteSpace(text)) return false;

            foreach (string marker in TaskMarkers) {
                if (text.Contains(marker, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return text.Contains('?') && text.Length <= 240;
        }

        private static string BuildTodoText(MessageViewModel message) {
            return $"auto cmid:{message.ConversationMessageId} from:{message.DisplaySenderName} {Shorten(message.DisplayText, 220)}";
        }

        private static string BuildReport(List<MessageViewModel> messages) {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"# Автоизвлечение задач: {DateTime.Now:dd.MM.yyyy HH:mm}");
            sb.AppendLine();
            sb.AppendLine($"Найдено кандидатов: {messages.Count}");
            sb.AppendLine("Источник: текущий открытый чат и последние сообщения из загруженного списка чатов. Полную историю VK не сканировал.");
            sb.AppendLine();

            if (messages.Count == 0) {
                sb.AppendLine("- Пусто.");
                return sb.ToString().TrimEnd();
            }

            foreach (MessageViewModel message in messages) {
                sb.AppendLine($"- peer:{message.PeerId} cmid:{message.ConversationMessageId} {Shorten(message.DisplayText, 160)}");
            }
            return sb.ToString().TrimEnd();
        }

        private static string Shorten(string text, int maxLength) {
            if (String.IsNullOrWhiteSpace(text)) return "(пусто)";

            string clean = text.Replace("\r", " ").Replace("\n", " ").Trim();
            if (clean.Length <= maxLength) return clean;
            return clean.Substring(0, maxLength - 1).TrimEnd() + "…";
        }
    }
}
