using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ELOR.Laney.Core;
using ELOR.Laney.ViewModels.SettingsCategories;
using ELOR.Laney.Views.Modals;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VKUI.Controls;

namespace ELOR.Laney.Views.SettingsCategories {
    public partial class Stickers : UserControl {
        public Stickers() {
            InitializeComponent();
        }

        private StickersViewModel ViewModel => DataContext as StickersViewModel;

        private Window OwnerWindow => TopLevel.GetTopLevel(this) as Window;

        private static FilePickerFileType GetLocalStickerFileType() {
            return new FilePickerFileType("Telegram stickers") {
                Patterns = ["*.zip", "*.tgs", "*.webm", "*.webp", "*.gif", "*.png", "*.jpg", "*.jpeg"],
                MimeTypes = ["application/zip", "application/x-tgsticker", "video/webm", "image/webp", "image/gif", "image/png", "image/jpeg"]
            };
        }

        private static FilePickerFileType GetEmojiManifestFileType() {
            return new FilePickerFileType("Emoji manifest") {
                Patterns = ["*.txt", "*.map", "*.csv"],
                MimeTypes = ["text/plain", "text/csv"]
            };
        }

        private async void EmojiCustomPackBrowse_Click(object sender, RoutedEventArgs e) {
            var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storageProvider?.CanOpen != true) return;

            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType> { GetEmojiManifestFileType(), FilePickerFileTypes.All }
            });
            IStorageFile file = files?.FirstOrDefault();
            if (file == null) return;

            ViewModel.EmojiCustomPackPath = file.TryGetLocalPath() ?? file.Path.LocalPath;
        }

        private async void ImportLocalStickerFiles_Click(object sender, RoutedEventArgs e) {
            var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (storageProvider?.CanOpen != true) return;

            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
                AllowMultiple = true,
                FileTypeFilter = new List<FilePickerFileType> { GetLocalStickerFileType() }
            });
            if (files == null || files.Count == 0) return;

            int imported = await LocalStickerStore.ImportAsync(files);
            ViewModel?.ReloadLocalManager();
            await ShowInfoAsync(
                imported > 0 ? "Стикеры импортированы" : "Стикеры не импортированы",
                imported > 0
                    ? $"Добавлено локально: {imported}. В picker они лежат во вкладке `Локальные`."
                    : "Поддерживаются zip, tgs, webm, webp, gif, png, jpg. Возможно, всё уже было в библиотеке.");
        }

        private async void ImportTelegramPackLink_Click(object sender, RoutedEventArgs e) {
            Window owner = OwnerWindow;
            if (owner == null) return;

            TextBox linkBox = new TextBox {
                PlaceholderText = "https://t.me/addstickers/pack_name",
                Width = 380
            };
            TextBox tokenBox = new TextBox {
                PlaceholderText = "123456:telegram-bot-api-token",
                Text = SecureVault.GetSecret(LocalStickerStore.TelegramBotTokenSecretName),
                PasswordChar = '*',
                Width = 380
            };
            TextBlock policy = new TextBlock {
                Text = "Импорт по ссылке идёт через официальный Telegram Bot API и твой bot token. Пак сохраняется локально с пометкой TG; закрытый/платный контент не обходим.",
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 380
            };
            policy.Classes.Add("Caption1");

            StackPanel content = new StackPanel {
                Spacing = 8,
                Children = {
                    new TextBlock { Text = "Ссылка или имя TG-пака" },
                    linkBox,
                    new TextBlock { Text = "Bot API token" },
                    tokenBox,
                    policy
                }
            };

            VKUIDialog dialog = new VKUIDialog("Импорт Telegram-стикеров", "Пак будет добавлен в локальную библиотеку.", ["Импортировать", "Отмена"], 1) {
                DialogContent = content
            };
            int result = await dialog.ShowDialog<int>(owner);
            if (result != 1) return;

            await ImportTelegramPackLinkAsync(linkBox.Text, tokenBox.Text);
        }

        private async Task ImportTelegramPackLinkAsync(string link, string botToken) {
            try {
                botToken = botToken?.Trim();
                if (!String.IsNullOrWhiteSpace(botToken)) SecureVault.SetSecret(LocalStickerStore.TelegramBotTokenSecretName, botToken);

                LocalStickerImportResult result = await LocalStickerStore.ImportTelegramPackLinkAsync(link, botToken);
                ViewModel?.ReloadLocalManager();
                await ShowInfoAsync(
                    result.Imported > 0 ? "TG-пак импортирован" : "TG-пак не импортирован",
                    result.Imported > 0
                        ? $"{result.Summary}. В picker ищи вкладку `Локальные` и бейдж TG."
                        : $"{result.Summary}. Возможно, всё уже было в библиотеке.");
            } catch (Exception ex) {
                await ShowInfoAsync("TG-пак не импортирован", ex.GetBaseException().Message);
            }
        }

        private async Task ShowInfoAsync(string title, string text) {
            Window owner = OwnerWindow;
            if (owner == null) return;

            VKUIDialog dialog = new VKUIDialog(title, text, ["Понятно"], 1);
            await dialog.ShowDialog<int>(owner);
        }

        private void LocalStickerPackEnabled_Click(object sender, RoutedEventArgs e) {
            if (sender is ToggleSwitch toggle && toggle.DataContext is LocalStickerPackItemViewModel pack) {
                ViewModel?.SetPackEnabled(pack, toggle.IsChecked == true);
            }
        }

        private void LocalStickerPackUp_Click(object sender, RoutedEventArgs e) {
            if (sender is Control control && control.DataContext is LocalStickerPackItemViewModel pack) {
                ViewModel?.MovePack(pack, -1);
            }
        }

        private void LocalStickerPackDown_Click(object sender, RoutedEventArgs e) {
            if (sender is Control control && control.DataContext is LocalStickerPackItemViewModel pack) {
                ViewModel?.MovePack(pack, 1);
            }
        }

        private void LocalStickerFavorite_Click(object sender, RoutedEventArgs e) {
            if (sender is Control control && control.DataContext is LocalStickerItemViewModel item) {
                ViewModel?.ToggleFavorite(item);
            }
        }

        private void LocalStickerTagsSave_Click(object sender, RoutedEventArgs e) {
            if (sender is Control control && control.DataContext is LocalStickerItemViewModel item) {
                ViewModel?.SaveTags(item);
            }
        }
    }
}
