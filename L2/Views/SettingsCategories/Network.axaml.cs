using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using ELOR.Laney.Core;
using ELOR.Laney.Views;
using ELOR.Laney.Views.Modals;
using System;
using System.Threading.Tasks;

namespace ELOR.Laney.Views.SettingsCategories {
    public partial class Network : UserControl {
        public Network() {
            InitializeComponent();
        }

        private void OpenApiDebugPanel_Click(object sender, RoutedEventArgs e) {
            Window owner = TopLevel.GetTopLevel(this) as Window;
            ApiDebugWindow window = new ApiDebugWindow {
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (owner == null) {
                window.Show();
            } else {
                window.Show(owner);
            }
        }

        private async void RunAccountHealthCheck_Click(object sender, RoutedEventArgs e) {
            Window owner = TopLevel.GetTopLevel(this) as Window;
            VKSession session = GetSession(owner);
            if (session == null) {
                await new VKUIDialog("Health-check недоступен", "Активная VK-сессия не найдена. Проверять нечего, магии не будет.", ["Понятно"], 1).ShowDialog(owner);
                return;
            }

            try {
                Task<AccountHealthReport> checkTask = AccountHealthChecker.CheckAsync(session);
                AccountHealthReport report = owner == null
                    ? await checkTask
                    : await new VKUIWaitDialog<AccountHealthReport>().ShowAsync(owner, checkTask);

                TextBox reportBox = new TextBox {
                    Text = report.ToDetailsText(),
                    IsReadOnly = true,
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.Wrap,
                    MinWidth = 520,
                    MinHeight = 280,
                    MaxHeight = 380
                };
                reportBox.Classes.Add("Mono");

                VKUIDialog dialog = new VKUIDialog("Health-check аккаунта", report.Summary, ["Понятно"], 1) {
                    DialogContent = reportBox
                };
                await dialog.ShowDialog(owner);
            } catch (Exception ex) {
                await new VKUIDialog("Health-check упал", ex.Message, ["Понятно"], 1).ShowDialog(owner);
            }
        }

        private static VKSession GetSession(Window owner) {
            if (owner is SettingsWindow settingsWindow && settingsWindow.Owner is MainWindow mainWindow && mainWindow.Session != null) {
                return mainWindow.Session;
            }

            return VKSession.Main;
        }
    }
}
