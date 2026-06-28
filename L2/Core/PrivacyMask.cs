using ELOR.Laney.Extensions;

namespace ELOR.Laney.Core {
    public static class PrivacyMask {
        public const string HiddenSenderName = "Скрытый отправитель";
        public const string HiddenMessage = "Сообщение скрыто";
        public const string HiddenSubtitle = "Данные скрыты";
        public const string HiddenActivity = "Активность скрыта";

        public static string GetPeerTitle(long peerId) {
            if (peerId.IsChat()) return "Скрытый чат";
            if (peerId.IsGroup()) return "Скрытое сообщество";
            return "Скрытый контакт";
        }

        public static string GetPeerInitials(long peerId) {
            if (peerId.IsChat()) return "Ч";
            if (peerId.IsGroup()) return "С";
            return "К";
        }
    }
}
