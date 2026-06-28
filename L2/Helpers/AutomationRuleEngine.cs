using ELOR.Laney.Core;
using ELOR.Laney.ViewModels;
using ELOR.Laney.ViewModels.Controls;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ELOR.Laney.Helpers {
    public sealed class AutomationRuleRunResult {
        public int MatchedCount { get; set; }
        public bool SuppressNotification { get; set; }
        public List<string> Actions { get; } = new List<string>();
    }

    public static class AutomationRuleEngine {
        public static async Task<AutomationRuleRunResult> ApplyAsync(ChatViewModel chat, MessageViewModel message, bool isMention) {
            AutomationRuleRunResult result = new AutomationRuleRunResult();
            if (chat == null || message == null) return result;

            AutomationRuleSet ruleSet = AutomationRuleStore.GetRuleSet();
            foreach (AutomationRule rule in ruleSet.Rules) {
                if (!Matches(rule, chat, message, isMention)) continue;

                result.MatchedCount++;
                bool suppress = await ExecuteAsync(rule, chat, message);
                result.SuppressNotification |= rule.SuppressNotification ?? suppress;
                result.Actions.Add(rule.ActionId);

                if (rule.StopProcessing) break;
            }

            if (result.MatchedCount > 0) {
                string category = String.Join("+", result.Actions.Distinct(StringComparer.OrdinalIgnoreCase));
                await QuickActionStore.AddAutoRuleHitAsync(chat.PeerId, message.ConversationMessageId, $"user:{category}", 30);
                Log.Information("User automation rules applied. Peer={PeerId}; cmid={Cmid}; count={Count}; suppress={Suppress}; actions={Actions}",
                    chat.PeerId, message.ConversationMessageId, result.MatchedCount, result.SuppressNotification, category);
            }

            return result;
        }

        private static bool Matches(AutomationRule rule, ChatViewModel chat, MessageViewModel message, bool isMention) {
            if (rule == null || !rule.Enabled) return false;
            if (!rule.IncludeOutgoing && message.IsOutgoing) return false;
            if (rule.PeerId != 0 && rule.PeerId != chat.PeerId) return false;
            if (rule.SenderId != 0 && rule.SenderId != message.SenderId) return false;
            if (rule.MentionsOnly && !isMention) return false;

            if (rule.ContainsAny.Count == 0) return true;

            string text = $"{chat.Title} {message.SenderName} {message.Text} {message}";
            return rule.ContainsAny.Any(token => text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static async Task<bool> ExecuteAsync(AutomationRule rule, ChatViewModel chat, MessageViewModel message) {
            string summary = BuildMessageSummary(message);
            switch (rule.ActionId) {
                case AutomationRuleActionIds.Mute:
                    int minutes = rule.DurationMinutes > 0 ? rule.DurationMinutes : 60;
                    Settings.SetPeerQuietUntil(chat.PeerId, DateTimeOffset.Now.AddMinutes(minutes));
                    return true;
                case AutomationRuleActionIds.Reminder:
                    await QuickActionStore.AddReminderAsync(chat.PeerId, BuildActionText(rule, message, summary), BuildDue(rule));
                    return false;
                case AutomationRuleActionIds.Todo:
                    await QuickActionStore.AddTodoAsync(chat.PeerId, BuildActionText(rule, message, summary));
                    return false;
                case AutomationRuleActionIds.Archive:
                    Settings.SetPeerArchived(chat.PeerId, true);
                    chat.RefreshLocalFolderState();
                    return true;
                case AutomationRuleActionIds.Tag:
                    await QuickActionStore.AddTagAsync(chat.PeerId, message.ConversationMessageId, rule.Value, summary);
                    return false;
                case AutomationRuleActionIds.Download:
                    await QuickActionStore.AddDownloadRequestAsync(chat.PeerId, message.ConversationMessageId, rule.Value, summary);
                    return false;
                default:
                    return false;
            }
        }

        private static string BuildActionText(AutomationRule rule, MessageViewModel message, string summary) {
            string prefix = String.IsNullOrWhiteSpace(rule.Value) ? rule.ActionId : rule.Value.Trim();
            return $"{prefix}: {summary}";
        }

        private static string BuildDue(AutomationRule rule) {
            if (rule.DurationMinutes <= 0) return null;
            return DateTimeOffset.Now.AddMinutes(rule.DurationMinutes).ToString("yyyy-MM-dd HH:mm");
        }

        private static string BuildMessageSummary(MessageViewModel message) {
            string text = message?.Text;
            if (String.IsNullOrWhiteSpace(text)) text = message?.ToString();
            if (String.IsNullOrWhiteSpace(text)) text = "(без текста)";

            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= 180 ? text : text[..180] + "...";
        }
    }
}
