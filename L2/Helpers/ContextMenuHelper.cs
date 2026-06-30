using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Labs.Qr;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ELOR.Laney.Core;
using ELOR.Laney.Core.Localization;
using ELOR.Laney.Extensions;
using ELOR.Laney.ViewModels;
using ELOR.Laney.ViewModels.Controls;
using ELOR.Laney.Views.Modals;
using ELOR.VKAPILib.Objects;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using VKUI.Controls;
using VKUI.Popups;

namespace ELOR.Laney.Helpers {

    // Кроме контекстного меню, тут будут функции, которые могут юзаться в разных конт. меню.
    public class ContextMenuHelper {
        private static string T(string key) => Localizer.Get(key);
        private static string TF(string key, params object[] args) => Localizer.GetFormatted(key, args);

        private static string[] BulkCleanupPeriodOptions => [T("cm_bulk_period_all_selected"), T("cm_bulk_period_today"), T("cm_bulk_period_7_days"), T("cm_bulk_period_30_days")];
        private static string[] BulkCleanupAttachmentOptions => [T("cm_bulk_attachment_any"), T("cm_bulk_attachment_media"), T("cm_bulk_attachment_documents"), T("cm_bulk_attachment_links"), T("cm_bulk_attachment_voice"), T("cm_bulk_attachment_graffiti"), T("cm_bulk_attachment_stickers"), T("cm_bulk_attachment_text")];
        private static string[] BulkCleanupSenderOptions => [T("cm_bulk_sender_any"), T("cm_bulk_sender_mine"), T("cm_bulk_sender_others")];
        private static string[] AttachmentDownloadProfileOptions => [T("cm_download_profile_all"), T("cm_download_profile_photos"), T("cm_download_profile_documents"), T("cm_download_profile_voice"), T("cm_download_profile_video"), T("cm_download_profile_audio"), T("cm_download_profile_stickers")];
        private static readonly string[] AttachmentDownloadProfileOptionIds = [
            AttachmentDownloadProfileIds.All,
            AttachmentDownloadProfileIds.Photos,
            AttachmentDownloadProfileIds.Documents,
            AttachmentDownloadProfileIds.Voice,
            AttachmentDownloadProfileIds.Video,
            AttachmentDownloadProfileIds.Audio,
            AttachmentDownloadProfileIds.Stickers
        ];
        private static string[] AttachmentDownloadSpeedOptions => [T("cm_download_speed_unlimited"), "512 KB/s", "2 MB/s", "8 MB/s"];
        private static readonly int[] AttachmentDownloadSpeedLimits = [0, 512, 2048, 8192];
        private static readonly string[] E2EPassphraseWords = [
            "atlas", "comet", "delta", "ember", "fable", "glow",
            "harbor", "iris", "juno", "karma", "lumen", "matrix",
            "nova", "orbit", "pixel", "quartz", "raven", "signal",
            "tundra", "umbra", "velvet", "wave", "xenon", "yuki"
        ];

        #region For chat

        public static void ShowForChat(ChatViewModel chat, Control target) {
            ActionSheet ash = new ActionSheet {
                IsSearchEnabled = true
            };

            ActionSheetItem debug = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20BugOutline },
                Header = $"ID: {chat.PeerId} ({chat.PeerType})"
            };
            ActionSheetItem debugDeleteConvoVisually = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20BugOutline },
                Header = T("cm_debug_simulate_deleted")
            };

            ActionSheetItem read = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20MessageOutline },
                Header = Assets.i18n.Resources.mark_read,
            };
            ActionSheetItem unread = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20MessageUnreadTopOutline },
                Header = Assets.i18n.Resources.mark_unread,
            };
            ActionSheetItem notifon = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20NotificationOutline },
                Header = Assets.i18n.Resources.notifications_enable,
            };
            ActionSheetItem notifoff = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20NotificationSlashOutline },
                Header = Assets.i18n.Resources.notifications_disable,
            };
            DateTimeOffset? quietUntil = Settings.GetPeerQuietUntil(chat.PeerId);
            bool localQuietActive = quietUntil != null && quietUntil.Value > DateTimeOffset.Now;
            ActionSheetItem localQuiet = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20RecentOutline },
                Header = T("cm_local_quiet"),
                Subtitle = localQuietActive ? TF("cm_until", quietUntil.Value.LocalDateTime.ToString("dd.MM HH:mm")) : T("cm_local_quiet_subtitle")
            };
            ActionSheetItem antiSpam = new ActionSheetItem {
                Before = new VKIcon { Id = Settings.IsPeerAntiSpamEnabled(chat.PeerId) ? VKIconNames.Icon20Check : VKIconNames.Icon20ReportOutline },
                Header = Settings.IsPeerAntiSpamEnabled(chat.PeerId) ? T("cm_antispam_disable") : T("cm_antispam_enable"),
                Subtitle = T("cm_antispam_subtitle")
            };
            ActionSheetItem clear = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20DeleteOutline },
                Header = Assets.i18n.Resources.chat_clear_history,
            };
            ActionSheetItem theme = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon28PaletteOutline },
                Header = T("cm_theme_profile"),
                Subtitle = T("cm_theme_profile_subtitle")
            };
            ActionSheetItem attachmentsGallery = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20PictureOutline },
                Header = T("cm_attachments_gallery"),
                Subtitle = T("cm_attachments_gallery_subtitle")
            };
            ActionSheetItem downloadAttachments = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20DocumentOutline },
                Header = T("cm_download_attachments"),
                Subtitle = T("cm_download_attachments_subtitle")
            };
            ActionSheetItem gift = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20GiftOutline },
                Header = T("cm_send_gift"),
                Subtitle = T("cm_send_gift_subtitle")
            };
            ActionSheetItem archive = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20DoorArrowRightOutline },
                Header = T("cm_archive"),
                Subtitle = T("cm_archive_subtitle")
            };
            ActionSheetItem unarchive = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20DoorEnterArrowRightOutline },
                Header = T("cm_unarchive"),
                Subtitle = T("cm_unarchive_subtitle")
            };
            ActionSheetItem e2e = new ActionSheetItem {
                Before = new VKIcon { Id = chat.HasE2EStatus ? VKIconNames.Icon20LockOutline : VKIconNames.Icon20UnlockOutline },
                Header = T("cm_e2e_profile"),
                Subtitle = chat.HasE2EStatus ? chat.E2EStatusText : T("cm_e2e_profile_subtitle")
            };
            ActionSheetItem splitView = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20MessageArrowRightOutline },
                Header = T("cm_open_split_view"),
                Subtitle = T("cm_open_split_view_subtitle")
            };
            ActionSheetItem floatingWindow = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20MessageOutline },
                Header = T("cm_open_floating_window"),
                Subtitle = T("cm_open_floating_window_subtitle")
            };
            ActionSheetItem jumpToDate = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20RecentOutline },
                Header = T("cm_jump_to_date"),
                Subtitle = T("cm_jump_to_date_subtitle")
            };
            ActionSheetItem leave = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20DoorArrowRightOutline },
                Header = chat.ChatSettings?.IsGroupChannel == true ? Assets.i18n.Resources.pp_exit_channel : Assets.i18n.Resources.pp_exit_chat,
            };
            ActionSheetItem creturn = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20DoorEnterArrowRightOutline },
                Header = chat.ChatSettings?.IsGroupChannel == true ? Assets.i18n.Resources.pp_return_channel : Assets.i18n.Resources.pp_return_chat,
            };
            ActionSheetItem gdeny = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20BlockOutline },
                Header = Assets.i18n.Resources.pp_deny,
            };

            clear.Classes.Add("Destructive");
            leave.Classes.Add("Destructive");
            gdeny.Classes.Add("Destructive");

            // Conditions

            var session = VKSession.GetByDataContext(target);

            bool notificationsDisabled = chat.PushSettings.DisabledForever || chat.PushSettings.DisabledUntil > DateTimeOffset.Now.ToUnixTimeSeconds();

            // Actions

            debugDeleteConvoVisually.Click += (a, b) => {
                session.LongPoll.DebugFireDeleteConvoEvent(chat.PeerId);
            };

            notifon.Click += async (a, b) => await SetChatNotificationsAsync(session, chat, true);

            notifoff.Click += async (a, b) => await SetChatNotificationsAsync(session, chat, false);

            read.Click += async (a, b) => await MarkChatAsReadAsync(session, chat);

            unread.Click += async (a, b) => await MarkChatAsUnreadAsync(session, chat);

            clear.Click += (a, b) => TryClearChat(session, chat.PeerId);
            theme.Click += (a, b) => new System.Action(async () => await Router.OpenPeerProfileAsync(session, chat.PeerId))();
            archive.Click += (a, b) => SetChatArchived(session, chat, true);
            unarchive.Click += (a, b) => SetChatArchived(session, chat, false);
            e2e.Click += (a, b) => new System.Action(async () => await Router.OpenPeerProfileAsync(session, chat.PeerId))();
            attachmentsGallery.Click += (a, b) => OpenChatAttachmentsGallery(session, chat);
            downloadAttachments.Click += async (a, b) => await ShowBulkAttachmentDownloadAsync(session, chat, target);
            gift.Click += async (a, b) => await OpenGiftFlowAsync(chat.PeerId);
            localQuiet.Click += (a, b) => ShowLocalQuietOptions(session, chat, target);
            antiSpam.Click += (a, b) => ToggleAntiSpam(session, chat);
            splitView.Click += (a, b) => session.OpenSecondaryChat(chat.PeerId);
            floatingWindow.Click += (a, b) => session.OpenFloatingChat(chat.PeerId);
            jumpToDate.Click += async (a, b) => await ShowJumpToDateAsync(session, chat, target);
            leave.Click += (a, b) => TryLeaveChat(session, chat.PeerId);
            creturn.Click += (a, b) => ReturnToChat(session, chat.PeerId);
            gdeny.Click += (a, b) => ExceptionHelper.ShowNotImplementedDialog(session.ModalWindow);

            // ¯\_(ツ)_/¯

            if (Settings.ShowDevItemsInContextMenus) {
                ash.Items.Add(ActionSheetItem.Section(T("cm_section_debug")));
                ash.Items.Add(debug);
                ash.Items.Add(debugDeleteConvoVisually);
            }

            ash.Items.Add(ActionSheetItem.Section(T("cm_section_status_notifications")));
            if (chat.UnreadMessagesCount > 0 || chat.IsMarkedAsUnread) ash.Items.Add(read);
            if (chat.UnreadMessagesCount == 0 && !chat.IsMarkedAsUnread) ash.Items.Add(unread);

            if (chat.PeerId != session.Id) {
                if (!notificationsDisabled) ash.Items.Add(notifoff);
                if (notificationsDisabled) ash.Items.Add(notifon);
                ash.Items.Add(localQuiet);
            }

            // TODO: Запретить сообщения для диалога с группой.

            ash.Items.Add(ActionSheetItem.Section(T("cm_section_appearance_security")));
            ash.Items.Add(theme);
            ash.Items.Add(e2e);
            if (chat.PeerId.IsUser() && chat.PeerId != session.Id) ash.Items.Add(gift);

            ash.Items.Add(ActionSheetItem.Section(T("cm_section_workspace")));
            ash.Items.Add(splitView);
            ash.Items.Add(floatingWindow);
            ash.Items.Add(jumpToDate);

            ash.Items.Add(ActionSheetItem.Section(T("cm_section_attachments")));
            ash.Items.Add(attachmentsGallery);
            ash.Items.Add(downloadAttachments);

            ash.Items.Add(ActionSheetItem.Section(T("cm_section_local_rules")));
            if (chat.PeerType == PeerType.Chat) ash.Items.Add(antiSpam);
            ash.Items.Add(chat.IsArchived ? unarchive : archive);

            ash.Items.Add(ActionSheetItem.Section(T("cm_section_danger_zone")));
            if (!session.IsGroup && chat.PeerType == PeerType.Chat && chat.ChatSettings != null) {
                if (chat.ChatSettings.State == UserStateInChat.In) ash.Items.Add(leave);
                if (chat.ChatSettings.State == UserStateInChat.Left) ash.Items.Add(creturn);
            }
            if (chat.PeerId != session.Id) ash.Items.Add(clear);

            if (ash.Items.Count > 0) ash.ShowAt(target, true);
        }

        public static async Task ShowJumpToDateAsync(VKSession session, ChatViewModel chat, Control target) {
            if (session == null || chat == null) return;

            TextBox dateBox = new TextBox {
                Text = DateTime.Now.ToString("yyyy-MM-dd"),
                PlaceholderText = T("cm_jump_date_placeholder"),
                MinWidth = 280
            };

            VKUIDialog dialog = new VKUIDialog(
                T("cm_jump_to_date"),
                T("cm_jump_to_date_dialog_text"),
                [T("go"), T("cancel")],
                2) {
                DialogContent = dateBox
            };

            int result = await dialog.ShowDialog<int>(session.ModalWindow);
            if (result != 1) return;

            try {
                DateTime? date = ParseOptionalDate(dateBox.Text, "Дата");
                if (date == null) throw new ArgumentException("Дата пустая. Тут без гадания на кофейной гуще.");
                await new VKUIWaitDialog<bool>().ShowAsync(session.ModalWindow, JumpToDateAsync(chat, date.Value));
            } catch (Exception ex) {
                await new VKUIDialog(T("cm_jump_failed_title"), ex.GetBaseException().Message, [T("ok")], 1).ShowDialog<int>(session.ModalWindow);
            }
        }

        private static async Task<bool> JumpToDateAsync(ChatViewModel chat, DateTime date) {
            await chat.JumpToDateAsync(date);
            return true;
        }

        public static async Task SetChatNotificationsAsync(VKSession session, ChatViewModel chat, bool enabled) {
            if (session == null || chat == null || DemoMode.IsEnabled) return;

            try {
                await session.API.Account.SetSilenceModeAsync(enabled ? 0 : -1, chat.PeerId, true);
            } catch (Exception ex) {
                await ExceptionHelper.ShowErrorDialogAsync(session.ModalWindow, ex);
            }
        }

        public static async Task MarkChatAsReadAsync(VKSession session, ChatViewModel chat) {
            if (session == null || chat?.LastMessage == null || DemoMode.IsEnabled) return;

            try {
                await session.API.Messages.MarkAsReadAsync(session.GroupId, chat.PeerId, chat.LastMessage.ConversationMessageId, true);
            } catch (Exception ex) {
                await ExceptionHelper.ShowErrorDialogAsync(session.ModalWindow, ex);
            }
        }

        public static async Task MarkChatAsUnreadAsync(VKSession session, ChatViewModel chat) {
            if (session == null || chat == null || DemoMode.IsEnabled) return;

            try {
                await session.API.Messages.MarkAsUnreadConversationAsync(session.GroupId, chat.PeerId);
            } catch (Exception ex) {
                await ExceptionHelper.ShowErrorDialogAsync(session.ModalWindow, ex);
            }
        }

        public static void OpenChatAttachmentsGallery(VKSession session, ChatViewModel chat) {
            if (session == null || chat == null) return;
            new ChatAttachmentsGallery(session, chat).ShowDialog(session.ModalWindow);
        }

        public static void ToggleAntiSpam(VKSession session, ChatViewModel chat) {
            bool enabled = !Settings.IsPeerAntiSpamEnabled(chat.PeerId);
            Settings.SetPeerAntiSpamEnabled(chat.PeerId, enabled);
            if (enabled) {
                new System.Action(async () => await chat.ReloadMessagesAsync())();
            }
            session.ShowNotification(new Notification(
                enabled ? "Анти-спам включен" : "Анти-спам выключен",
                "Работает локально только в Laney.",
                NotificationType.Success));
        }

        public static Task ShowBulkAttachmentDownloadAsync(VKSession session, ChatViewModel chat, Control target) {
            return ShowBulkAttachmentDownloadAsync(session, chat, target == null ? null : TopLevel.GetTopLevel(target)?.StorageProvider);
        }

        public static async Task ShowBulkAttachmentDownloadAsync(VKSession session, ChatViewModel chat, IStorageProvider storageProvider) {
            if (session == null || chat == null) return;

            ComboBox profileBox = CreateBulkCleanupComboBox(AttachmentDownloadProfileOptions);
            ComboBox speedBox = CreateBulkCleanupComboBox(AttachmentDownloadSpeedOptions);
            TextBox filterBox = new TextBox {
                PlaceholderText = T("cm_download_filter_placeholder"),
                MinWidth = 360
            };
            TextBox senderBox = new TextBox {
                PlaceholderText = T("cm_download_sender_placeholder"),
                MinWidth = 360
            };
            TextBox fromBox = new TextBox {
                PlaceholderText = T("cm_download_from_placeholder"),
                MinWidth = 360
            };
            TextBox toBox = new TextBox {
                PlaceholderText = T("cm_download_to_placeholder"),
                MinWidth = 360
            };
            TextBox maxSizeBox = new TextBox {
                PlaceholderText = T("cm_download_size_placeholder"),
                MinWidth = 360
            };
            CheckBox dedupeBox = new CheckBox {
                Content = T("cm_download_dedup"),
                IsChecked = true
            };
            CheckBox sidecarBox = new CheckBox {
                Content = T("cm_download_sidecar"),
                IsChecked = true
            };
            CheckBox resumeBox = new CheckBox {
                Content = T("cm_download_resume"),
                IsChecked = true
            };
            CheckBox fullHistoryBox = new CheckBox {
                Content = T("cm_download_backfill"),
                IsChecked = false
            };
            CheckBox pauseAfterPageBox = new CheckBox {
                Content = T("cm_download_pause"),
                IsChecked = false
            };

            StackPanel content = new StackPanel {
                Spacing = 8,
                MinWidth = 380
            };
            content.Children.Add(CreateBulkCleanupField(T("cm_download_field_profile"), profileBox));
            content.Children.Add(CreateBulkCleanupField(T("cm_download_field_filter"), filterBox));
            content.Children.Add(CreateBulkCleanupField(T("cm_download_field_sender"), senderBox));
            content.Children.Add(CreateBulkCleanupField(T("cm_download_field_from"), fromBox));
            content.Children.Add(CreateBulkCleanupField(T("cm_download_field_to"), toBox));
            content.Children.Add(CreateBulkCleanupField(T("cm_download_field_size"), maxSizeBox));
            content.Children.Add(CreateBulkCleanupField(T("cm_download_field_speed"), speedBox));
            content.Children.Add(dedupeBox);
            content.Children.Add(sidecarBox);
            content.Children.Add(resumeBox);
            content.Children.Add(fullHistoryBox);
            content.Children.Add(pauseAfterPageBox);

            VKUIDialog dialog = new VKUIDialog(
                T("cm_download_attachments"),
                T("cm_download_dialog_text"),
                [T("cancel"), T("cm_download_choose_folder")],
                2) {
                DialogContent = new ScrollViewer {
                    MaxHeight = 520,
                    Content = content
                }
            };

            if (await dialog.ShowDialog<int>(session.ModalWindow) != 2) return;

            ChatAttachmentDownloadOptions options;
            try {
                options = BuildAttachmentDownloadOptions(profileBox, speedBox, filterBox, senderBox, fromBox, toBox, maxSizeBox, dedupeBox, sidecarBox, resumeBox, fullHistoryBox, pauseAfterPageBox);
            } catch (ArgumentException ex) {
                await new VKUIDialog("Фильтр не принят", ex.Message, ["Понятно"], 1).ShowDialog(session.ModalWindow);
                return;
            }

            if (storageProvider?.CanOpen != true) {
                await new VKUIDialog("Папка недоступна", "StorageProvider не дает открыть folder picker. Без папки скачать некуда, очевидно.", ["Понятно"], 1).ShowDialog(session.ModalWindow);
                return;
            }

            IReadOnlyList<IStorageFolder> folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions {
                Title = "Папка для экспорта вложений Laney",
                AllowMultiple = false
            });
            IStorageFolder folder = folders?.FirstOrDefault();
            if (folder == null) return;

            string rootPath = folder.TryGetLocalPath();
            if (String.IsNullOrWhiteSpace(rootPath)) {
                await new VKUIDialog("Папка не файловая", "Выбранное место не имеет локального пути. Этот экспорт пишет обычные файлы, без облачного шаманства.", ["Понятно"], 1).ShowDialog(session.ModalWindow);
                return;
            }

            string targetDirectory = BuildAttachmentDownloadDirectory(rootPath, chat, options.ResumeExisting);
            try {
                Task<ChatAttachmentDownloadResult> downloadTask = ChatAttachmentDownloadHelper.DownloadAttachmentsAsync(session, chat, targetDirectory, options);
                ChatAttachmentDownloadResult result = await new VKUIWaitDialog<ChatAttachmentDownloadResult>().ShowAsync(session.ModalWindow, downloadTask);
                NotificationType notificationType = result.Failed > 0 ? NotificationType.Warning : NotificationType.Success;
                session.ShowNotification(new Notification("Вложения скачаны", result.Summary, notificationType));

                if (result.Paused) {
                    await new VKUIDialog("Очередь на паузе", $"{result.Summary}\n\nПовтори экспорт в эту же папку с Resume, чтобы продолжить. Да, как нормальные люди.", ["Понятно"], 1).ShowDialog(session.ModalWindow);
                } else if (result.Failed > 0) {
                    string errors = String.Join(Environment.NewLine, result.Errors.Take(8));
                    await new VKUIDialog("Часть вложений не скачалась", $"{result.Summary}\n\n{errors}", ["Понятно"], 1).ShowDialog(session.ModalWindow);
                }
            } catch (Exception ex) {
                await ExceptionHelper.ShowErrorDialogAsync(session.ModalWindow, ex, true);
            }
        }

        private static ChatAttachmentDownloadOptions BuildAttachmentDownloadOptions(ComboBox profileBox, ComboBox speedBox, TextBox filterBox, TextBox senderBox, TextBox fromBox, TextBox toBox, TextBox maxSizeBox, CheckBox dedupeBox, CheckBox sidecarBox, CheckBox resumeBox, CheckBox fullHistoryBox, CheckBox pauseAfterPageBox) {
            string profileId = AttachmentDownloadProfileOptionIds[Math.Clamp(profileBox.SelectedIndex, 0, AttachmentDownloadProfileOptionIds.Length - 1)];
            ChatAttachmentDownloadOptions options = ChatAttachmentDownloadOptions.CreateForProfile(profileId);
            options.TextFilter = filterBox.Text?.Trim();
            options.SenderId = ParseOptionalLong(senderBox.Text, "sender id");
            options.FromDate = ParseOptionalDate(fromBox.Text, "Дата с");
            options.ToDate = ParseOptionalDate(toBox.Text, "Дата по");
            options.MaxSizeBytes = ParseOptionalMegabytes(maxSizeBox.Text, "макс. размер");
            options.SpeedLimitKbPerSecond = AttachmentDownloadSpeedLimits[Math.Clamp(speedBox.SelectedIndex, 0, AttachmentDownloadSpeedLimits.Length - 1)];
            options.DedupeByHash = dedupeBox.IsChecked == true;
            options.WriteSidecarJson = sidecarBox.IsChecked == true;
            options.ResumeExisting = resumeBox.IsChecked == true;
            options.FullHistoryBackfill = fullHistoryBox.IsChecked == true;
            options.PauseAfterBackfillPage = pauseAfterPageBox.IsChecked == true;

            if (options.FromDate != null && options.ToDate != null && options.ToDate.Value.Date < options.FromDate.Value.Date) {
                throw new ArgumentException("Дата по меньше даты с. Машина времени в комплект не входит.");
            }

            return options;
        }

        private static string BuildAttachmentDownloadDirectory(string rootPath, ChatViewModel chat, bool resumeExisting) {
            string title = ChatAttachmentDownloadHelper.SanitizeFileName(chat.Title, $"peer{chat.PeerId}", 48);
            if (resumeExisting) return Path.Combine(rootPath, $"Laney-{title}-peer{chat.PeerId}");
            return Path.Combine(rootPath, $"Laney-{title}-{DateTime.Now:yyyyMMdd-HHmmss}");
        }

        private static long? ParseOptionalLong(string value, string fieldName) {
            if (String.IsNullOrWhiteSpace(value)) return null;
            if (long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long result)) return result;
            if (long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out result)) return result;
            throw new ArgumentException($"{fieldName}: нужен обычный integer id.");
        }

        private static DateTime? ParseOptionalDate(string value, string fieldName) {
            if (String.IsNullOrWhiteSpace(value)) return null;

            string text = value.Trim();
            string[] formats = ["yyyy-MM-dd", "dd.MM.yyyy", "dd.MM.yy", "yyyy.MM.dd"];
            if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime exact)) return exact.Date;
            if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out DateTime parsed)) return parsed.Date;
            throw new ArgumentException($"{fieldName}: дата должна быть вроде 2026-06-27 или 27.06.2026.");
        }

        private static ulong? ParseOptionalMegabytes(string value, string fieldName) {
            if (String.IsNullOrWhiteSpace(value)) return null;

            string text = value.Trim();
            if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out decimal megabytes)
                && !decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out megabytes)) {
                throw new ArgumentException($"{fieldName}: нужен размер в MB, например 50 или 12.5.");
            }
            if (megabytes <= 0) throw new ArgumentException($"{fieldName}: размер должен быть больше нуля.");
            return (ulong)(megabytes * 1024 * 1024);
        }

        public static void ShowLocalQuietOptions(VKSession session, ChatViewModel chat, Control target) {
            ActionSheet ash = new ActionSheet();
            AddLocalQuietOption(ash, session, chat, T("cm_quiet_15m"), TimeSpan.FromMinutes(15));
            AddLocalQuietOption(ash, session, chat, T("cm_quiet_1h"), TimeSpan.FromHours(1));
            AddLocalQuietOption(ash, session, chat, T("cm_quiet_until_tomorrow"), DateTime.Today.AddDays(1).AddHours(9) - DateTime.Now);

            if (Settings.IsPeerQuietNow(chat.PeerId, DateTimeOffset.Now)) {
                ActionSheetItem clear = new ActionSheetItem {
                    Before = new VKIcon { Id = VKIconNames.Icon20NotificationOutline },
                    Header = T("cm_quiet_clear"),
                    Subtitle = T("cm_quiet_clear_subtitle")
                };
                clear.Click += (a, b) => {
                    Settings.SetPeerQuietUntil(chat.PeerId, null);
                    session.ShowNotification(new Notification(T("cm_quiet_cleared_title"), T("cm_quiet_cleared_body"), NotificationType.Success));
                };
                ash.Items.Add(clear);
            }

            ash.ShowAt(target, true);
        }

        private static void AddLocalQuietOption(ActionSheet ash, VKSession session, ChatViewModel chat, string header, TimeSpan duration) {
            if (duration <= TimeSpan.Zero) duration = TimeSpan.FromHours(1);
            DateTimeOffset until = DateTimeOffset.Now.Add(duration);
            ActionSheetItem item = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20NotificationSlashOutline },
                Header = header,
                Subtitle = TF("cm_until", until.LocalDateTime.ToString("dd.MM HH:mm", CultureInfo.CurrentCulture))
            };
            item.Click += (a, b) => {
                Settings.SetPeerQuietUntil(chat.PeerId, until);
                session.ShowNotification(new Notification(T("cm_quiet_applied_title"), TF("cm_quiet_applied_body", until.LocalDateTime.ToString("dd.MM HH:mm", CultureInfo.CurrentCulture)), NotificationType.Success));
            };
            ash.Items.Add(item);
        }

        public static void ShowE2EOptions(VKSession session, ChatViewModel chat, Control target) {
            ActionSheet ash = new ActionSheet();
            E2EPeerState state = E2EManager.GetPeerState(chat.PeerId);
            bool configured = state != null && E2EKeyStore.HasPeerKeys(chat.PeerId, state.ProfileId);

            ActionSheetItem setup = new ActionSheetItem {
                Before = new VKIcon { Id = configured ? VKIconNames.Icon20WriteOutline : VKIconNames.Icon20LockOutline },
                Header = configured ? T("cm_e2e_change_key_profile") : T("cm_e2e_setup"),
                Subtitle = T("cm_e2e_profile_mode_subtitle")
            };
            setup.Click += async (a, b) => await ShowE2ESetupDialogAsync(session, chat);
            ash.Items.Add(setup);

            ActionSheetItem createHandshake = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20ShareOutline },
                Header = T("cm_e2e_copy_handshake"),
                Subtitle = T("cm_e2e_copy_handshake_subtitle")
            };
            createHandshake.Click += async (a, b) => await ShowCreateX25519HandshakeDialogAsync(session, chat, target);
            ash.Items.Add(createHandshake);

            ActionSheetItem importHandshake = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20DocumentOutline },
                Header = T("cm_e2e_import_handshake"),
                Subtitle = T("cm_e2e_import_handshake_subtitle")
            };
            importHandshake.Click += async (a, b) => await ShowImportX25519HandshakeDialogAsync(session, chat, target);
            ash.Items.Add(importHandshake);

            ActionSheetItem importBackup = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20DocumentOutline },
                Header = T("cm_e2e_import_backup"),
                Subtitle = T("cm_e2e_import_backup_subtitle")
            };
            importBackup.Click += async (a, b) => await ShowImportE2EBackupDialogAsync(session, chat);
            ash.Items.Add(importBackup);

            if (state != null) {
                ActionSheetItem fingerprint = new ActionSheetItem {
                    Before = new VKIcon { Id = VKIconNames.Icon20InfoCircleOutline },
                    Header = T("cm_e2e_fingerprint"),
                    Subtitle = T("cm_e2e_fingerprint_subtitle")
                };
                fingerprint.Click += async (a, b) => await ShowE2EFingerprintDialogAsync(session, chat, target);
                ash.Items.Add(fingerprint);

                ActionSheetItem verify = new ActionSheetItem {
                    Before = new VKIcon { Id = state.IsVerified ? VKIconNames.Icon20UnlockOutline : VKIconNames.Icon20Check },
                    Header = state.IsVerified ? T("cm_e2e_unverify") : T("cm_e2e_verify"),
                    Subtitle = state.IsVerified ? T("cm_e2e_unverify_subtitle") : T("cm_e2e_verify_subtitle")
                };
                verify.Click += (a, b) => {
                    E2EManager.SetPeerVerified(chat.PeerId, !state.IsVerified);
                    chat.RefreshE2EState();
                    session.ShowNotification(new Notification("Laney E2E", !state.IsVerified ? "Ключ отмечен как сверенный." : "Сверка ключа снята.", NotificationType.Success));
                };
                ash.Items.Add(verify);

                ActionSheetItem autoEncrypt = new ActionSheetItem {
                    Before = new VKIcon { Id = state.AutoEncryptText ? VKIconNames.Icon20Check : VKIconNames.Icon20LockOutline },
                    Header = state.AutoEncryptText ? T("cm_e2e_auto_encrypt_enabled") : T("cm_e2e_auto_encrypt"),
                    Subtitle = T("cm_e2e_auto_encrypt_subtitle")
                };
                autoEncrypt.Click += (a, b) => {
                    E2EManager.SetAutoEncryptText(chat.PeerId, !state.AutoEncryptText);
                    chat.RefreshE2EState();
                    session.ShowNotification(new Notification("Laney E2E", !state.AutoEncryptText ? "Автошифрование текста включено." : "Автошифрование текста выключено.", NotificationType.Success));
                };
                ash.Items.Add(autoEncrypt);

                ActionSheetItem rotate = new ActionSheetItem {
                    Before = new VKIcon { Id = VKIconNames.Icon24RepeatOutline },
                    Header = T("cm_e2e_rotate_keys"),
                    Subtitle = T("cm_e2e_rotate_keys_subtitle")
                };
                rotate.Click += async (a, b) => await RotateX25519KeysAsync(session, chat, target);
                ash.Items.Add(rotate);

                ActionSheetItem exportBackup = new ActionSheetItem {
                    Before = new VKIcon { Id = VKIconNames.Icon20LockOutline },
                    Header = T("cm_e2e_export_backup"),
                    Subtitle = T("cm_e2e_export_backup_subtitle")
                };
                exportBackup.Click += async (a, b) => await ShowExportE2EBackupDialogAsync(session, chat, target);
                ash.Items.Add(exportBackup);

                ActionSheetItem reset = new ActionSheetItem {
                    Before = new VKIcon { Id = VKIconNames.Icon20DeleteOutline },
                    Header = T("cm_e2e_reset_chat"),
                    Subtitle = T("cm_e2e_reset_chat_subtitle")
                };
                reset.Classes.Add("Destructive");
                reset.Click += async (a, b) => await ResetE2EAsync(session, chat);
                ash.Items.Add(reset);
            }

            ash.ShowAt(target, true);
        }

        public static async Task ShowE2ESetupDialogAsync(VKSession session, ChatViewModel chat) {
            if (session == null || chat == null || chat.PeerId == 0) return;

            E2EPeerState state = E2EManager.GetPeerState(chat.PeerId);
            List<string> profileIds = E2ESecurityProfileIds.All.ToList();
            ComboBox profileBox = new ComboBox {
                ItemsSource = profileIds.Select(E2ESecurityProfileIds.GetTitle).ToList(),
                SelectedIndex = Math.Max(0, profileIds.IndexOf(E2ESecurityProfileIds.Normalize(state?.ProfileId))),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            TextBox passphraseBox = new TextBox {
                PasswordChar = '*',
                PlaceholderText = "Общая E2E-фраза",
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            Button generateButton = new Button {
                Classes = { "Secondary" },
                Content = "Сгенерировать",
                Margin = new Avalonia.Thickness(8, 0, 0, 0)
            };
            generateButton.Click += (a, b) => {
                passphraseBox.PasswordChar = '\0';
                passphraseBox.Text = GenerateE2EPassphrase();
            };
            Grid passphraseRow = new Grid {
                ColumnDefinitions = new ColumnDefinitions("* Auto"),
                Children = {
                    passphraseBox,
                    generateButton
                }
            };
            Grid.SetColumn(generateButton, 1);
            CheckBox verifiedBox = new CheckBox {
                Content = "Сразу отметить ключ сверенным",
                IsChecked = state?.IsVerified == true
            };
            CheckBox autoEncryptBox = new CheckBox {
                Content = "Автошифровать обычный исходящий текст",
                IsChecked = state?.AutoEncryptText == true
            };
            StackPanel content = new StackPanel {
                Spacing = 8,
                Children = {
                    new TextBlock {
                        Text = "Фраза должна совпадать на втором Laney-клиенте. Это shared-secret MVP, не Double Ratchet.",
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    profileBox,
                    passphraseRow,
                    verifiedBox,
                    autoEncryptBox
                }
            };

            VKUIDialog dialog = new VKUIDialog("Laney E2E", "Выбери профиль и введи фразу. /encrypt начнёт отправлять зашифрованный payload.", ["Отмена", "Сохранить"], 2) {
                DialogContent = content
            };
            int result = await dialog.ShowDialog<int>(session.ModalWindow);
            if (result != 2) return;

            try {
                string profileId = profileIds[Math.Clamp(profileBox.SelectedIndex, 0, profileIds.Count - 1)];
                E2EPeerState newState = E2EManager.ConfigurePeerFromPassphrase(chat.PeerId, profileId, passphraseBox.Text, verifiedBox.IsChecked == true, autoEncryptBox.IsChecked == true);
                chat.RefreshE2EState();
                session.ShowNotification(new Notification("Laney E2E настроен", $"{E2ESecurityProfileIds.GetTitle(newState.ProfileId)} · SAS {newState.Sas}", NotificationType.Success));
            } catch (Exception ex) {
                await new VKUIDialog("E2E не настроен", ex.Message, ["Понятно"], 1).ShowDialog(session.ModalWindow);
            }
        }

        private static string GenerateE2EPassphrase() {
            List<string> words = new List<string>(7);
            for (int i = 0; i < 6; i++) {
                words.Add(E2EPassphraseWords[RandomNumberGenerator.GetInt32(E2EPassphraseWords.Length)]);
            }
            words.Add(RandomNumberGenerator.GetInt32(10, 100).ToString(CultureInfo.InvariantCulture));
            return String.Join("-", words);
        }

        public static async Task ShowCreateX25519HandshakeDialogAsync(VKSession session, ChatViewModel chat, Control target) {
            E2EPeerState state = E2EManager.GetPeerState(chat.PeerId);
            List<string> profileIds = E2ESecurityProfileIds.All.ToList();
            ComboBox profileBox = new ComboBox {
                ItemsSource = profileIds.Select(E2ESecurityProfileIds.GetTitle).ToList(),
                SelectedIndex = Math.Max(0, profileIds.IndexOf(E2ESecurityProfileIds.Normalize(state?.ProfileId))),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            CheckBox autoEncryptBox = new CheckBox {
                Content = "Автошифровать обычный исходящий текст после handshake",
                IsChecked = state?.AutoEncryptText == true
            };
            StackPanel content = new StackPanel {
                Spacing = 8,
                Children = {
                    new TextBlock {
                        Text = "Создаст локальную X25519-пару и скопирует public handshake-token. Отправь его собеседнику, потом импортируй ответный токен.",
                        TextWrapping = TextWrapping.Wrap
                    },
                    profileBox,
                    autoEncryptBox
                }
            };

            VKUIDialog dialog = new VKUIDialog("X25519 handshake", "Создать новый public handshake-token для этого чата?", ["Отмена", "Создать"], 2) {
                DialogContent = content
            };
            if (await dialog.ShowDialog<int>(session.ModalWindow) != 2) return;

            try {
                string profileId = profileIds[Math.Clamp(profileBox.SelectedIndex, 0, profileIds.Count - 1)];
                E2EHandshakeResult result = E2EManager.CreateX25519Handshake(chat.PeerId, profileId, autoEncryptBox.IsChecked == true);
                await CopyTextAsync(target, result.Token);
                chat.RefreshE2EState();
                await ShowTokenDialogAsync(session, "X25519 token создан", "Токен скопирован. Отправь его собеседнику, потом импортируй ответный токен.", result.Token);
            } catch (Exception ex) {
                await new VKUIDialog("Handshake не создан", ex.Message, ["Понятно"], 1).ShowDialog(session.ModalWindow);
            }
        }

        public static async Task ShowImportX25519HandshakeDialogAsync(VKSession session, ChatViewModel chat, Control target) {
            string clipboardText = await TryGetClipboardTextAsync(target);
            TextBox tokenBox = new TextBox {
                Text = clipboardText?.Contains(E2EManager.HandshakePrefix) == true ? clipboardText : String.Empty,
                PlaceholderText = "laney-e2e-handshake:v1:...",
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 120,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            CheckBox verifiedBox = new CheckBox { Content = "Сразу отметить ключ сверенным" };
            CheckBox autoEncryptBox = new CheckBox {
                Content = "Автошифровать обычный исходящий текст",
                IsChecked = E2EManager.GetPeerState(chat.PeerId)?.AutoEncryptText == true
            };
            StackPanel content = new StackPanel {
                Spacing = 8,
                Children = {
                    new TextBlock {
                        Text = "Вставь handshake-token собеседника. Если это первый импорт, Laney создаст ответный токен и скопирует его в clipboard.",
                        TextWrapping = TextWrapping.Wrap
                    },
                    tokenBox,
                    verifiedBox,
                    autoEncryptBox
                }
            };

            VKUIDialog dialog = new VKUIDialog("Импорт X25519 handshake", "Импортировать public token собеседника?", ["Отмена", "Импорт"], 2) {
                DialogContent = content
            };
            if (await dialog.ShowDialog<int>(session.ModalWindow) != 2) return;

            try {
                E2EHandshakeImportResult result = E2EManager.ImportX25519Handshake(chat.PeerId, tokenBox.Text, verifiedBox.IsChecked == true, autoEncryptBox.IsChecked == true);
                await CopyTextAsync(target, result.ResponseToken);
                chat.RefreshE2EState();
                await ShowTokenDialogAsync(session, "X25519 handshake импортирован", $"SAS {result.State.Sas}. Ответный токен скопирован в clipboard.", result.ResponseToken);
            } catch (Exception ex) {
                await new VKUIDialog("Handshake не импортирован", ex.Message, ["Понятно"], 1).ShowDialog(session.ModalWindow);
            }
        }

        public static async Task RotateX25519KeysAsync(VKSession session, ChatViewModel chat, Control target) {
            VKUIDialog confirm = new VKUIDialog("Повернуть X25519-ключи?", "Будет создан новый public handshake-token. Старую сверку придётся подтвердить заново, да, безопасность иногда душнит.", ["Повернуть", "Отмена"], 2);
            if (await confirm.ShowDialog<int>(session.ModalWindow) != 1) return;

            try {
                E2EHandshakeResult result = E2EManager.RotatePeerX25519Keys(chat.PeerId);
                await CopyTextAsync(target, result.Token);
                chat.RefreshE2EState();
                await ShowTokenDialogAsync(session, "X25519 ключи повернуты", "Новый handshake-token скопирован. Отправь его собеседнику и импортируй ответ.", result.Token);
            } catch (Exception ex) {
                await new VKUIDialog("Ротация не выполнена", ex.Message, ["Понятно"], 1).ShowDialog(session.ModalWindow);
            }
        }

        public static async Task ShowExportE2EBackupDialogAsync(VKSession session, ChatViewModel chat, Control target) {
            TextBox passphraseBox = new TextBox {
                PasswordChar = '*',
                PlaceholderText = "Backup-фраза",
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            StackPanel content = new StackPanel {
                Spacing = 8,
                Children = {
                    new TextBlock {
                        Text = "Backup содержит локальные E2E-ключи этого чата и шифруется фразой. Храни его у себя, в чат это лучше не кидать.",
                        TextWrapping = TextWrapping.Wrap
                    },
                    passphraseBox
                }
            };

            VKUIDialog dialog = new VKUIDialog("Trusted-device backup", "Экспортировать зашифрованный backup-token?", ["Отмена", "Экспорт"], 2) {
                DialogContent = content
            };
            if (await dialog.ShowDialog<int>(session.ModalWindow) != 2) return;

            try {
                E2ETrustedBackupResult result = E2EManager.ExportTrustedDeviceBackup(chat.PeerId, passphraseBox.Text);
                await CopyTextAsync(target, result.Token);
                chat.RefreshE2EState();
                await ShowTokenDialogAsync(session, "Backup создан", "Зашифрованный backup-token скопирован в clipboard.", result.Token);
            } catch (Exception ex) {
                await new VKUIDialog("Backup не создан", ex.Message, ["Понятно"], 1).ShowDialog(session.ModalWindow);
            }
        }

        public static async Task ShowImportE2EBackupDialogAsync(VKSession session, ChatViewModel chat) {
            TextBox tokenBox = new TextBox {
                PlaceholderText = "laney-e2e-backup:v1:...",
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 120,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            TextBox passphraseBox = new TextBox {
                PasswordChar = '*',
                PlaceholderText = "Backup-фраза",
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            StackPanel content = new StackPanel {
                Spacing = 8,
                Children = {
                    tokenBox,
                    passphraseBox
                }
            };

            VKUIDialog dialog = new VKUIDialog("Импорт trusted backup", "Восстановить E2E-ключи из backup-token?", ["Отмена", "Импорт"], 2) {
                DialogContent = content
            };
            if (await dialog.ShowDialog<int>(session.ModalWindow) != 2) return;

            try {
                long peerId = E2EManager.ImportTrustedDeviceBackup(tokenBox.Text, passphraseBox.Text);
                if (peerId == chat.PeerId) chat.RefreshE2EState();
                session.ShowNotification(new Notification("Backup импортирован", $"Ключи восстановлены для peer {peerId}.", NotificationType.Success));
            } catch (Exception ex) {
                await new VKUIDialog("Backup не импортирован", ex.Message, ["Понятно"], 1).ShowDialog(session.ModalWindow);
            }
        }

        private static async Task ShowTokenDialogAsync(VKSession session, string title, string subtitle, string token) {
            TextBox tokenBox = new TextBox {
                Text = token,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 120,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            VKUIDialog dialog = new VKUIDialog(title, subtitle, ["Понятно"], 1) {
                DialogContent = tokenBox
            };
            await dialog.ShowDialog(session.ModalWindow);
        }

        private static async Task CopyTextAsync(Control target, string text) {
            TopLevel topLevel = TopLevel.GetTopLevel(target);
            if (topLevel?.Clipboard != null) await topLevel.Clipboard.SetTextAsync(text);
        }

        private static async Task<string> TryGetClipboardTextAsync(Control target) {
            try {
                TopLevel topLevel = TopLevel.GetTopLevel(target);
                return topLevel?.Clipboard != null ? await topLevel.Clipboard.TryGetValueAsync(DataFormat.Text) : null;
            } catch {
                return null;
            }
        }

        public static async Task ShowE2EFingerprintDialogAsync(VKSession session, ChatViewModel chat, Control target) {
            E2EPeerState state = E2EManager.GetPeerState(chat.PeerId);
            if (state == null) return;

            string text = $"Профиль: {E2ESecurityProfileIds.GetTitle(state.ProfileId)}\nSAS: {state.Sas ?? "нет"}\nFingerprint:\n{state.Fingerprint ?? "нет"}";
            string qrText = $"laney-e2e-verify:v1|profile={E2ESecurityProfileIds.Normalize(state.ProfileId)}|sas={state.Sas}|fingerprint={state.Fingerprint}";
            StackPanel content = new StackPanel {
                Spacing = 10,
                Children = {
                    new QrCode {
                        Data = qrText,
                        Width = 196,
                        Height = 196,
                        Background = Brushes.White,
                        Foreground = Brushes.Black,
                        HorizontalAlignment = HorizontalAlignment.Center
                    },
                    new TextBlock {
                        Text = text,
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            };
            VKUIDialog dialog = new VKUIDialog("Laney E2E fingerprint", "Сверь QR, SAS или fingerprint с собеседником. В QR нет ключа, только проверочные отпечатки.", ["Скопировать", state.IsVerified ? "Снять сверку" : "Сверено", "Закрыть"], 3) {
                DialogContent = content
            };
            int result = await dialog.ShowDialog<int>(session.ModalWindow);
            if (result == 1) {
                TopLevel topLevel = TopLevel.GetTopLevel(target);
                if (topLevel?.Clipboard != null) await topLevel.Clipboard.SetTextAsync($"{text}\nQR: {qrText}");
                session.ShowNotification(new Notification("Laney E2E", "Fingerprint скопирован.", NotificationType.Success));
            } else if (result == 2) {
                E2EManager.SetPeerVerified(chat.PeerId, !state.IsVerified);
                chat.RefreshE2EState();
            }
        }

        public static async Task ResetE2EAsync(VKSession session, ChatViewModel chat) {
            VKUIDialog dialog = new VKUIDialog("Сбросить Laney E2E?", "Локальные ключи и статус этого чата будут удалены. VK, естественно, ничего не узнает.", ["Сбросить", "Отмена"], 2);
            int result = await dialog.ShowDialog<int>(session.ModalWindow);
            if (result != 1) return;

            E2EManager.DisablePeer(chat.PeerId);
            chat.RefreshE2EState();
            session.ShowNotification(new Notification("Laney E2E", "Ключи удалены локально.", NotificationType.Success));
        }

        public static void SetChatArchived(VKSession session, ChatViewModel chat, bool archived) {
            Settings.SetPeerArchived(chat.PeerId, archived);
            chat.RefreshLocalFolderState();

            string title = archived ? "Чат в архиве" : "Чат возвращен";
            string text = archived ? "VK не трогали, просто спрятали локально. Красота без драмы." : "Снова виден в обычных папках.";
            session.ShowNotification(new Notification(title, text, NotificationType.Success));
        }

        public static void ShowChatThemePicker(ChatViewModel chat, Control target) {
            ActionSheet ash = new ActionSheet();
            AddChatThemeSection(ash, "Фон", "Цвет подложки сообщений", () => ShowChatThemeOptionPicker(
                chat,
                target,
                "Фон чата",
                AppearanceManager.ChatBackgroundOptionsWithInherit,
                Settings.GetPeerLocalTheme,
                Settings.SetPeerLocalTheme));

            AddChatThemeSection(ash, "Цвет", "Цвет исходящих bubble", () => ShowChatThemeOptionPicker(
                chat,
                target,
                "Цвет сообщений",
                AppearanceManager.BubbleColorOptionsWithInherit,
                Settings.GetPeerLocalBubbleColor,
                Settings.SetPeerLocalBubbleColor));

            AddChatThemeSection(ash, "Плотность", "Компактнее или свободнее список сообщений", () => ShowChatThemeOptionPicker(
                chat,
                target,
                "Плотность чата",
                AppearanceManager.ChatDensityOptionsWithInherit,
                Settings.GetPeerLocalDensity,
                Settings.SetPeerLocalDensity));

            AddChatThemeSection(ash, "Шрифт", "Размер текста в этом чате", () => ShowChatThemeOptionPicker(
                chat,
                target,
                "Шрифт чата",
                AppearanceManager.ChatFontOptionsWithInherit,
                Settings.GetPeerLocalFont,
                Settings.SetPeerLocalFont));

            AddChatThemeSection(ash, "Bubble style", "Форма пузырей сообщений", () => ShowChatThemeOptionPicker(
                chat,
                target,
                "Bubble style",
                AppearanceManager.BubbleStyleOptionsWithInherit,
                Settings.GetPeerLocalBubbleStyle,
                Settings.SetPeerLocalBubbleStyle));

            ash.ShowAt(target, true);
        }

        private static void AddChatThemeSection(ActionSheet ash, string header, string subtitle, System.Action click) {
            ActionSheetItem item = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20ArticleOutline },
                Header = header,
                Subtitle = subtitle
            };
            item.Click += (a, b) => click();
            ash.Items.Add(item);
        }

        private static void ShowChatThemeOptionPicker(ChatViewModel chat, Control target, string title, IReadOnlyList<AppearanceOption> options, Func<long, string> getter, Action<long, string> setter) {
            ActionSheet ash = new ActionSheet();
            string currentId = getter(chat.PeerId);
            if (String.IsNullOrWhiteSpace(currentId)) currentId = AppearanceManager.InheritChatBackgroundId;

            foreach (AppearanceOption option in options) {
                ActionSheetItem item = new ActionSheetItem {
                    Before = new VKIcon {
                        Id = option.Id == currentId ? VKIconNames.Icon20Check : VKIconNames.Icon20ArticleOutline
                    },
                    Header = option.Title,
                    Subtitle = option.Subtitle
                };

                item.Click += (a, b) => {
                    setter(chat.PeerId, option.Id);
                    VKSession.GetByDataContext(target)?.ShowNotification(new Notification(title, "Сохранено локально. VK не беспокоили.", NotificationType.Success));
                };
                ash.Items.Add(item);
            }

            ash.ShowAt(target, true);
        }

        public static void TryClearChat(VKSession session, long peerId, System.Action onSuccess = null) {
            new System.Action(async () => {
                VKUIDialog dlg = new VKUIDialog(Assets.i18n.Resources.chat_clear_modal_title, Assets.i18n.Resources.chat_clear_modal_text, [Assets.i18n.Resources.yes, Assets.i18n.Resources.no], 2);
                if (await dlg.ShowDialog<int>(session.ModalWindow) == 1) {
                    if (DemoMode.IsEnabled) return;
                    try {
                        var response = await session.API.Messages.DeleteConversationAsync(session.GroupId, peerId);
                        onSuccess?.Invoke(); // TODO: Snackbar
                    } catch (Exception ex) {
                        await ExceptionHelper.ShowErrorDialogAsync(session.ModalWindow, ex, true);
                    }
                }
            })();
        }

        public static void TryLeaveChat(VKSession session, long peerId, System.Action onSuccess = null) {
            new System.Action(async () => {
                VKUIDialog dlg = new VKUIDialog(Assets.i18n.Resources.chat_leave_modal_title, Assets.i18n.Resources.chat_leave_modal_text, [Assets.i18n.Resources.yes, Assets.i18n.Resources.no], 2);
                if (await dlg.ShowDialog<int>(session.ModalWindow) == 1) {
                    if (DemoMode.IsEnabled) return;
                    try {
                        var response = await session.API.Messages.RemoveChatUserAsync(session.GroupId, peerId - 2000000000, session.Id);
                        onSuccess?.Invoke(); // TODO: Snackbar
                    } catch (Exception ex) {
                        await ExceptionHelper.ShowErrorDialogAsync(session.ModalWindow, ex, true);
                    }
                }
            })();
        }

        public static void ReturnToChat(VKSession session, long peerId, System.Action onSuccess = null) {
            if (DemoMode.IsEnabled) return;
            new System.Action(async () => {
                try {
                    var response = await session.API.Messages.AddChatUserAsync(session.GroupId, peerId - 2000000000, session.Id);
                    onSuccess?.Invoke(); // TODO: Snackbar
                } catch (Exception ex) {
                    await ExceptionHelper.ShowErrorDialogAsync(session.ModalWindow, ex, true);
                }
            })();
        }

        #endregion


        #region For message

        public static void ShowForMessage(MessageViewModel message, ChatViewModel chat, Control target) {
            if (chat.PeerId != message.PeerId) return;
            ActionSheet ash = new ActionSheet();

            int totalReactions = 0;
            if (message.Reactions != null) foreach (var reaction in message.Reactions) {
                    totalReactions += reaction.Count;
                }

            ActionSheetItem debug = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20BugOutline },
                Header = $"ID: {message.GlobalId}, CMID: {message.ConversationMessageId}"
            };
            ActionSheetItem readers = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20ViewOutline },
                Header = Assets.i18n.Resources.loading
            };
            ActionSheetItem reactions = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20Stars },
                Header = Localizer.GetDeclensionFormatted(totalReactions, "reactions")
            };
            ActionSheetItem localReaction = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20Stars },
                Header = T("cm_local_reaction"),
                Subtitle = T("cm_local_reaction_subtitle"),
            };
            ActionSheetItem quickReactionsSettings = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20GearOutline },
                Header = T("cm_quick_reactions"),
                Subtitle = T("cm_quick_reactions_subtitle"),
            };
            ActionSheetItem gift = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20GiftOutline },
                Header = T("cm_send_gift_to_author"),
                Subtitle = T("cm_send_gift_subtitle"),
            };
            ActionSheetItem reply = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20ReplyOutline },
                Header = Assets.i18n.Resources.reply
            };
            ActionSheetItem repriv = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20ReplyOutline },
                Header = Assets.i18n.Resources.reply_privately
            };
            ActionSheetItem forward = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20ShareOutline },
                Header = Assets.i18n.Resources.forward
            };
            ActionSheetItem forwardCleanHere = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20MessageArrowRightOutline },
                Header = T("cm_clean_copy"),
                Subtitle = T("cm_clean_copy_subtitle"),
            };
            ActionSheetItem copyPlainText = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20ArticleOutline },
                Header = T("cm_copy_plain_text"),
                Subtitle = T("cm_copy_plain_text_subtitle"),
            };
            ActionSheetItem copyMarkdownText = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20ArticleOutline },
                Header = T("cm_copy_markdown_text"),
                Subtitle = T("cm_copy_markdown_text_subtitle"),
            };
            ActionSheetItem copyMessageLink = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20LinkCircleOutline },
                Header = T("cm_copy_message_link"),
            };
            ActionSheetItem copyAllLinks = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20LinkCircleOutline },
                Header = T("cm_copy_all_links"),
            };
            ActionSheetItem forwardHere = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20ShareOutline },
                Header = Assets.i18n.Resources.forward_here
            };
            ActionSheetItem mark = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20FavoriteOutline },
                Header = Assets.i18n.Resources.mark_important,
            };
            ActionSheetItem unmark = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20UnfavoriteOutline },
                Header = Assets.i18n.Resources.unmark_important,
            };
            ActionSheetItem pin = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20PinOutline },
                Header = Assets.i18n.Resources.pin,
            };
            ActionSheetItem unpin = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20PinSlashOutline },
                Header = Assets.i18n.Resources.unpin,
            };
            ActionSheetItem edit = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20WriteOutline },
                Header = Assets.i18n.Resources.edit,
            };
            ActionSheetItem todo = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20ListBulletOutline },
                Header = T("cm_todo"),
            };
            ActionSheetItem remind = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20RecentOutline },
                Header = T("cm_remind"),
            };
            ActionSheetItem softDelete = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20ViewOutline },
                Header = T("cm_soft_delete"),
            };
            ActionSheetItem muteSender = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20NotificationSlashOutline },
                Header = T("cm_mute_sender"),
                Subtitle = T("cm_mute_sender_subtitle"),
            };
            ActionSheetItem shadowBan = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20BlockOutline },
                Header = T("cm_shadowban_sender"),
                Subtitle = T("cm_shadowban_sender_subtitle"),
            };
            ActionSheetItem deleteDryRun = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20ArticleOutline },
                Header = T("cm_delete_dry_run"),
            };
            ActionSheetItem selfDestruct = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20RecentOutline },
                Header = T("cm_self_destruct"),
            };
            ActionSheetItem inspector = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20ArticleOutline },
                Header = T("cm_message_inspector"),
                Subtitle = T("cm_message_inspector_subtitle"),
            };
            ActionSheetItem spam = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20ReportOutline },
                Header = Assets.i18n.Resources.mark_spam,
            };
            ActionSheetItem delete = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20DeleteOutline },
                Header = Assets.i18n.Resources.delete,
            };
            spam.Classes.Add("Destructive");
            delete.Classes.Add("Destructive");

            // Conditions

            var session = VKSession.GetByDataContext(target);

            bool canReplyPrivately = chat.PeerType == PeerType.Chat && message.SenderId.IsUser() && message.SenderId != session.Id;
            if (message.SenderId.IsUser()) {
                User sender = CacheManager.GetUser(message.SenderId);
                if (sender != null) canReplyPrivately = sender.CanWritePrivateMessage == 1;
            }

            bool canShowReaders = message.IsOutgoing && chat.ChatSettings?.State == UserStateInChat.In;
            bool canShowReactions = totalReactions > 0 && chat.PeerType == PeerType.Chat;
            bool isAdminInChat = false;
            if (chat.ChatSettings?.AdminIDs != null) isAdminInChat = chat.ChatSettings.AdminIDs.Contains(session.Id);

            bool canPin = chat.ChatSettings != null ? chat.ChatSettings.ACL.CanChangePin : false;
            bool isMessagePinned = chat.PinnedMessage != null
                ? chat.PinnedMessage.ConversationMessageId == message.ConversationMessageId : false;

            bool canEdit = message.CanEdit(session.Id);
            bool canForward = chat.ChatSettings == null ? true : chat.ChatSettings.ACL.CanForwardMessages;
            bool canForwardCleanHere = canForward && chat.CanWrite.Allowed;
            bool hasTextToCopy = !String.IsNullOrWhiteSpace(message.DisplayText);
            bool hasLinksToCopy = GetMessageLinks(message).Count > 0;
            bool canMuteSender = chat.PeerType == PeerType.Chat && message.SenderId != session.Id && !chat.IsSenderMutedLocally(message.SenderId);
            bool canShadowBan = message.SenderId != session.Id && !chat.IsSenderShadowBanned(message.SenderId);
            bool canSendGift = message.SenderId.IsUser() && message.SenderId != session.Id;

            bool canDeleteWithoutConfirmation = message.SenderId != session.Id || chat.PeerId == session.Id;
            bool canDeleteForAll = message.SenderId == session.Id && message.PeerId != message.SenderId
                && message.SentTime > DateTime.Now.AddDays(-1);

            bool isCall = message.Attachments.Any(a => a.Type == AttachmentType.Call || a.Type == AttachmentType.GroupCallInProgress);
            bool canShowContextMenu = message.Action == null && !message.IsExpired && !isCall;
            bool canSendReaction = canShowContextMenu && message.UIType != MessageUIType.Gift;
            if (chat.ChatSettings?.ACL.CanSendReactions == false) canSendReaction = false;

            // Actions

            readers.Click += async (a, b) => {
                WhoReadMessage wrm = new WhoReadMessage(session, message.PeerId, message.ConversationMessageId);
                await wrm.ShowDialog(session.ModalWindow);
            };

            reactions.Click += async (a, b) => {
                ReactedMembers rmw = new ReactedMembers(session, message.PeerId, message.ConversationMessageId);
                await rmw.ShowDialog(session.ModalWindow);
            };
            localReaction.Click += async (a, b) => await ShowLocalReactionDialogAsync(session, message);
            quickReactionsSettings.Click += async (a, b) => await ShowQuickReactionsDialogAsync(session, message);
            gift.Click += async (a, b) => await OpenGiftFlowAsync(message.SenderId);

            reply.Click += (a, b) => chat.Composer.AddReply(message);

            repriv.Click += (a, b) => {
                if (DemoMode.IsEnabled) return;
                session.GoToChat(message.SenderId);
                session.CurrentOpenedChat.Composer.AddForwardedMessages(chat.PeerId, new List<MessageViewModel> { message });
            };

            forward.Click += (a, b) => session.Share(chat.PeerId, new List<MessageViewModel> { message });

            forwardHere.Click += (a, b) => {
                chat.Composer.Clear();
                chat.Composer.AddForwardedMessages(chat.PeerId, new List<MessageViewModel> { message });
            };
            forwardCleanHere.Click += (a, b) => AddCleanCopyToComposer(session, chat, new List<MessageViewModel> { message });
            copyPlainText.Click += async (a, b) => await CopyMessagePlainTextAsync(session, target, message);
            copyMarkdownText.Click += async (a, b) => await CopyMessageMarkdownTextAsync(session, target, message);
            copyMessageLink.Click += async (a, b) => await CopyMessageLinkAsync(session, target, message);
            copyAllLinks.Click += async (a, b) => await CopyMessageLinksAsync(session, target, message);

            edit.Click += (a, b) => chat.Composer.StartEditing(message);

            mark.Click += async (a, b) => {
                if (DemoMode.IsEnabled) return;
                try {
                    var response = await session.API.Messages.MarkAsImportantAsync(message.PeerId, new List<int> { message.ConversationMessageId }, true);
                } catch (Exception ex) {
                    await ExceptionHelper.ShowErrorDialogAsync(session.ModalWindow, ex, true);
                }
            };

            unmark.Click += async (a, b) => {
                if (DemoMode.IsEnabled) return;
                try {
                    var response = await session.API.Messages.MarkAsImportantAsync(message.PeerId, new List<int> { message.ConversationMessageId }, false);
                } catch (Exception ex) {
                    await ExceptionHelper.ShowErrorDialogAsync(session.ModalWindow, ex, true);
                }
            };

            pin.Click += async (a, b) => {
                if (DemoMode.IsEnabled) return;
                try {
                    var response = await session.API.Messages.PinAsync(session.GroupId, chat.PeerId, message.ConversationMessageId);
                } catch (Exception ex) {
                    await ExceptionHelper.ShowErrorDialogAsync(session.ModalWindow, ex, true);
                }
            };

            unpin.Click += async (a, b) => {
                if (DemoMode.IsEnabled) return;
                try {
                    var response = await session.API.Messages.UnpinAsync(session.GroupId, chat.PeerId);
                } catch (Exception ex) {
                    await ExceptionHelper.ShowErrorDialogAsync(session.ModalWindow, ex, true);
                }
            };

            spam.Click += async (a, b) => await DeleteMessagesAsync(session, chat.PeerId, new List<int> { message.ConversationMessageId }, false, true);
            delete.Click += async (a, b) => await TryDeleteMessagesAsync(canDeleteWithoutConfirmation, session, chat.PeerId, new List<MessageViewModel> { message }, canDeleteForAll);
            todo.Click += async (a, b) => await AddMessagesToQuickActionAsync(session, chat.PeerId, new List<MessageViewModel> { message }, true);
            remind.Click += async (a, b) => await AddMessagesToQuickActionAsync(session, chat.PeerId, new List<MessageViewModel> { message }, false);
            softDelete.Click += async (a, b) => await SoftDeleteMessagesLocallyAsync(session, chat, new List<MessageViewModel> { message });
            muteSender.Click += async (a, b) => await MuteSenderLocallyAsync(session, chat, message.SenderId);
            shadowBan.Click += async (a, b) => await ShadowBanSenderLocallyAsync(session, chat, message.SenderId);
            deleteDryRun.Click += async (a, b) => await ShowDeleteDryRunAsync(session, chat.PeerId, new List<MessageViewModel> { message }, canDeleteForAll);
            selfDestruct.Click += async (a, b) => await ScheduleSelfDestructAsync(session, chat, new List<MessageViewModel> { message });
            inspector.Click += async (a, b) => await ShowMessageInspectorAsync(chat, message, TopLevel.GetTopLevel(target) as Window);

            // ¯\_(ツ)_/¯

            if (Settings.ShowDevItemsInContextMenus) {
                ash.Items.Add(ActionSheetItem.Section(T("cm_section_debug")));
                ash.Items.Add(debug);
            }
            if (canShowContextMenu) {
                ash.Items.Add(ActionSheetItem.Section(T("cm_section_activity")));
                if (canShowReaders) ash.Items.Add(readers);
                if (canShowReactions) ash.Items.Add(reactions);
                ash.Items.Add(localReaction);
                if (canSendReaction) ash.Items.Add(quickReactionsSettings);

                ash.Items.Add(ActionSheetItem.Section(T("cm_section_reply_forward")));
                if (chat.CanWrite.Allowed) ash.Items.Add(reply);
                if (canReplyPrivately && chat.PeerType == PeerType.Chat) ash.Items.Add(repriv);
                if (canForward) ash.Items.Add(forward);
                if (canForwardCleanHere) ash.Items.Add(forwardCleanHere);
                // if (chat.CanWrite.Allowed) ash.Items.Add(forwardHere);

                ash.Items.Add(ActionSheetItem.Section(T("cm_section_copying")));
                if (hasTextToCopy) ash.Items.Add(copyPlainText);
                if (hasTextToCopy) ash.Items.Add(copyMarkdownText);
                ash.Items.Add(copyMessageLink);
                if (hasLinksToCopy) ash.Items.Add(copyAllLinks);

                ash.Items.Add(ActionSheetItem.Section(T("cm_section_organization")));
                if (canSendGift) ash.Items.Add(gift);
                if (!session.IsGroup && !message.IsImportant) ash.Items.Add(mark);
                if (!session.IsGroup && message.IsImportant) ash.Items.Add(unmark);
                if (canPin && !isMessagePinned) ash.Items.Add(pin);
                if (canPin && isMessagePinned) ash.Items.Add(unpin);
                if (canEdit) ash.Items.Add(edit);
                ash.Items.Add(todo);
                ash.Items.Add(remind);

                ash.Items.Add(ActionSheetItem.Section(T("cm_section_local_rules")));
                ash.Items.Add(softDelete);
                if (canMuteSender) ash.Items.Add(muteSender);
                if (canShadowBan) ash.Items.Add(shadowBan);

                ash.Items.Add(ActionSheetItem.Section(T("cm_section_diagnostics")));
                ash.Items.Add(inspector);

                ash.Items.Add(ActionSheetItem.Section(T("cm_section_danger_zone")));
                ash.Items.Add(deleteDryRun);
                ash.Items.Add(selfDestruct);
                if (message.SenderId != session.Id) ash.Items.Add(spam);
                ash.Items.Add(delete);
            }
            if (ash.Items.Count > 0) {
                if (canSendReaction) ash.Above = new ReactionsPicker(message.PeerId, message.ConversationMessageId, message.SelectedReactionId, target, ash);
                ash.ShowAt(target, true);
            }

            // Show message readers count
            if (canShowReaders) {
                new System.Action(async () => {
                    try {
                        var wrm = await session.API.Messages.GetMessageReadPeersAsync(session.GroupId, message.PeerId, message.ConversationMessageId, 0, 3, new List<string> { "photos_50", "sex" });
                        if (wrm.TotalCount > 0) {
                            readers.Header = Localizer.GetDeclensionFormatted(wrm.TotalCount, "views");
                        } else {
                            readers.Header = Assets.i18n.Resources.views_empty;
                        }
                    } catch (Exception ex) {
                        Log.Error(ex, $"Cannot check who read message to display in context menu! {message.PeerId}_{message.ConversationMessageId}");
                        // readers.Header = Assets.i18n.Resources.error;
                        readers.Header = Assets.i18n.Resources.views;
                    }
                })();
            }
        }

        public static void ShowForMultipleMessages(List<MessageViewModel> messages, ChatViewModel chat, Control target) {
            ActionSheet ash = new ActionSheet {
                Placement = PlacementMode.LeftEdgeAlignedTop,
                IsSearchEnabled = true
            };

            ActionSheetItem mark = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20FavoriteOutline },
                Header = Assets.i18n.Resources.mark_important,
            };
            ActionSheetItem unmark = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20UnfavoriteOutline },
                Header = Assets.i18n.Resources.unmark_important,
            };
            ActionSheetItem spam = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20ReportOutline },
                Header = Assets.i18n.Resources.mark_spam,
            };
            ActionSheetItem delete = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20DeleteOutline },
                Header = Assets.i18n.Resources.delete,
            };
            ActionSheetItem todo = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20ListBulletOutline },
                Header = T("cm_todo"),
            };
            ActionSheetItem remind = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20RecentOutline },
                Header = T("cm_remind"),
            };
            ActionSheetItem softDelete = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20ViewOutline },
                Header = T("cm_soft_delete"),
            };
            ActionSheetItem forwardCleanHere = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20MessageArrowRightOutline },
                Header = T("cm_clean_copy"),
                Subtitle = T("cm_clean_copy_subtitle"),
            };
            ActionSheetItem deleteDryRun = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20ArticleOutline },
                Header = T("cm_delete_dry_run"),
            };
            ActionSheetItem selfDestruct = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20RecentOutline },
                Header = T("cm_self_destruct"),
            };
            ActionSheetItem bulkCleanup = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20ListBulletOutline },
                Header = T("cm_bulk_cleanup"),
            };
            spam.Classes.Add("Destructive");
            delete.Classes.Add("Destructive");
            bulkCleanup.Classes.Add("Destructive");

            // Conditions

            var session = VKSession.GetByDataContext(target);
            bool isAllMessagesMarkedAsImportant = messages.Where(m => m.IsImportant).Count() == messages.Count;
            bool canDeleteForAll = messages.All(m => m.SenderId == session.Id && m.PeerId != m.SenderId
                && m.SentTime > DateTime.Now.AddDays(-1));
            bool canDeleteWithoutConfirmation = messages.All(m => m.SenderId != session.Id || chat.PeerId == session.Id);
            bool spamAvailable = messages.All(m => m.SenderId != session.Id);
            bool canForwardCleanHere = chat.CanWrite.Allowed && (chat.ChatSettings == null || chat.ChatSettings.ACL.CanForwardMessages);

            // Actions

            mark.Click += async (a, b) => {
                if (DemoMode.IsEnabled) return;
                try {
                    var response = await session.API.Messages.MarkAsImportantAsync(chat.PeerId, messages.Select(m => m.ConversationMessageId).ToList(), true);
                } catch (Exception ex) {
                    await ExceptionHelper.ShowErrorDialogAsync(session.ModalWindow, ex, true);
                }
            };
            unmark.Click += async (a, b) => {
                if (DemoMode.IsEnabled) return;
                try {
                    var response = await session.API.Messages.MarkAsImportantAsync(chat.PeerId, messages.Select(m => m.ConversationMessageId).ToList(), false);
                } catch (Exception ex) {
                    await ExceptionHelper.ShowErrorDialogAsync(session.ModalWindow, ex, true);
                }
            };

            spam.Click += async (a, b) => await DeleteMessagesAsync(session, chat.PeerId, messages.Select(m => m.ConversationMessageId).ToList(), false, true);
            delete.Click += async (a, b) => await TryDeleteMessagesAsync(canDeleteWithoutConfirmation, session, chat.PeerId, messages, canDeleteForAll);
            todo.Click += async (a, b) => await AddMessagesToQuickActionAsync(session, chat.PeerId, messages, true);
            remind.Click += async (a, b) => await AddMessagesToQuickActionAsync(session, chat.PeerId, messages, false);
            softDelete.Click += async (a, b) => await SoftDeleteMessagesLocallyAsync(session, chat, messages);
            forwardCleanHere.Click += (a, b) => AddCleanCopyToComposer(session, chat, messages);
            deleteDryRun.Click += async (a, b) => await ShowDeleteDryRunAsync(session, chat.PeerId, messages, canDeleteForAll);
            selfDestruct.Click += async (a, b) => await ScheduleSelfDestructAsync(session, chat, messages);
            bulkCleanup.Click += async (a, b) => await ShowBulkCleanupAsync(session, chat, messages);

            // ¯\_(ツ)_/¯

            ash.Items.Add(ActionSheetItem.Section(T("cm_section_quick_actions")));
            ash.Items.Add(todo);
            ash.Items.Add(remind);
            if (canForwardCleanHere) ash.Items.Add(forwardCleanHere);

            ash.Items.Add(ActionSheetItem.Section(T("cm_section_local")));
            ash.Items.Add(softDelete);

            ash.Items.Add(ActionSheetItem.Section(T("cm_section_danger_zone")));
            if (spamAvailable) ash.Items.Add(spam);
            ash.Items.Add(deleteDryRun);
            ash.Items.Add(selfDestruct);
            ash.Items.Add(bulkCleanup);
            ash.Items.Add(delete);

                ash.Items.Add(ActionSheetItem.Section(T("cm_section_organization")));
            if (!isAllMessagesMarkedAsImportant) ash.Items.Add(mark);
            if (isAllMessagesMarkedAsImportant) ash.Items.Add(unmark);

            if (ash.Items.Count > 0) ash.ShowAt(target, true);
        }

        public static async Task<bool> PinMessageAsync(VKSession session, long peerId, int cmid) {
            try {
                var response = await session.API.Messages.PinAsync(session.GroupId, peerId, cmid);
                return true;
            } catch (Exception ex) {
                await ExceptionHelper.ShowErrorDialogAsync(session.ModalWindow, ex, true);
            }
            return false;
        }

        private static async Task ShowMessageInspectorAsync(ChatViewModel chat, MessageViewModel message, Window owner) {
            if (chat == null || message == null) return;

            bool redact = Settings.StreamerMode;
            StringBuilder text = new StringBuilder();
            text.AppendLine($"Peer: {message.PeerId}; cmid: {message.ConversationMessageId}; global: {message.GlobalId}; random: {message.RandomId}");
            text.AppendLine($"Sender: {(redact ? "redacted" : $"{message.SenderName} ({message.SenderId})")}; outgoing={message.IsOutgoing}; state={message.State}; ui={message.UIType}");
            text.AppendLine($"Sent: {message.SentTime:O}; edited: {message.EditTime?.ToString("O") ?? "none"}; important={message.IsImportant}; expired={message.IsExpired}; ttl={message.TTL}");
            text.AppendLine($"Read: incoming-read-cmid={chat.InRead}; outgoing-read-cmid={chat.OutRead}; read-by-me={message.ConversationMessageId <= chat.InRead}; read-by-peer={message.ConversationMessageId <= chat.OutRead}");
            text.AppendLine($"E2E: encrypted={message.IsE2EEncrypted}; failed={message.IsE2EDecryptionFailed}; unavailable={message.IsUnavailable}; more-nested={message.HasMoreNestedMessage}");
            text.AppendLine();
            text.AppendLine($"Text length: {message.Text?.Length ?? 0}");
            text.AppendLine(redact ? "[message text redacted]" : TrimInspectorValue(message.Text, 1400));
            text.AppendLine();
            text.AppendLine($"Payload: {BuildPayloadInspectorText(message.Payload, redact)}");
            text.AppendLine($"Reactions: selected={message.SelectedReactionId}; count={message.Reactions?.Count ?? 0}; summary={BuildReactionSummary(message)}");
            text.AppendLine($"Reply: {(message.ReplyMessage == null ? "none" : $"cmid={message.ReplyMessage.ConversationMessageId}; from={message.ReplyMessage.FromId}")}");
            text.AppendLine($"Forwarded: {message.ForwardedMessages?.Count ?? 0}");
            text.AppendLine();
            text.AppendLine($"Attachments: {message.Attachments?.Count ?? 0}");
            foreach (string line in BuildAttachmentInspectorLines(message)) {
                text.AppendLine(line);
            }

            TextBox textBox = new TextBox {
                Text = text.ToString(),
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinWidth = 620,
                MinHeight = 420,
                MaxHeight = 520
            };
            textBox.Classes.Add("Mono");

            VKUIDialog dialog = new VKUIDialog("Инспектор сообщения", "Payload, вложения, reactions и read/debug info.", ["Закрыть"], 1) {
                DialogContent = textBox
            };
            await dialog.ShowDialog<int>(owner);
        }

        private static string BuildPayloadInspectorText(string payload, bool redact) {
            if (String.IsNullOrWhiteSpace(payload)) return "none";
            if (redact) return $"redacted; length={payload.Length}";
            return TrimInspectorValue(payload, 1200);
        }

        private static string BuildReactionSummary(MessageViewModel message) {
            if (message?.Reactions == null || message.Reactions.Count == 0) return "none";
            return String.Join(", ", message.Reactions.Select(r => $"{r.ReactionId}:{r.Count}").Take(12));
        }

        private static IEnumerable<string> BuildAttachmentInspectorLines(MessageViewModel message) {
            if (message?.Attachments == null) yield break;

            int index = 0;
            foreach (Attachment attachment in message.Attachments) {
                if (attachment == null) continue;

                yield return $"  [{index}] type={attachment.Type}; object={attachment}";
                index++;
            }
        }

        private static string TrimInspectorValue(string value, int limit) {
            if (String.IsNullOrWhiteSpace(value)) return "(empty)";
            string normalized = value.Replace("\r", " ").Trim();
            return normalized.Length <= limit ? normalized : $"{normalized[..limit]}...";
        }

        public static async Task<bool> UnpinMessageAsync(VKSession session, long peerId) {
            try {
                var response = await session.API.Messages.UnpinAsync(session.GroupId, peerId);
                return true;
            } catch (Exception ex) {
                await ExceptionHelper.ShowErrorDialogAsync(session.ModalWindow, ex, true);
            }
            return false;
        }

        private static async Task ScheduleSelfDestructAsync(VKSession session, ChatViewModel chat, List<MessageViewModel> messages) {
            List<int> ids = messages?
                .Where(m => m != null && m.ConversationMessageId > 0)
                .Select(m => m.ConversationMessageId)
                .Distinct()
                .ToList() ?? new List<int>();
            if (ids.Count == 0) return;

            CheckBox bestEffortDelete = new CheckBox {
                Content = "Также попробовать удалить через VK API"
            };

            VKUIDialog dialog = new VKUIDialog(
                "Self-destruct",
                $"Выбрано сообщений: {ids.Count}. Laney локально скроет их по таймеру. VK-delete — только best-effort, без магии.",
                ["Через 1 минуту", "Через 1 час", "Отмена"],
                2) {
                DialogContent = bestEffortDelete
            };

            int result = await dialog.ShowDialog<int>(session.ModalWindow);
            if (result == 0 || result == 3) return;

            DateTimeOffset hideAt = result == 1
                ? DateTimeOffset.Now.AddMinutes(1)
                : DateTimeOffset.Now.AddHours(1);

            Settings.ScheduleSelfDestructMessages(chat.PeerId, ids, hideAt, bestEffortDelete.IsChecked == true);
            chat.RefreshSelfDestructState();
            new System.Action(async () => await ApplySelfDestructAfterDelayAsync(chat, hideAt))();

            session.ShowNotification(new Notification(
                "Self-destruct включен",
                $"Сообщений: {ids.Count}; срок: {hideAt:dd.MM HH:mm}",
                NotificationType.Success));
        }

        private static async Task ApplySelfDestructAfterDelayAsync(ChatViewModel chat, DateTimeOffset hideAt) {
            TimeSpan delay = hideAt - DateTimeOffset.Now;
            if (delay > TimeSpan.Zero) await Task.Delay(delay);
            await Dispatcher.UIThread.InvokeAsync(async () => await chat.ApplyExpiredSelfDestructMessagesAsync());
        }

        private static async Task ShowBulkCleanupAsync(VKSession session, ChatViewModel chat, List<MessageViewModel> sourceMessages) {
            if (sourceMessages == null || sourceMessages.Count == 0) return;

            ComboBox periodBox = CreateBulkCleanupComboBox(BulkCleanupPeriodOptions);
            ComboBox attachmentBox = CreateBulkCleanupComboBox(BulkCleanupAttachmentOptions);
            ComboBox senderBox = CreateBulkCleanupComboBox(BulkCleanupSenderOptions);
            TextBox regexBox = new TextBox {
                PlaceholderText = "regex по тексту, можно пусто",
                MinWidth = 360
            };
            CheckBox scrubBeforeDelete = new CheckBox {
                Content = Localizer.Get("msg_scrub_before_delete"),
                IsEnabled = sourceMessages.Any(m => CanScrubBeforeDelete(m, session.Id))
            };
            CheckBox deleteForAll = new CheckBox {
                Content = $"{Assets.i18n.Resources.delete_for_all} для подходящих сообщений"
            };

            StackPanel content = new StackPanel {
                Spacing = 8,
                MinWidth = 360
            };
            content.Children.Add(CreateBulkCleanupField("Период", periodBox));
            content.Children.Add(CreateBulkCleanupField("Тип", attachmentBox));
            content.Children.Add(CreateBulkCleanupField("Отправитель", senderBox));
            content.Children.Add(CreateBulkCleanupField("Текст", regexBox));
            content.Children.Add(scrubBeforeDelete);
            content.Children.Add(deleteForAll);

            VKUIDialog dialog = new VKUIDialog(
                "Массовая очистка",
                $"Источник: текущий выбор ({sourceMessages.Count}) в чате {chat.PeerId}. Фильтр не лезет в незагруженную историю VK.",
                ["Dry-run", "Удалить", "Отмена"],
                1) {
                DialogContent = content
            };

            int result = await dialog.ShowDialog<int>(session.ModalWindow);
            if (result == 0 || result == 3) return;

            BulkCleanupResult filterResult;
            try {
                filterResult = BuildBulkCleanupSelection(sourceMessages, session.Id, periodBox.SelectedIndex, attachmentBox.SelectedIndex, senderBox.SelectedIndex, regexBox.Text);
            } catch (ArgumentException ex) {
                VKUIDialog errorDialog = new VKUIDialog("Regex не взлетел", ex.Message, ["Понятно"], 1);
                await errorDialog.ShowDialog<int>(session.ModalWindow);
                return;
            }

            if (filterResult.Messages.Count == 0) {
                VKUIDialog emptyDialog = new VKUIDialog("Нечего чистить", $"{filterResult.Summary}\n\nПод фильтр не попало ни одного сообщения.", ["Понятно"], 1);
                await emptyDialog.ShowDialog<int>(session.ModalWindow);
                return;
            }

            bool forAll = deleteForAll.IsChecked == true;
            bool canDeleteForAll = CanDeleteForAll(filterResult.Messages, session.Id);
            if (forAll && !canDeleteForAll) {
                VKUIDialog unavailableDialog = new VKUIDialog(
                    "delete_for_all недоступен",
                    $"{filterResult.Summary}\n\nВ итоговом наборе есть чужие или слишком старые сообщения. VK такое не удалит у всех, и притворяться не будем.",
                    ["Понятно"],
                    1);
                await unavailableDialog.ShowDialog<int>(session.ModalWindow);
                return;
            }

            if (result == 1) {
                await ShowDeleteDryRunAsync(session, chat.PeerId, filterResult.Messages, forAll && canDeleteForAll, filterResult.Summary);
                return;
            }

            VKUIDialog confirmDialog = new VKUIDialog(
                "Удалить отфильтрованное?",
                $"{filterResult.Summary}\n\nVK delete: {filterResult.Messages.Count}. Scrub перед удалением: {(scrubBeforeDelete.IsChecked == true ? "включен" : "выключен")}. Удалить у всех: {(forAll ? "да" : "нет")}.",
                ["Удалить", "Отмена"],
                2);
            int confirmResult = await confirmDialog.ShowDialog<int>(session.ModalWindow);
            if (confirmResult != 1) return;

            await DeleteMessagesAsync(
                session,
                chat.PeerId,
                filterResult.Messages.Select(m => m.ConversationMessageId).ToList(),
                forAll,
                false,
                filterResult.Messages,
                scrubBeforeDelete.IsChecked == true);
        }

        private static ComboBox CreateBulkCleanupComboBox(string[] items) {
            return new ComboBox {
                ItemsSource = items,
                SelectedIndex = 0,
                MinWidth = 360
            };
        }

        private static StackPanel CreateBulkCleanupField(string label, Control control) {
            TextBlock text = new TextBlock {
                Text = label
            };
            text.Classes.Add("Caption1");

            StackPanel panel = new StackPanel {
                Spacing = 4
            };
            panel.Children.Add(text);
            panel.Children.Add(control);
            return panel;
        }

        private static BulkCleanupResult BuildBulkCleanupSelection(List<MessageViewModel> sourceMessages, long sessionId, int periodIndex, int attachmentIndex, int senderIndex, string regexPattern) {
            IEnumerable<MessageViewModel> query = sourceMessages.Where(m => m.ConversationMessageId > 0);
            DateTime now = DateTime.Now;

            if (periodIndex == 1) {
                query = query.Where(m => m.SentTime >= now.Date);
            } else if (periodIndex == 2) {
                query = query.Where(m => m.SentTime >= now.AddDays(-7));
            } else if (periodIndex == 3) {
                query = query.Where(m => m.SentTime >= now.AddDays(-30));
            }

            if (senderIndex == 1) {
                query = query.Where(m => m.SenderId == sessionId);
            } else if (senderIndex == 2) {
                query = query.Where(m => m.SenderId != sessionId);
            }

            query = query.Where(m => MatchesBulkAttachmentFilter(m, attachmentIndex));

            string pattern = regexPattern?.Trim();
            if (!String.IsNullOrWhiteSpace(pattern)) {
                Regex regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
                query = query.Where(m => regex.IsMatch(m.Text ?? String.Empty));
            }

            List<MessageViewModel> result = query
                .GroupBy(m => m.ConversationMessageId)
                .Select(g => g.First())
                .OrderBy(m => m.ConversationMessageId)
                .ToList();

            return new BulkCleanupResult {
                Messages = result,
                Summary = BuildBulkCleanupSummary(sourceMessages.Count, result.Count, periodIndex, attachmentIndex, senderIndex, pattern)
            };
        }

        private static string BuildBulkCleanupSummary(int sourceCount, int resultCount, int periodIndex, int attachmentIndex, int senderIndex, string regexPattern) {
            string regex = String.IsNullOrWhiteSpace(regexPattern) ? "нет" : TrimDialogText(regexPattern, 120);

            StringBuilder summary = new StringBuilder();
            summary.AppendLine($"Источник: текущий выбор, {sourceCount}");
            summary.AppendLine($"Итоговый набор: {resultCount}");
            summary.AppendLine($"Период: {BulkCleanupPeriodOptions[Math.Clamp(periodIndex, 0, BulkCleanupPeriodOptions.Length - 1)]}");
            summary.AppendLine($"Тип: {BulkCleanupAttachmentOptions[Math.Clamp(attachmentIndex, 0, BulkCleanupAttachmentOptions.Length - 1)]}");
            summary.AppendLine($"Отправитель: {BulkCleanupSenderOptions[Math.Clamp(senderIndex, 0, BulkCleanupSenderOptions.Length - 1)]}");
            summary.AppendLine($"Regex: {regex}");
            return summary.ToString().TrimEnd();
        }

        private static string TrimDialogText(string value, int maxLength) {
            if (String.IsNullOrEmpty(value) || value.Length <= maxLength) return value;
            return $"{value.Substring(0, maxLength - 1)}…";
        }

        private static bool MatchesBulkAttachmentFilter(MessageViewModel message, int attachmentIndex) {
            if (attachmentIndex == 0) return true;
            if (attachmentIndex == 7) return message.Attachments.Count == 0 && !String.IsNullOrWhiteSpace(message.Text);

            return message.Attachments.Any(a => {
                if (attachmentIndex == 1) return a.Type == AttachmentType.Photo || a.Type == AttachmentType.Video;
                if (attachmentIndex == 2) return a.Type == AttachmentType.Document;
                if (attachmentIndex == 3) return a.Type == AttachmentType.Link;
                if (attachmentIndex == 4) return a.Type == AttachmentType.AudioMessage;
                if (attachmentIndex == 5) return a.Type == AttachmentType.Graffiti;
                if (attachmentIndex == 6) return a.Type == AttachmentType.Sticker || a.Type == AttachmentType.UGCSticker;
                return true;
            });
        }

        private static bool CanDeleteForAll(List<MessageViewModel> messages, long sessionId) {
            return messages.Count > 0 && messages.All(m => m.SenderId == sessionId && m.PeerId != m.SenderId && m.SentTime > DateTime.Now.AddDays(-1));
        }

        private static async Task SoftDeleteMessagesLocallyAsync(VKSession session, ChatViewModel chat, List<MessageViewModel> messages) {
            List<MessageViewModel> hiddenMessages = chat.HideMessagesLocally(messages);
            if (hiddenMessages.Count == 0) return;

            string title = hiddenMessages.Count == 1 ? "Сообщение скрыто локально" : $"Скрыто локально: {hiddenMessages.Count}";
            VKUIDialog undoDialog = new VKUIDialog(title, "В VK ничего не удалялось. Вернуть сообщения в текущий чат?", ["Вернуть", "Оставить"], 2);
            int result = await undoDialog.ShowDialog<int>(session.ModalWindow);
            if (result == 1) chat.RestoreLocallyHiddenMessages(hiddenMessages);
        }

        private static void AddCleanCopyToComposer(VKSession session, ChatViewModel chat, List<MessageViewModel> messages) {
            chat.Composer.Clear();
            CleanCopyResult result = chat.Composer.AddMessagesAsCleanCopies(messages);
            if (result.CopiedItems == 0) {
                session.ShowNotification(new Notification("Копия без автора", "В этих сообщениях нечего перенести штатно.", NotificationType.Warning));
                return;
            }

            NotificationType type = result.HasSkippedItems ? NotificationType.Warning : NotificationType.Success;
            session.ShowNotification(new Notification("Копия без автора", result.BuildSummary(), type));
        }

        private static async Task CopyMessagePlainTextAsync(VKSession session, Control target, MessageViewModel message) {
            string text = TextParser.GetParsedText(message?.DisplayText);
            if (String.IsNullOrWhiteSpace(text)) return;

            await CopyTextAsync(target, text);
            session.ShowNotification(new Notification("Скопировано", "Текст без форматирования.", NotificationType.Success));
        }

        private static async Task CopyMessageMarkdownTextAsync(VKSession session, Control target, MessageViewModel message) {
            string text = message?.DisplayText ?? String.Empty;
            if (String.IsNullOrWhiteSpace(text)) return;

            await CopyTextAsync(target, text);
            session.ShowNotification(new Notification("Скопировано", "Markdown-текст сообщения.", NotificationType.Success));
        }

        private static async Task CopyMessageLinkAsync(VKSession session, Control target, MessageViewModel message) {
            string link = BuildMessageLink(message);
            if (String.IsNullOrWhiteSpace(link)) return;

            await CopyTextAsync(target, link);
            session.ShowNotification(new Notification("Скопировано", "Ссылка на сообщение.", NotificationType.Success));
        }

        private static async Task CopyMessageLinksAsync(VKSession session, Control target, MessageViewModel message) {
            IReadOnlyList<string> links = GetMessageLinks(message);
            if (links.Count == 0) {
                session.ShowNotification(new Notification("Ссылок нет", "В сообщении нечего копировать.", NotificationType.Warning));
                return;
            }

            await CopyTextAsync(target, String.Join(Environment.NewLine, links));
            session.ShowNotification(new Notification("Ссылки скопированы", $"Штук: {links.Count}.", NotificationType.Success));
        }

        private static IReadOnlyList<string> GetMessageLinks(MessageViewModel message) {
            if (message == null) return Array.Empty<string>();

            List<string> links = TextParser.GetLinks(message.DisplayText).ToList();
            foreach (Attachment attachment in message.Attachments) {
                if (attachment?.Type != AttachmentType.Link || attachment.Link == null) continue;

                if (!String.IsNullOrWhiteSpace(attachment.Link.Url)) links.Add(attachment.Link.Url);
                if (!String.IsNullOrWhiteSpace(attachment.Link.Button?.Action?.Url)) links.Add(attachment.Link.Button.Action.Url);
            }

            return links
                .Where(link => !String.IsNullOrWhiteSpace(link))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string BuildMessageLink(MessageViewModel message) {
            if (message == null || message.PeerId == 0 || message.ConversationMessageId <= 0) return String.Empty;

            string selector = message.PeerId.IsChat()
                ? $"c{message.PeerId - 2000000000}"
                : message.PeerId.ToString();
            return $"https://vk.com/im?sel={selector}&msgid={message.ConversationMessageId}";
        }

        private static async Task OpenGiftFlowAsync(long userId) {
            if (!userId.IsUser()) return;

            await Launcher.LaunchUrl($"https://vk.com/gifts?to={userId}");
        }

        private static async Task ShowLocalReactionDialogAsync(VKSession session, MessageViewModel message) {
            if (message == null) return;

            string currentReaction = Settings.GetLocalMessageReaction(message.PeerId, message.ConversationMessageId);
            TextBox reactionBox = new TextBox {
                Text = String.IsNullOrWhiteSpace(currentReaction) ? "👍" : currentReaction,
                PlaceholderText = "emoji или короткая метка",
                MaxLength = 16,
                MinWidth = 320
            };

            VKUIDialog dialog = new VKUIDialog(
                "Laney-only реакция",
                "Видно только в Laney на этом устройстве. В VK ничего не отправляется.",
                ["Сохранить", "Убрать", "Отмена"],
                1) {
                DialogContent = reactionBox
            };

            int result = await dialog.ShowDialog<int>(session.ModalWindow);
            if (result == 0 || result == 3) return;

            string reaction = result == 2 ? null : reactionBox.Text;
            Settings.SetLocalMessageReaction(message.PeerId, message.ConversationMessageId, reaction);

            string subtitle = result == 2 || String.IsNullOrWhiteSpace(reaction)
                ? "Локальная реакция убрана."
                : "Локальная реакция сохранена.";
            session.ShowNotification(new Notification("Laney-only реакция", subtitle, NotificationType.Success));
        }

        private static async Task ShowQuickReactionsDialogAsync(VKSession session, MessageViewModel message) {
            if (message == null) return;

            List<int> available = CacheManager.AvailableReactions ?? new List<int>();
            IReadOnlyList<int> current = Settings.GetPeerQuickReactionIds(message.PeerId);
            string currentText = current.Count > 0
                ? String.Join(", ", current)
                : String.Join(", ", available.Take(6));
            string availableText = available.Count == 0
                ? "Список доступных реакций ещё не загружен."
                : $"Доступные id: {String.Join(", ", available.Take(32))}";

            TextBox reactionBox = new TextBox {
                Text = currentText,
                PlaceholderText = "id через запятую, например: 1, 2, 3",
                MinWidth = 360
            };

            VKUIDialog dialog = new VKUIDialog(
                "Быстрые реакции",
                $"{availableText}. Пусто = сброс на дефолт, без танцев.",
                ["Сохранить", "Сбросить", "Отмена"],
                1) {
                DialogContent = reactionBox
            };

            int result = await dialog.ShowDialog<int>(session.ModalWindow);
            if (result == 0 || result == 3) return;

            if (result == 2 || String.IsNullOrWhiteSpace(reactionBox.Text)) {
                Settings.SetPeerQuickReactionIds(message.PeerId, Array.Empty<int>());
                session.ShowNotification(new Notification("Быстрые реакции", "Набор сброшен на дефолт для этого чата.", NotificationType.Success));
                return;
            }

            List<int> picked = ParseReactionIds(reactionBox.Text, available);
            if (picked.Count == 0) {
                await new VKUIDialog("Быстрые реакции не сохранены", "Не нашёл ни одного валидного id реакции. Цифры, брат, нужны цифры.", ["Понятно"], 1).ShowDialog(session.ModalWindow);
                return;
            }

            Settings.SetPeerQuickReactionIds(message.PeerId, picked);
            session.ShowNotification(new Notification("Быстрые реакции", $"Сохранено: {String.Join(", ", picked)}", NotificationType.Success));
        }

        private static List<int> ParseReactionIds(string text, IReadOnlyCollection<int> available) {
            HashSet<int> availableSet = available != null && available.Count > 0 ? available.ToHashSet() : null;
            return Regex.Matches(text ?? String.Empty, @"\d+")
                .Select(match => Int32.TryParse(match.Value, out int id) ? id : 0)
                .Where(id => id > 0 && (availableSet == null || availableSet.Contains(id)))
                .Distinct()
                .Take(8)
                .ToList();
        }

        private static async Task ShadowBanSenderLocallyAsync(VKSession session, ChatViewModel chat, long senderId) {
            string title = GetCachedPeerTitle(senderId);
            VKUIDialog dialog = new VKUIDialog(
                "Теневой бан",
                $"Скрыть сообщения автора «{title}» только в этом чате? VK ничего не узнает.",
                ["Забанить", "Отмена"],
                2);

            if (await dialog.ShowDialog<int>(session.ModalWindow) != 1) return;

            int removed = chat.ShadowBanSenderLocally(senderId);
            session.ShowNotification(new Notification("Теневой бан", $"Скрыто сообщений: {removed}", NotificationType.Success));
        }

        private static async Task MuteSenderLocallyAsync(VKSession session, ChatViewModel chat, long senderId) {
            string title = GetCachedPeerTitle(senderId);
            VKUIDialog dialog = new VKUIDialog(
                "Локальный mute",
                $"Отключить уведомления от автора «{title}» только в этом чате? Сообщения останутся в ленте.",
                ["Замьютить", "Отмена"],
                2);

            if (await dialog.ShowDialog<int>(session.ModalWindow) != 1) return;

            chat.MuteSenderLocally(senderId);
            session.ShowNotification(new Notification("Локальный mute", "Уведомления автора отключены в этом чате", NotificationType.Success));
        }

        private static string GetCachedPeerTitle(long peerId) {
            if (peerId.IsUser()) {
                User user = CacheManager.GetUser(peerId);
                if (user != null) return user.FullName;
            }

            if (peerId.IsGroup()) {
                ELOR.VKAPILib.Objects.Group group = CacheManager.GetGroup(-peerId);
                if (group != null) return group.Name;
            }

            return peerId.ToString();
        }

        private static async Task AddMessagesToQuickActionAsync(VKSession session, long peerId, List<MessageViewModel> messages, bool todo) {
            string text = BuildQuickActionText(messages);
            if (todo) {
                await QuickActionStore.AddTodoAsync(peerId, text);
                session.ShowNotification(new Notification("Todo сохранён", "Лежит локально в quick-actions/todo.md", NotificationType.Success));
            } else {
                string due = await ChooseReminderDueAsync(session);
                if (due == null) return;

                await QuickActionStore.AddReminderAsync(peerId, text, due);
                string suffix = String.IsNullOrWhiteSpace(due) ? "Лежит локально в quick-actions/reminders.md" : $"Срок: {due}";
                session.ShowNotification(new Notification("Reminder сохранён", suffix, NotificationType.Success));
            }
        }

        private static async Task<string> ChooseReminderDueAsync(VKSession session) {
            VKUIDialog dialog = new VKUIDialog(
                "Напоминание",
                "Когда поднять это сообщение?",
                ["Через 2 часа", "Завтра", "Без даты"],
                1);

            int result = await dialog.ShowDialog<int>(session.ModalWindow);
            if (result == 0) return null;
            if (result == 1) return FormatReminderDue(DateTimeOffset.Now.AddHours(2));
            if (result == 2) return FormatReminderDue(BuildTomorrowMorning());
            return String.Empty;
        }

        private static DateTimeOffset BuildTomorrowMorning() {
            DateTimeOffset now = DateTimeOffset.Now;
            DateTime tomorrow = now.Date.AddDays(1).AddHours(9);
            return new DateTimeOffset(tomorrow, now.Offset);
        }

        private static string FormatReminderDue(DateTimeOffset due) {
            return due.ToString("yyyy-MM-ddTHH:mmzzz");
        }

        private static string BuildQuickActionText(List<MessageViewModel> messages) {
            if (messages == null || messages.Count == 0) return "Сообщение";
            if (messages.Count == 1) return BuildQuickActionText(messages[0]);

            return String.Join(" | ", messages
                .OrderBy(m => m.ConversationMessageId)
                .Take(20)
                .Select(BuildQuickActionText));
        }

        private static string BuildQuickActionText(MessageViewModel message) {
            string text = !String.IsNullOrWhiteSpace(message.Text) ? message.Text : message.ToString();
            if (String.IsNullOrWhiteSpace(text)) text = "Сообщение без текста";
            return $"cmid:{message.ConversationMessageId} {text}";
        }

        private static async Task ShowDeleteDryRunAsync(VKSession session, long peerId, List<MessageViewModel> messages, bool canDeleteForAll, string scope = null) {
            int total = messages.Count;
            int own = messages.Count(m => m.SenderId == session.Id);
            int foreign = total - own;
            int scrubAvailable = messages.Count(m => CanScrubBeforeDelete(m, session.Id));
            int scrubSkipped = total - scrubAvailable;
            int expiredOrService = messages.Count(m => m.Action != null || m.IsExpired);

            StringBuilder report = new StringBuilder();
            if (!String.IsNullOrWhiteSpace(scope)) {
                report.AppendLine(scope);
                report.AppendLine();
            }
            report.AppendLine($"Выбрано: {total}");
            report.AppendLine($"VK delete: будет отправлено {total} id в messages.delete");
            report.AppendLine($"Свои сообщения: {own}");
            report.AppendLine($"Чужие сообщения: {foreign}");
            report.AppendLine($"Scrub before delete: доступно {scrubAvailable}, пропуск {scrubSkipped}");
            report.AppendLine($"Удалить у всех: {(canDeleteForAll ? "доступно для текущего выбора" : "недоступно")}");
            if (!canDeleteForAll) report.AppendLine("Причина: delete_for_all работает только для своих сообщений в рамках окна VK API.");
            if (expiredOrService > 0) report.AppendLine($"Сервисные/истекшие сообщения: {expiredOrService}, scrub для них не применяется.");
            report.AppendLine();
            report.AppendLine("Dry-run: VK API не вызван, история не изменена.");

            VKUIDialog dialog = new VKUIDialog("Симулятор удаления", report.ToString(), ["Понятно"], 1);
            await dialog.ShowDialog<int>(session.ModalWindow);

            Log.Information(
                "Delete dry-run. Peer={PeerId}; total={Total}; own={Own}; foreign={Foreign}; scrub={Scrub}; canDeleteForAll={CanDeleteForAll}; scope={Scope}",
                peerId,
                total,
                own,
                foreign,
                scrubAvailable,
                canDeleteForAll,
                scope);
        }

        private static async Task TryDeleteMessagesAsync(bool withoutConfirmation, VKSession session, long peerId, List<MessageViewModel> messages, bool canDeleteForAll) {
            List<int> ids = messages.Select(m => m.ConversationMessageId).ToList();
            if (withoutConfirmation) {
                await DeleteMessagesAsync(session, peerId, ids, false, false);
            } else {
                string title, subtitle = String.Empty;

                if (ids.Count == 1) {
                    title = canDeleteForAll ? Assets.i18n.Resources.msg_delete_dialog_single_question : Assets.i18n.Resources.msg_delete_dialog_single_title;
                } else {
                    title = canDeleteForAll ? Assets.i18n.Resources.msg_delete_dialog_multi_question : Assets.i18n.Resources.msg_delete_dialog_multi_title;
                }
                subtitle = canDeleteForAll ? String.Empty : Localizer.GetDeclensionFormatted(ids.Count, "msg_delete_dialog_text");

                VKUIDialog dlg = new VKUIDialog(title, subtitle, [Assets.i18n.Resources.yes, Assets.i18n.Resources.no], 2);
                CheckBox forAll = new CheckBox { Content = Assets.i18n.Resources.delete_for_all };
                CheckBox scrubBeforeDelete = new CheckBox { Content = Localizer.Get("msg_scrub_before_delete") };
                bool canScrubBeforeDelete = messages.Any(m => CanScrubBeforeDelete(m, session.Id));

                if (canDeleteForAll || canScrubBeforeDelete) {
                    StackPanel options = new StackPanel();
                    if (canDeleteForAll) options.Children.Add(forAll);
                    if (canScrubBeforeDelete) options.Children.Add(scrubBeforeDelete);
                    dlg.DialogContent = options;
                }
                int result = await dlg.ShowDialog<int>(session.ModalWindow);
                if (result == 1) await DeleteMessagesAsync(session, peerId, ids, forAll.IsChecked == true, false, messages, scrubBeforeDelete.IsChecked == true);
            }
        }

        private static async Task DeleteMessagesAsync(VKSession session, long peerId, List<int> ids, bool forAll, bool spam, List<MessageViewModel> messages = null, bool scrubBeforeDelete = false) {
            if (DemoMode.IsEnabled) return;
            try {
                if (scrubBeforeDelete && messages != null) {
                    await ScrubMessagesBeforeDeleteAsync(session, peerId, messages);
                }

                var response = await session.API.Messages.DeleteAsync(session.GroupId, peerId, ids, spam, forAll);
                int count = response.Where(r => r.Response == 1).Count();

                string type = spam ? "spam" : "deleted";
                string multi = count > 1 ? "multi" : "single";
                Log.Information($"Messages deleted. Type = {type}; count = {count}; snackbar text = {Localizer.Get($"message_{type}_{multi}")}");
                session.ShowNotification(new Notification(Localizer.Get($"message_{type}_{multi}"), null, NotificationType.Success));
            } catch (Exception ex) {
                await ExceptionHelper.ShowErrorDialogAsync(session.ModalWindow, ex, true);
            }
        }

        private static bool CanScrubBeforeDelete(MessageViewModel message, long sessionId) {
            return message.CanEdit(sessionId) && !String.IsNullOrWhiteSpace(message.Text);
        }

        private static async Task ScrubMessagesBeforeDeleteAsync(VKSession session, long peerId, List<MessageViewModel> messages) {
            string replacement = Localizer.Get("msg_scrubbed_text");
            int scrubbed = 0;

            foreach (MessageViewModel message in messages.Where(m => CanScrubBeforeDelete(m, session.Id))) {
                try {
                    await session.API.Messages.EditAsync(session.GroupId, peerId, message.ConversationMessageId,
                        replacement, 0, 0, null, false, false, true);
                    scrubbed++;
                } catch (Exception ex) {
                    Log.Warning(ex, "Cannot scrub message before delete. Peer={PeerId}; CMID={CMID}", peerId, message.ConversationMessageId);
                }
            }

            Log.Information("Scrub before delete finished. Peer={PeerId}; scrubbed={Scrubbed}; requested={Requested}", peerId, scrubbed, messages.Count);
        }

        private sealed class BulkCleanupResult {
            public List<MessageViewModel> Messages { get; set; }
            public string Summary { get; set; }
        }

        #endregion
    }
}
