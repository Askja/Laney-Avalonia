using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia;
using Avalonia.Media.Imaging;
using ELOR.Laney.Controls;
using ELOR.Laney.Core;
using ELOR.Laney.Core.Localization;
using ELOR.Laney.DataModels;
using ELOR.Laney.Extensions;
using ELOR.Laney.Helpers;
using ELOR.Laney.Views.Modals;
using ELOR.VKAPILib.Objects;
using NeoSmart.Unicode;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using VKUI.Controls;
using VKUI.Popups;

namespace ELOR.Laney.ViewModels.Controls {
    public sealed class SlashCommandSuggestion {
        public string IconId { get; init; }
        public string Command { get; init; }
        public string Hint { get; init; }
        public string Keywords { get; init; }
        public string InsertText { get; init; }

        public bool Matches(string query) {
            if (String.IsNullOrWhiteSpace(query)) return true;

            string haystack = $"{Command} {Hint} {Keywords} {InsertText}";
            string[] terms = query.Trim().TrimStart('/').Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return terms.All(term => haystack.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
    }

    public sealed class CleanCopyResult {
        public int SourceMessages { get; set; }
        public int TextBlocks { get; set; }
        public int Attachments { get; set; }
        public int SkippedAttachments { get; set; }
        public int SkippedForwardedMessages { get; set; }
        public int CopiedItems => TextBlocks + Attachments;
        public bool HasSkippedItems => SkippedAttachments > 0 || SkippedForwardedMessages > 0;

        public string BuildSummary() {
            List<string> copied = new List<string>();
            if (TextBlocks > 0) copied.Add($"текст: {TextBlocks}");
            if (Attachments > 0) copied.Add($"вложения: {Attachments}");

            List<string> skipped = new List<string>();
            if (SkippedAttachments > 0) skipped.Add($"несовместимые вложения: {SkippedAttachments}");
            if (SkippedForwardedMessages > 0) skipped.Add($"вложенные форварды: {SkippedForwardedMessages}");

            string copiedText = copied.Count == 0 ? "ничего" : String.Join(", ", copied);
            if (skipped.Count == 0) return $"Скопировано: {copiedText}.";
            return $"Скопировано: {copiedText}. Пропущено: {String.Join(", ", skipped)}.";
        }
    }

    public class ComposerViewModel : CommonViewModel {
        private static string T(string key) => Localizer.Get(key);

        private static readonly IReadOnlyList<SlashCommandSuggestion> SlashCommandCatalog = [
            new SlashCommandSuggestion {
                IconId = VKIconNames.Icon20ArticleOutline,
                Command = "/todo",
                Hint = "сохранить текст как локальную задачу",
                Keywords = "task задача дело",
                InsertText = "/todo "
            },
            new SlashCommandSuggestion {
                IconId = VKIconNames.Icon20FavoriteOutline,
                Command = "/remind",
                Hint = "сохранить reminder в quick-actions",
                Keywords = "reminder напомнить",
                InsertText = "/remind "
            },
            new SlashCommandSuggestion {
                IconId = VKIconNames.Icon20RecentOutline,
                Command = "/later",
                Hint = "отложить отправку: /later 15m текст",
                Keywords = "schedule отложить позже",
                InsertText = "/later 15m "
            },
            new SlashCommandSuggestion {
                IconId = VKIconNames.Icon20RecentOutline,
                Command = "/repeat",
                Hint = "повторять отправку: /repeat 1d текст",
                Keywords = "schedule repeat повтор",
                InsertText = "/repeat 1d "
            },
            new SlashCommandSuggestion {
                IconId = VKIconNames.Icon20MessageUnreadTopOutline,
                Command = "/qr",
                Hint = "быстрый ответ текущего чата",
                Keywords = "quick reply быстрый ответ",
                InsertText = "/qr "
            },
            new SlashCommandSuggestion {
                IconId = VKIconNames.Icon20ListBulletOutline,
                Command = "/qrf",
                Hint = "быстрый ответ текущей папки",
                Keywords = "quick reply folder папка",
                InsertText = "/qrf "
            },
            new SlashCommandSuggestion {
                IconId = VKIconNames.Icon20UserOutline,
                Command = "/qrp",
                Hint = "быстрый ответ конкретного человека",
                Keywords = "quick reply person персона отправитель",
                InsertText = "/qrp "
            },
            new SlashCommandSuggestion {
                IconId = VKIconNames.Icon20PinOutline,
                Command = "/qrpin",
                Hint = "закрепить быстрый ответ в composer",
                Keywords = "quick reply pin закрепить",
                InsertText = "/qrpin "
            },
            new SlashCommandSuggestion {
                IconId = VKIconNames.Icon20PinSlashOutline,
                Command = "/unqrpin",
                Hint = "снять закрепленный быстрый ответ",
                Keywords = "quick reply unpin открепить",
                InsertText = "/unqrpin"
            },
            new SlashCommandSuggestion {
                IconId = VKIconNames.Icon20LockOutline,
                Command = "/encrypt",
                Hint = "отправить текст через Laney E2E",
                Keywords = "e2e crypto шифр",
                InsertText = "/encrypt "
            },
            new SlashCommandSuggestion {
                IconId = VKIconNames.Icon20DocumentOutline,
                Command = "/download",
                Hint = "bulk-export вложений текущего чата",
                Keywords = "media attachments скачать вложения export",
                InsertText = "/download media last 30d"
            },
            new SlashCommandSuggestion {
                IconId = VKIconNames.Icon20NotificationSlashOutline,
                Command = "/mute",
                Hint = "локальная тишина: /mute 2h",
                Keywords = "quiet silence тишина заглушить",
                InsertText = "/mute 2h"
            },
            new SlashCommandSuggestion {
                IconId = VKIconNames.Icon20NotificationOutline,
                Command = "/unmute",
                Hint = "снять локальную тишину",
                Keywords = "quiet clear снять",
                InsertText = "/unmute"
            },
            new SlashCommandSuggestion {
                IconId = VKIconNames.Icon20BlockOutline,
                Command = "/shadowban",
                Hint = "локально скрыть sender id",
                Keywords = "ban block скрыть теневой",
                InsertText = "/shadowban "
            },
            new SlashCommandSuggestion {
                IconId = VKIconNames.Icon20UnlockOutline,
                Command = "/unshadowban",
                Hint = "вернуть sender id в текущем чате",
                Keywords = "ban unblock вернуть",
                InsertText = "/unshadowban "
            },
            new SlashCommandSuggestion {
                IconId = VKIconNames.Icon20UserOutline,
                Command = "/status",
                Hint = "автостатус: work, busy, gaming, sleep, dnd",
                Keywords = "auto status автостатус dnd",
                InsertText = "/status dnd"
            },
            new SlashCommandSuggestion {
                IconId = VKIconNames.Icon28PaletteOutline,
                Command = "/theme",
                Hint = "тема текущего чата",
                Keywords = "appearance фон оформление",
                InsertText = "/theme"
            },
            new SlashCommandSuggestion {
                IconId = VKIconNames.Icon20LockOutline,
                Command = "/e2e",
                Hint = "настройки Laney E2E текущего чата",
                Keywords = "encrypt crypto key ключи",
                InsertText = "/e2e"
            }
        ];

        private VKSession session;

        private bool _isGroupSession;
        private bool _canSendMessage;
        private bool _isRecordingAudio;
        private int _editingMessageId;
        private string _text;
        private string _pinnedQuickReply;
        private int _textSelectionStart;
        private int _textSelectionEnd;
        private ObservableCollection<OutboundAttachmentViewModel> _attachments = new ObservableCollection<OutboundAttachmentViewModel>();
        private MessageViewModel _reply;
        private BotKeyboard _botKeyboard;
        private List<Sticker> _suggestedStickers;
        private List<LocalSticker> _suggestedLocalStickers;
        private List<SingleEmoji> _suggestedEmojis;
        private ObservableCollection<Entity> _suggestedMentions = new ObservableCollection<Entity>();
        private ObservableCollection<SlashCommandSuggestion> _suggestedSlashCommands = new ObservableCollection<SlashCommandSuggestion>();
        private string suppressedSlashSuggestionText;

        private RelayCommand _sendCommand;
        private RelayCommand _recordAudioCommand;
        private DispatcherTimer draftSaveTimer;

        public bool IsGroupSession { get { return _isGroupSession; } private set { _isGroupSession = value; OnPropertyChanged(); } }
        public bool CanSendMessage { get { return _canSendMessage; } private set { _canSendMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanShowSendButton)); OnPropertyChanged(nameof(CanShowRecordAudioButton)); } }
        public bool IsRecordingAudio { get { return _isRecordingAudio; } private set { _isRecordingAudio = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanShowSendButton)); OnPropertyChanged(nameof(CanShowRecordAudioButton)); OnPropertyChanged(nameof(RecordAudioIconId)); } }
        public bool CanShowSendButton => CanSendMessage && !IsRecordingAudio;
        public bool CanShowRecordAudioButton => !CanSendMessage || IsRecordingAudio;
        public string RecordAudioIconId => IsRecordingAudio ? VKIconNames.Icon24Pause : VKIconNames.Icon24VoiceOutline;
        public int EditingMessageId { get { return _editingMessageId; } private set { _editingMessageId = value; OnPropertyChanged(); } }
        public string Text { get { return _text; } set { if (_text == value) return; _text = value; OnPropertyChanged(); ScheduleDraftSave(); UpdateSlashCommandSuggestions(); } }
        public string PinnedQuickReply { get { return _pinnedQuickReply; } private set { _pinnedQuickReply = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasPinnedQuickReply)); OnPropertyChanged(nameof(PinnedQuickReplyHeader)); } }
        public bool HasPinnedQuickReply => !String.IsNullOrWhiteSpace(PinnedQuickReply);
        public string PinnedQuickReplyHeader => BuildQuickReplyHeader(PinnedQuickReply);
        public int TextSelectionStart { get { return _textSelectionStart; } set { _textSelectionStart = value; OnPropertyChanged(); } }
        public int TextSelectionEnd { get { return _textSelectionEnd; } set { _textSelectionEnd = value; OnPropertyChanged(); } }
        public ObservableCollection<OutboundAttachmentViewModel> Attachments { get { return _attachments; } private set { _attachments = value; OnPropertyChanged(); } }
        public MessageViewModel Reply { get { return _reply; } private set { _reply = value; OnPropertyChanged(); } }
        public BotKeyboard BotKeyboard { get { return _botKeyboard; } set { _botKeyboard = value; OnPropertyChanged(); } }
        public List<Sticker> SuggestedStickers { get { return _suggestedStickers; } set { _suggestedStickers = value; OnPropertyChanged(); } }
        public List<LocalSticker> SuggestedLocalStickers { get { return _suggestedLocalStickers; } set { _suggestedLocalStickers = value; OnPropertyChanged(); } }
        public List<SingleEmoji> SuggestedEmojis { get { return _suggestedEmojis; } set { _suggestedEmojis = value; OnPropertyChanged(); } }
        public ObservableCollection<Entity> SuggestedMentions { get { return _suggestedMentions; } private set { _suggestedMentions = value; OnPropertyChanged(); } }
        public ObservableCollection<SlashCommandSuggestion> SuggestedSlashCommands { get { return _suggestedSlashCommands; } private set { _suggestedSlashCommands = value; OnPropertyChanged(); } }

        public RelayCommand SendCommand { get { return _sendCommand; } private set { _sendCommand = value; OnPropertyChanged(); } }
        public RelayCommand RecordAudioCommand { get { return _recordAudioCommand; } private set { _recordAudioCommand = value; OnPropertyChanged(); } }

        ChatViewModel Chat;
        Random Random = null;
        int RandomId = 0;
        int StickerId = 0;
        int MentionStart = -1;
        VoiceRecorder voiceRecorder;
        DateTimeOffset voiceRecordingStartedAt;
        const int MaxGraffitiPixelSide = 512;
        const long MaxE2EAttachmentBytes = 32L * 1024L * 1024L;

        public ComposerViewModel(VKSession session, ChatViewModel chat) {
            this.session = session;
            IsGroupSession = session.IsGroup;
            Chat = chat;
            Attachments.CollectionChanged += (a, b) => CheckCanSendMessage();
            SendCommand = new RelayCommand(async (o) => await SendMessageAsync());
            RecordAudioCommand = new RelayCommand(async (o) => await ToggleAudioRecordingAsync());

            Random = new Random();
            RandomId = Random.Next(Int32.MinValue, Int32.MaxValue);
            _text = Settings.GetPeerCurrentDraft(chat.PeerId);
            PinnedQuickReply = Settings.GetPeerPinnedQuickReply(chat.PeerId);
            UpdateSlashCommandSuggestions();
            CheckCanSendMessage();
            ScheduledMessageManager.EnsureStarted(session);

            PropertyChanged += OnPropertyChanged;
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(Text)) {
                CheckCanSendMessage();
                CheckStickersSuggestions();
                CheckMentionSuggestions();
            }
        }

        private void CheckCanSendMessage() {
            CanSendMessage = !String.IsNullOrEmpty(Text) || Attachments.Count > 0 || StickerId > 0;
        }

        private void ScheduleDraftSave() {
            if (Chat == null || EditingMessageId != 0) return;

            if (draftSaveTimer == null) {
                draftSaveTimer = new DispatcherTimer {
                    Interval = TimeSpan.FromMilliseconds(900)
                };
                draftSaveTimer.Tick += (a, b) => {
                    draftSaveTimer.Stop();
                    Settings.SavePeerDraftSnapshot(Chat.PeerId, Text);
                };
            }

            draftSaveTimer.Stop();
            draftSaveTimer.Start();
        }

        private void UpdateSlashCommandSuggestions() {
            SuggestedSlashCommands.Clear();

            string raw = Text ?? String.Empty;
            if (String.Equals(raw, suppressedSlashSuggestionText, StringComparison.Ordinal)) return;
            suppressedSlashSuggestionText = null;

            string text = raw.TrimStart();
            if (String.IsNullOrWhiteSpace(text) || !text.StartsWith('/')) return;
            if (text.IndexOf('\n') >= 0 || text.IndexOf('\r') >= 0) return;

            int separator = text.IndexOfAny([' ', '\t']);
            if (separator >= 0 && text[(separator + 1)..].Trim().Length > 0) return;

            foreach (SlashCommandSuggestion suggestion in SlashCommandCatalog.Where(c => c.Matches(text)).Take(8)) {
                SuggestedSlashCommands.Add(suggestion);
            }
        }

        public void ApplySlashCommandSuggestion(SlashCommandSuggestion suggestion) {
            if (suggestion == null) return;

            suppressedSlashSuggestionText = suggestion.InsertText;
            Text = suggestion.InsertText;
            TextSelectionStart = Text.Length;
            TextSelectionEnd = Text.Length;
            SuggestedSlashCommands.Clear();
        }

        public bool TryApplyFirstSlashCommandSuggestion() {
            SlashCommandSuggestion suggestion = SuggestedSlashCommands.FirstOrDefault();
            if (suggestion == null) return false;

            ApplySlashCommandSuggestion(suggestion);
            return true;
        }

        public void ShowAttachmentPickerContextMenu(Control target) {
            if (DemoMode.IsEnabled) return;
            ActionSheet ash = new ActionSheet {
                Placement = PlacementMode.TopEdgeAlignedLeft,
                IsSearchEnabled = true,
                SearchWatermark = T("cm_attachment_search")
            };

            ActionSheetItem photo = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20PictureOutline },
                Header = Assets.i18n.Resources.photo,
            };
            ActionSheetItem video = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20VideoOutline },
                Header = Assets.i18n.Resources.video,
            };
            ActionSheetItem file = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20DocumentOutline },
                Header = Assets.i18n.Resources.doc,
            };
            ActionSheetItem graffiti = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon24BrushOutline },
                Header = Assets.i18n.Resources.graffiti,
            };
            ActionSheetItem audioMessage = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon24VoiceOutline },
                Header = T("cm_audio_message_from_file"),
            };
            ActionSheetItem importTelegramStickers = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon24SmileOutline },
                Header = T("cm_import_telegram_stickers"),
                Subtitle = "Импорт и управление паками теперь в настройках"
            };
            ActionSheetItem buildMiniPack = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20PictureOutline },
                Header = T("cm_mini_pack_builder"),
            };
            ActionSheetItem imageEditor = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20WriteOutline },
                Header = T("cm_image_editor"),
            };
            ActionSheetItem localPhotoAlbum = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20PictureOutline },
                Header = T("cm_local_photo_album"),
            };
            ActionSheetItem frequentFiles = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20RecentOutline },
                Header = T("cm_frequent_files"),
            };
            ActionSheetItem localDocument = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20DocumentOutline },
                Header = T("cm_file_as_document"),
            };
            ActionSheetItem e2eDocument = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20LockOutline },
                Header = T("cm_e2e_document"),
            };
            ActionSheetItem poll = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20PollOutline },
                Header = Assets.i18n.Resources.poll,
            };

            var session = VKSession.GetByDataContext(target);
            int count = Attachments.Where(a => a.Type == OutboundAttachmentType.Attachment).Count();

            photo.Click += async (a, b) => {
                AttachmentPicker ap = new AttachmentPicker(session, 10 - count, 0);
                await AddAttachmentsAsync(await ap.ShowDialog<object>(session.Window));
            };
            video.Click += async (a, b) => {
                AttachmentPicker ap = new AttachmentPicker(session, 10 - count, 1);
                await AddAttachmentsAsync(await ap.ShowDialog<object>(session.Window));
            };
            file.Click += async (a, b) => {
                AttachmentPicker ap = new AttachmentPicker(session, 10 - count, 2);
                await AddAttachmentsAsync(await ap.ShowDialog<object>(session.Window));
            };
            localPhotoAlbum.Click += async (a, b) => await AddLocalFilesAsync(target, Constants.PhotoUploadCommand, FilePickerFileTypes.ImageAll);
            frequentFiles.Click += async (a, b) => await AddFrequentFilesAsync(target);
            localDocument.Click += async (a, b) => await AddLocalFilesAsync(target, Constants.FileUploadCommand, FilePickerFileTypes.All);
            e2eDocument.Click += async (a, b) => await AddE2EDocumentAsync(target);
            graffiti.Click += async (a, b) => await AddLocalFilesAsync(target, Constants.GraffitiUploadCommand, FilePickerFileTypes.ImageAll, true);
            audioMessage.Click += async (a, b) => await AddLocalFilesAsync(target, Constants.AudioMessageUploadCommand, GetAudioMessageFileType());
            importTelegramStickers.Click += async (a, b) => {
                var settings = new ELOR.Laney.Views.SettingsWindow(typeof(ELOR.Laney.Views.SettingsCategories.Stickers));
                await settings.ShowDialog(session.ModalWindow);
            };
            buildMiniPack.Click += async (a, b) => await BuildMiniPackAsync(target);
            imageEditor.Click += async (a, b) => await EditImageBeforeSendAsync(target);
            poll.Click += async (a, b) => {
                if (count >= 10) {
                    VKUIDialog limit = new VKUIDialog("Слишком много вложений", "VK дает максимум 10 вложений в сообщении. Опрос тоже не пролезет, как ни уговаривай.");
                    await limit.ShowDialog(session.ModalWindow);
                    return;
                }

                long ownerId = session.IsGroup ? -session.GroupId : 0;
                Poll createdPoll = await PollDialogHelper.CreatePollWithDialogAsync(session, session.ModalWindow, ownerId);
                if (createdPoll != null) Attachments.Add(new OutboundAttachmentViewModel(session, createdPoll));
            };

            ash.Items.Add(ActionSheetItem.Section("VK"));
            ash.Items.Add(photo);
            ash.Items.Add(video);
            ash.Items.Add(file);
            ash.Items.Add(ActionSheetItem.Section(T("cm_section_local_files")));
            ash.Items.Add(localPhotoAlbum);
            ash.Items.Add(frequentFiles);
            ash.Items.Add(localDocument);
            ash.Items.Add(e2eDocument);
            ash.Items.Add(ActionSheetItem.Section(T("cm_section_stickers_images")));
            ash.Items.Add(graffiti);
            ash.Items.Add(audioMessage);
            ash.Items.Add(importTelegramStickers);
            ash.Items.Add(buildMiniPack);
            ash.Items.Add(imageEditor);
            ash.Items.Add(ActionSheetItem.Section(T("cm_section_extra")));
            ash.Items.Add(poll);
            ash.ShowAt(target);
        }

        private static FilePickerFileType GetAudioMessageFileType() {
            return new FilePickerFileType("Audio message") {
                Patterns = ["*.ogg", "*.opus", "*.mp3", "*.m4a", "*.aac", "*.wav"],
                MimeTypes = ["audio/ogg", "audio/opus", "audio/mpeg", "audio/mp4", "audio/aac", "audio/wav"]
            };
        }

        private static FilePickerFileType GetTelegramStickerFileType() {
            return new FilePickerFileType("Telegram stickers") {
                Patterns = ["*.zip", "*.tgs", "*.webm", "*.webp", "*.gif", "*.png", "*.jpg", "*.jpeg"],
                MimeTypes = ["application/zip", "application/x-tgsticker", "video/webm", "image/webp", "image/gif", "image/png", "image/jpeg"]
            };
        }

        private async Task ImportLocalStickersAsync(Control target) {
            TextBox linkBox = new TextBox {
                PlaceholderText = "https://t.me/addstickers/pack_name",
                Width = 360
            };
            TextBox tokenBox = new TextBox {
                PlaceholderText = "123456:telegram-bot-api-token",
                Text = SecureVault.GetSecret(LocalStickerStore.TelegramBotTokenSecretName),
                PasswordChar = '*',
                Width = 360
            };
            TextBlock policy = new TextBlock {
                Text = "По ссылке импорт идёт через официальный Telegram Bot API и твой bot token. Пак сохраняется локально с пометкой TG; обход платного/закрытого контента и мутный scraping не делаем.",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                MaxWidth = 360
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

            VKUIDialog modeDialog = new VKUIDialog("Импорт Telegram-стикеров", "Выбери источник: локальный zip/tgs/webm/webp/gif или официальный импорт по ссылке.", ["Файлы", "По ссылке", "Отмена"], 2) {
                DialogContent = content
            };

            int mode = await modeDialog.ShowDialog<int>(session.ModalWindow);
            if (mode == 1) {
                await ImportLocalStickerFilesAsync(target);
                return;
            }

            if (mode != 2) return;

            await ImportTelegramStickerPackLinkAsync(linkBox.Text, tokenBox.Text);
        }

        private async Task ImportLocalStickerFilesAsync(Control target) {
            var storageProvider = TopLevel.GetTopLevel(target)?.StorageProvider;
            if (storageProvider?.CanOpen != true) return;

            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
                AllowMultiple = true,
                FileTypeFilter = new List<FilePickerFileType> { GetTelegramStickerFileType() }
            });
            if (files == null || files.Count == 0) return;

            int imported = await LocalStickerStore.ImportAsync(files);
            if (imported == 0) {
                session.ShowNotification(new Notification("Стикеры не импортированы", "Поддерживаются zip, tgs, webm, webp, gif, png, jpg.", NotificationType.Warning));
                return;
            }

            session.ShowNotification(new Notification("Стикеры импортированы", $"Добавлено локально: {imported}. Открой вкладку `Локальные` в стикерах.", NotificationType.Success));
        }

        private async Task ImportTelegramStickerPackLinkAsync(string link, string botToken) {
            try {
                botToken = botToken?.Trim();
                if (!String.IsNullOrWhiteSpace(botToken)) SecureVault.SetSecret(LocalStickerStore.TelegramBotTokenSecretName, botToken);

                LocalStickerImportResult result = await LocalStickerStore.ImportTelegramPackLinkAsync(link, botToken);
                if (result.Imported == 0) {
                    session.ShowNotification(new Notification("TG-пак не импортирован", $"{result.Summary}. Возможно, всё уже было в библиотеке.", NotificationType.Warning));
                    return;
                }

                session.ShowNotification(new Notification("TG-пак импортирован", $"{result.Summary}. В picker ищи вкладку `Локальные` и бейдж TG.", NotificationType.Success));
            } catch (Exception ex) {
                Log.Warning(ex, "Telegram sticker pack import failed.");
                await new VKUIDialog("TG-пак не импортирован", ex.GetBaseException().Message, ["Понятно"], 1).ShowDialog(session.ModalWindow);
            }
        }

        private async Task BuildMiniPackAsync(Control target) {
            var storageProvider = TopLevel.GetTopLevel(target)?.StorageProvider;
            if (storageProvider?.CanOpen != true) return;

            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
                AllowMultiple = true,
                FileTypeFilter = new List<FilePickerFileType> { FilePickerFileTypes.ImageAll }
            });
            if (files == null || files.Count == 0) return;

            List<IStorageFile> selected = files.Take(20).ToList();
            List<IStorageFile> stickers = await StickerMiniPackBuilder.BuildAsync(storageProvider, selected);
            if (stickers.Count == 0) {
                await new VKUIDialog("Мини-пак не создан", "Не удалось обработать выбранные картинки. Проверь формат и размер, а то магия опять не завезена.").ShowDialog(session.ModalWindow);
                return;
            }

            int imported = await LocalStickerStore.ImportAsync(stickers);
            session.ShowNotification(new Notification("Мини-пак создан", $"Готово PNG-стикеров: {imported}. Фон подчищен, края обрезаны, обводка добавлена.", NotificationType.Success));
        }

        private async Task EditImageBeforeSendAsync(Control target) {
            var storageProvider = TopLevel.GetTopLevel(target)?.StorageProvider;
            if (storageProvider?.CanOpen != true) return;

            if (GetRemainingAttachmentSlots() <= 0) {
                await new VKUIDialog("Слишком много вложений", "VK дает максимум 10 вложений в сообщении. Изображение уже некуда воткнуть.").ShowDialog(session.ModalWindow);
                return;
            }

            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType> { FilePickerFileTypes.ImageAll }
            });
            IStorageFile file = files?.FirstOrDefault();
            if (file == null) return;

            ImagePreSendEditorDialogState state = BuildImageEditorDialogContent();
            state.StickerButton.Click += async (a, b) => {
                IReadOnlyList<IStorageFile> stickerFiles = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
                    AllowMultiple = false,
                    FileTypeFilter = new List<FilePickerFileType> { FilePickerFileTypes.ImageAll }
                });
                IStorageFile stickerFile = stickerFiles?.FirstOrDefault();
                string path = stickerFile?.TryGetLocalPath();
                if (!String.IsNullOrWhiteSpace(path)) state.StickerPath.Text = path;
            };

            VKUIDialog dialog = new VKUIDialog("Редактор изображения", "Быстрые правки перед отправкой: кадрирование, блюр, приватная плашка, стрелка и подпись.", ["Отмена", "Добавить"], 2) {
                DialogContent = state.Content
            };

            if (await dialog.ShowDialog<int>(session.ModalWindow) != 2) return;

            try {
                IStorageFile edited = await ImagePreSendEditorHelper.EditAsync(storageProvider, file, new ImagePreSendEditOptions {
                    CropSquare = state.CropSquare.IsChecked == true,
                    BlurCenter = state.BlurCenter.IsChecked == true,
                    PrivacyMask = state.PrivacyMask.IsChecked == true,
                    Arrow = state.Arrow.IsChecked == true,
                    DrawMarker = state.DrawMarker.IsChecked == true,
                    OverlayStickerPath = state.StickerPath.Text,
                    Text = state.Text.Text
                });

                if (edited == null) throw new IOException("Не удалось получить временный PNG после редактирования.");

                await AddStorageFilesAsync([edited], Constants.PhotoUploadCommand, false);
                session.ShowNotification(new Notification("Изображение готово", "PNG после правок добавлен в очередь отправки.", NotificationType.Success));
            } catch (Exception ex) {
                await new VKUIDialog("Редактор не вывез", ex.Message, ["Понятно"], 1).ShowDialog(session.ModalWindow);
            }
        }

        private static ImagePreSendEditorDialogState BuildImageEditorDialogContent() {
            ImagePreSendEditorDialogState state = new ImagePreSendEditorDialogState();
            state.CropSquare = new CheckBox { Content = "Обрезать в квадрат" };
            state.BlurCenter = new CheckBox { Content = "Замылить центр" };
            state.PrivacyMask = new CheckBox { Content = "Закрыть нижнюю приватную зону" };
            state.Arrow = new CheckBox { Content = "Добавить красную стрелку" };
            state.DrawMarker = new CheckBox { Content = "Нарисовать маркерный росчерк" };
            state.StickerPath = new TextBox {
                IsReadOnly = true,
                PlaceholderText = "Overlay-стикер не выбран"
            };
            state.StickerButton = new Button {
                Content = "Выбрать overlay-стикер",
                HorizontalAlignment = HorizontalAlignment.Left
            };
            state.Text = new TextBox {
                PlaceholderText = "Подпись поверх изображения",
                MaxLength = 120
            };

            state.Content = new StackPanel {
                Spacing = 8,
                MinWidth = 320,
                Children = {
                    state.CropSquare,
                    state.BlurCenter,
                    state.PrivacyMask,
                    state.Arrow,
                    state.DrawMarker,
                    state.StickerButton,
                    state.StickerPath,
                    state.Text
                }
            };

            return state;
        }

        private async Task AddLocalFilesAsync(Control target, int uploadType, FilePickerFileType fileType, bool prepareGraffiti = false) {
            var storageProvider = TopLevel.GetTopLevel(target)?.StorageProvider;
            if (storageProvider?.CanOpen != true) return;

            int limit = GetRemainingAttachmentSlots();
            if (limit <= 0) {
                VKUIDialog limitDialog = new VKUIDialog("Слишком много вложений", "VK дает максимум 10 вложений в сообщении. Еще одно вложение туда уже не влезет.");
                await limitDialog.ShowDialog(session.ModalWindow);
                return;
            }

            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
                AllowMultiple = true,
                FileTypeFilter = new List<FilePickerFileType> { fileType }
            });
            if (files == null || files.Count == 0) return;

            List<IStorageFile> originalSelectedFiles = files.Take(limit).ToList();
            IEnumerable<IStorageFile> selectedFiles = originalSelectedFiles;
            if (prepareGraffiti) {
                selectedFiles = await PrepareGraffitiFilesAsync(storageProvider, selectedFiles);
            } else if (uploadType == Constants.PhotoUploadCommand) {
                List<ImageUploadOptimizationResult> optimizedFiles = await ImageUploadOptimizer.OptimizeForPhotoUploadAsync(storageProvider, selectedFiles);
                selectedFiles = optimizedFiles.Select(f => f.File).ToList();
                ShowImageOptimizationReport(optimizedFiles);
            }

            await AddStorageFilesAsync(selectedFiles, uploadType, false);
            FrequentLocalFileStore.MarkUsed(originalSelectedFiles, uploadType);
        }

        private async Task AddFrequentFilesAsync(Control target) {
            IStorageProvider storageProvider = TopLevel.GetTopLevel(target)?.StorageProvider;
            if (storageProvider == null) return;

            int limit = GetRemainingAttachmentSlots();
            if (limit <= 0) {
                await new VKUIDialog("Слишком много вложений", "VK дает максимум 10 вложений в сообщении. Частые файлы подождут, как и все остальные.").ShowDialog(session.ModalWindow);
                return;
            }

            List<FrequentLocalFile> files = FrequentLocalFileStore.GetTop(20);
            if (files.Count == 0) {
                await new VKUIDialog("Альбом пуст", "Выбери локальные фото, документы, граффити или голосовые через меню вложений. После этого они появятся здесь.").ShowDialog(session.ModalWindow);
                return;
            }

            FrequentFilesDialogState state = BuildFrequentFilesDialogContent(files);
            VKUIDialog dialog = new VKUIDialog("Частые файлы", "Локальный альбом. VK ничего об этом списке не знает.", ["Очистить", "Добавить", "Закрыть"], 2) {
                DialogContent = state.Content
            };

            int result = await dialog.ShowDialog<int>(session.ModalWindow);
            if (result == 1) {
                FrequentLocalFileStore.Clear();
                session.ShowNotification(new Notification("Альбом очищен", "Список частых файлов удален локально.", NotificationType.Success));
                return;
            }
            if (result != 2) return;

            List<FrequentLocalFile> selected = state.GetSelected().Take(limit).ToList();
            if (selected.Count == 0) {
                session.ShowNotification(new Notification("Ничего не выбрано", "Поставь галочку хотя бы на один файл.", NotificationType.Warning));
                return;
            }

            await AddFrequentFileItemsAsync(storageProvider, selected);
        }

        private async Task AddFrequentFileItemsAsync(IStorageProvider storageProvider, List<FrequentLocalFile> selected) {
            int added = 0;

            foreach (IGrouping<int, FrequentLocalFile> group in selected.GroupBy(f => f.UploadType)) {
                List<IStorageFile> storageFiles = new List<IStorageFile>();
                List<FrequentLocalFile> resolvedItems = new List<FrequentLocalFile>();

                foreach (FrequentLocalFile item in group) {
                    if (!File.Exists(item.FilePath)) continue;

                    IStorageFile file = await storageProvider.TryGetFileFromPathAsync(item.FilePath);
                    if (file == null) continue;

                    storageFiles.Add(file);
                    resolvedItems.Add(item);
                }

                if (storageFiles.Count == 0) continue;

                await AddStorageFilesAsync(storageFiles, group.Key);
                foreach (FrequentLocalFile item in resolvedItems) {
                    FrequentLocalFileStore.MarkUsed(item);
                    added++;
                }
            }

            if (added == 0) {
                session.ShowNotification(new Notification("Файлы недоступны", "Похоже, выбранные файлы уже удалены или недоступны по старому пути.", NotificationType.Warning));
            }
        }

        private static FrequentFilesDialogState BuildFrequentFilesDialogContent(List<FrequentLocalFile> files) {
            FrequentFilesDialogState state = new FrequentFilesDialogState();
            StackPanel list = new StackPanel { Spacing = 4 };

            foreach (FrequentLocalFile file in files) {
                CheckBox checkBox = new CheckBox {
                    Content = BuildFrequentFileRow(file),
                    Tag = file
                };
                list.Children.Add(checkBox);
                state.CheckBoxes.Add(checkBox);
            }

            state.Content = new StackPanel {
                MinWidth = 420,
                MaxWidth = 640,
                Spacing = 10,
                Children = {
                    new ScrollViewer {
                        MaxHeight = 420,
                        Content = list
                    }
                }
            };

            return state;
        }

        private static Grid BuildFrequentFileRow(FrequentLocalFile file) {
            Grid row = new Grid {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                ColumnSpacing = 10,
                Margin = new Thickness(0, 4)
            };

            row.Children.Add(new VKIcon {
                Id = file.IconId,
                Width = 20,
                Height = 20,
                VerticalAlignment = VerticalAlignment.Center
            });

            StackPanel text = new StackPanel { Spacing = 2 };
            text.Children.Add(new TextBlock {
                Text = file.DisplayTitle,
                TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis
            });
            text.Children.Add(new TextBlock {
                Text = file.DisplaySubtitle,
                FontSize = 12,
                Opacity = 0.72,
                TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis
            });

            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            return row;
        }

        private sealed class FrequentFilesDialogState {
            public StackPanel Content { get; set; }
            public List<CheckBox> CheckBoxes { get; } = new List<CheckBox>();

            public IEnumerable<FrequentLocalFile> GetSelected() {
                return CheckBoxes
                    .Where(c => c.IsChecked == true)
                    .Select(c => c.Tag as FrequentLocalFile)
                    .Where(f => f != null);
            }
        }

        private async Task AddE2EDocumentAsync(Control target) {
            if (!E2EManager.CanEncrypt(Chat.PeerId)) {
                await new VKUIDialog("E2E не настроен", "Сначала открой профиль чата → Laney E2E → Настроить. Файл без ключа шифровать бессмысленно, магии тут нет.").ShowDialog(session.ModalWindow);
                return;
            }

            if (GetRemainingAttachmentSlots() <= 0) {
                await new VKUIDialog("Слишком много вложений", "VK дает максимум 10 вложений в сообщении. E2E-документ туда уже не пролезет.").ShowDialog(session.ModalWindow);
                return;
            }

            if (!String.IsNullOrWhiteSpace(Text)) {
                VKUIDialog replaceDialog = new VKUIDialog("Заменить текст?", "E2E-документ кладёт ключ файла в поле ввода как encrypted payload. Текущий текст будет заменён.", ["Заменить", "Отмена"], 2);
                int replace = await replaceDialog.ShowDialog<int>(session.ModalWindow);
                if (replace != 1) return;
            }

            var storageProvider = TopLevel.GetTopLevel(target)?.StorageProvider;
            if (storageProvider?.CanOpen != true) return;

            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType> { FilePickerFileTypes.All }
            });
            IStorageFile file = files?.FirstOrDefault();
            if (file == null) return;

            try {
                byte[] plain = await ReadStorageFileBytesAsync(file, MaxE2EAttachmentBytes);
                E2EEncryptedAttachmentFile encrypted = E2EManager.EncryptAttachmentFile(plain);
                IStorageFile encryptedFile = await SaveEncryptedAttachmentTempFileAsync(storageProvider, file.Name, encrypted.Bytes);
                if (encryptedFile == null) throw new IOException("Cannot create encrypted temporary file.");

                Text = E2EManager.EncryptAttachmentKeyMessage(Chat.PeerId, file.Name, plain.LongLength, encrypted);
                await AddStorageFilesAsync([encryptedFile], Constants.FileUploadCommand, false);
                session.ShowNotification(new Notification("E2E-документ готов", "Дождись upload и отправь сообщение. Ключ файла уже в encrypted payload.", NotificationType.Success));
            } catch (Exception ex) {
                await new VKUIDialog("E2E-документ не создан", ex.Message, ["Понятно"], 1).ShowDialog(session.ModalWindow);
            }
        }

        private static async Task<byte[]> ReadStorageFileBytesAsync(IStorageFile file, long maxBytes) {
            using Stream input = await file.OpenReadAsync();
            if (input.CanSeek && input.Length > maxBytes) {
                throw new InvalidOperationException($"Файл больше лимита {maxBytes / 1024 / 1024} МБ. Streaming-шифрование ещё не подключено.");
            }

            using MemoryStream memory = new MemoryStream();
            await input.CopyToAsync(memory);
            if (memory.Length > maxBytes) {
                throw new InvalidOperationException($"Файл больше лимита {maxBytes / 1024 / 1024} МБ. Streaming-шифрование ещё не подключено.");
            }

            return memory.ToArray();
        }

        private static async Task<IStorageFile> SaveEncryptedAttachmentTempFileAsync(IStorageProvider storageProvider, string originalName, byte[] bytes) {
            string directory = Path.Combine(Path.GetTempPath(), "Laney", "E2EAttachments");
            Directory.CreateDirectory(directory);

            string safeName = GetSafeTempFileName(originalName);
            string path = Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(safeName)}-{Guid.NewGuid():N}.laney");
            await File.WriteAllBytesAsync(path, bytes);
            return await storageProvider.TryGetFileFromPathAsync(path);
        }

        private static string GetSafeTempFileName(string fileName) {
            if (String.IsNullOrWhiteSpace(fileName)) return "file";

            char[] invalid = Path.GetInvalidFileNameChars();
            string safe = new string(fileName.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
            return String.IsNullOrWhiteSpace(safe) ? "file" : safe;
        }

        private sealed class ImagePreSendEditorDialogState {
            public StackPanel Content { get; set; }
            public CheckBox CropSquare { get; set; }
            public CheckBox BlurCenter { get; set; }
            public CheckBox PrivacyMask { get; set; }
            public CheckBox Arrow { get; set; }
            public CheckBox DrawMarker { get; set; }
            public Button StickerButton { get; set; }
            public TextBox StickerPath { get; set; }
            public TextBox Text { get; set; }
        }

        private void ShowImageOptimizationReport(List<ImageUploadOptimizationResult> results) {
            List<string> reports = results.Where(r => r.Optimized && !String.IsNullOrWhiteSpace(r.Report)).Select(r => r.Report).ToList();
            if (reports.Count == 0) return;

            string text = String.Join("\n", reports.Take(2));
            if (reports.Count > 2) text += $"\n+ ещё {reports.Count - 2}";
            session.ShowNotification(new Notification("Изображения подготовлены", text, NotificationType.Success));
        }

        private static async Task<List<IStorageFile>> PrepareGraffitiFilesAsync(IStorageProvider storageProvider, IEnumerable<IStorageFile> files) {
            List<IStorageFile> preparedFiles = new List<IStorageFile>();

            foreach (IStorageFile file in files) {
                IStorageFile prepared = await TryPrepareGraffitiFileAsync(storageProvider, file);
                preparedFiles.Add(prepared ?? file);
            }

            return preparedFiles;
        }

        private static async Task<IStorageFile> TryPrepareGraffitiFileAsync(IStorageProvider storageProvider, IStorageFile file) {
            try {
                using Stream stream = await file.OpenReadAsync();
                using Bitmap source = new Bitmap(stream);

                PixelSize targetSize = GetGraffitiPixelSize(source.PixelSize);
                string directory = Path.Combine(Path.GetTempPath(), "Laney", "Graffiti");
                Directory.CreateDirectory(directory);

                string path = Path.Combine(directory, $"laney-graffiti-{Guid.NewGuid():N}.png");
                if (targetSize == source.PixelSize) {
                    source.Save(path);
                } else {
                    using Bitmap scaled = source.CreateScaledBitmap(targetSize, BitmapInterpolationMode.HighQuality);
                    scaled.Save(path);
                }

                return await storageProvider.TryGetFileFromPathAsync(path);
            } catch (Exception ex) {
                Log.Warning(ex, "Unable to prepare graffiti file {FileName}. Original file will be uploaded.", file.Name);
                return null;
            }
        }

        private static PixelSize GetGraffitiPixelSize(PixelSize sourceSize) {
            if (sourceSize.Width <= 0 || sourceSize.Height <= 0) return sourceSize;

            int maxSide = Math.Max(sourceSize.Width, sourceSize.Height);
            if (maxSide <= MaxGraffitiPixelSide) return sourceSize;

            double scale = (double)MaxGraffitiPixelSide / maxSide;
            return new PixelSize(
                Math.Max(1, (int)Math.Round(sourceSize.Width * scale)),
                Math.Max(1, (int)Math.Round(sourceSize.Height * scale))
            );
        }

        public void ShowGroupTemplates(Button target) {
            if (Chat.PeerType != PeerType.User || !session.IsGroup) return;
            var currentChatUser = CacheManager.GetUser(Chat.PeerId);
            var currentAdmin = CacheManager.GetUser(VKSession.Main.Id);
            var groupName = CacheManager.GetGroup(session.GroupId).Name;

            var picker = new GroupMessageTemplates(session, currentChatUser, currentAdmin, groupName) {
                Width = 320,
                Height = 320
            };

            VKUIFlyout flyout = new VKUIFlyout {
                Content = picker
            };

            picker.TemplateSelected += (a, b) => {
                flyout.Hide();
                Text = b;
            };

            flyout.ShowAt(target);
        }

        public void ShowQuickActions(Button target) {
            ActionSheet ash = new ActionSheet {
                Placement = PlacementMode.TopEdgeAlignedRight
            };

            List<PeerDraftHistoryItem> draftHistory = Settings.GetPeerDraftHistory(Chat.PeerId).Take(5).ToList();
            string filterId = GetCurrentQuickReplyFilterId();
            string filterTitle = GetCurrentQuickReplyFilterTitle();
            List<string> filterQuickReplies = Settings.GetChatFilterQuickReplies(session.Id, filterId).ToList();
            List<string> quickReplies = Settings.GetPeerQuickReplies(Chat.PeerId).ToList();
            bool hasPersonTarget = TryGetPersonQuickReplyTarget(out long personQuickReplySenderId, out string personQuickReplyTitle);
            List<string> personQuickReplies = hasPersonTarget
                ? Settings.GetPersonQuickReplies(session.Id, personQuickReplySenderId).ToList()
                : new List<string>();
            List<string> knownQuickReplies = filterQuickReplies
                .Concat(personQuickReplies)
                .Concat(quickReplies)
                .Append(PinnedQuickReply)
                .ToList();
            List<string> frequentPhrases = FrequentPhraseStore.GetTop(Chat.PeerId, knownQuickReplies, 6);
            List<string> recentSnippets = GetRecentQuickReplySnippets(knownQuickReplies);

            if (HasPinnedQuickReply) {
                ActionSheetItem pinnedItem = new ActionSheetItem {
                    Before = new VKIcon { Id = VKIconNames.Icon20PinOutline },
                    Header = BuildQuickReplyHeader(PinnedQuickReply),
                    Subtitle = T("cm_pinned_in_composer")
                };
                pinnedItem.Click += (a, b) => ApplyPinnedQuickReply();
                ash.Items.Add(pinnedItem);
            }

            if (HasPinnedQuickReply && (draftHistory.Count > 0 || filterQuickReplies.Count > 0 || personQuickReplies.Count > 0 || quickReplies.Count > 0 || frequentPhrases.Count > 0 || recentSnippets.Count > 0)) {
                ash.Items.Add(new ActionSheetItem());
            }

            foreach (PeerDraftHistoryItem draft in draftHistory) {
                ActionSheetItem draftItem = new ActionSheetItem {
                    Before = new VKIcon { Id = VKIconNames.Icon20WriteOutline },
                    Header = BuildQuickReplyHeader(draft.Text),
                    Subtitle = $"Черновик: {DateTimeOffset.FromUnixTimeSeconds(draft.UpdatedAtUnix).LocalDateTime:dd.MM HH:mm}"
                };
                draftItem.Click += (a, b) => AppendTextRaw(draft.Text);
                ash.Items.Add(draftItem);
            }

            if (draftHistory.Count > 0 && (filterQuickReplies.Count > 0 || personQuickReplies.Count > 0 || quickReplies.Count > 0 || frequentPhrases.Count > 0 || recentSnippets.Count > 0)) ash.Items.Add(new ActionSheetItem());

            foreach (string quickReply in filterQuickReplies) {
                ActionSheetItem quickReplyItem = new ActionSheetItem {
                    Before = new VKIcon { Id = VKIconNames.Icon20ListBulletOutline },
                    Header = BuildQuickReplyHeader(quickReply),
                    Subtitle = $"Папка: {filterTitle}"
                };
                quickReplyItem.Click += (a, b) => InsertTextTemplate(quickReply);
                ash.Items.Add(quickReplyItem);
            }

            if (filterQuickReplies.Count > 0 && (personQuickReplies.Count > 0 || quickReplies.Count > 0 || frequentPhrases.Count > 0 || recentSnippets.Count > 0)) ash.Items.Add(new ActionSheetItem());

            foreach (string quickReply in personQuickReplies) {
                ActionSheetItem quickReplyItem = new ActionSheetItem {
                    Before = new VKIcon { Id = VKIconNames.Icon20UserOutline },
                    Header = BuildQuickReplyHeader(quickReply),
                    Subtitle = $"Персона: {personQuickReplyTitle}"
                };
                quickReplyItem.Click += (a, b) => InsertTextTemplate(quickReply);
                ash.Items.Add(quickReplyItem);
            }

            if (personQuickReplies.Count > 0 && (quickReplies.Count > 0 || frequentPhrases.Count > 0 || recentSnippets.Count > 0)) ash.Items.Add(new ActionSheetItem());

            foreach (string quickReply in quickReplies) {
                ActionSheetItem quickReplyItem = new ActionSheetItem {
                    Before = new VKIcon { Id = VKIconNames.Icon20MessageOutline },
                    Header = BuildQuickReplyHeader(quickReply),
                    Subtitle = T("cm_quick_reply_chat")
                };
                quickReplyItem.Click += (a, b) => InsertTextTemplate(quickReply);
                ash.Items.Add(quickReplyItem);
            }

            if (quickReplies.Count > 0 && (frequentPhrases.Count > 0 || recentSnippets.Count > 0)) ash.Items.Add(new ActionSheetItem());

            foreach (string phrase in frequentPhrases) {
                ActionSheetItem phraseItem = new ActionSheetItem {
                    Before = new VKIcon { Id = VKIconNames.Icon20MessageUnreadTopOutline },
                    Header = BuildQuickReplyHeader(phrase),
                    Subtitle = T("cm_frequent_phrase")
                };
                phraseItem.Click += (a, b) => InsertTextTemplate(phrase);
                ash.Items.Add(phraseItem);
            }

            if (frequentPhrases.Count > 0 && recentSnippets.Count > 0) ash.Items.Add(new ActionSheetItem());

            foreach (string snippet in recentSnippets) {
                ActionSheetItem snippetItem = new ActionSheetItem {
                    Before = new VKIcon { Id = VKIconNames.Icon20RecentOutline },
                    Header = BuildQuickReplyHeader(snippet),
                    Subtitle = T("cm_recent_snippet")
                };
                snippetItem.Click += (a, b) => InsertTextTemplate(snippet);
                ash.Items.Add(snippetItem);
            }

            if (filterQuickReplies.Count > 0 || personQuickReplies.Count > 0 || quickReplies.Count > 0 || frequentPhrases.Count > 0 || recentSnippets.Count > 0) ash.Items.Add(new ActionSheetItem());

            ActionSheetItem busy = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20WriteOutline },
                Header = T("cm_template_later")
            };
            busy.Click += (a, b) => InsertTextTemplate("Сейчас занят, отвечу позже.");

            ActionSheetItem call = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20WriteOutline },
                Header = T("cm_template_call")
            };
            call.Click += (a, b) => InsertTextTemplate("Давай созвонимся, так быстрее.");

            ActionSheetItem todo = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20ListBulletOutline },
                Header = T("cm_make_todo_from_text")
            };
            todo.Click += async (a, b) => await SaveCurrentTextAsTodoAsync();

            ActionSheetItem remind = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20FavoriteOutline },
                Header = T("cm_make_reminder_from_text")
            };
            remind.Click += async (a, b) => await SaveCurrentTextAsReminderAsync();

            ActionSheetItem schedule15 = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20RecentOutline },
                Header = T("cm_schedule_15m")
            };
            schedule15.Click += async (a, b) => await ScheduleCurrentTextAsync(TimeSpan.FromMinutes(15), null);

            ActionSheetItem schedule60 = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20RecentOutline },
                Header = T("cm_schedule_1h")
            };
            schedule60.Click += async (a, b) => await ScheduleCurrentTextAsync(TimeSpan.FromHours(1), null);

            ActionSheetItem repeatDaily = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20RecentOutline },
                Header = T("cm_repeat_daily"),
                Subtitle = T("cm_first_time_24h")
            };
            repeatDaily.Click += async (a, b) => await ScheduleCurrentTextAsync(TimeSpan.FromDays(1), TimeSpan.FromDays(1));

            ActionSheetItem saveQuickReply = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20MessageUnreadTopOutline },
                Header = T("cm_save_quick_reply"),
                Subtitle = T("cm_only_this_chat")
            };
            saveQuickReply.Click += async (a, b) => await SaveCurrentTextAsQuickReplyAsync();

            ActionSheetItem saveFilterQuickReply = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20ListBulletOutline },
                Header = T("cm_save_filter_reply"),
                Subtitle = filterTitle
            };
            saveFilterQuickReply.Click += async (a, b) => await SaveCurrentTextAsFilterQuickReplyAsync();

            ActionSheetItem savePersonQuickReply = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20UserOutline },
                Header = T("cm_save_person_reply"),
                Subtitle = hasPersonTarget ? personQuickReplyTitle : T("cm_need_sender_or_history")
            };
            savePersonQuickReply.Click += async (a, b) => await SaveCurrentTextAsPersonQuickReplyAsync();

            ActionSheetItem pinQuickReply = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20PinOutline },
                Header = T("cm_pin_current_text"),
                Subtitle = T("cm_show_above_composer")
            };
            pinQuickReply.Click += async (a, b) => await PinCurrentTextAsQuickReplyAsync();

            ActionSheetItem clearQuickReplies = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20DeleteOutline },
                Header = T("cm_clear_quick_replies"),
                Subtitle = T("cm_only_this_chat")
            };
            clearQuickReplies.Classes.Add("Destructive");
            clearQuickReplies.Click += async (a, b) => await ClearPeerQuickRepliesAsync();

            ActionSheetItem clearFilterQuickReplies = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20DeleteOutline },
                Header = T("cm_clear_filter_replies"),
                Subtitle = filterTitle
            };
            clearFilterQuickReplies.Classes.Add("Destructive");
            clearFilterQuickReplies.Click += async (a, b) => await ClearFilterQuickRepliesAsync();

            ActionSheetItem clearPersonQuickReplies = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20DeleteOutline },
                Header = T("cm_clear_person_replies"),
                Subtitle = personQuickReplyTitle
            };
            clearPersonQuickReplies.Classes.Add("Destructive");
            clearPersonQuickReplies.Click += async (a, b) => await ClearPersonQuickRepliesAsync(personQuickReplySenderId, personQuickReplyTitle);

            ActionSheetItem clearPinnedQuickReply = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20PinSlashOutline },
                Header = T("cm_clear_pinned_reply")
            };
            clearPinnedQuickReply.Classes.Add("Destructive");
            clearPinnedQuickReply.Click += (a, b) => ClearPinnedQuickReply();

            ActionSheetItem encrypt = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20LockOutline },
                Header = T("cm_prepare_encrypt")
            };
            encrypt.Click += (a, b) => {
                Text = String.IsNullOrWhiteSpace(Text) ? "/encrypt " : $"/encrypt {Text}";
                TextSelectionStart = Text.Length;
                TextSelectionEnd = Text.Length;
            };

            ash.Items.Add(busy);
            ash.Items.Add(call);
            ash.Items.Add(new ActionSheetItem());
            ash.Items.Add(todo);
            ash.Items.Add(remind);
            ash.Items.Add(schedule15);
            ash.Items.Add(schedule60);
            ash.Items.Add(repeatDaily);
            ash.Items.Add(saveQuickReply);
            ash.Items.Add(saveFilterQuickReply);
            ash.Items.Add(savePersonQuickReply);
            ash.Items.Add(pinQuickReply);
            if (quickReplies.Count > 0) ash.Items.Add(clearQuickReplies);
            if (filterQuickReplies.Count > 0) ash.Items.Add(clearFilterQuickReplies);
            if (personQuickReplies.Count > 0) ash.Items.Add(clearPersonQuickReplies);
            if (HasPinnedQuickReply) ash.Items.Add(clearPinnedQuickReply);
            ash.Items.Add(encrypt);
            ash.ShowAt(target);
        }

        public void ShowFormattingActions(Button target) {
            ActionSheet ash = new ActionSheet {
                Placement = PlacementMode.TopEdgeAlignedRight
            };

            ash.Items.Add(CreateFormattingItem("Жирный", "**текст**", ApplyBoldFormat));
            ash.Items.Add(CreateFormattingItem("Курсив", "__текст__", ApplyItalicFormat));
            ash.Items.Add(CreateFormattingItem("Код", "`текст`", ApplyCodeFormat));
            ash.Items.Add(CreateFormattingItem("Зачеркнуть", "~~текст~~", ApplyStrikeFormat));
            ash.ShowAt(target);
        }

        private static ActionSheetItem CreateFormattingItem(string header, string subtitle, System.Action action) {
            ActionSheetItem item = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20ArticleOutline },
                Header = header,
                Subtitle = subtitle
            };
            item.Click += (a, b) => action();
            return item;
        }

        public void ApplyBoldFormat() {
            ApplyTextFormat("**", "**", "текст");
        }

        public void ApplyItalicFormat() {
            ApplyTextFormat("__", "__", "текст");
        }

        public void ApplyCodeFormat() {
            ApplyTextFormat("`", "`", "текст");
        }

        public void ApplyStrikeFormat() {
            ApplyTextFormat("~~", "~~", "текст");
        }

        private void ApplyTextFormat(string prefix, string suffix, string placeholder) {
            string current = Text ?? String.Empty;
            int start = Math.Clamp(Math.Min(TextSelectionStart, TextSelectionEnd), 0, current.Length);
            int end = Math.Clamp(Math.Max(TextSelectionStart, TextSelectionEnd), 0, current.Length);
            bool hasSelection = end > start;
            string selected = hasSelection ? current.Substring(start, end - start) : placeholder;
            string formatted = $"{prefix}{selected}{suffix}";

            Text = current.Remove(start, end - start).Insert(start, formatted);
            TextSelectionStart = start + prefix.Length;
            TextSelectionEnd = TextSelectionStart + selected.Length;
        }

        private void InsertTextTemplate(string template) {
            AppendTextRaw(ExpandMessageTemplateVariables(template));
        }

        private void AppendTextRaw(string value) {
            if (String.IsNullOrWhiteSpace(value)) return;

            if (String.IsNullOrWhiteSpace(Text)) {
                Text = value;
            } else {
                Text = $"{Text.TrimEnd()}\r\n{value}";
            }

            TextSelectionStart = Text.Length;
            TextSelectionEnd = Text.Length;
        }

        public void ApplyPinnedQuickReply() {
            if (!HasPinnedQuickReply) return;
            InsertTextTemplate(PinnedQuickReply);
        }

        public void ClearPinnedQuickReply() {
            Settings.SetPeerPinnedQuickReply(Chat.PeerId, null);
            PinnedQuickReply = String.Empty;
            session.ShowNotification(new Notification("Закрепленный ответ снят", "Только в текущем чате.", NotificationType.Success));
        }

        private async Task PinCurrentTextAsQuickReplyAsync() {
            await PinTextAsQuickReplyAsync(Text);
        }

        private async Task PinTextAsQuickReplyAsync(string text) {
            if (String.IsNullOrWhiteSpace(text)) {
                await new VKUIDialog("Нечего закреплять", "Напиши текст быстрого ответа. Закреплять воздух — сомнительная автоматизация.").ShowDialog(session.ModalWindow);
                return;
            }

            Settings.SetPeerPinnedQuickReply(Chat.PeerId, text);
            PinnedQuickReply = Settings.GetPeerPinnedQuickReply(Chat.PeerId);
            session.ShowNotification(new Notification("Быстрый ответ закреплён", BuildQuickReplyHeader(PinnedQuickReply), NotificationType.Success));
        }

        private string ExpandMessageTemplateVariables(string template) {
            if (String.IsNullOrEmpty(template)) return template;

            DateTime now = DateTime.Now;
            string peerName = Chat.DisplayTitle ?? String.Empty;
            string firstName = peerName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? peerName;

            return template
                .Replace("{name}", peerName, StringComparison.OrdinalIgnoreCase)
                .Replace("{peer}", peerName, StringComparison.OrdinalIgnoreCase)
                .Replace("{first_name}", firstName, StringComparison.OrdinalIgnoreCase)
                .Replace("{date}", now.ToString("dd.MM.yyyy"), StringComparison.OrdinalIgnoreCase)
                .Replace("{time}", now.ToString("HH:mm"), StringComparison.OrdinalIgnoreCase)
                .Replace("{datetime}", now.ToString("dd.MM.yyyy HH:mm"), StringComparison.OrdinalIgnoreCase)
                .Replace("{link}", BuildCurrentPeerLink(), StringComparison.OrdinalIgnoreCase)
                .Replace("{last_order}", ExtractLastOrderText(), StringComparison.OrdinalIgnoreCase);
        }

        private string BuildCurrentPeerLink() {
            if (Chat.PeerId.IsChat()) return $"https://vk.com/im?sel=c{Chat.PeerId - 2000000000}";
            if (Chat.PeerId.IsUser()) return $"https://vk.com/id{Chat.PeerId}";
            if (Chat.PeerId.IsGroup()) return $"https://vk.com/club{-Chat.PeerId}";
            return $"https://vk.com/im?sel={Chat.PeerId}";
        }

        private string ExtractLastOrderText() {
            string text = Chat.LastMessage?.Text;
            if (String.IsNullOrWhiteSpace(text)) return String.Empty;

            System.Text.RegularExpressions.Match number = System.Text.RegularExpressions.Regex.Match(text, @"#\d{2,}");
            if (number.Success) return number.Value;

            System.Text.RegularExpressions.Match order = System.Text.RegularExpressions.Regex.Match(text, @"\bзаказ[^\r\n.!?]{0,80}", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return order.Success ? order.Value.Trim() : String.Empty;
        }

        private async Task<bool> TryExecuteSlashCommandAsync() {
            string commandText = Text?.Trim();
            if (String.IsNullOrWhiteSpace(commandText) || !commandText.StartsWith('/')) return false;

            int separator = commandText.IndexOf(' ');
            string command = separator < 0 ? commandText.ToLowerInvariant() : commandText.Substring(0, separator).ToLowerInvariant();
            string argument = separator < 0 ? String.Empty : commandText.Substring(separator + 1).Trim();

            switch (command) {
                case "/todo":
                    await SaveTextAsTodoAsync(argument);
                    return true;
                case "/remind":
                    await SaveTextAsReminderAsync(argument);
                    return true;
                case "/qr":
                case "/quickreply":
                    await SaveTextAsQuickReplyAsync(argument);
                    return true;
                case "/qrf":
                case "/folderreply":
                    await SaveTextAsFilterQuickReplyAsync(argument);
                    return true;
                case "/qrp":
                case "/personreply":
                    await SavePersonQuickReplyFromCommandAsync(argument);
                    return true;
                case "/qrpin":
                    await PinTextAsQuickReplyAsync(argument);
                    Clear();
                    return true;
                case "/unqrpin":
                    ClearPinnedQuickReply();
                    Clear();
                    return true;
                case "/encrypt":
                    await SendEncryptedTextAsync(argument);
                    return true;
                case "/later":
                case "/schedule":
                    await ScheduleTextFromCommandAsync(argument, false);
                    return true;
                case "/repeat":
                    await ScheduleTextFromCommandAsync(argument, true);
                    return true;
                case "/download":
                    await ShowBulkAttachmentDownloadFromCommandAsync(argument);
                    return true;
                case "/mute":
                    await SetLocalQuietFromCommandAsync(argument);
                    return true;
                case "/unmute":
                    ClearLocalQuietFromCommand();
                    return true;
                case "/shadowban":
                    await ShadowBanFromCommandAsync(argument, false);
                    return true;
                case "/unshadowban":
                    await ShadowBanFromCommandAsync(argument, true);
                    return true;
                case "/status":
                    await SetAutoStatusFromCommandAsync(argument);
                    return true;
                case "/theme":
                    ShowChatThemeFromCommand();
                    return true;
                case "/e2e":
                    ShowE2EFromCommand();
                    return true;
                default:
                    return false;
            }
        }

        private async Task ShowBulkAttachmentDownloadFromCommandAsync(string argument) {
            Clear();
            await ContextMenuHelper.ShowBulkAttachmentDownloadAsync(session, Chat, session.Window?.StorageProvider);
        }

        private async Task SetLocalQuietFromCommandAsync(string argument) {
            string normalized = NormalizeSlashArgument(argument);
            if (IsClearToken(normalized)) {
                ClearLocalQuietFromCommand();
                return;
            }

            string durationText = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "1h";
            if (!TryParseScheduleDelay(NormalizeDurationToken(durationText), out TimeSpan duration)) {
                await new VKUIDialog("Mute не включён", "Формат: /mute 15m, /mute 2h или /mute 1d. /unmute снимает локальную тишину.").ShowDialog(session.ModalWindow);
                return;
            }

            DateTimeOffset until = DateTimeOffset.Now.Add(duration);
            Settings.SetPeerQuietUntil(Chat.PeerId, until);
            Clear();
            session.ShowNotification(new Notification("Чат заглушен локально", $"До {until.LocalDateTime:dd.MM HH:mm}. VK не трогали.", NotificationType.Success));
        }

        private void ClearLocalQuietFromCommand() {
            Settings.SetPeerQuietUntil(Chat.PeerId, null);
            Clear();
            session.ShowNotification(new Notification("Локальная тишина снята", "VK не трогали.", NotificationType.Success));
        }

        private async Task ShadowBanFromCommandAsync(string argument, bool remove) {
            if (!TryResolveSenderId(argument, out long senderId)) {
                await new VKUIDialog(
                    remove ? "Shadow-ban не снят" : "Shadow-ban не включён",
                    "Укажи sender id: /shadowban 123456 или открой чат с пользователем. VK shortname без API-резолва тут не угадываем.",
                    ["Понятно"],
                    1).ShowDialog(session.ModalWindow);
                return;
            }

            if (senderId == session.Id) {
                await new VKUIDialog("Нельзя", "Себя банить локально не будем. Для самокритики есть другие инструменты.", ["Понятно"], 1).ShowDialog(session.ModalWindow);
                return;
            }

            if (remove) {
                Settings.UnshadowBanSenderLocally(Chat.PeerId, senderId);
                await Chat.ReloadMessagesAsync();
                Clear();
                session.ShowNotification(new Notification("Теневой бан снят", $"sender:{senderId} снова виден в этом чате.", NotificationType.Success));
                return;
            }

            int removed = Chat.ShadowBanSenderLocally(senderId);
            Clear();
            session.ShowNotification(new Notification("Теневой бан", $"sender:{senderId} скрыт локально. Убрано сообщений: {removed}.", NotificationType.Success));
        }

        private async Task SetAutoStatusFromCommandAsync(string argument) {
            string normalized = NormalizeSlashArgument(argument);
            if (IsClearToken(normalized)) {
                Settings.AutoStatusEnabled = false;
                Clear();
                session.ShowNotification(new Notification("Автостатус выключен", "Laney-only статус больше не показывается.", NotificationType.Success));
                return;
            }

            if (!TryNormalizeAutoStatusMode(normalized, out string mode)) {
                await new VKUIDialog("Автостатус не изменён", "Формат: /status work, /status busy, /status gaming, /status sleep, /status dnd или /status off.", ["Понятно"], 1).ShowDialog(session.ModalWindow);
                return;
            }

            Settings.AutoStatusEnabled = true;
            Settings.AutoStatusMode = mode;
            Clear();
            session.ShowNotification(new Notification("Автостатус включён", AutoStatusManager.GetTitle(mode), NotificationType.Success));
        }

        private void ShowChatThemeFromCommand() {
            Clear();
            if (session.Window == null) return;
            ContextMenuHelper.ShowChatThemePicker(Chat, session.Window);
        }

        private void ShowE2EFromCommand() {
            Clear();
            if (session.Window == null) return;
            ContextMenuHelper.ShowE2EOptions(session, Chat, session.Window);
        }

        private async Task SaveCurrentTextAsTodoAsync() {
            await SaveTextAsTodoAsync(Text);
        }

        private async Task SaveCurrentTextAsReminderAsync() {
            await SaveTextAsReminderAsync(Text);
        }

        private async Task ScheduleCurrentTextAsync(TimeSpan delay, TimeSpan? repeatInterval) {
            await ScheduleTextAsync(Text, delay, repeatInterval);
        }

        private async Task SaveCurrentTextAsQuickReplyAsync() {
            await SaveTextAsQuickReplyAsync(Text);
        }

        private async Task SaveCurrentTextAsFilterQuickReplyAsync() {
            await SaveTextAsFilterQuickReplyAsync(Text);
        }

        private async Task SaveCurrentTextAsPersonQuickReplyAsync() {
            await SaveTextAsPersonQuickReplyAsync(Text, 0);
        }

        private async Task SaveTextAsTodoAsync(string text) {
            if (String.IsNullOrWhiteSpace(text)) {
                await new VKUIDialog("Todo не создан", "Напиши текст после /todo. Пустоту в список дел добавлять не будем, она и так везде.").ShowDialog(session.ModalWindow);
                return;
            }

            await QuickActionStore.AddTodoAsync(Chat.PeerId, text);
            Clear();
            session.ShowNotification(new Notification("Todo сохранён", "Лежит локально в quick-actions/todo.md", NotificationType.Success));
        }

        private async Task SaveTextAsReminderAsync(string text) {
            if (String.IsNullOrWhiteSpace(text)) {
                await new VKUIDialog("Reminder не создан", "Напиши текст после /remind. Машина времени без текста не заводится.").ShowDialog(session.ModalWindow);
                return;
            }

            await QuickActionStore.AddReminderAsync(Chat.PeerId, text);
            Clear();
            session.ShowNotification(new Notification("Reminder сохранён", "Лежит локально в quick-actions/reminders.md", NotificationType.Success));
        }

        private async Task ScheduleTextFromCommandAsync(string argument, bool repeat) {
            if (!TryParseScheduleCommand(argument, out TimeSpan delay, out string text)) {
                await new VKUIDialog(
                    repeat ? "Repeat не создан" : "Отложка не создана",
                    repeat ? "Формат: /repeat 1d текст. Поддержка: m/h/d." : "Формат: /later 15m текст. Поддержка: m/h/d.",
                    ["Понятно"],
                    1).ShowDialog(session.ModalWindow);
                return;
            }

            await ScheduleTextAsync(text, delay, repeat ? delay : null);
        }

        private async Task ScheduleTextAsync(string text, TimeSpan delay, TimeSpan? repeatInterval) {
            string normalized = text?.Trim();
            if (String.IsNullOrWhiteSpace(normalized)) {
                await new VKUIDialog("Отложка не создана", "Нужен текст. Вложениями и пустотой очередь пока не жонглирует.").ShowDialog(session.ModalWindow);
                return;
            }

            if (EditingMessageId != 0 || Reply != null || Attachments.Count > 0 || StickerId > 0) {
                await new VKUIDialog(
                    "Только текст",
                    "Отложенная отправка сейчас поддерживает только простой текст без reply, вложений и редактирования. Очередь upload/retry сделаем отдельным блоком, без героического болота.",
                    ["Понятно"],
                    1).ShowDialog(session.ModalWindow);
                return;
            }

            DateTimeOffset nextSend = DateTimeOffset.Now.Add(delay <= TimeSpan.Zero ? TimeSpan.FromMinutes(1) : delay);
            Settings.AddScheduledMessage(new ScheduledMessageItem {
                SessionId = session.Id,
                GroupId = session.GroupId,
                PeerId = Chat.PeerId,
                Text = ExpandMessageTemplateVariables(normalized),
                NextSendUnix = nextSend.ToUnixTimeSeconds(),
                RepeatIntervalMinutes = repeatInterval == null ? 0 : Math.Max(1, (int)Math.Round(repeatInterval.Value.TotalMinutes))
            });
            ScheduledMessageManager.EnsureStarted(session);
            Clear();

            string repeatText = repeatInterval == null ? String.Empty : $" Повтор: каждые {FormatScheduleDelay(repeatInterval.Value)}.";
            session.ShowNotification(new Notification("Сообщение запланировано", $"Отправка: {nextSend.LocalDateTime:dd.MM HH:mm}.{repeatText}", NotificationType.Success));
        }

        private static bool TryParseScheduleCommand(string argument, out TimeSpan delay, out string text) {
            delay = TimeSpan.Zero;
            text = String.Empty;
            if (String.IsNullOrWhiteSpace(argument)) return false;

            string[] parts = argument.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2 || !TryParseScheduleDelay(parts[0], out delay)) return false;

            text = parts[1];
            return !String.IsNullOrWhiteSpace(text);
        }

        private static bool TryParseScheduleDelay(string value, out TimeSpan delay) {
            delay = TimeSpan.Zero;
            if (String.IsNullOrWhiteSpace(value)) return false;

            string normalized = value.Trim().ToLowerInvariant();
            char unit = normalized[^1];
            string numberPart = Char.IsLetter(unit) ? normalized[..^1] : normalized;
            if (!double.TryParse(numberPart, out double number) || number <= 0) return false;

            delay = unit switch {
                'm' => TimeSpan.FromMinutes(number),
                'h' => TimeSpan.FromHours(number),
                'd' => TimeSpan.FromDays(number),
                _ when Char.IsDigit(unit) => TimeSpan.FromMinutes(number),
                _ => TimeSpan.Zero
            };
            return delay > TimeSpan.Zero;
        }

        private static string FormatScheduleDelay(TimeSpan delay) {
            if (delay.TotalDays >= 1) return $"{Math.Round(delay.TotalDays, 1)} д";
            if (delay.TotalHours >= 1) return $"{Math.Round(delay.TotalHours, 1)} ч";
            return $"{Math.Round(delay.TotalMinutes, 0)} мин";
        }

        private static string NormalizeSlashArgument(string argument) {
            return argument?.Trim() ?? String.Empty;
        }

        private static bool IsClearToken(string value) {
            string normalized = NormalizeSlashArgument(value).ToLowerInvariant();
            return normalized == "0"
                || normalized == "off"
                || normalized == "clear"
                || normalized == "disable"
                || normalized == "stop"
                || normalized == "выкл"
                || normalized == "снять";
        }

        private static string NormalizeDurationToken(string value) {
            string normalized = NormalizeSlashArgument(value).ToLowerInvariant();
            return normalized
                .Replace("minutes", "m", StringComparison.Ordinal)
                .Replace("minute", "m", StringComparison.Ordinal)
                .Replace("mins", "m", StringComparison.Ordinal)
                .Replace("min", "m", StringComparison.Ordinal)
                .Replace("минут", "m", StringComparison.Ordinal)
                .Replace("мин", "m", StringComparison.Ordinal)
                .Replace("м", "m", StringComparison.Ordinal)
                .Replace("hours", "h", StringComparison.Ordinal)
                .Replace("hour", "h", StringComparison.Ordinal)
                .Replace("часов", "h", StringComparison.Ordinal)
                .Replace("часа", "h", StringComparison.Ordinal)
                .Replace("час", "h", StringComparison.Ordinal)
                .Replace("ч", "h", StringComparison.Ordinal)
                .Replace("days", "d", StringComparison.Ordinal)
                .Replace("day", "d", StringComparison.Ordinal)
                .Replace("дней", "d", StringComparison.Ordinal)
                .Replace("дня", "d", StringComparison.Ordinal)
                .Replace("день", "d", StringComparison.Ordinal)
                .Replace("д", "d", StringComparison.Ordinal);
        }

        private bool TryResolveSenderId(string argument, out long senderId) {
            if (TryParseSenderToken(argument, out senderId)) return true;

            if (Chat.PeerId.IsUser() && Chat.PeerId != session.Id) {
                senderId = Chat.PeerId;
                return true;
            }

            MessageViewModel lastVisible = Chat.DisplayedMessages?
                .Where(m => m.SenderId != 0 && m.SenderId != session.Id)
                .LastOrDefault();
            MessageViewModel lastReceived = Chat.ReceivedMessages?
                .Where(m => m.SenderId != 0 && m.SenderId != session.Id)
                .LastOrDefault();
            senderId = lastVisible?.SenderId ?? lastReceived?.SenderId ?? 0;
            return senderId != 0;
        }

        private static bool TryParseSenderToken(string argument, out long senderId) {
            senderId = 0;
            string token = NormalizeSlashArgument(argument).Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (String.IsNullOrWhiteSpace(token)) return false;

            token = token.Trim().Trim(',', ';');
            int slashIndex = token.LastIndexOf('/');
            if (slashIndex >= 0 && slashIndex < token.Length - 1) token = token[(slashIndex + 1)..];

            if (token.StartsWith("[", StringComparison.Ordinal) && token.IndexOf('|') >= 0) {
                token = token[1..token.IndexOf('|')];
            }

            token = token.TrimStart('@');
            if (token.StartsWith("id", StringComparison.OrdinalIgnoreCase)) token = token[2..];
            if (token.StartsWith("club", StringComparison.OrdinalIgnoreCase)) token = "-" + token[4..];
            if (token.StartsWith("public", StringComparison.OrdinalIgnoreCase)) token = "-" + token[6..];

            return long.TryParse(token, out senderId) && senderId != 0;
        }

        private static bool TryNormalizeAutoStatusMode(string argument, out string mode) {
            mode = null;
            string normalized = NormalizeSlashArgument(argument)
                .ToLowerInvariant()
                .Replace('-', '_')
                .Replace(' ', '_');

            mode = normalized switch {
                "busy" or "занят" or "занята" => AutoStatusModeIds.Busy,
                "work" or "working" or "работа" or "работаю" => AutoStatusModeIds.Work,
                "gaming" or "game" or "игра" or "играю" => AutoStatusModeIds.Gaming,
                "sleep" or "sleeping" or "сон" or "сплю" => AutoStatusModeIds.Sleep,
                "dnd" or "do_not_disturb" or "not_disturb" or "не_трогать" or "не_беспокоить" => AutoStatusModeIds.DoNotDisturb,
                _ => null
            };
            return mode != null;
        }

        private async Task SaveTextAsQuickReplyAsync(string text) {
            if (String.IsNullOrWhiteSpace(text)) {
                await new VKUIDialog("Быстрый ответ не сохранён", "Напиши текст после /qr или в поле сообщения. Сохранять пустую заготовку — это уже искусство, но не функционал.").ShowDialog(session.ModalWindow);
                return;
            }

            bool added = Settings.AddPeerQuickReply(Chat.PeerId, text);
            if (!added) {
                session.ShowNotification(new Notification("Быстрый ответ уже есть", "Дубликат не добавлен.", NotificationType.Warning));
                return;
            }

            Clear();
            session.ShowNotification(new Notification("Быстрый ответ сохранён", "Будет в quick actions только этого чата.", NotificationType.Success));
        }

        private async Task SaveTextAsFilterQuickReplyAsync(string text) {
            if (String.IsNullOrWhiteSpace(text)) {
                await new VKUIDialog("Ответ папки не сохранён", "Напиши текст после /qrf или в поле сообщения. Папка с пустотой нам не помощник.").ShowDialog(session.ModalWindow);
                return;
            }

            string filterId = GetCurrentQuickReplyFilterId();
            bool added = Settings.AddChatFilterQuickReply(session.Id, filterId, text);
            if (!added) {
                session.ShowNotification(new Notification("Ответ папки уже есть", "Дубликат не добавлен.", NotificationType.Warning));
                return;
            }

            Clear();
            session.ShowNotification(new Notification("Ответ папки сохранён", $"Папка: {GetCurrentQuickReplyFilterTitle()}", NotificationType.Success));
        }

        private async Task SavePersonQuickReplyFromCommandAsync(string argument) {
            if (!TryExtractPersonQuickReplyCommand(argument, out long senderId, out string text)) {
                await new VKUIDialog(
                    "Ответ человеку не сохранён",
                    "Формат: /qrp 123456 текст или /qrp текст, если в чате можно угадать последнего отправителя.",
                    ["Понятно"],
                    1).ShowDialog(session.ModalWindow);
                return;
            }

            await SaveTextAsPersonQuickReplyAsync(text, senderId);
        }

        private async Task SaveTextAsPersonQuickReplyAsync(string text, long preferredSenderId) {
            if (String.IsNullOrWhiteSpace(text)) {
                await new VKUIDialog("Ответ человеку не сохранён", "Нужен текст. Персональный шаблон без текста — это уже молчаливый протест.").ShowDialog(session.ModalWindow);
                return;
            }

            long senderId = preferredSenderId;
            string title = null;
            if (senderId == 0 && !TryGetPersonQuickReplyTarget(out senderId, out title)) {
                await new VKUIDialog("Ответ человеку не сохранён", "Не нашёл отправителя. В групповом чате укажи id: /qrp 123456 текст.").ShowDialog(session.ModalWindow);
                return;
            }

            if (String.IsNullOrWhiteSpace(title)) title = GetPersonQuickReplyTitle(senderId);
            bool added = Settings.AddPersonQuickReply(session.Id, senderId, text);
            if (!added) {
                session.ShowNotification(new Notification("Ответ человеку уже есть", "Дубликат не добавлен.", NotificationType.Warning));
                return;
            }

            Clear();
            session.ShowNotification(new Notification("Ответ человеку сохранён", title, NotificationType.Success));
        }

        private async Task ClearPeerQuickRepliesAsync() {
            IReadOnlyList<string> quickReplies = Settings.GetPeerQuickReplies(Chat.PeerId);
            if (quickReplies.Count == 0) return;

            VKUIDialog dialog = new VKUIDialog(
                "Очистить быстрые ответы?",
                $"Удалить локальные быстрые ответы этого чата: {quickReplies.Count} шт.?",
                ["Очистить", "Отмена"],
                2);

            if (await dialog.ShowDialog<int>(session.ModalWindow) != 1) return;

            Settings.SetPeerQuickReplies(Chat.PeerId, Array.Empty<string>());
            session.ShowNotification(new Notification("Быстрые ответы очищены", "Только для текущего чата.", NotificationType.Success));
        }

        private async Task ClearFilterQuickRepliesAsync() {
            string filterId = GetCurrentQuickReplyFilterId();
            string filterTitle = GetCurrentQuickReplyFilterTitle();
            IReadOnlyList<string> quickReplies = Settings.GetChatFilterQuickReplies(session.Id, filterId);
            if (quickReplies.Count == 0) return;

            VKUIDialog dialog = new VKUIDialog(
                "Очистить ответы папки?",
                $"Удалить локальные быстрые ответы папки «{filterTitle}»: {quickReplies.Count} шт.?",
                ["Очистить", "Отмена"],
                2);

            if (await dialog.ShowDialog<int>(session.ModalWindow) != 1) return;

            Settings.SetChatFilterQuickReplies(session.Id, filterId, Array.Empty<string>());
            session.ShowNotification(new Notification("Ответы папки очищены", filterTitle, NotificationType.Success));
        }

        private async Task ClearPersonQuickRepliesAsync(long senderId, string title) {
            IReadOnlyList<string> quickReplies = Settings.GetPersonQuickReplies(session.Id, senderId);
            if (quickReplies.Count == 0) return;

            VKUIDialog dialog = new VKUIDialog(
                "Очистить ответы человеку?",
                $"Удалить локальные быстрые ответы для «{title}»: {quickReplies.Count} шт.?",
                ["Очистить", "Отмена"],
                2);

            if (await dialog.ShowDialog<int>(session.ModalWindow) != 1) return;

            Settings.SetPersonQuickReplies(session.Id, senderId, Array.Empty<string>());
            session.ShowNotification(new Notification("Ответы человеку очищены", title, NotificationType.Success));
        }

        private string GetCurrentQuickReplyFilterId() {
            return session.ImViewModel?.CurrentChatFilterId ?? "All";
        }

        private string GetCurrentQuickReplyFilterTitle() {
            return session.ImViewModel?.CurrentChatFilterTitle ?? Assets.i18n.Resources.all;
        }

        private bool TryGetPersonQuickReplyTarget(out long senderId, out string title) {
            senderId = 0;
            title = null;

            if (Chat.PeerId.IsUser() && Chat.PeerId != session.Id) {
                senderId = Chat.PeerId;
                title = GetPersonQuickReplyTitle(senderId);
                return true;
            }

            MessageViewModel lastVisible = Chat.DisplayedMessages?
                .Where(m => m.SenderId != 0 && m.SenderId != session.Id)
                .LastOrDefault();
            MessageViewModel lastReceived = Chat.ReceivedMessages?
                .Where(m => m.SenderId != 0 && m.SenderId != session.Id)
                .LastOrDefault();

            senderId = lastVisible?.SenderId ?? lastReceived?.SenderId ?? 0;
            if (senderId == 0) return false;

            title = GetPersonQuickReplyTitle(senderId);
            return true;
        }

        private static string GetPersonQuickReplyTitle(long senderId) {
            return $"{CacheManager.GetNameOnly(senderId)} · sender:{senderId}";
        }

        private bool TryExtractPersonQuickReplyCommand(string argument, out long senderId, out string text) {
            senderId = 0;
            text = argument?.Trim() ?? String.Empty;
            if (String.IsNullOrWhiteSpace(text)) return false;

            string[] parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length > 1 && TryParseSenderToken(parts[0], out senderId)) {
                text = parts[1].Trim();
                return !String.IsNullOrWhiteSpace(text);
            }

            return TryGetPersonQuickReplyTarget(out senderId, out _);
        }

        private List<string> GetRecentQuickReplySnippets(IEnumerable<string> knownReplies) {
            HashSet<string> known = new HashSet<string>(
                (knownReplies ?? Array.Empty<string>()).Select(NormalizeRecentSnippet).Where(s => !String.IsNullOrWhiteSpace(s)),
                StringComparer.OrdinalIgnoreCase);

            IEnumerable<MessageViewModel> source = Chat.DisplayedMessages?.Count > 0
                ? Chat.DisplayedMessages
                : Chat.ReceivedMessages ?? Enumerable.Empty<MessageViewModel>();

            List<string> snippets = new List<string>();
            foreach (MessageViewModel message in source.Reverse()) {
                string snippet = NormalizeRecentSnippet(message?.Text);
                if (String.IsNullOrWhiteSpace(snippet)) continue;
                if (known.Contains(snippet)) continue;
                if (snippets.Any(s => String.Equals(s, snippet, StringComparison.OrdinalIgnoreCase))) continue;

                snippets.Add(snippet);
                if (snippets.Count >= 6) break;
            }

            return snippets;
        }

        private static string NormalizeRecentSnippet(string text) {
            if (String.IsNullOrWhiteSpace(text)) return String.Empty;

            string normalized = text.Replace("\r", " ").Replace("\n", " ").Trim();
            if (normalized.Length < 6) return String.Empty;
            if (normalized.StartsWith("/", StringComparison.Ordinal)) return String.Empty;
            return normalized.Length <= 180 ? normalized : normalized[..180] + "...";
        }

        private static string BuildQuickReplyHeader(string text) {
            if (String.IsNullOrWhiteSpace(text)) return "Быстрый ответ";

            string oneLine = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return oneLine.Length <= 44 ? oneLine : $"{oneLine[..41]}...";
        }

        private async Task SendEncryptedTextAsync(string text) {
            if (String.IsNullOrWhiteSpace(text)) {
                await new VKUIDialog("E2E не отправлен", "Напиши текст после /encrypt. Шифровать пустоту можно, но зачем нам этот цирк.").ShowDialog(session.ModalWindow);
                return;
            }

            if (!E2EManager.CanEncrypt(Chat.PeerId)) {
                await new VKUIDialog("E2E ещё не настроен", "Открой профиль чата → Laney E2E → Настроить. Base64 вместо крипты отправлять не будем, мы же не варвары.").ShowDialog(session.ModalWindow);
                return;
            }

            string commandText = Text;
            try {
                Text = E2EManager.EncryptMessage(Chat.PeerId, text);
                await SendMessageAsync();
                Chat.RefreshE2EState();
            } catch (Exception ex) {
                Text = commandText;
                await new VKUIDialog("E2E не отправлен", ex.Message, ["Понятно"], 1).ShowDialog(session.ModalWindow);
            }
        }

        public void ShowEmojiStickerPicker(Control target) {
            var picker = new EmojiStickerPicker {
                Width = 400,
                Height = 438,
                DataContext = new EmojiStickerPickerViewModel(session, Chat?.PeerId ?? 0)
            };

            VKUIFlyout flyout = new VKUIFlyout {
                Content = picker
            };

            picker.EmojiPicked += Picker_EmojiPicked;
            picker.StickerPicked += async (a, b) => {
                flyout.Hide();
                await SendStickerAsync(b.StickerId);
            };
            picker.LocalStickerPicked += async (a, b) => {
                flyout.Hide();
                await SendLocalStickerAsync(b);
            };

            flyout.ShowAt(target);
        }

        private void Picker_EmojiPicked(object sender, string e) {
            try {
                if (TextSelectionStart == TextSelectionEnd) {
                    if (String.IsNullOrEmpty(Text)) {
                        Text = e;
                    } else {
                        Text = Text.Insert(TextSelectionEnd, e);
                    }
                    TextSelectionStart += e.Length;
                    TextSelectionEnd += e.Length;
                } else {
                    int start = Math.Min(TextSelectionStart, TextSelectionEnd);
                    int end = Math.Max(TextSelectionStart, TextSelectionEnd);
                    string newText = Text.Remove(start, end - start);
                    Text = newText.Insert(start, e);
                    start += e.Length;
                    TextSelectionStart = start;
                    TextSelectionEnd = start;
                }
            } catch (ArgumentOutOfRangeException) { // Workaround for issue #20 that mostye not reproducible
                if (String.IsNullOrEmpty(Text)) {
                    Text = e;
                } else {
                    Text = Text.Insert(Text.Length - 1, e);
                }
            }
        }

        public void ApplyEmojiSuggestion(SingleEmoji emoji) {
            string value = emoji.ToString();
            if (String.IsNullOrEmpty(Text) || !TryGetCurrentWordRange(out int start, out int end)) {
                Text = String.IsNullOrEmpty(Text) ? value : Text.Insert(Math.Clamp(TextSelectionEnd, 0, Text.Length), value);
                TextSelectionStart = TextSelectionEnd = Math.Clamp((TextSelectionEnd + value.Length), 0, Text.Length);
                SuggestedEmojis = null;
                return;
            }

            string suffix = end < Text.Length && !Char.IsWhiteSpace(Text[end]) ? " " : String.Empty;
            Text = Text.Remove(start, end - start).Insert(start, value + suffix);
            int caret = start + value.Length + suffix.Length;
            TextSelectionStart = caret;
            TextSelectionEnd = caret;
            SuggestedEmojis = null;
        }

        private bool TryGetCurrentWordRange(out int start, out int end) {
            start = -1;
            end = -1;
            if (String.IsNullOrWhiteSpace(Text)) return false;

            int caret = Math.Clamp(TextSelectionEnd, 0, Text.Length);
            start = caret;
            while (start > 0 && !Char.IsWhiteSpace(Text[start - 1])) start--;

            end = caret;
            while (end < Text.Length && !Char.IsWhiteSpace(Text[end])) end++;

            return end > start;
        }

        public void InsertMention(Entity entity) {
            if (entity == null || MentionStart < 0 || String.IsNullOrEmpty(Text)) return;

            int caret = Math.Clamp(TextSelectionEnd, 0, Text.Length);
            if (caret < MentionStart) return;

            string mention = GetMentionText(entity);
            string suffix = caret < Text.Length && !Char.IsWhiteSpace(Text[caret]) ? " " : String.Empty;
            Text = Text.Remove(MentionStart, caret - MentionStart).Insert(MentionStart, mention + suffix);

            int newCaret = MentionStart + mention.Length + suffix.Length;
            TextSelectionStart = newCaret;
            TextSelectionEnd = newCaret;
            MentionStart = -1;
            SuggestedMentions.Clear();
        }

        private static string GetMentionText(Entity entity) {
            string label = String.IsNullOrWhiteSpace(entity.Name) ? "mention" : entity.Name;
            if (entity.Id > 0) return $"[id{entity.Id}|{label}]";
            return $"[club{Math.Abs(entity.Id)}|{label}]";
        }

        private async Task AddAttachmentsAsync(object pickerResult) {
            if (pickerResult == null) return;
            if (pickerResult is List<AttachmentBase> attachments) {
                foreach (AttachmentBase attachment in attachments) {
                    Attachments.Add(new OutboundAttachmentViewModel(session, attachment));
                }
            } else if (pickerResult is Tuple<int, List<IStorageFile>> pfiles) {
                await AddStorageFilesAsync(pfiles.Item2, pfiles.Item1);
            }
        }

        public async Task AddStorageFilesAsync(IEnumerable<IStorageFile> files, int uploadType, bool optimizePhotoUpload = true) {
            if (files == null) return;
            IEnumerable<IStorageFile> selectedFiles = files.Take(GetRemainingAttachmentSlots()).ToList();
            if (optimizePhotoUpload && uploadType == Constants.PhotoUploadCommand && session.Window?.StorageProvider?.CanOpen == true) {
                List<ImageUploadOptimizationResult> optimizedFiles = await ImageUploadOptimizer.OptimizeForPhotoUploadAsync(session.Window.StorageProvider, selectedFiles);
                selectedFiles = optimizedFiles.Select(f => f.File).ToList();
                ShowImageOptimizationReport(optimizedFiles);
            }

            foreach (IStorageFile file in selectedFiles) {
                Attachments.Add(new OutboundAttachmentViewModel(session, file, uploadType));
                await Task.Delay(500);
            }
        }

        private int GetRemainingAttachmentSlots() {
            int count = Attachments.Where(a => a.Type == OutboundAttachmentType.Attachment).Count();
            return Math.Max(0, 10 - count);
        }

        public void AddReply(MessageViewModel message) {
            Reply = message;
            var favm = Attachments.Where(a => a.Type == OutboundAttachmentType.ForwardedMessages).FirstOrDefault();
            if (favm != null) Attachments.Remove(favm);
        }

        public void AddForwardedMessages(long fromPeerId, List<MessageViewModel> messages, long groupId = 0) {
            if (messages == null || messages.Count == 0) return;
            var favm = Attachments.Where(a => a.Type == OutboundAttachmentType.ForwardedMessages).FirstOrDefault();
            if (favm != null) Attachments.Remove(favm);
            Attachments.Insert(0, new OutboundAttachmentViewModel(fromPeerId, messages, groupId));
            Reply = null;
        }

        public CleanCopyResult AddMessagesAsCleanCopies(IEnumerable<MessageViewModel> messages) {
            CleanCopyResult result = new CleanCopyResult();
            if (messages == null) return result;

            List<MessageViewModel> source = messages
                .Where(m => m != null)
                .DistinctBy(m => m.ConversationMessageId)
                .OrderBy(m => m.ConversationMessageId)
                .ToList();
            result.SourceMessages = source.Count;
            if (source.Count == 0) return result;

            List<string> textBlocks = source
                .Select(m => m.Text?.Trim())
                .Where(t => !String.IsNullOrWhiteSpace(t))
                .ToList();
            string text = String.Join("\r\n\r\n", textBlocks);
            AppendTextRaw(text);
            result.TextBlocks = textBlocks.Count;

            foreach (MessageViewModel message in source) {
                if (message.ForwardedMessages?.Count > 0) result.SkippedForwardedMessages += message.ForwardedMessages.Count;
                if (GetRemainingAttachmentSlots() <= 0) break;
                if (message.Attachments == null || message.Attachments.Count == 0) continue;

                foreach (Attachment attachment in CollectionsMarshal.AsSpan(message.Attachments)) {
                    if (GetRemainingAttachmentSlots() <= 0) {
                        result.SkippedAttachments++;
                        continue;
                    }
                    if (!attachment.Type.CanAttachToSend()) {
                        result.SkippedAttachments++;
                        continue;
                    }

                    OutboundAttachmentViewModel oavm = OutboundAttachmentViewModel.FromAttachmentBase(session, attachment);
                    if (oavm == null) {
                        result.SkippedAttachments++;
                        continue;
                    }

                    Attachments.Add(oavm);
                    result.Attachments++;
                }
            }

            Reply = null;
            return result;
        }

        public void StartEditing(MessageViewModel message) {
            Clear();
            EditingMessageId = message.ConversationMessageId;

            Text = message.Text;
            if (message.ReplyMessage != null) Reply = MessageViewModel.Create(message.ReplyMessage, session);

            foreach (var attachment in CollectionsMarshal.AsSpan(message.Attachments)) {
                if (attachment.Type.CanAttachToSend()) {
                    var oavm = OutboundAttachmentViewModel.FromAttachmentBase(session, attachment);
                    if (oavm != null) Attachments.Add(oavm);
                }
            }

            // TODO: удаление пересланных сообщений
            // AddForwardedMessages(message.ForwardedMessages);
        }

        public void DeleteReply() {
            Reply = null;
        }

        public void CancelEditing() {
            Clear();
        }

        public async Task SendMessageAsync() {
            if (!CanSendMessage || IsLoading) return;
            if (await TryExecuteSlashCommandAsync()) return;

            int uploadingFiles = Attachments.Where(a => a.Type == OutboundAttachmentType.Attachment && a.IsUploading).Count();
            int failedFiles = Attachments.Where(a => a.Type == OutboundAttachmentType.Attachment && a.UploadException != null).Count();

            if (uploadingFiles > 0) {
                VKUIDialog dlg = new VKUIDialog("Cannot send a message at this moment", $"Please wait until {uploadingFiles} files has been uploaded");
                await dlg.ShowDialog(session.Window);
                return;
            }

            if (failedFiles > 0) {
                VKUIDialog dlg = new VKUIDialog("Cannot send a message", $"You have {failedFiles} failed attachments. Please re-upload or delete these attachments.");
                await dlg.ShowDialog(session.Window);
                return;
            }

            string text = !String.IsNullOrEmpty(Text) ? Text.Replace("\r\n", "\r").Replace("\r", "\r\n").Trim() : null;
            string phraseForLearning = text;
            bool skipPhraseLearning = false;
            if (EditingMessageId == 0 && E2EManager.ShouldAutoEncryptText(Chat.PeerId, text)) {
                text = E2EManager.EncryptMessage(Chat.PeerId, text);
                skipPhraseLearning = true;
            }

            var attachments = Attachments.Where(a => a.Type == OutboundAttachmentType.Attachment)
                .Select(a => a?.Attachment?.ToString()).ToList();


            var favm = Attachments.Where(a => a.Type == OutboundAttachmentType.ForwardedMessages).FirstOrDefault();
            int replyTo = Reply?.ConversationMessageId ?? 0;
            string forward = String.Empty;

            if (replyTo > 0) {
                forward = $"{{\"peer_id\":{Chat.PeerId},\"conversation_message_ids\":[{replyTo}],\"is_reply\":true}}";
            } else if (favm != null) {
                List<int> cmids = new List<int>();
                if (favm.ForwardedMessagesFromGroupId > 0) {
                    long ownerId = favm.ForwardedMessagesFromGroupId * -1;
                    foreach (var m in favm.ForwardedMessages) cmids.Add(m.ConversationMessageId);
                    string cmidstr = String.Join(',', cmids);
                    forward = $"{{\"owner_id\":{ownerId},\"peer_id\":{favm.ForwardedMessagesFromPeerId},\"conversation_message_ids\":[{cmidstr}]}}";
                } else {
                    cmids = favm.ForwardedMessages.Select(m => m.ConversationMessageId).ToList();
                    string cmidstr = String.Join(',', cmids);
                    forward = $"{{\"peer_id\":{favm.ForwardedMessagesFromPeerId},\"conversation_message_ids\":[{cmidstr}]}}";
                }
            }

            bool dontParseLinks = Settings.DontParseLinks;
            bool disableMentions = Settings.DisableMentions;

            IsLoading = true;

            try {
                if (EditingMessageId == 0) {
                    Log.Verbose($"Sending message: session={session.Id}; peer_id={Chat.PeerId}, random={RandomId}");
                    var response = await session.API.Messages.SendAsync(session.GroupId,
                        Chat.PeerId, RandomId, text, 0, 0, attachments, forward, StickerId,
                        dontParseLinks: dontParseLinks, disableMentions: disableMentions);
                    if (!skipPhraseLearning) FrequentPhraseStore.MarkUsed(Chat.PeerId, phraseForLearning);
                    RandomId = Random.Next(Int32.MinValue, Int32.MaxValue);
                    Log.Verbose($"Sending message result: {response.MessageId}; new random: {RandomId}");
                } else {
                    var response = await session.API.Messages.EditAsync(session.GroupId, Chat.PeerId, EditingMessageId,
                        text, 0, 0, attachments, true, true, dontParseLinks);
                    // TODO: keep snippets и сделать недоступным добавление пересланных, если активен режим редактирования. 
                    // TODO: удаление пересланных сообщений
                }
                Clear();
                IsLoading = false;
            } catch (Exception ex) {
                IsLoading = false;
                if (await ExceptionHelper.ShowErrorDialogAsync(session.Window, ex)) await SendMessageAsync();
            }
        }

        public async Task SendStickerAsync(int stickerId) {
            StickerId = stickerId;
            CheckCanSendMessage();
            await SendMessageAsync();
        }

        public async Task SendLocalStickerAsync(LocalSticker sticker) {
            if (sticker == null || !File.Exists(sticker.FilePath)) return;
            if (GetRemainingAttachmentSlots() <= 0) {
                await new VKUIDialog("Слишком много вложений", "VK дает максимум 10 вложений в сообщении. Стикер уже некуда прикрутить.").ShowDialog(session.ModalWindow);
                return;
            }

            IStorageProvider storageProvider = session.Window?.StorageProvider;
            if (storageProvider?.CanOpen != true) return;

            int uploadType = ResolveLocalStickerUploadType(sticker, out string uploadPath);
            IStorageFile file = await storageProvider.TryGetFileFromPathAsync(uploadPath);
            if (file == null) return;

            if (uploadType == Constants.GraffitiUploadCommand) {
                List<IStorageFile> prepared = await PrepareGraffitiFilesAsync(storageProvider, [file]);
                await AddStorageFilesAsync(prepared, Constants.GraffitiUploadCommand, false);
            } else {
                await AddStorageFilesAsync([file], uploadType, false);
            }

            LocalStickerStore.MarkUsed(sticker.Id);
        }

        private static int ResolveLocalStickerUploadType(LocalSticker sticker, out string uploadPath) {
            uploadPath = sticker.FilePath;
            string mode = Settings.LocalStickerSendMode;
            if (mode != LocalStickerSendModeIds.File && sticker.HasRasterFallback) {
                uploadPath = sticker.FallbackFilePath;
                return mode switch {
                    LocalStickerSendModeIds.Image when sticker.FallbackCanUploadAsImageAttachment => Constants.PhotoUploadCommand,
                    LocalStickerSendModeIds.Auto when sticker.ShouldUploadFallbackAsGraffiti() => Constants.GraffitiUploadCommand,
                    LocalStickerSendModeIds.Graffiti when sticker.ShouldUploadFallbackAsGraffiti() => Constants.GraffitiUploadCommand,
                    _ => Constants.FileUploadCommand
                };
            }

            return mode switch {
                LocalStickerSendModeIds.Graffiti when sticker.ShouldUploadAsGraffiti() => Constants.GraffitiUploadCommand,
                LocalStickerSendModeIds.Image when sticker.CanUploadAsImageAttachment => Constants.PhotoUploadCommand,
                LocalStickerSendModeIds.File => Constants.FileUploadCommand,
                LocalStickerSendModeIds.Auto when sticker.ShouldUploadAsGraffiti() => Constants.GraffitiUploadCommand,
                _ => Constants.FileUploadCommand
            };
        }

        private async Task ToggleAudioRecordingAsync() {
            if (DemoMode.IsEnabled || IsLoading) return;

            if (IsRecordingAudio) {
                await StopAudioRecordingAsync();
            } else {
                await StartAudioRecordingAsync();
            }
        }

        private async Task StartAudioRecordingAsync() {
            if (!VoiceRecorder.IsSupported) {
                await new VKUIDialog("Запись недоступна", "Сейчас запись голосовых реализована через Windows waveIn. На Linux/macOS нужен отдельный backend, без притворства.").ShowDialog(session.ModalWindow);
                return;
            }

            if (GetRemainingAttachmentSlots() <= 0) {
                await new VKUIDialog("Слишком много вложений", "VK дает максимум 10 вложений в сообщении. Голосовое уже некуда прикрутить.").ShowDialog(session.ModalWindow);
                return;
            }

            try {
                string path = GetVoiceRecordingPath();
                voiceRecorder = new VoiceRecorder(path);
                voiceRecorder.Start();
                voiceRecordingStartedAt = DateTimeOffset.Now;
                IsRecordingAudio = true;
                session.ShowNotification(new Notification("Запись началась", "Нажми микрофон ещё раз, чтобы остановить.", NotificationType.Success));
            } catch (Exception ex) {
                voiceRecorder?.Dispose();
                voiceRecorder = null;
                IsRecordingAudio = false;
                await ExceptionHelper.ShowErrorDialogAsync(session.ModalWindow, ex, true);
            }
        }

        private async Task StopAudioRecordingAsync() {
            VoiceRecorder recorder = voiceRecorder;
            if (recorder == null) {
                IsRecordingAudio = false;
                return;
            }

            string path = recorder.OutputPath;
            TimeSpan duration = DateTimeOffset.Now - voiceRecordingStartedAt;

            try {
                recorder.Stop();
                voiceRecorder = null;
                IsRecordingAudio = false;

                if (duration < TimeSpan.FromSeconds(1)) {
                    TryDeleteFile(path);
                    session.ShowNotification(new Notification("Голосовое слишком короткое", "Меньше секунды не отправляем, это уже нервный тик.", NotificationType.Warning));
                    return;
                }

                VoiceProcessingResult processing = VoiceAudioProcessor.ProcessRecordedWav(path);
                if (!String.IsNullOrWhiteSpace(processing?.OutputPath)) {
                    path = processing.OutputPath;
                }

                string transcript = await LocalVoiceTranscriptionService.TryTranscribeRecordedWavAsync(path);
                if (!String.IsNullOrWhiteSpace(transcript)) {
                    Text = String.IsNullOrWhiteSpace(Text) ? $"Расшифровка: {transcript}" : $"{Text}\nРасшифровка: {transcript}";
                }

                IStorageFile file = await session.Window.StorageProvider.TryGetFileFromPathAsync(path);
                if (file == null) throw new FileNotFoundException("Recorded voice file was not found.", path);

                await AddStorageFilesAsync([file], Constants.AudioMessageUploadCommand, false);
                string processingText = processing != null
                    ? $" Шумодав/нормализация: {processing.GainDb:+0.0;-0.0;0.0} dB."
                    : " DSP пропущен, отправляю исходник.";
                string transcriptText = !String.IsNullOrWhiteSpace(transcript) ? " Локальная расшифровка добавлена в текст." : String.Empty;
                session.ShowNotification(new Notification("Голосовое записано", $"Длительность: {duration:mm\\:ss}.{processingText}{transcriptText} Дождись загрузки и отправляй.", NotificationType.Success));
            } catch (Exception ex) {
                voiceRecorder = null;
                IsRecordingAudio = false;
                await ExceptionHelper.ShowErrorDialogAsync(session.ModalWindow, ex, true);
            }
        }

        private static string GetVoiceRecordingPath() {
            string directory = Path.Combine(Path.GetTempPath(), "Laney", "Voice");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, $"laney-voice-{DateTimeOffset.Now.ToUnixTimeSeconds()}-{Guid.NewGuid():N}.wav");
        }

        private static void TryDeleteFile(string path) {
            try {
                if (File.Exists(path)) File.Delete(path);
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot delete short voice recording {Path}", path);
            }
        }

        public void Clear() {
            draftSaveTimer?.Stop();
            if (Chat != null && EditingMessageId == 0) Settings.ClearPeerCurrentDraft(Chat.PeerId);
            EditingMessageId = 0;
            Reply = null;
            Text = null;
            Attachments.Clear();
            StickerId = 0;
        }

        // Подсказки стикеров

        private void CheckStickersSuggestions() {
            if (!Settings.SuggestStickers) {
                SuggestedStickers = null;
                SuggestedLocalStickers = null;
                SuggestedEmojis = null;
                return;
            }

            var stickers = StickersManager.GetStickersByWord(Text);
            SuggestedStickers = stickers;
            string localQuery = GetLastStickerQueryWord(Text);
            SuggestedLocalStickers = String.IsNullOrWhiteSpace(localQuery) ? null : LocalStickerStore.Search(localQuery).Take(8).ToList();
            SuggestedEmojis = String.IsNullOrWhiteSpace(localQuery) ? null : L2Emoji.SearchByWord(localQuery, Chat?.PeerId ?? 0).Take(8).ToList();
        }

        private static string GetLastStickerQueryWord(string text) {
            if (String.IsNullOrWhiteSpace(text)) return String.Empty;

            string[] parts = text.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0) return String.Empty;

            string last = parts.LastOrDefault();
            return last?.Length >= 2 ? last : String.Empty;
        }

        private void CheckMentionSuggestions() {
            SuggestedMentions.Clear();
            MentionStart = -1;

            if (Chat?.PeerType != PeerType.Chat || String.IsNullOrEmpty(Text)) return;
            if (Chat.MembersUsers.Count == 0 && Chat.MembersGroups.Count == 0) return;

            int caret = Math.Clamp(TextSelectionEnd, 0, Text.Length);
            if (caret == 0) return;

            int start = Text.LastIndexOf('@', Math.Max(0, caret - 1), caret);
            if (start < 0 || !IsMentionStart(Text, start)) return;

            string query = Text.Substring(start + 1, caret - start - 1);
            if (query.Any(Char.IsWhiteSpace)) return;

            MentionStart = start;
            foreach (Entity mention in GetMentionCandidates(query).Take(8)) {
                SuggestedMentions.Add(mention);
            }
        }

        private IEnumerable<Entity> GetMentionCandidates(string query) {
            string normalizedQuery = query?.Trim().ToLowerInvariant() ?? String.Empty;

            foreach (User user in Chat.MembersUsers) {
                string name = user.FullName;
                if (!IsMentionCandidateMatch(name, user.Domain, normalizedQuery)) continue;
                yield return new Entity(user.Id, user.Photo, name, user.Domain, null);
            }

            foreach (Group group in Chat.MembersGroups) {
                string name = group.Name;
                string domain = !String.IsNullOrWhiteSpace(group.ScreenName) ? group.ScreenName : $"club{group.Id}";
                if (!IsMentionCandidateMatch(name, domain, normalizedQuery)) continue;
                yield return new Entity(-group.Id, group.Photo, name, domain, null);
            }
        }

        private static bool IsMentionCandidateMatch(string name, string domain, string query) {
            if (String.IsNullOrWhiteSpace(query)) return true;
            return (name?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                || (domain?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        private static bool IsMentionStart(string text, int index) {
            if (index <= 0) return true;

            char previous = text[index - 1];
            return Char.IsWhiteSpace(previous) || previous == '(' || previous == '[' || previous == '{';
        }
    }
}
