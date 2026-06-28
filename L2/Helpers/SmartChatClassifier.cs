using ELOR.Laney.Core;
using ELOR.Laney.ViewModels;
using ELOR.Laney.ViewModels.Controls;
using ELOR.VKAPILib.Objects;
using System;
using System.Linq;

namespace ELOR.Laney.Helpers {
    public static class SmartChatClassifier {
        private static readonly string[] MoneyKeywords = ["чек", "оплата", "счет", "счёт", "заказ", "доставка", "банк", "карта", "перевод", "платеж", "платёж", "руб", "₽", "receipt", "invoice", "payment", "order"];
        private static readonly string[] DocumentKeywords = ["документ", "доки", "договор", "акт", "паспорт", "снилс", "инн", "pdf", "doc", "xlsx", "файл", "скан", "scan"];
        private static readonly string[] WorkKeywords = ["работа", "проект", "задача", "таск", "дедлайн", "встреча", "созвон", "релиз", "deploy", "прод", "pr", "bug", "баг"];
        private static readonly string[] MemeKeywords = ["мем", "мемы", "ахах", "хаха", "лол", "lol", "кек", "ору", "жиза"];
        private static readonly string[] ShoppingKeywords = ["покуп", "купить", "корзина", "маркет", "магазин", "доставка", "заказ", "ozon", "wildberries", "яндекс маркет", "shop", "store", "cart"];
        private static readonly string[] ServiceKeywords = ["магазин", "маркет", "доставка", "заказ", "промокод", "скидка", "чек", "получите", "подтверждение", "код", "service", "shop", "store", "delivery"];
        private static readonly string[] SpamKeywords = ["казино", "ставк", "букмек", "крипт", "airdrop", "заработ", "доход", "инвест", "розыгрыш", "подпишись", "скидка 90", "million", "prize"];

        public static bool MatchesMoney(ChatViewModel chat) {
            return MatchesAnyLocalTag(chat, "money", "finance", "receipt", "bill") || MatchesKeywords(GetFilterText(chat), MoneyKeywords);
        }

        public static bool MatchesDocuments(ChatViewModel chat) {
            return MatchesAnyLocalTag(chat, "docs", "documents", "paper") || MatchesKeywords(GetFilterText(chat), DocumentKeywords) || HasAttachment(chat, AttachmentType.Document);
        }

        public static bool MatchesWork(ChatViewModel chat) {
            return MatchesAnyLocalTag(chat, "work", "job", "project") || MatchesKeywords(GetFilterText(chat), WorkKeywords);
        }

        public static bool MatchesMemes(ChatViewModel chat) {
            return MatchesAnyLocalTag(chat, "meme", "memes", "fun") || MatchesKeywords(GetFilterText(chat), MemeKeywords) || HasAnyAttachment(chat, AttachmentType.Sticker, AttachmentType.UGCSticker, AttachmentType.Graffiti);
        }

        public static bool MatchesShopping(ChatViewModel chat) {
            return MatchesAnyLocalTag(chat, "shop", "shopping", "buy", "purchase") || MatchesKeywords(GetFilterText(chat), ShoppingKeywords);
        }

        public static bool MatchesServices(ChatViewModel chat) {
            return MatchesAnyLocalTag(chat, "service", "services", "bot", "notify") || (chat?.PeerType == PeerType.Group && MatchesKeywords(GetFilterText(chat), ServiceKeywords));
        }

        public static bool MatchesSpam(ChatViewModel chat) {
            return MatchesAnyLocalTag(chat, "spam", "ignore", "trash") || MatchesKeywords(GetFilterText(chat), SpamKeywords);
        }

        public static bool TryGetAutoRule(ChatViewModel chat, MessageViewModel message, bool isMention, out string category, out int ttlDays) {
            category = null;
            ttlDays = 0;

            if (chat == null || message == null || message.IsOutgoing) return false;
            if (isMention || chat.IsImportant || chat.IsPinned || chat.HasMention || message.IsImportant) return false;

            string text = GetFilterText(chat, message);
            if (MatchesKeywords(text, MoneyKeywords)) {
                category = "money";
                ttlDays = 365;
                return true;
            }

            if (chat.PeerType == PeerType.Group && MatchesKeywords(text, ServiceKeywords)) {
                category = "service";
                ttlDays = 30;
                return true;
            }

            return false;
        }

        private static bool MatchesKeywords(string text, string[] keywords) {
            if (String.IsNullOrWhiteSpace(text)) return false;
            return keywords.Any(k => text.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string GetFilterText(ChatViewModel chat) {
            return GetFilterText(chat, chat?.LastMessage);
        }

        private static string GetFilterText(ChatViewModel chat, MessageViewModel message) {
            string title = chat?.Title ?? String.Empty;
            string tags = chat?.LocalTagsText ?? String.Empty;
            string messageText = message?.Text ?? message?.ToString() ?? String.Empty;
            return $"{title} {tags} {messageText}";
        }

        private static bool MatchesAnyLocalTag(ChatViewModel chat, params string[] tags) {
            if (chat == null || tags == null || tags.Length == 0) return false;
            return tags.Any(tag => Settings.PeerHasLocalTag(chat.PeerId, tag));
        }

        private static bool HasAttachment(ChatViewModel chat, AttachmentType type) {
            return chat?.LastMessage?.Attachments?.Any(a => a.Type == type) == true;
        }

        private static bool HasAnyAttachment(ChatViewModel chat, params AttachmentType[] types) {
            return chat?.LastMessage?.Attachments?.Any(a => types.Contains(a.Type)) == true;
        }
    }
}
