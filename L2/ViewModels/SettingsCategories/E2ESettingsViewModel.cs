using ELOR.Laney.Core;
using ELOR.Laney.DataModels;
using System.Collections.ObjectModel;
using System.Linq;

namespace ELOR.Laney.ViewModels.SettingsCategories {
    public sealed class E2ESettingsViewModel : CommonViewModel {
        public ObservableCollection<TwoStringTuple> Profiles { get; } = new ObservableCollection<TwoStringTuple>(
            E2ESecurityProfileIds.All.Select(id => new TwoStringTuple(E2ESecurityProfileIds.GetTitle(id), E2ESecurityProfileIds.GetSubtitle(id))));

        public string CurrentChatStatus {
            get {
                var chat = VKSession.Main?.CurrentOpenedChat;
                return chat?.HasE2EStatus == true ? chat.E2EStatusText : "Для текущего чата E2E не настроен";
            }
        }

        public string WorkflowText {
            get { return "E2E ключи живут на уровне чата: профиль, handshake, fingerprint, автошифрование, backup и сброс лежат в профиле конкретного диалога."; }
        }
    }
}
