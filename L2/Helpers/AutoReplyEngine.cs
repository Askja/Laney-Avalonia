using ELOR.Laney.Core;
using ELOR.Laney.ViewModels;
using ELOR.Laney.ViewModels.Controls;
using ELOR.VKAPILib.Methods;
using ELOR.VKAPILib.Objects;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ELOR.Laney.Helpers {
    public static class AutoReplyEngine {
        public static async Task<int> ApplyAsync(ChatViewModel chat, MessageViewModel message, bool isMention) {
            if (chat == null || message == null || message.IsOutgoing) return 0;
            if (chat.Session?.API == null || chat.CanWrite?.Allowed != true) return 0;

            try {
                AutoReplyRuleSet ruleSet = AutoReplyRuleStore.GetRuleSet();
                foreach (AutoReplyRule rule in ruleSet.Rules) {
                    if (!Matches(rule, chat, message, isMention, DateTime.Now)) continue;

                    DateTimeOffset now = DateTimeOffset.Now;
                    if (!AutoReplyRuleStore.TryReserveSend(rule, chat.PeerId, message.SenderId, now)) continue;

                    string text = BuildReplyText(rule, chat, message);
                    await chat.Session.API.Messages.SendAsync(
                        chat.Session.GroupId,
                        chat.PeerId,
                        Random.Shared.Next(Int32.MinValue, Int32.MaxValue),
                        text,
                        0,
                        0,
                        new List<string>(),
                        null,
                        0,
                        dontParseLinks: Settings.DontParseLinks,
                        disableMentions: Settings.DisableMentions,
                        intent: MessageIntent.None);

                    await QuickActionStore.AddAutoRuleHitAsync(chat.PeerId, message.ConversationMessageId, $"auto-reply:{rule.Id}", 30);
                    Log.Information("Auto-reply sent. Peer={PeerId}; sender={SenderId}; cmid={Cmid}; rule={RuleId}",
                        chat.PeerId, message.SenderId, message.ConversationMessageId, rule.Id);
                    return 1;
                }
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot apply auto-reply rules.");
            }

            return 0;
        }

        private static bool Matches(AutoReplyRule rule, ChatViewModel chat, MessageViewModel message, bool isMention, DateTime now) {
            if (rule == null || !rule.Enabled) return false;
            if (rule.PeerId != 0 && rule.PeerId != chat.PeerId) return false;
            if (rule.SenderId != 0 && rule.SenderId != message.SenderId) return false;
            if (rule.PrivateOnly && chat.PeerType != PeerType.User) return false;
            if (rule.ChatsOnly && chat.PeerType != PeerType.Chat) return false;
            if (rule.MentionsOnly && !isMention) return false;
            if (rule.StartHour >= 0 && rule.EndHour >= 0 && !AutoStatusManager.IsScheduleActive(now, rule.StartHour, rule.EndHour)) return false;

            if (rule.ContainsAny.Count == 0) return true;

            string text = $"{chat.Title} {message.SenderName} {message.Text} {message}";
            return rule.ContainsAny.Any(token => text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string BuildReplyText(AutoReplyRule rule, ChatViewModel chat, MessageViewModel message) {
            string sourceText = message.Text;
            if (String.IsNullOrWhiteSpace(sourceText)) sourceText = message.ToString();
            sourceText = Normalize(sourceText, 160);

            return (rule.Text ?? String.Empty)
                .Replace("{sender}", message.SenderName ?? String.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("{chat}", chat.Title ?? String.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("{text}", sourceText, StringComparison.OrdinalIgnoreCase)
                .Trim();
        }

        private static string Normalize(string text, int limit) {
            if (String.IsNullOrWhiteSpace(text)) return String.Empty;

            string normalized = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return normalized.Length <= limit ? normalized : normalized[..limit] + "...";
        }
    }
}
