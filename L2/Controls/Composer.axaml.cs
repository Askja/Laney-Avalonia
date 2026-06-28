using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using ELOR.Laney.Core;
using ELOR.Laney.DataModels;
using ELOR.Laney.Helpers;
using ELOR.Laney.ViewModels.Controls;
using ELOR.Laney.Views.Modals;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ELOR.Laney.Controls {
    public partial class Composer : UserControl {
        private const int MaxClipboardDataImageBytes = 12 * 1024 * 1024;
        private static readonly Regex DataImageRegex = new Regex(
            "data:(image/(?:png|jpe?g|webp));base64,([A-Za-z0-9+/=\\r\\n\\t ]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private ComposerViewModel ViewModel { get { return DataContext as ComposerViewModel; } }
        private Tuple<int, int> OldSelectionRange = null;

        public Composer() {
            InitializeComponent();
            DataContextChanged += Composer_DataContextChanged;
            DetachedFromVisualTree += Composer_DetachedFromVisualTree;
            Settings.SettingChanged += Settings_SettingChanged;
            UpdateActionButtonsVisibility();
        }

        public void FocusMessageText() {
            MessageText.Focus();
        }

        public void ShowAttachmentPicker() {
            ViewModel?.ShowAttachmentPickerContextMenu(AttachmentButton);
        }

        public void ShowStickerPicker() {
            ViewModel?.ShowEmojiStickerPicker(StickersButton);
        }

        public void ShowQuickActions() {
            ViewModel?.ShowQuickActions(QuickActionsButton);
        }

        private void Composer_DataContextChanged(object sender, EventArgs e) {
            UpdateActionButtonsVisibility();
        }

        private void Composer_DetachedFromVisualTree(object sender, Avalonia.VisualTreeAttachmentEventArgs e) {
            Settings.SettingChanged -= Settings_SettingChanged;
        }

        private void Settings_SettingChanged(string key, object value) {
            if (key == Settings.COMPOSER_ACTION_FORMATTING
                || key == Settings.COMPOSER_ACTION_QUICK
                || key == Settings.COMPOSER_ACTION_GROUP_TEMPLATES
                || key == Settings.COMPOSER_ACTION_STICKERS) {
                UpdateActionButtonsVisibility();
            }
        }

        private void UpdateActionButtonsVisibility() {
            FormattingButton.IsVisible = Settings.ComposerActionFormatting;
            QuickActionsButton.IsVisible = Settings.ComposerActionQuick;
            GroupsTemplatesButton.IsVisible = Settings.ComposerActionGroupTemplates && ViewModel?.IsGroupSession == true;
            StickersButton.IsVisible = Settings.ComposerActionStickers;
        }

        private void MessageText_DataContextChanged(object sender, EventArgs e) {
            MessageText.Focus();
        }

        private void MessageText_Loaded(object? sender, RoutedEventArgs e) {
            MessageText.Focus();
        }

        // Костыль для сохранения выделения в тексте сообщения после потери фокуса.
        private void MessageText_PropertyChanged(object sender, Avalonia.AvaloniaPropertyChangedEventArgs e) {
            if (e.Property == TextBox.SelectionStartProperty || e.Property == TextBox.SelectionEndProperty) {
                new Action(async () => {
                    await Task.Delay(10);
                    OldSelectionRange = new Tuple<int, int>(MessageText.SelectionStart, MessageText.SelectionEnd);
                })();
            }
        }

        // Костыль для сохранения выделения в тексте сообщения после потери фокуса.
        private void MessageText_LostFocus(object sender, RoutedEventArgs e) {
            if (OldSelectionRange != null && OldSelectionRange.Item1 != OldSelectionRange.Item2) {
                ViewModel.TextSelectionStart = OldSelectionRange.Item1;
                ViewModel.TextSelectionEnd = OldSelectionRange.Item2;
            }

            MessageText.SelectionStart = ViewModel.TextSelectionStart;
            MessageText.SelectionEnd = ViewModel.TextSelectionEnd;
        }

        private async void MessageText_KeyUp(object? sender, KeyEventArgs e) {
            Debug.WriteLine($"KeyUp: {e.Key}; Modifiers: {e.KeyModifiers}");
            if ((e.KeyModifiers & KeyModifiers.Control) == KeyModifiers.Control && e.Key == Key.V) {
                e.Handled = await TryPasteAttachmentsAsync();
            }
        }

        private async Task<bool> TryPasteAttachmentsAsync() {
            if (ViewModel == null || DemoMode.IsEnabled) return false;

            TopLevel topLevel = TopLevel.GetTopLevel(this);
            IClipboard clipboard = topLevel?.Clipboard;
            if (clipboard == null) return false;

            try {
                var items = await clipboard.TryGetFilesAsync();
                var files = items?.OfType<IStorageFile>().Take(10).ToList();
                if (files != null && files.Count > 0) {
                    await ViewModel.AddStorageFilesAsync(files, files.GetUploadCommandType());
                    return true;
                }

                IStorageFile bitmapFile = await TryCreateClipboardBitmapFileAsync(topLevel, clipboard);
                if (bitmapFile != null) {
                    await ViewModel.AddStorageFilesAsync(new[] { bitmapFile }, Constants.PhotoUploadCommand);
                    return true;
                }

                IStorageFile dataImageFile = await TryCreateClipboardDataImageFileAsync(topLevel, clipboard);
                if (dataImageFile != null) {
                    await ViewModel.AddStorageFilesAsync(new[] { dataImageFile }, Constants.PhotoUploadCommand);
                    return true;
                }
            } catch (Exception ex) {
                Log.Error(ex, "Unable to paste attachments from clipboard.");
                await ExceptionHelper.ShowErrorDialogAsync(VKSession.GetByDataContext(this).ModalWindow, ex, true);
            }

            return false;
        }

        private static async Task<IStorageFile> TryCreateClipboardBitmapFileAsync(TopLevel topLevel, IClipboard clipboard) {
            using Bitmap bitmap = await clipboard.TryGetBitmapAsync();
            if (bitmap == null) return null;

            string directory = Path.Combine(Path.GetTempPath(), "Laney", "Clipboard");
            Directory.CreateDirectory(directory);

            string path = Path.Combine(directory, $"laney-clipboard-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.png");
            bitmap.Save(path);

            return await topLevel.StorageProvider.TryGetFileFromPathAsync(path);
        }

        private static async Task<IStorageFile> TryCreateClipboardDataImageFileAsync(TopLevel topLevel, IClipboard clipboard) {
            string text = await clipboard.TryGetValueAsync(DataFormat.Text);
            if (String.IsNullOrWhiteSpace(text)) return null;

            Match match = DataImageRegex.Match(text);
            if (!match.Success) return null;

            string mime = match.Groups[1].Value.ToLowerInvariant();
            string base64 = match.Groups[2].Value;
            int estimatedBytes = base64.Count(c => !Char.IsWhiteSpace(c)) * 3 / 4;
            if (estimatedBytes > MaxClipboardDataImageBytes) {
                throw new InvalidOperationException($"Clipboard data image is too large: {estimatedBytes} bytes.");
            }

            byte[] bytes = Convert.FromBase64String(base64);

            string directory = Path.Combine(Path.GetTempPath(), "Laney", "Clipboard");
            Directory.CreateDirectory(directory);

            string extension = mime.Contains("jpeg") || mime.Contains("jpg") ? "jpg" : mime.Split('/').Last();
            string path = Path.Combine(directory, $"laney-clipboard-data-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.{extension}");
            await File.WriteAllBytesAsync(path, bytes);

            return await topLevel.StorageProvider.TryGetFileFromPathAsync(path);
        }

        private void MessageText_KeyDown(object sender, KeyEventArgs e) {
            Debug.WriteLine($"KeyDown: {e.Key}; Modifiers: {e.KeyModifiers}");

            if (e.Key == Key.Tab && ViewModel?.TryApplyFirstSlashCommandSuggestion() == true) {
                e.Handled = true;
                MessageText.Focus();
                return;
            }

            new Action(async () => {
                if ((e.KeyModifiers & KeyModifiers.Control) == KeyModifiers.Control) {
                    if (e.Key == Key.B) {
                        e.Handled = true;
                        ViewModel.ApplyBoldFormat();
                        return;
                    }

                    if (e.Key == Key.I) {
                        e.Handled = true;
                        ViewModel.ApplyItalicFormat();
                        return;
                    }

                    if (e.Key == Key.K) {
                        e.Handled = true;
                        ViewModel.ApplyCodeFormat();
                        return;
                    }

                    if ((e.KeyModifiers & KeyModifiers.Shift) == KeyModifiers.Shift && e.Key == Key.X) {
                        e.Handled = true;
                        ViewModel.ApplyStrikeFormat();
                        return;
                    }
                }

                if (e.Key == Key.Enter) {
                    if (!Settings.SentViaEnter) {
                        if (e.KeyModifiers == KeyModifiers.Control && ViewModel.CanSendMessage && !ViewModel.IsLoading) {
                            e.Handled = true;
                            await ViewModel.SendMessageAsync();
                        } else {
                            InsertNewLine();
                        }
                    } else {
                        if (e.KeyModifiers != KeyModifiers.Shift && ViewModel.CanSendMessage && !ViewModel.IsLoading) {
                            e.Handled = true;
                            await ViewModel.SendMessageAsync();
                        } else {
                            InsertNewLine();
                        }
                    }
                }
            })();
        }

        // Костыль для ручного ввода символа новой строки,
        // ибо при AcceptsReturn = true не срабатывает KeyDown при нажатии на Enter.
        private void InsertNewLine() {
            try {
                if (MessageText.SelectionStart == MessageText.SelectionEnd) {
                    if (!String.IsNullOrEmpty(MessageText.Text)) {
                        MessageText.Text = MessageText.Text.Insert(MessageText.SelectionEnd, "\n");
                    } else {
                        MessageText.Text = "\n";
                    }
                    MessageText.SelectionStart += 1;
                    MessageText.SelectionEnd += 1;
                } else {
                    int start = Math.Min(MessageText.SelectionStart, MessageText.SelectionEnd);
                    int end = Math.Max(MessageText.SelectionStart, MessageText.SelectionEnd);
                    string newText = MessageText.Text.Remove(start, end - start);
                    MessageText.Text = newText.Insert(start, "\n");
                    start += 1;
                    MessageText.SelectionStart = start;
                    MessageText.SelectionEnd = start;
                }
            } catch (Exception ex) {
                Log.Error($"WTF with Composer.InsertNewLine... 0x{ex.HResult.ToString("x8")}");
            }
        }

        private void BotKbdButton_Click(object sender, RoutedEventArgs e) {
            BotKeyboardToggle.IsChecked = !BotKeyboardToggle.IsChecked;
        }

        private void ShowTemplatesFlyout(object sender, RoutedEventArgs e) {
            ViewModel.ShowGroupTemplates(sender as Button);
        }

        private void ShowQuickActionsFlyout(object sender, RoutedEventArgs e) {
            ViewModel.ShowQuickActions(sender as Button);
        }

        private void ShowFormattingFlyout(object sender, RoutedEventArgs e) {
            ViewModel.ShowFormattingActions(sender as Button);
        }

        private void ShowStickersFlyout(object sender, RoutedEventArgs e) {
            ViewModel.ShowEmojiStickerPicker(sender as Button);
        }

        private void MentionSuggestion_Click(object sender, RoutedEventArgs e) {
            if (sender is Button button && button.DataContext is Entity entity) {
                ViewModel?.InsertMention(entity);
                MessageText.Focus();
            }
        }

        private void LocalStickerSuggestion_Click(object sender, RoutedEventArgs e) {
            if (sender is Button button && button.DataContext is LocalSticker sticker) {
                new Action(async () => await ViewModel.SendLocalStickerAsync(sticker))();
                MessageText.Focus();
            }
        }

        private void EmojiSuggestion_Click(object sender, RoutedEventArgs e) {
            if (sender is Button button && button.DataContext is NeoSmart.Unicode.SingleEmoji emoji) {
                ViewModel?.ApplyEmojiSuggestion(emoji);
                MessageText.Focus();
            }
        }

        private void SlashCommandSuggestion_Click(object sender, RoutedEventArgs e) {
            if (sender is Button button && button.DataContext is SlashCommandSuggestion suggestion) {
                ViewModel?.ApplySlashCommandSuggestion(suggestion);
                MessageText.Focus();
            }
        }

        private void PinnedQuickReply_Click(object sender, RoutedEventArgs e) {
            ViewModel?.ApplyPinnedQuickReply();
            MessageText.Focus();
        }

        private void PinnedQuickReplyClear_Click(object sender, RoutedEventArgs e) {
            ViewModel?.ClearPinnedQuickReply();
            MessageText.Focus();
        }

        private void ShowAttachmentPickerContextMenu(object sender, RoutedEventArgs e) {
            ViewModel.ShowAttachmentPickerContextMenu(sender as Button);
        }

        private void RemoveAttachment(object sender, RoutedEventArgs e) {
            OutboundAttachmentViewModel oavm = (sender as Button).DataContext as OutboundAttachmentViewModel;
            if (oavm != null) {
                oavm.CancelUpload();
                ViewModel.Attachments.Remove(oavm);
            }
        }

        private void ShowAttachmentErrorInfo(object sender, RoutedEventArgs e) {
            OutboundAttachmentViewModel oavm = (sender as Button).DataContext as OutboundAttachmentViewModel;
            if (oavm != null) {
                Exception ex = oavm.UploadException;
                new Action(async () => await ShowAttachmentErrorInfoAsync(oavm, ex))();
            }
        }

        private async Task ShowAttachmentErrorInfoAsync(OutboundAttachmentViewModel oavm, Exception ex) {
            string[] buttons = new string[] { Assets.i18n.Resources.retry, Assets.i18n.Resources.delete };

            VKUIDialog dlg = new VKUIDialog(Assets.i18n.Resources.upload_error, ex.Message, buttons, 1);
            int id = await dlg.ShowDialog<int>(VKSession.GetByDataContext(this).Window);
            switch (id) {
                case 1:
                    oavm.DoUploadFile();
                    break;
                case 2:
                    ViewModel.Attachments.Remove(oavm);
                    break;
            }
        }
    }
}
