using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Threading;
using ELOR.Laney.Controls;
using ELOR.Laney.Controls.Attachments;
using ELOR.Laney.Core;
using ELOR.Laney.Core.Network;
using ELOR.Laney.Core.Localization;
using ELOR.Laney.Extensions;
using ELOR.Laney.Helpers;
using ELOR.Laney.ViewModels;
using ELOR.Laney.ViewModels.Controls;
using ELOR.Laney.ViewModels.Modals;
using ELOR.Laney.Views.Modals;
using ELOR.Laney.Views.SettingsCategories;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VKUI.Controls;
using PeerType = ELOR.VKAPILib.Objects.PeerType;
using UserStateInChat = ELOR.VKAPILib.Objects.UserStateInChat;

namespace ELOR.Laney.Views {
    public sealed partial class MainWindow : Window {
        public VKSession Session => DataContext as VKSession;
        private DateTimeOffset lastUserActivity = DateTimeOffset.Now;
        private DispatcherTimer autoStatusTimer;
        private DispatcherTimer autoLockTimer;
        private DispatcherTimer memoryPressureTimer;
        private BackgroundSearchIndexer backgroundSearchIndexer;
        private BackgroundHistoryStatisticsIndexer backgroundHistoryStatisticsIndexer;
        private bool lastAutoStatusWasIdle;
        private bool isChatListSplitterDragging;
        private const int AccountRailColumnIndex = 0;
        private const int ChatListColumnIndex = 1;
        private const int ChatColumnIndex = 3;
        private const double ChatListMinWidth = 280d;
        private const double ChatListMaxWidth = 560d;

        public MainWindow() {
            InitializeComponent();
            Log.Information($"{nameof(MainWindow)} initialized.");

            Opened += MainWindow_Opened;
            Unloaded += MainWindow_Unloaded;
            Activated += MainWindow_Activated;
            DataContextChanged += MainWindow_DataContextChanged;
            VKSession.SessionsChanged += VKSession_SessionsChanged;
            KeyDown += MainWindow_KeyDown;
            EffectiveViewportChanged += MainWindow_EffectiveViewportChanged;
            ChatView.BackButtonClick += ChatView_BackButtonClick;
            SecondaryChatView.BackButtonClick += ChatView_BackButtonClick;
            PointerMoved += MainWindow_UserActivity;
            PointerPressed += MainWindow_UserActivity;
            PointerWheelChanged += MainWindow_UserActivity;

            // Window size, position & state
            bool isMaximized = Settings.Get(Settings.WIN_MAXIMIZED, false);
            if (!isMaximized) WindowState = WindowState.Normal;

            Width = Settings.Get<double>(Settings.WIN_SIZE_W, 800);
            Height = Settings.Get<double>(Settings.WIN_SIZE_H, 600);
            int wx = Settings.Get(Settings.WIN_POS_X, 128);
            int wy = Settings.Get(Settings.WIN_POS_Y, 32);
            if (wx < 0) wx = 128;
            if (wy < 0) wy = 32;
            Position = new PixelPoint(wx, wy);

            // Audio player
            AudioPlayerViewModel.InstancesChanged += AudioPlayerViewModel_InstancesChanged;

            //
            RendererDiagnostics.DebugOverlays = Settings.ShowFPS ? RendererDebugOverlays.Fps : RendererDebugOverlays.None;
            RAMInfoOverlay.IsVisible = Settings.ShowRAMUsage;
            ToggleRAMInfoOverlay();
            Settings.SettingChanged += Settings_SettingChanged;
            Separator.Cursor = new Cursor(StandardCursorType.SizeWestEast);
            ApplyChatListWidth(Settings.ChatListWidth);
            RefreshAccountRail();
            ApplyWindowIcon();
            ToggleAutoStatusTimer();
            ToggleAutoLockTimer();
            StartMemoryPressureTimer();

            PanicLockTitle.Text = Localizer.Get("panic_lock_title");
            PanicLockUnlockButton.Content = Localizer.Get("panic_lock_unlock");
        }

        private void AudioPlayerViewModel_InstancesChanged(object sender, EventArgs e) {
            MainMAP.DataContext = AudioPlayerViewModel.MainInstance;
            MainMAPC.IsVisible = AudioPlayerViewModel.MainInstance != null;
        }

        private void MainWindow_DataContextChanged(object sender, EventArgs e) {
            AppearanceManager.ApplyAccountAppearanceSettings(Session);
            RefreshAccountRail();
        }

        private void VKSession_SessionsChanged(object sender, EventArgs e) {
            Dispatcher.UIThread.Post(RefreshAccountRail);
        }

        private void MainWindow_Unloaded(object sender, Avalonia.Interactivity.RoutedEventArgs e) {
            Opened -= MainWindow_Opened;
            Unloaded -= MainWindow_Unloaded;
            Activated -= MainWindow_Activated;
            DataContextChanged -= MainWindow_DataContextChanged;
            VKSession.SessionsChanged -= VKSession_SessionsChanged;
            KeyDown -= MainWindow_KeyDown;
            EffectiveViewportChanged -= MainWindow_EffectiveViewportChanged;
            ChatView.BackButtonClick -= ChatView_BackButtonClick;
            SecondaryChatView.BackButtonClick -= ChatView_BackButtonClick;
            PointerMoved -= MainWindow_UserActivity;
            PointerPressed -= MainWindow_UserActivity;
            PointerWheelChanged -= MainWindow_UserActivity;
            Settings.SettingChanged -= Settings_SettingChanged;
            backgroundSearchIndexer?.Dispose();
            backgroundHistoryStatisticsIndexer?.Dispose();
            if (autoStatusTimer != null) autoStatusTimer.Stop();
            if (autoLockTimer != null) autoLockTimer.Stop();
            if (memoryPressureTimer != null) memoryPressureTimer.Stop();
        }

        private async void MainWindow_Opened(object? sender, EventArgs e) {
            if (Settings.FirstRunOnboardingDone || Session == null || App.HasCmdLineValue("perf-open-settings")) return;

            await Task.Delay(450);
            await ShowFirstRunOnboardingAsync();
        }

        private async void MainWindow_KeyDown(object? sender, KeyEventArgs e) {
            lastUserActivity = DateTimeOffset.Now;
            if (lastAutoStatusWasIdle) UpdateAutoStatusUI();
            if (e.Handled || Session == null) return;

            if (MatchesShortcut(e, Settings.KeymapPanicLock)) {
                e.Handled = true;
                await PanicLockAsync();
                return;
            }

            if (OwnedWindows.Count > 0) return;

            if (MatchesShortcut(e, Settings.KeymapCommandPalette)) {
                e.Handled = true;
                await ShowCommandPaletteAsync();
                return;
            }

            if (MatchesShortcut(e, Settings.KeymapGlobalSearch)) {
                if (!DemoMode.IsEnabled) await LeftNav.NavigationRouter.NavigateToAsync(new SearchView());
                e.Handled = true;
                return;
            }

            if (MatchesShortcut(e, Settings.KeymapChatSearch)) {
                ChatView.OpenSearchInChat();
                e.Handled = true;
                return;
            }

            if (MatchesShortcut(e, Settings.KeymapFocusComposer)) {
                ChatView.FocusComposer();
                e.Handled = true;
                return;
            }

            if (MatchesShortcut(e, Settings.KeymapAttachments)) {
                ChatView.OpenAttachmentPicker();
                e.Handled = true;
                return;
            }

            if (MatchesShortcut(e, Settings.KeymapStickers)) {
                ChatView.OpenStickerPicker();
                e.Handled = true;
                return;
            }

            if (MatchesShortcut(e, Settings.KeymapSettings)) {
                SettingsWindow settings = new SettingsWindow();
                await settings.ShowDialog(this);
                e.Handled = true;
                return;
            }

            if (MatchesShortcut(e, Settings.KeymapBack) && !IsTextInputFocused()) {
                if (Session.CurrentOpenedChat != null) {
                    Session.GoToChat(0);
                    e.Handled = true;
                }
            }
        }

        private bool IsTextInputFocused() {
            return FocusManager?.GetFocusedElement() is TextBox;
        }

        private static bool MatchesShortcut(KeyEventArgs e, string shortcuts) {
            if (String.IsNullOrWhiteSpace(shortcuts)) return false;

            foreach (string rawShortcut in shortcuts.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
                if (TryParseShortcut(rawShortcut, out Key key, out KeyModifiers modifiers)
                    && e.Key == key
                    && NormalizeShortcutModifiers(e.KeyModifiers) == modifiers) {
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseShortcut(string shortcut, out Key key, out KeyModifiers modifiers) {
            key = Key.None;
            modifiers = KeyModifiers.None;
            if (String.IsNullOrWhiteSpace(shortcut)) return false;

            foreach (string rawPart in shortcut.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
                string part = rawPart.Trim();
                string normalized = part.ToLowerInvariant();
                if (normalized == "ctrl" || normalized == "control") {
                    modifiers |= KeyModifiers.Control;
                    continue;
                }

                if (normalized == "shift") {
                    modifiers |= KeyModifiers.Shift;
                    continue;
                }

                if (normalized == "alt") {
                    modifiers |= KeyModifiers.Alt;
                    continue;
                }

                if (!TryParseShortcutKey(part, out key)) return false;
            }

            return key != Key.None;
        }

        private static bool TryParseShortcutKey(string value, out Key key) {
            key = value.Trim().ToLowerInvariant() switch {
                "," or "comma" => Key.OemComma,
                "." or "period" => Key.OemPeriod,
                "esc" or "escape" => Key.Escape,
                "enter" or "return" => Key.Enter,
                "left" or "arrowleft" => Key.Left,
                "right" or "arrowright" => Key.Right,
                "up" or "arrowup" => Key.Up,
                "down" or "arrowdown" => Key.Down,
                "space" => Key.Space,
                _ => Key.None
            };

            return key != Key.None || Enum.TryParse(value, true, out key);
        }

        private static KeyModifiers NormalizeShortcutModifiers(KeyModifiers modifiers) {
            return modifiers & (KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt);
        }

        private async Task ShowCommandPaletteAsync() {
            CommandPalette palette = new CommandPalette(BuildCommandPaletteActions());
            CommandPaletteAction action = await palette.ShowDialog<CommandPaletteAction>(this);
            if (action != null) await action.ExecuteAsync();
        }

        private IReadOnlyList<CommandPaletteAction> BuildCommandPaletteActions() {
            List<CommandPaletteAction> actions = new List<CommandPaletteAction> {
                new CommandPaletteAction(VKIconNames.Icon28SearchOutline, "Глобальный поиск", "Искать по чатам и сообщениям", "search find global чат сообщение", async () => {
                    if (!DemoMode.IsEnabled) await LeftNav.NavigationRouter.NavigateToAsync(new SearchView());
                }),
                new CommandPaletteAction(VKIconNames.Icon28ArticleOutline, "Новости VK", "Лента постов с фильтрами и локальным вырезанием промо", "news feed новости лента vk реклама фильтр", async () => {
                    await LeftNav.NavigationRouter.NavigateToAsync(new NewsFeedView());
                }),
                new CommandPaletteAction(VKIconNames.Icon28MusicOutline, "Музыка VK", "Треки, очередь, EQ, скачивание, VK status и локальный scrobble", "music audio vk музыка аудио трек очередь эквалайзер скачать status scrobble", async () => {
                    await LeftNav.NavigationRouter.NavigateToAsync(new MusicView());
                }),
                new CommandPaletteAction(VKIconNames.Icon28SearchOutline, "Поиск в текущем чате", "Открыть поиск внутри выбранного диалога", "search current chat найти здесь", () => {
                    ChatView.OpenSearchInChat();
                    return Task.CompletedTask;
                }),
                new CommandPaletteAction(VKIconNames.Icon28ArticleOutline, "Сводка дня", "Кто писал, что важно, что просрочено и где ждут ответа", "digest summary day сводка сегодня важно просрочено", ShowDailyDigestAsync),
                new CommandPaletteAction(VKIconNames.Icon28CheckCircleOutline, "Извлечь задачи", "Найти кандидаты в todo по загруженным сообщениям", "todo task extract задачи дела автоизвлечение", ShowExtractedTasksAsync),
                new CommandPaletteAction(VKIconNames.Icon28PictureOutline, "OCR загруженных картинок", "Локально распознать текст на фото из загруженных сообщений", "ocr image text распознать текст картинки фото", ShowLocalOcrAsync),
                new CommandPaletteAction(VKIconNames.Icon28MusicOutline, "История прослушивания", "Последние треки, подкасты и голосовые с позицией", "audio music history слушал треки голосовые подкасты", ShowAudioPlaybackHistoryAsync),
                new CommandPaletteAction(VKIconNames.Icon24Story, "Истории VK", "Нативный просмотр stories.get прямо внутри Laney", "stories story история сторис вк", ShowStoriesHubAsync),
                new CommandPaletteAction(VKIconNames.Icon28UserOutgoingOutline, "Автостатус", "Laney-only режим: занят, работаю, играю, сплю, не трогать", "status автостатус busy work sleep dnd", ShowAutoStatusDialogAsync),
                new CommandPaletteAction(VKIconNames.Icon28WriteSquareOutline, "Фокус на сообщение", "Поставить курсор в composer", "composer input сообщение написать", () => {
                    ChatView.FocusComposer();
                    return Task.CompletedTask;
                }),
                new CommandPaletteAction(VKIconNames.Icon28DocumentOutline, "Прикрепить файл", "Открыть меню вложений текущего чата", "attach file photo video document вложение файл фото", () => {
                    ChatView.OpenAttachmentPicker();
                    return Task.CompletedTask;
                }),
                new CommandPaletteAction(VKIconNames.Icon28SmileOutline, "Стикеры и emoji", "Открыть picker стикеров", "stickers emoji стикер эмодзи", () => {
                    ChatView.OpenStickerPicker();
                    return Task.CompletedTask;
                }),
                new CommandPaletteAction(VKIconNames.Icon28SettingsOutline, "Настройки", "Открыть настройки Laney", "settings prefs параметры", async () => {
                    SettingsWindow settings = new SettingsWindow();
                    await settings.ShowDialog(this);
                }),
                new CommandPaletteAction(VKIconNames.Icon28PaletteOutline, "Внешний вид", "Открыть настройки тем, акцента и фона", "appearance theme accent фон тема цвет", async () => {
                    SettingsWindow settings = new SettingsWindow(typeof(Appearance));
                    await settings.ShowDialog(this);
                }),
                new CommandPaletteAction(VKIconNames.Icon28SettingsOutline, "Производительность", "RAM, кэш изображений, анимации и лимиты медиа", "performance budget ram memory cache animations производительность память", async () => {
                    SettingsWindow settings = new SettingsWindow(typeof(Memory));
                    await settings.ShowDialog(this);
                }),
                new CommandPaletteAction(VKIconNames.Icon28SettingsOutline, "Правила автоматизации", "Настроить кто/где/что/когда -> mute/tag/download/remind/archive", "rules automation rule engine mute tag download remind archive правила автоматизация", async () => {
                    SettingsWindow settings = new SettingsWindow(typeof(Automation));
                    await settings.ShowDialog(this);
                }),
                new CommandPaletteAction(VKIconNames.Icon28PrivacyOutline, "Приватность", "Режим стримера, panic-замок, auto-lock и очистка буфера", "privacy streamer panic lock clipboard приватность", async () => {
                    SettingsWindow settings = new SettingsWindow(typeof(Privacy));
                    await settings.ShowDialog(this);
                }),
                new CommandPaletteAction(VKIconNames.Icon28SettingsOutline, "Диагностика сети", "Long Poll, API debug, health-check и экономия трафика", "network diagnostics api debug health longpoll сеть", async () => {
                    SettingsWindow settings = new SettingsWindow(typeof(Network));
                    await settings.ShowDialog(this);
                }),
                new CommandPaletteAction(VKIconNames.Icon28BugOutline, "API debug", "Последние VK API вызовы с redaction", "api debug monitor calls vk диагностика запросы", () => {
                    OpenApiDebugWindow();
                    return Task.CompletedTask;
                }),
                new CommandPaletteAction(VKIconNames.Icon28DoneOutline, "Health-check аккаунта", "Сеть, API auth, LongPoll, очереди и recent ошибки", "health check account api longpoll rate limit здоровье", ShowAccountHealthCheckAsync),
                new CommandPaletteAction(VKIconNames.Icon28SettingsOutline, "First-run setup", "Layout, RAM budget, animations и privacy defaults", "first run onboarding setup layout ram privacy первый запуск", ShowFirstRunOnboardingAsync),
                new CommandPaletteAction(VKIconNames.Icon28CheckCircleOutline, "Закрепить workspace", "Сохранить текущий фильтр, макет списка и открытый чат", "workspace layout save pin закрепить рабочее место фильтр", SaveCurrentWorkspaceLayoutAsync),
                new CommandPaletteAction(VKIconNames.Icon28PrivacyOutline, Settings.StreamerMode ? "Выключить streamer mode" : "Включить streamer mode", "Маскировать имена, аватары и текст на экране", "streamer privacy mask приватность стрим", () => {
                    Settings.StreamerMode = !Settings.StreamerMode;
                    return Task.CompletedTask;
                }),
                new CommandPaletteAction(VKIconNames.Icon28ArrowLeftOutline, "Вернуться к списку чатов", "Закрыть правую область на узком экране", "back chats list назад список", () => {
                    Session?.GoToChat(0);
                    return Task.CompletedTask;
                }),
                new CommandPaletteAction(VKIconNames.Icon28PrivacyOutline, "Panic lock", "Скрыть окно и очистить clipboard", "panic lock скрыть окно clipboard", PanicLockAsync)
            };

            AddCurrentChatPaletteActions(actions);
            AddWorkspaceLayoutPaletteActions(actions);
            AddPersonalScenarioPaletteActions(actions);
            AddPluginPaletteActions(actions);
            AddAccountPaletteActions(actions);
            return actions;
        }

        private void AddCurrentChatPaletteActions(List<CommandPaletteAction> actions) {
            if (Session?.SecondaryOpenedChat != null) {
                actions.Add(new CommandPaletteAction(VKIconNames.Icon28ArrowLeftOutline, "Закрыть второй слот", Session.SecondaryOpenedChat.DisplayTitle, "split view close закрыть второй слот", () => {
                    Session.CloseSecondaryChat();
                    ShowPaletteNotification("Split view закрыт", "Второй чат убран.", NotificationType.Success);
                    return Task.CompletedTask;
                }));
            }

            ChatViewModel chat = Session?.CurrentOpenedChat;
            if (chat == null) return;

            string chatTitle = chat.DisplayTitle;
            actions.Add(new CommandPaletteAction(VKIconNames.Icon28InfoCircleOutline, "Информация о текущем чате", chatTitle, "profile info чат профиль", async () => {
                await Router.OpenPeerProfileAsync(Session, chat.PeerId);
            }));
            actions.Add(new CommandPaletteAction(VKIconNames.Icon28MessageOutline, "Открыть чат во втором слоте", "Split view: закрепить справа и выбрать другой чат слева", "split view второй слот два чата рядом", () => {
                Session.OpenSecondaryChat(chat.PeerId);
                ShowPaletteNotification("Split view открыт", chatTitle, NotificationType.Success);
                return Task.CompletedTask;
            }));
            actions.Add(new CommandPaletteAction(VKIconNames.Icon28MessageOutline, "Открыть чат в мини-окне", "Topmost-окно поверх всех окон для быстрого контроля", "floating mini window topmost мини окно поверх чат", () => {
                Session.OpenFloatingChat(chat.PeerId);
                ShowPaletteNotification("Мини-окно открыто", chatTitle, NotificationType.Success);
                return Task.CompletedTask;
            }));
            actions.Add(new CommandPaletteAction(VKIconNames.Icon28SearchOutline, "Перейти к дате в чате", "Быстрый jump-to-date по загруженной истории или дешевому поиску cmid", "jump date дата история перейти день", async () => {
                await ContextMenuHelper.ShowJumpToDateAsync(Session, chat, this);
            }));
            actions.Add(new CommandPaletteAction(VKIconNames.Icon28PaletteOutline, "Тема текущего чата", "Фон, плотность, шрифт и bubble style", "theme appearance фон bubble оформление чат", () => {
                ContextMenuHelper.ShowChatThemePicker(chat, this);
                return Task.CompletedTask;
            }));
            actions.Add(new CommandPaletteAction(VKIconNames.Icon28PictureOutline, "Галерея вложений чата", "Фото, документы, аудио, голосовые, видео и стикеры", "attachments media gallery вложения галерея фото файлы", () => {
                ContextMenuHelper.OpenChatAttachmentsGallery(Session, chat);
                return Task.CompletedTask;
            }));
            actions.Add(new CommandPaletteAction(VKIconNames.Icon28DocumentOutline, "Скачать вложения чата", "Фильтры, дедуп, sidecar JSON и лимит скорости", "download export attachments скачать вложения файлы", async () => {
                await ContextMenuHelper.ShowBulkAttachmentDownloadAsync(Session, chat, this);
            }));
            actions.Add(new CommandPaletteAction(VKIconNames.Icon28WriteSquareOutline, "Быстрые действия composer", "Шаблоны, todo, reminder, quick replies и /encrypt", "quick actions replies templates todo reminder быстрые ответы шаблон", () => {
                ChatView.OpenQuickActions();
                return Task.CompletedTask;
            }));
            actions.Add(new CommandPaletteAction(VKIconNames.Icon28PrivacyOutline, "Laney E2E текущего чата", "Профиль, handshake, backup, fingerprint и автошифрование", "e2e encrypt crypto key fingerprint backup handshake", () => {
                ContextMenuHelper.ShowE2EOptions(Session, chat, this);
                return Task.CompletedTask;
            }));
            AddInboxZeroPaletteActions(actions, chat, chatTitle);

            if (chat.PeerId != Session.Id) {
                actions.Add(new CommandPaletteAction(VKIconNames.Icon28NotificationDisableOutline, "Локальная тишина", "Заглушить toast/sound на время без VK API", "local quiet mute silence тишина уведомления звук", () => {
                    ContextMenuHelper.ShowLocalQuietOptions(Session, chat, this);
                    return Task.CompletedTask;
                }));
            }

            if (chat.UnreadMessagesCount > 0 || chat.IsMarkedAsUnread) {
                actions.Add(new CommandPaletteAction(VKIconNames.Icon28MessageOutline, "Отметить чат прочитанным", chatTitle, "read mark прочитано отметить", async () => {
                    await ContextMenuHelper.MarkChatAsReadAsync(Session, chat);
                }));
            } else {
                actions.Add(new CommandPaletteAction(VKIconNames.Icon28MessageOutline, "Отметить чат непрочитанным", chatTitle, "unread mark непрочитано отметить", async () => {
                    await ContextMenuHelper.MarkChatAsUnreadAsync(Session, chat);
                }));
            }

            bool notificationsDisabled = chat.PushSettings != null && (chat.PushSettings.DisabledForever || chat.PushSettings.DisabledUntil > DateTimeOffset.Now.ToUnixTimeSeconds());
            actions.Add(new CommandPaletteAction(
                notificationsDisabled ? VKIconNames.Icon28Notifications : VKIconNames.Icon28NotificationDisableOutline,
                notificationsDisabled ? "Включить уведомления чата" : "Отключить уведомления чата",
                chatTitle,
                "notifications mute sound уведомления звук чат",
                async () => {
                    await ContextMenuHelper.SetChatNotificationsAsync(Session, chat, notificationsDisabled);
                }));

            actions.Add(new CommandPaletteAction(
                chat.IsArchived ? VKIconNames.Icon28DoorArrowRightOutline : VKIconNames.Icon28MoreHorizontal,
                chat.IsArchived ? "Вернуть чат из архива" : "Отправить чат в архив",
                "Локальная папка Laney, VK не трогаем",
                "archive folder архив скрыть вернуть",
                () => {
                    ContextMenuHelper.SetChatArchived(Session, chat, !chat.IsArchived);
                    return Task.CompletedTask;
                }));

            if (chat.PeerType == PeerType.Chat) {
                actions.Add(new CommandPaletteAction(
                    Settings.IsPeerAntiSpamEnabled(chat.PeerId) ? VKIconNames.Icon28CheckCircleOutline : VKIconNames.Icon28PrivacyOutline,
                    Settings.IsPeerAntiSpamEnabled(chat.PeerId) ? "Выключить анти-спам чата" : "Включить анти-спам чата",
                    "Локально скрывать повторы и link+spam-keyword",
                    "anti spam антиспам repeats links локальные правила",
                    () => {
                        ContextMenuHelper.ToggleAntiSpam(Session, chat);
                        return Task.CompletedTask;
                    }));
            }

            if (!Session.IsGroup && chat.PeerType == PeerType.Chat && chat.ChatSettings != null) {
                bool isInChat = chat.ChatSettings.State == UserStateInChat.In;
                actions.Add(new CommandPaletteAction(
                    VKIconNames.Icon28DoorArrowRightOutline,
                    isInChat ? "Выйти из текущего чата" : "Вернуться в текущий чат",
                    chat.ChatSettings.IsGroupChannel == true ? "Канал VK" : "Беседа VK",
                    "leave return chat channel выйти вернуться чат беседа канал",
                    () => {
                        if (isInChat) {
                            ContextMenuHelper.TryLeaveChat(Session, chat.PeerId);
                        } else {
                            ContextMenuHelper.ReturnToChat(Session, chat.PeerId);
                        }
                        return Task.CompletedTask;
                    }));
            }

            if (chat.PeerId != Session.Id) {
                actions.Add(new CommandPaletteAction(VKIconNames.Icon28DoorArrowRightOutline, "Очистить историю текущего чата", "С подтверждением, чтобы не устроить цирк одним Enter", "clear delete history очистить историю удалить чат", () => {
                    ContextMenuHelper.TryClearChat(Session, chat.PeerId);
                    return Task.CompletedTask;
                }));
            }

            actions.Add(new CommandPaletteAction(VKIconNames.Icon28BugOutline, "Chat health panel", "API errors, LongPoll, queues, cache и локальные счетчики", "chat health api longpoll cache sync диагностика чат", async () => {
                await ShowChatHealthPanelAsync(chat);
            }));

            if (chat.LastMessage != null) {
                actions.Add(new CommandPaletteAction(VKIconNames.Icon28ArticleOutline, "Инспектор последнего сообщения", "Payload, вложения, reactions, read/debug info с redaction", "message inspector payload attachments reactions debug сообщение инспектор", async () => {
                    await ShowMessageInspectorAsync(chat, chat.LastMessage);
                }));
            }
        }

        private void AddInboxZeroPaletteActions(List<CommandPaletteAction> actions, ChatViewModel chat, string chatTitle) {
            actions.Add(new CommandPaletteAction(VKIconNames.Icon28NotificationDisableOutline, "Отложить чат на час", "Локальная тишина и напоминание вернуться", "inbox zero snooze отложить тишина remind", async () => {
                SetCurrentChatQuiet(chat, TimeSpan.FromHours(1));
                await QuickActionStore.AddReminderAsync(chat.PeerId, $"Вернуться к чату: {BuildInboxChatTitle(chat)}", DateTimeOffset.Now.AddHours(1).ToString("yyyy-MM-dd HH:mm"));
            }));
            actions.Add(new CommandPaletteAction(VKIconNames.Icon28ArticleOutline, "Напомнить ответить", chatTitle, "inbox zero remind reply позже ответить", async () => {
                await QuickActionStore.AddReminderAsync(chat.PeerId, $"Ответить: {BuildInboxChatTitle(chat)}", DateTimeOffset.Now.AddHours(2).ToString("yyyy-MM-dd HH:mm"));
                ShowPaletteNotification("Напоминание добавлено", chatTitle, NotificationType.Success);
            }));
            actions.Add(new CommandPaletteAction(VKIconNames.Icon28CheckCircleOutline, "Задача из последнего сообщения", "Сохранить last message в локальный todo", "inbox zero todo task last message задача", async () => {
                await QuickActionStore.AddTodoAsync(chat.PeerId, BuildInboxMessageText(chat.LastMessage));
                ShowPaletteNotification("Todo добавлен", chatTitle, NotificationType.Success);
            }));
            actions.Add(new CommandPaletteAction(VKIconNames.Icon28MessageOutline, "Ответить позже", "Добавить reminder и оставить чат непрочитанным", "inbox zero reply later unread ответить позже", async () => {
                await QuickActionStore.AddReminderAsync(chat.PeerId, $"Ответить позже: {BuildInboxMessageText(chat.LastMessage)}", DateTimeOffset.Now.AddHours(4).ToString("yyyy-MM-dd HH:mm"));
                await ContextMenuHelper.MarkChatAsUnreadAsync(Session, chat);
                ShowPaletteNotification("Ответ отложен", chatTitle, NotificationType.Success);
            }));
            actions.Add(new CommandPaletteAction(VKIconNames.Icon28DoneOutline, "Очистить уведомления чата", "Mark read и сброс локальных counters", "inbox zero clear notifications read прочитано очистить", async () => {
                await ContextMenuHelper.MarkChatAsReadAsync(Session, chat);
                chat.UnreadReactions = null;
                ShowPaletteNotification("Уведомления очищены", chatTitle, NotificationType.Success);
            }));
        }

        private void AddPersonalScenarioPaletteActions(List<CommandPaletteAction> actions) {
            ChatViewModel chat = Session?.CurrentOpenedChat;
            foreach (QuickActionScenario scenario in QuickActionStore.GetScenarios()) {
                if (scenario.RequiresChat && chat == null) continue;

                actions.Add(new CommandPaletteAction(
                    GetScenarioIconId(scenario.ActionId),
                    scenario.Title,
                    scenario.Subtitle,
                    $"scenario сценарий personal персональный {scenario.Keywords}",
                    async () => await ExecutePersonalScenarioAsync(scenario)));
            }
        }

        private void AddWorkspaceLayoutPaletteActions(List<CommandPaletteAction> actions) {
            foreach (WorkspaceLayout layout in WorkspaceLayoutStore.GetAll()) {
                actions.Add(new CommandPaletteAction(
                    VKIconNames.Icon28MoreHorizontal,
                    $"Workspace: {layout.Title}",
                    BuildWorkspaceLayoutSubtitle(layout),
                    $"workspace layout restore открыть восстановить {layout.Title} {layout.ChatFilterTitle}",
                    () => {
                        WorkspaceLayoutStore.Apply(Session, layout);
                        ShowPaletteNotification("Workspace открыт", layout.Title, NotificationType.Success);
                        return Task.CompletedTask;
                    }));
            }
        }

        private async Task SaveCurrentWorkspaceLayoutAsync() {
            if (Session == null) return;

            string defaultTitle = $"{Session.ImViewModel?.CurrentChatFilterTitle ?? "Все"} · {DateTime.Now:dd.MM HH:mm}";
            TextBox titleBox = new TextBox {
                Text = defaultTitle,
                MinWidth = 360,
                PlaceholderText = "Название workspace"
            };

            VKUIDialog dialog = new VKUIDialog(
                "Закрепить workspace",
                "Сохраняются фильтр списка, compact/comfy layout, размеры аватаров/текста и текущий чат.",
                ["Сохранить", "Отмена"],
                2) {
                DialogContent = titleBox
            };

            int result = await dialog.ShowDialog<int>(this);
            if (result != 1) return;

            WorkspaceLayout layout = WorkspaceLayoutStore.SaveCurrent(Session, titleBox.Text);
            ShowPaletteNotification("Workspace закреплён", layout.Title, NotificationType.Success);
        }

        private static string BuildWorkspaceLayoutSubtitle(WorkspaceLayout layout) {
            string filter = String.IsNullOrWhiteSpace(layout.ChatFilterTitle) ? "фильтр: Все" : $"фильтр: {layout.ChatFilterTitle}";
            string peer = layout.CurrentPeerId == 0 ? "без открытого чата" : $"peer: {layout.CurrentPeerId}";
            return $"{filter}; {layout.ChatListLayout}/{layout.ChatListDensity}; {peer}";
        }

        private void OpenApiDebugWindow() {
            ApiDebugWindow window = new ApiDebugWindow {
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            window.Show(this);
        }

        private async Task ShowAccountHealthCheckAsync() {
            if (Session == null) return;

            try {
                AccountHealthReport report = await new VKUIWaitDialog<AccountHealthReport>().ShowAsync(this, AccountHealthChecker.CheckAsync(Session));
                await ShowTextDialogAsync("Health-check аккаунта", report.Summary, report.ToDetailsText(), 560, 340);
            } catch (Exception ex) {
                await new VKUIDialog("Health-check упал", ex.Message, ["Понятно"], 1).ShowDialog(this);
            }
        }

        private async Task ShowChatHealthPanelAsync(ChatViewModel chat) {
            if (chat == null) return;

            BitmapCacheSnapshot bitmap = BitmapManager.GetCacheSnapshot();
            MediaMemorySnapshot media = MediaMemoryGovernor.GetSnapshot();
            LNetQueueSnapshot network = LNet.GetQueueSnapshot();
            LongPoll longPoll = Session?.LongPoll;
            DateTimeOffset now = DateTimeOffset.UtcNow;
            IReadOnlyList<ApiDebugCallEntry> apiEntries = ApiDebugMonitor.GetSnapshot();
            int recentApiErrors = apiEntries.Count(e => now - e.StartedAt <= TimeSpan.FromMinutes(5) && (e.ErrorType != null || e.StatusCode >= 400));
            int recentApiCalls = apiEntries.Count(e => now - e.StartedAt <= TimeSpan.FromMinutes(5));

            StringBuilder text = new StringBuilder();
            text.AppendLine($"Chat: {BuildInboxChatTitle(chat)}");
            text.AppendLine($"Peer: {chat.PeerId}; type: {chat.PeerType}; unread: {chat.UnreadMessagesCount}; reactions: {chat.UnreadReactions?.Count ?? 0}");
            text.AppendLine($"Messages: displayed={chat.DisplayedMessages?.Count ?? 0}; received-cache={chat.ReceivedMessages?.Count ?? 0}; selected={chat.SelectedMessagesCount}");
            text.AppendLine($"Last message: cmid={chat.LastMessage?.ConversationMessageId ?? 0}; state={chat.LastMessage?.State.ToString() ?? "none"}; sent={chat.LastMessage?.SentTime.ToString("O") ?? "none"}");
            text.AppendLine();
            text.AppendLine($"LongPoll: running={longPoll?.IsRunning.ToString() ?? "false"}; state={longPoll?.State.ToString() ?? "none"}; last-sync={FormatLocalTime(longPoll?.LastSyncUtc)}; last-error={FormatLocalTime(longPoll?.LastErrorUtc)}; last-updates={longPoll?.LastUpdatesCount ?? 0}");
            text.AppendLine($"Network queues: active={network.ActiveRequests}; seq-get={network.SequentialGetRequests}; seq-post={network.SequentialPostRequests}");
            text.AppendLine($"API debug: calls-5m={recentApiCalls}; errors-5m={recentApiErrors}; monitor={(Settings.ApiDebugMonitorEnabled ? "on" : "off")}");
            text.AppendLine();
            text.AppendLine($"Bitmap cache: entries={bitmap.EntryCount}; loading={bitmap.LoadingCount}; size={FormatBytes(bitmap.SizeBytes)} / {FormatBytes(bitmap.LimitBytes)}");
            text.AppendLine($"Media governor: budget={FormatBytes(media.TotalBudgetBytes)}; used={FormatBytes(media.EstimatedUsedBytes)}; headroom={FormatBytes(media.HeadroomBytes)}; active-loads={media.ActiveMediaLoads}/{media.MediaLoadConcurrency}; prefetch={(media.CanPrefetchMedia ? "open" : "gated")}");
            text.AppendLine();
            text.AppendLine($"Local moderation: anti-spam={(Settings.IsPeerAntiSpamEnabled(chat.PeerId) ? "on" : "off")}; hidden={Settings.GetLocallyHiddenMessageIds(chat.PeerId).Count}; muted-senders={Settings.GetMutedSenderIds(chat.PeerId).Count}; shadow-senders={Settings.GetShadowBannedSenderIds(chat.PeerId).Count}");

            await ShowTextDialogAsync("Chat health panel", "Локальная диагностика текущего чата.", text.ToString(), 600, 380);
        }

        private async Task ShowMessageInspectorAsync(ChatViewModel chat, MessageViewModel message) {
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

            await ShowTextDialogAsync("Инспектор сообщения", "Payload, вложения, reactions и read/debug info.", text.ToString(), 620, 420);
        }

        private async Task ShowTextDialogAsync(string title, string subtitle, string text, double minWidth, double minHeight) {
            TextBox textBox = new TextBox {
                Text = text,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinWidth = minWidth,
                MinHeight = minHeight,
                MaxHeight = 520
            };
            textBox.Classes.Add("Mono");

            VKUIDialog dialog = new VKUIDialog(title, subtitle, ["Закрыть"], 1) {
                DialogContent = textBox
            };
            await dialog.ShowDialog<int>(this);
        }

        private async Task ShowFirstRunOnboardingAsync() {
            string[] profileIds = [InterfaceProfileIds.Balanced, InterfaceProfileIds.Compact, InterfaceProfileIds.Work, InterfaceProfileIds.LowRam, InterfaceProfileIds.Streamer];
            string[] profileTitles = ["Сбалансированный", "Компактный", "Рабочий", "Low RAM", "Streamer"];
            int currentProfileIndex = Math.Max(0, Array.IndexOf(profileIds, Settings.InterfaceProfile));

            ComboBox profileBox = new ComboBox {
                ItemsSource = profileTitles,
                SelectedIndex = currentProfileIndex,
                MinWidth = 260
            };
            ComboBox ramBox = new ComboBox {
                ItemsSource = new[] { "128 MB", "256 MB", "512 MB" },
                SelectedIndex = Settings.MediaMemoryBudgetMb <= 128 ? 0 : Settings.MediaMemoryBudgetMb >= 512 ? 2 : 1,
                MinWidth = 140
            };
            ComboBox animationBox = new ComboBox {
                ItemsSource = new[] { "Hover", "Click", "Never" },
                SelectedIndex = Settings.StickerAnimation == StickerAnimationMode.Never ? 2 : Settings.StickerAnimation == StickerAnimationMode.Click ? 1 : 0,
                MinWidth = 140
            };
            CheckBox streamerBox = new CheckBox { Content = "Режим стримера", IsChecked = Settings.StreamerMode };
            CheckBox autoLockBox = new CheckBox { Content = "Автоблокировка", IsChecked = Settings.AutoLockEnabled };
            CheckBox clipboardBox = new CheckBox { Content = "Чистить буфер", IsChecked = Settings.PanicLockClearClipboard };

            StackPanel content = new StackPanel {
                Spacing = 10,
                Children = {
                    BuildLabeledControl("Профиль", profileBox),
                    BuildLabeledControl("RAM бюджет", ramBox),
                    BuildLabeledControl("Анимация стикеров", animationBox),
                    streamerBox,
                    autoLockBox,
                    clipboardBox
                }
            };

            VKUIDialog dialog = new VKUIDialog(
                "First-run setup",
                "Базовые локальные параметры без маркетинговой мишуры.",
                ["Сохранить", "Пропустить"],
                1) {
                DialogContent = content
            };

            int result = await dialog.ShowDialog<int>(this);
            Settings.FirstRunOnboardingDone = true;
            if (result != 1) return;

            ApplyOnboardingProfile(profileIds[Math.Clamp(profileBox.SelectedIndex, 0, profileIds.Length - 1)]);
            Settings.MediaMemoryBudgetMb = ramBox.SelectedIndex switch {
                0 => 128,
                2 => 512,
                _ => 256
            };
            Settings.StickerAnimation = animationBox.SelectedIndex switch {
                1 => StickerAnimationMode.Click,
                2 => StickerAnimationMode.Never,
                _ => StickerAnimationMode.Hover
            };
            Settings.StreamerMode = streamerBox.IsChecked == true;
            Settings.AutoLockEnabled = autoLockBox.IsChecked == true;
            Settings.PanicLockClearClipboard = clipboardBox.IsChecked == true;
            Settings.AutoLockClearClipboard = clipboardBox.IsChecked == true && autoLockBox.IsChecked == true;
            AppearanceManager.ApplyAccountAppearanceSettings(Session);
            ToggleAutoLockTimer();
            ToggleAutoStatusTimer();
        }

        private static Grid BuildLabeledControl(string label, Control control) {
            Grid grid = new Grid {
                ColumnDefinitions = new ColumnDefinitions("160 *"),
                Children = {
                    new TextBlock {
                        Text = label,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                    },
                    control
                }
            };
            Grid.SetColumn(control, 1);
            return grid;
        }

        private static void ApplyOnboardingProfile(string profileId) {
            switch (profileId) {
                case InterfaceProfileIds.Compact:
                    Settings.SetBatch(new Dictionary<string, object> {
                        { Settings.INTERFACE_PROFILE, InterfaceProfileIds.Compact },
                        { Settings.CHAT_LIST_DENSITY, ChatListDensityIds.Small },
                        { Settings.CHAT_LIST_LAYOUT, ChatListLayoutIds.Compact },
                        { Settings.MESSAGE_FONT_SIZE, TextSizeIds.Small },
                        { Settings.MESSAGE_BUBBLE_DENSITY, BubbleDensityIds.Compact },
                        { Settings.MESSAGE_BUBBLE_WIDTH, BubbleWidthIds.Narrow }
                    });
                    break;
                case InterfaceProfileIds.Work:
                    Settings.SetBatch(new Dictionary<string, object> {
                        { Settings.INTERFACE_PROFILE, InterfaceProfileIds.Work },
                        { Settings.CHAT_LIST_DENSITY, ChatListDensityIds.Small },
                        { Settings.CHAT_LIST_LAYOUT, ChatListLayoutIds.Compact },
                        { Settings.GROUPS_BACKGROUND_LONGPOLL_LIMIT, 2 },
                        { Settings.MESSAGE_BUBBLE_STYLE, BubbleStyleIds.Minimal }
                    });
                    break;
                case InterfaceProfileIds.LowRam:
                    Settings.SetBatch(new Dictionary<string, object> {
                        { Settings.INTERFACE_PROFILE, InterfaceProfileIds.LowRam },
                        { Settings.LOW_MEMORY_MODE, true },
                        { Settings.LOW_TRAFFIC_MODE, true },
                        { Settings.LOW_MOTION_MODE, true },
                        { Settings.DEBUG_LOAD_IMAGES_SEQUENTIAL, true },
                        { Settings.STICKERS_ANIMATION_MODE, (int)StickerAnimationMode.Never },
                        { Settings.STICKERS_ANIMATE, false }
                    });
                    break;
                case InterfaceProfileIds.Streamer:
                    Settings.SetBatch(new Dictionary<string, object> {
                        { Settings.INTERFACE_PROFILE, InterfaceProfileIds.Streamer },
                        { Settings.STREAMER_MODE, true },
                        { Settings.LOW_MOTION_MODE, true },
                        { Settings.STICKERS_ANIMATION_MODE, (int)StickerAnimationMode.Never },
                        { Settings.STICKERS_ANIMATE, false },
                        { Settings.DISABLE_MENTIONS, true },
                        { Settings.DONT_PARSE_LINKS, true }
                    });
                    break;
                default:
                    Settings.SetBatch(new Dictionary<string, object> {
                        { Settings.INTERFACE_PROFILE, InterfaceProfileIds.Balanced },
                        { Settings.CHAT_LIST_DENSITY, ChatListDensityIds.Medium },
                        { Settings.CHAT_LIST_LAYOUT, ChatListLayoutIds.Classic },
                        { Settings.MESSAGE_FONT_SIZE, TextSizeIds.Medium },
                        { Settings.MESSAGE_BUBBLE_DENSITY, BubbleDensityIds.Normal },
                        { Settings.MESSAGE_BUBBLE_WIDTH, BubbleWidthIds.Medium }
                    });
                    break;
            }
        }

        private static string BuildInboxChatTitle(ChatViewModel chat) {
            if (chat == null) return "чат";
            return Settings.StreamerMode ? $"peer:{chat.PeerId}" : chat.DisplayTitle;
        }

        private static string BuildInboxMessageText(MessageViewModel message) {
            if (message == null) return "(нет сообщения)";

            string text = Settings.StreamerMode ? "[message redacted]" : message.DisplayPreviewText;
            if (String.IsNullOrWhiteSpace(text)) text = $"message:{message.PeerId}_{message.ConversationMessageId}";
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= 220 ? text : $"{text[..220]}...";
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
            foreach (var attachment in message.Attachments) {
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

        private static string FormatLocalTime(DateTimeOffset? value) {
            return value == null ? "none" : value.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss");
        }

        private static string FormatBytes(long bytes) {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{Math.Round(bytes / 1024d, 1)} KB";
            return $"{Math.Round(bytes / 1024d / 1024d, 1)} MB";
        }

        private async Task ExecutePersonalScenarioAsync(QuickActionScenario scenario) {
            ChatViewModel chat = Session?.CurrentOpenedChat;
            if (scenario.RequiresChat && chat == null) {
                ShowPaletteNotification("Сценарий недоступен", "Сначала открой чат, без цели стрелять нечем.", NotificationType.Warning);
                return;
            }

            switch (scenario.ActionId) {
                case QuickActionScenarioActionIds.DailyDigest:
                    await ShowDailyDigestAsync();
                    return;
                case QuickActionScenarioActionIds.ExtractTasks:
                    await ShowExtractedTasksAsync();
                    return;
                case QuickActionScenarioActionIds.OcrLoadedImages:
                    await ShowLocalOcrAsync();
                    return;
                case QuickActionScenarioActionIds.QuickActions:
                    ChatView.OpenQuickActions();
                    return;
                case QuickActionScenarioActionIds.DownloadAttachments:
                    await ContextMenuHelper.ShowBulkAttachmentDownloadAsync(Session, chat, this);
                    return;
                case QuickActionScenarioActionIds.LocalQuiet1h:
                    SetCurrentChatQuiet(chat, TimeSpan.FromHours(1));
                    return;
                case QuickActionScenarioActionIds.E2E:
                    ContextMenuHelper.ShowE2EOptions(Session, chat, this);
                    return;
                case QuickActionScenarioActionIds.ArchiveToggle:
                    ContextMenuHelper.SetChatArchived(Session, chat, !chat.IsArchived);
                    return;
                case QuickActionScenarioActionIds.StatusWork:
                    SetAutoStatusFromScenario(AutoStatusModeIds.Work);
                    return;
                case QuickActionScenarioActionIds.StatusDoNotDisturb:
                    SetAutoStatusFromScenario(AutoStatusModeIds.DoNotDisturb);
                    return;
                default:
                    ShowPaletteNotification("Сценарий не выполнен", $"Неизвестное действие: {scenario.ActionId}", NotificationType.Warning);
                    return;
            }
        }

        private void SetCurrentChatQuiet(ChatViewModel chat, TimeSpan duration) {
            if (chat == null) return;

            DateTimeOffset until = DateTimeOffset.Now.Add(duration <= TimeSpan.Zero ? TimeSpan.FromHours(1) : duration);
            Settings.SetPeerQuietUntil(chat.PeerId, until);
            ShowPaletteNotification("Чат заглушен локально", $"До {until.LocalDateTime:dd.MM HH:mm}. VK не беспокоили.", NotificationType.Success);
        }

        private void SetAutoStatusFromScenario(string mode) {
            string normalized = AutoStatusModeIds.Normalize(mode);
            Settings.AutoStatusEnabled = true;
            Settings.AutoStatusMode = normalized;
            UpdateAutoStatusUI();
            ShowPaletteNotification("Автостатус включён", AutoStatusManager.GetTitle(normalized), NotificationType.Success);
        }

        private void ShowPaletteNotification(string title, string text, NotificationType type) {
            Session?.ShowNotification(new Notification(title, text, type));
        }

        private static string GetScenarioIconId(string actionId) {
            return actionId switch {
                QuickActionScenarioActionIds.DailyDigest => VKIconNames.Icon28ArticleOutline,
                QuickActionScenarioActionIds.ExtractTasks => VKIconNames.Icon28CheckCircleOutline,
                QuickActionScenarioActionIds.OcrLoadedImages => VKIconNames.Icon28PictureOutline,
                QuickActionScenarioActionIds.QuickActions => VKIconNames.Icon28WriteSquareOutline,
                QuickActionScenarioActionIds.DownloadAttachments => VKIconNames.Icon28DocumentOutline,
                QuickActionScenarioActionIds.LocalQuiet1h => VKIconNames.Icon28NotificationDisableOutline,
                QuickActionScenarioActionIds.E2E => VKIconNames.Icon28PrivacyOutline,
                QuickActionScenarioActionIds.ArchiveToggle => VKIconNames.Icon28DoorArrowRightOutline,
                QuickActionScenarioActionIds.StatusWork => VKIconNames.Icon28UserOutgoingOutline,
                QuickActionScenarioActionIds.StatusDoNotDisturb => VKIconNames.Icon28NotificationDisableOutline,
                _ => VKIconNames.Icon28MoreHorizontal
            };
        }

        private void AddPluginPaletteActions(List<CommandPaletteAction> actions) {
            ChatViewModel chat = Session?.CurrentOpenedChat;
            foreach (PluginPaletteCommandDescriptor descriptor in PluginRegistry.GetPaletteCommands()) {
                PluginPaletteCommand command = descriptor.Command;
                if (command.RequiresChat && chat == null) continue;

                actions.Add(new CommandPaletteAction(
                    String.IsNullOrWhiteSpace(command.IconId) ? VKIconNames.Icon28MoreHorizontal : command.IconId,
                    $"Плагин: {command.Title}",
                    BuildPluginSubtitle(descriptor),
                    $"plugin плагин {descriptor.Plugin.Title} {command.Keywords}",
                    async () => await ExecutePluginPaletteCommandAsync(descriptor)));
            }
        }

        private async Task ExecutePluginPaletteCommandAsync(PluginPaletteCommandDescriptor descriptor) {
            PluginPaletteCommand command = descriptor.Command;
            ChatViewModel chat = Session?.CurrentOpenedChat;

            switch (command.ActionId) {
                case PluginActionIds.OpenUrl:
                    if (!TryCreateSafePluginUri(command.Value, out Uri uri)) {
                        ShowPaletteNotification("Плагин не выполнен", "open_url принимает только http/https/mailto. Исполняемые схемы идут лесом.", NotificationType.Warning);
                        return;
                    }

                    await ELOR.Laney.Core.Launcher.LaunchUrl(uri);
                    return;
                case PluginActionIds.CopyText:
                    if (Clipboard == null) {
                        ShowPaletteNotification("Плагин не выполнен", "Clipboard недоступен.", NotificationType.Warning);
                        return;
                    }

                    await Clipboard.SetTextAsync(ExpandPluginValue(command.Value, chat));
                    ShowPaletteNotification("Плагин", "Текст скопирован.", NotificationType.Success);
                    return;
                case PluginActionIds.BuiltInScenario:
                    await ExecutePersonalScenarioAsync(new QuickActionScenario {
                        Title = command.Title,
                        ActionId = command.Value,
                        RequiresChat = command.RequiresChat || IsChatScenario(command.Value)
                    });
                    return;
                case PluginActionIds.SlashCommand:
                    if (chat == null) {
                        ShowPaletteNotification("Плагин не выполнен", "Для slash template нужен открытый чат.", NotificationType.Warning);
                        return;
                    }

                    ChatView.SetComposerText(ExpandPluginValue(command.Value, chat));
                    ShowPaletteNotification("Плагин", "Команда вставлена в composer.", NotificationType.Success);
                    return;
                default:
                    ShowPaletteNotification("Плагин не выполнен", $"Action `{command.ActionId}` не разрешён.", NotificationType.Warning);
                    return;
            }
        }

        private static string BuildPluginSubtitle(PluginPaletteCommandDescriptor descriptor) {
            string subtitle = descriptor.Command.Subtitle;
            string source = $"{descriptor.Plugin.Title} {descriptor.Plugin.Version}".Trim();
            return String.IsNullOrWhiteSpace(subtitle) ? source : $"{subtitle} · {source}";
        }

        private static bool TryCreateSafePluginUri(string value, out Uri uri) {
            uri = null;
            if (!Uri.TryCreate(value, UriKind.Absolute, out Uri parsed)) return false;
            if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeMailto) return false;

            uri = parsed;
            return true;
        }

        private static bool IsChatScenario(string actionId) {
            return actionId == QuickActionScenarioActionIds.QuickActions
                || actionId == QuickActionScenarioActionIds.DownloadAttachments
                || actionId == QuickActionScenarioActionIds.LocalQuiet1h
                || actionId == QuickActionScenarioActionIds.E2E
                || actionId == QuickActionScenarioActionIds.ArchiveToggle;
        }

        private static string ExpandPluginValue(string value, ChatViewModel chat) {
            DateTimeOffset now = DateTimeOffset.Now;
            return (value ?? String.Empty)
                .Replace("{peer_id}", chat?.PeerId.ToString() ?? String.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("{chat_title}", chat?.DisplayTitle ?? String.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("{chat_link}", BuildPluginPeerLink(chat), StringComparison.OrdinalIgnoreCase)
                .Replace("{date}", now.ToString("dd.MM.yyyy"), StringComparison.OrdinalIgnoreCase)
                .Replace("{time}", now.ToString("HH:mm"), StringComparison.OrdinalIgnoreCase)
                .Replace("{datetime}", now.ToString("dd.MM.yyyy HH:mm"), StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildPluginPeerLink(ChatViewModel chat) {
            if (chat == null) return String.Empty;
            if (chat.PeerType == PeerType.Chat) return $"https://vk.com/im?sel=c{chat.PeerId - 2000000000}";
            if (chat.PeerType == PeerType.User) return $"https://vk.com/id{chat.PeerId}";
            if (chat.PeerType == PeerType.Group) return $"https://vk.com/club{-chat.PeerId}";
            return $"https://vk.com/im?sel={chat.PeerId}";
        }

        private void AddAccountPaletteActions(List<CommandPaletteAction> actions) {
            if (Session == null || VKSession.Sessions.Count <= 1) return;

            foreach (VKSession session in VKSession.Sessions.Where(s => s.Id != Session.Id)) {
                actions.Add(new CommandPaletteAction(
                    VKIconNames.Icon28UserOutgoingOutline,
                    $"Переключиться: {session.DisplayName}",
                    session.IsGroup ? "Окно сообщества" : "Окно аккаунта",
                    $"account session switch аккаунт сессия {session.Name} {session.Id}",
                    () => {
                        session.TryOpenWindow();
                        return Task.CompletedTask;
                    }));
            }
        }

        private async Task ShowDailyDigestAsync() {
            string digest = await DailyDigestHelper.BuildAsync(Session?.ImViewModel?.SortedChats);
            TextBox digestBox = new TextBox {
                Text = digest,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinWidth = 400,
                MinHeight = 260,
                MaxHeight = 420
            };

            VKUIDialog dialog = new VKUIDialog("Сводка дня", "Сохранено локально в quick-actions/daily-digest.md", ["Закрыть"], 1) {
                DialogContent = digestBox
            };
            await dialog.ShowDialog<int>(this);
        }

        private async Task ShowExtractedTasksAsync() {
            string report = await TaskExtractionHelper.ExtractAsync(Session?.ImViewModel?.SortedChats, Session?.CurrentOpenedChat);
            TextBox reportBox = new TextBox {
                Text = report,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinWidth = 520,
                MinHeight = 320,
                MaxHeight = 480
            };

            VKUIDialog dialog = new VKUIDialog("Извлечь задачи", "Кандидаты сохранены локально в quick-actions/todo.md", ["Закрыть"], 1) {
                DialogContent = reportBox
            };
            await dialog.ShowDialog<int>(this);
        }

        private async Task ShowLocalOcrAsync() {
            LocalOcrBatchResult result = await LocalOcrService.RecognizeLoadedImagesAsync(Session?.ImViewModel?.SortedChats);
            string text = result.Summary;
            if (result.Errors.Count > 0) text += "\n\n" + String.Join("\n", result.Errors);

            VKUIDialog dialog = new VKUIDialog("OCR загруженных картинок", text, ["Закрыть"], 1);
            await dialog.ShowDialog<int>(this);
        }

        private async Task ShowAudioPlaybackHistoryAsync() {
            IReadOnlyList<AudioPlaybackHistoryItem> history = Settings.GetAudioPlaybackHistory();
            string text = history.Count == 0
                ? "История пуста. Запусти любой трек, подкаст или голосовое, и тут появится след."
                : String.Join("\n", history.Select(FormatAudioPlaybackHistoryItem));

            TextBox historyBox = new TextBox {
                Text = text,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinWidth = 520,
                MinHeight = 320,
                MaxHeight = 480
            };

            VKUIDialog dialog = new VKUIDialog(
                "История прослушивания",
                $"Записей: {history.Count}. Всё локально, VK не докладываем.",
                ["Очистить", "Закрыть"],
                2) {
                DialogContent = historyBox
            };

            int result = await dialog.ShowDialog<int>(this);
            if (result == 1) {
                Settings.ClearAudioPlaybackHistory();
                Session?.ShowNotification(new Notification("История прослушивания", "Очищено локально.", NotificationType.Success));
            }
        }

        public async Task ShowStoriesHubAsync() {
            IReadOnlyList<ELOR.VKAPILib.Objects.Story> stories = Array.Empty<ELOR.VKAPILib.Objects.Story>();

            try {
                stories = await LoadVKStoriesAsync();
                if (stories.Count > 0) {
                    await StoryViewerWindow.ShowAsync(this, Session, stories);
                    return;
                }
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot load VK stories.");
                await new VKUIDialog("Истории VK", $"Не удалось загрузить stories.get: {ex.GetBaseException().Message}", ["Закрыть"], 1).ShowDialog(this);
                return;
            }

            await new VKUIDialog("Истории VK", "stories.get не вернул активные истории. Когда VK отдаст свежие сторис, Laney откроет их прямо в приложении.", ["Закрыть"], 1).ShowDialog(this);
        }

        private async Task<IReadOnlyList<ELOR.VKAPILib.Objects.Story>> LoadVKStoriesAsync() {
            return await VKStoriesService.LoadStoriesAsync(Session, 40);
        }

        private static string FormatAudioPlaybackHistoryItem(AudioPlaybackHistoryItem item) {
            DateTimeOffset updated = item.UpdatedAtUnix > 0
                ? DateTimeOffset.FromUnixTimeSeconds(item.UpdatedAtUnix).ToLocalTime()
                : DateTimeOffset.Now;
            string title = String.IsNullOrWhiteSpace(item.Title) ? $"#{item.Id}" : item.Title;
            string performer = String.IsNullOrWhiteSpace(item.Performer) ? "без исполнителя" : item.Performer;
            string position = FormatDuration(TimeSpan.FromMilliseconds(Math.Max(0, item.PositionMs)));
            string duration = item.DurationMs > 0 ? FormatDuration(TimeSpan.FromMilliseconds(item.DurationMs)) : "--:--";
            return $"{updated:dd.MM HH:mm} · {item.Type} · {performer} — {title} · {position}/{duration}";
        }

        private static string FormatDuration(TimeSpan value) {
            return value.TotalHours >= 1
                ? value.ToString(@"h\:mm\:ss")
                : value.ToString(@"m\:ss");
        }

        private async Task ShowAutoStatusDialogAsync() {
            List<string> modes = AutoStatusManager.Modes.ToList();
            ComboBox modeBox = new ComboBox {
                ItemsSource = modes.Select(AutoStatusManager.GetTitle).ToList(),
                SelectedIndex = Math.Max(0, modes.IndexOf(Settings.AutoStatusMode)),
                MinWidth = 260
            };
            CheckBox enabledBox = new CheckBox {
                Content = "Включить Laney-only автостатус",
                IsChecked = Settings.AutoStatusEnabled
            };
            TextBlock hint = new TextBlock {
                Text = "Расписание и простой ПК настраиваются в Уведомлениях.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = App.GetResource<IBrush>("VKTextSecondaryBrush")
            };
            StackPanel content = new StackPanel {
                Spacing = 10,
                Children = {
                    enabledBox,
                    modeBox,
                    hint
                }
            };

            VKUIDialog dialog = new VKUIDialog(
                "Автостатус",
                "Локальный статус клиента. VK-профиль не меняется.",
                ["Сохранить", "Выключить", "Отмена"],
                1) {
                DialogContent = content
            };

            int result = await dialog.ShowDialog<int>(this);
            if (result == 0 || result == 3) return;

            if (result == 2) {
                Settings.AutoStatusEnabled = false;
            } else {
                Settings.AutoStatusEnabled = enabledBox.IsChecked == true;
                Settings.AutoStatusMode = modes[Math.Clamp(modeBox.SelectedIndex, 0, modes.Count - 1)];
            }
            UpdateAutoStatusUI();
        }

        public Task PanicLockAsync() {
            return LockClientAsync(true, Settings.PanicLockClearClipboard);
        }

        private async Task LockClientAsync(bool hideWindow, bool clearClipboard) {
            PanicLockOverlay.IsVisible = true;

            if (clearClipboard) {
                try {
                    if (Clipboard != null) await Clipboard.SetTextAsync(String.Empty);
                } catch (Exception ex) {
                    Log.Warning(ex, "Cannot clear clipboard during panic lock.");
                }
            }

            if (hideWindow) Hide();
        }

        private void PanicLockUnlockButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) {
            PanicLockOverlay.IsVisible = false;
            lastUserActivity = DateTimeOffset.Now;
            if (lastAutoStatusWasIdle) UpdateAutoStatusUI();
            Show();
            Activate();
        }

        private void MainWindow_EffectiveViewportChanged(object? sender, Avalonia.Layout.EffectiveViewportChangedEventArgs e) {
            CheckAdaptivity(e.EffectiveViewport.Width);
        }

        private void MainWindow_Activated(object? sender, EventArgs e) {
            Program.StopStopwatch();
            Log.Information($"{nameof(MainWindow)} activated. Launch time: {Program.LaunchTime} ms.");
            Activated -= MainWindow_Activated;
            Closing += MainWindow_Closing;
            SizeChanged += MainWindow_SizeChanged; // not optimal, but working perfectly
            PositionChanged += MainWindow_PositionChanged; // not optimal, but working perfectly
            VKSession.GetByDataContext(this).PropertyChanged += SessionPropertyChanged;
            SetSessionNameInWindowTitle(VKSession.GetByDataContext(this).Name);
            backgroundSearchIndexer = new BackgroundSearchIndexer(Session);
            backgroundSearchIndexer.Start();
            backgroundHistoryStatisticsIndexer = new BackgroundHistoryStatisticsIndexer(Session);
            backgroundHistoryStatisticsIndexer.Start();

            new Action(async () => {
                await LeftNav.NavigationRouter.NavigateToAsync(new ImView());
                TryOpenPerfDemoChat();
                TryOpenPerfNewsFeed();
                TryOpenPerfSettings();
            })();
        }

        private void TryOpenPerfDemoChat() {
            if (!DemoMode.IsEnabled) return;

            string peer = App.GetCmdLineValue("perf-open-peer");
            if (!long.TryParse(peer, out long peerId) || peerId == 0) return;

            Dispatcher.UIThread.Post(() => {
                Log.Information("Opening perf demo chat {0}", peerId);
                Session.GoToChat(peerId);
                if (App.HasCmdLineValue("perf-live-qa")) {
                    _ = RunPerfLiveQaAsync(peerId);
                }
                if (App.HasCmdLineValue("perf-scroll-qa")) {
                    _ = RunPerfScrollQaAsync(peerId);
                }
            }, DispatcherPriority.Background);
        }

        private async Task RunPerfScrollQaAsync(long peerId) {
            try {
                await Task.Delay(1800);

                ChatViewModel chat = Session.CurrentOpenedChat;
                if (chat == null || chat.PeerId != peerId) {
                    Log.Error("Perf scroll QA failed: chat {PeerId} is not opened.", peerId);
                    return;
                }

                Stopwatch readiness = Stopwatch.StartNew();
                while ((chat.IsLoading || chat.DisplayedMessages == null || chat.DisplayedMessages.Count == 0) && readiness.Elapsed < TimeSpan.FromSeconds(12)) {
                    await Task.Delay(80);
                }

                await Task.Delay(500);

                ChatPerfScrollQaResult result = await ChatView.RunPerfScrollQaAsync(TimeSpan.FromSeconds(4));
                bool fpsOk = result.AverageFps >= 58 && result.JankFrames <= 8;
                Log.Information(
                    "Perf scroll QA result: fpsOk={FpsOk}; canLeaveBottom={CanLeaveBottom}; leaveBottom={LeaveBottomFinal:F1}/{LeaveBottomTarget:F1}; leaveBottomMax={LeaveBottomMax:F1}; leaveBottomDistance={LeaveBottomDistance:F1}; avgFps={AverageFps:F1}; avgFrame={AverageFrameMs:F2}ms; lastFrame={LastFrameMs:F2}ms; maxFrame={MaxFrameMs:F2}ms; jank={JankFrames}; visibleControls={VisibleControls}; samples={Samples}; privateMemory={PrivateMemoryMb:F2}MB",
                    fpsOk,
                    result.CanScrollAwayFromBottom,
                    result.ScrollAwayFromBottomFinal,
                    result.ScrollAwayFromBottomTarget,
                    result.ScrollAwayFromBottomMax,
                    result.ScrollAwayFromBottomDistance,
                    result.AverageFps,
                    result.AverageFrameMs,
                    result.LastFrameMs,
                    result.MaxFrameMs,
                    result.JankFrames,
                    result.VisibleControls,
                    result.Samples,
                    result.PrivateMemoryMb);
            } catch (Exception ex) {
                Log.Error(ex, "Perf scroll QA failed.");
            }
        }

        private async Task RunPerfLiveQaAsync(long peerId) {
            try {
                await Task.Delay(800);

                ChatViewModel chat = Session.CurrentOpenedChat;
                if (chat == null || chat.PeerId != peerId) {
                    Log.Error("Perf live QA failed: chat {PeerId} is not opened.", peerId);
                    return;
                }

                Stopwatch readiness = Stopwatch.StartNew();
                while ((chat.IsLoading || chat.DisplayedMessages == null || chat.DisplayedMessages.Count == 0) && readiness.Elapsed < TimeSpan.FromSeconds(8)) {
                    await Task.Delay(80);
                }

                int initialCount = chat.DisplayedMessages?.Count ?? 0;
                int firstId = chat.DisplayedMessages?.First?.ConversationMessageId ?? 0;
                int lastId = chat.DisplayedMessages?.Last?.ConversationMessageId ?? 0;

                ChatHistoryBoundaryQaResult boundary = await ChatView.RunHistoryBoundaryQaAsync(TimeSpan.FromSeconds(5));
                int afterPreviousCount = chat.DisplayedMessages?.Count ?? 0;
                int afterPreviousFirstId = chat.DisplayedMessages?.First?.ConversationMessageId ?? 0;

                int currentCount = chat.DisplayedMessages?.Count ?? 0;
                int middleIndex = currentCount > 0 ? currentCount / 2 : 0;
                int middleId = currentCount > 0 ? chat.DisplayedMessages[middleIndex].ConversationMessageId : 0;

                if (middleId > 0) await chat.GoToMessageAsync(middleId);
                await Task.Delay(300);

                bool selectionOk = false;
                try {
                    chat.SelectedMessages.Clear();
                    if (currentCount > 0) chat.SelectedMessages.Select(middleIndex);
                    selectionOk = chat.SelectedMessagesCount == 1;
                    chat.SelectedMessages.Clear();
                } catch (Exception ex) {
                    Log.Warning(ex, "Perf live QA selection check failed.");
                }

                await chat.GoToMessageAsync(lastId);
                await chat.LoadNextMessagesAsync(null);
                int afterNextCount = chat.DisplayedMessages?.Count ?? 0;
                int afterNextLastId = chat.DisplayedMessages?.Last?.ConversationMessageId ?? 0;

                bool scrollToOk = middleId > 0;
                bool previousOk = boundary.PreviousLoaded;
                bool nextOk = afterNextCount >= afterPreviousCount && afterNextLastId >= lastId;
                bool unreadNavigationOk = chat.UnreadMessagesCount >= 0 && chat.UnreadReactions?.Count >= 0 || chat.UnreadReactions == null;

                Log.Information(
                    "Perf live QA result: scrollTo={ScrollTo}; selection={Selection}; previous={Previous}; previousAnchor={PreviousAnchor}; next={Next}; unreadNavigation={Unread}; initial={InitialCount} first={FirstId} last={LastId} afterPrevious={AfterPreviousCount}/{AfterPreviousFirstId} afterNext={AfterNextCount}/{AfterNextLastId}; boundaryReady={BoundaryReady}; boundaryHasHolder={BoundaryHasHolder}; boundaryHolderReady={BoundaryHolderReady}; boundaryCanScroll={BoundaryCanScroll}; boundaryPrevFlag={BoundaryPrevFlag}; boundaryNextFlag={BoundaryNextFlag}; boundaryTrigger={BoundaryTrigger}; boundarySkip={BoundarySkip}; boundaryStarted={BoundaryStarted}; boundaryLoaded={BoundaryLoaded}; boundaryAnchorId={BoundaryAnchorId}; boundaryUserDelta={BoundaryUserDelta:F1}; boundaryDrift={BoundaryDrift:F2}px; boundaryRestore={RestoreOldOffset:F1}/{RestoreOldHeight:F1}->{RestoreFinalOffset:F1}/{RestoreFinalHeight:F1}; boundaryOffset={BoundaryOffset:F1}/{BoundaryExtent:F1}",
                    scrollToOk,
                    selectionOk,
                    previousOk,
                    boundary.AnchorStable,
                    nextOk,
                    unreadNavigationOk,
                    initialCount,
                    firstId,
                    lastId,
                    afterPreviousCount,
                    afterPreviousFirstId,
                    afterNextCount,
                    afterNextLastId,
                    boundary.Ready,
                    boundary.HasHolder,
                    boundary.HolderReady,
                    boundary.CanChangeScroll,
                    boundary.WasPreviousLoadFlagSet,
                    boundary.WasNextLoadFlagSet,
                    boundary.TriggerAccepted,
                    boundary.TriggerSkipReason,
                    boundary.Started,
                    boundary.PreviousLoaded,
                    boundary.AnchorId,
                    boundary.UserOffsetDelta,
                    boundary.AnchorDriftPx,
                    boundary.RestoreOldOffset,
                    boundary.RestoreOldHeight,
                    boundary.RestoreFinalOffset,
                    boundary.RestoreFinalHeight,
                    boundary.FinalOffset,
                    boundary.FinalExtent);
            } catch (Exception ex) {
                Log.Error(ex, "Perf live QA failed.");
            }
        }

        private void TryOpenPerfSettings() {
            if (!DemoMode.IsEnabled || !App.HasCmdLineValue("perf-open-settings")) return;

            Dispatcher.UIThread.Post(async () => {
                Log.Information("Opening settings for perf/demo smoke.");
                SettingsWindow settings = new SettingsWindow();
                await settings.ShowDialog(this);
            }, DispatcherPriority.Background);
        }

        private void TryOpenPerfNewsFeed() {
            if (!DemoMode.IsEnabled || !App.HasCmdLineValue("perf-open-newsfeed")) return;

            Dispatcher.UIThread.Post(async () => {
                Log.Information("Opening news feed for perf/demo smoke.");
                await LeftNav.NavigationRouter.NavigateToAsync(new NewsFeedView());
            }, DispatcherPriority.Background);
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e) {
            SaveWindowParameters();
        }

        private void MainWindow_PositionChanged(object sender, PixelPointEventArgs e) {
            SaveWindowParameters();
        }

        private void SessionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
            VKSession session = sender as VKSession;
            if (e.PropertyName == nameof(VKSession.Name)) SetSessionNameInWindowTitle(session.Name);
            if (e.PropertyName == nameof(VKSession.IsSplitViewOpen)) CheckAdaptivity(Bounds.Width);
        }

        private void SetSessionNameInWindowTitle(string name) {
            string appName = "Laney";
            string statusSuffix = GetAutoStatusTitleSuffix();
            string displayName = String.IsNullOrWhiteSpace(statusSuffix) ? name : $"{name} · {statusSuffix}";
#if RELEASE
            Title = $"{displayName} - {appName}";
            if (DemoMode.IsEnabled) {
                Title += $" (demo mode)";
            }
#elif BETA
            Title = $"{displayName} - {appName} beta";
            if (DemoMode.IsEnabled) {
                Title += $" (demo mode)";
            }
#else
            Title = $"{displayName} - {appName} dev";
#endif
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e) {
            e.Cancel = true;
            SaveWindowParameters();
            Hide();

            // Clear RAM.
            MessageViewModel.ClearCache();
            BitmapManager.ClearCachedImages();
        }

        private void Settings_SettingChanged(string key, object value) {
            switch (key) {
                case Settings.DEBUG_FPS:
                    RendererDiagnostics.DebugOverlays = (bool)value ? RendererDebugOverlays.Fps : RendererDebugOverlays.None;
                    break;
                case Settings.DEBUG_COUNTERS_RAM:
                    RAMInfoOverlay.IsVisible = (bool)value;
                    ToggleRAMInfoOverlay();
                    break;
                case Settings.CHAT_LIST_WIDTH:
                    ApplyChatListWidth(Convert.ToDouble(value));
                    break;
                case Settings.SHOW_ACCOUNT_RAIL:
                    ApplyAccountRailVisibility();
                    break;
                case Settings.APP_ICON_VARIANT:
                    ApplyWindowIcon();
                    break;
                default:
                    if (key == Settings.ACCENT_COLOR || key == Settings.BuildAccountAccentKey(Session?.Id ?? 0)) {
                        AppearanceManager.ApplyAccountAppearanceSettings(Session);
                    }

                    if (AutoStatusManager.IsAutoStatusSettingKey(key)) ToggleAutoStatusTimer();
                    if (key == Settings.AUTO_LOCK_ENABLED || key == Settings.AUTO_LOCK_IDLE_MINUTES) ToggleAutoLockTimer();
                    break;
            }
        }

        private void SaveWindowParameters() {
            if (WindowState == WindowState.Maximized) {
                Settings.Set(Settings.WIN_MAXIMIZED, true);
                return;
            } else {
                if (Position.X <= Width * -1) return; // workaround for strange bug maybe caused by last version of Avalonia
                Settings.SetBatch(new Dictionary<string, object> {
                    { Settings.WIN_SIZE_W, Width },
                    { Settings.WIN_SIZE_H, Height },
                    { Settings.WIN_POS_X, Position.X },
                    { Settings.WIN_POS_Y, Position.Y },
                    { Settings.WIN_MAXIMIZED, WindowState == WindowState.Maximized }
                });
            }
        }

        #region Auto status

        private void MainWindow_UserActivity(object? sender, PointerEventArgs e) {
            lastUserActivity = DateTimeOffset.Now;
            if (lastAutoStatusWasIdle) UpdateAutoStatusUI();
        }

        private void ToggleAutoStatusTimer() {
            UpdateAutoStatusUI();

            if (!Settings.AutoStatusEnabled) {
                if (autoStatusTimer != null) autoStatusTimer.Stop();
                return;
            }

            if (autoStatusTimer == null) {
                autoStatusTimer = new DispatcherTimer {
                    Interval = TimeSpan.FromSeconds(30)
                };
                autoStatusTimer.Tick += (a, b) => UpdateAutoStatusUI();
            }
            autoStatusTimer.Start();
        }

        private void ToggleAutoLockTimer() {
            if (!Settings.AutoLockEnabled) {
                if (autoLockTimer != null) autoLockTimer.Stop();
                return;
            }

            if (autoLockTimer == null) {
                autoLockTimer = new DispatcherTimer {
                    Interval = TimeSpan.FromSeconds(15)
                };
                autoLockTimer.Tick += (a, b) => TryAutoLockAsync();
            }
            autoLockTimer.Start();
        }

        private async void TryAutoLockAsync() {
            if (!Settings.AutoLockEnabled || PanicLockOverlay.IsVisible) return;

            TimeSpan idle = DateTimeOffset.Now - lastUserActivity;
            if (idle < TimeSpan.FromMinutes(Settings.AutoLockIdleMinutes)) return;

            await LockClientAsync(false, Settings.AutoLockClearClipboard);
        }

        private void UpdateAutoStatusUI() {
            AutoStatusState state = AutoStatusManager.GetCurrent(lastUserActivity, DateTime.Now);
            lastAutoStatusWasIdle = state.IsActive && state.Reason.StartsWith("простой", StringComparison.Ordinal);
            AutoStatusOverlay.IsVisible = state.IsActive;
            AutoStatusText.Text = state.IsActive ? $"{state.Title} · {state.Reason}" : String.Empty;
            if (Session != null) SetSessionNameInWindowTitle(Session.Name);
        }

        private string GetAutoStatusTitleSuffix() {
            AutoStatusState state = AutoStatusManager.GetCurrent(lastUserActivity, DateTime.Now);
            return state.IsActive ? state.Title : String.Empty;
        }

        #endregion

        private void StartMemoryPressureTimer() {
            if (memoryPressureTimer != null) {
                memoryPressureTimer.Start();
                return;
            }

            memoryPressureTimer = new DispatcherTimer {
                Interval = TimeSpan.FromSeconds(8)
            };
            memoryPressureTimer.Tick += (a, b) => BitmapManager.TrimForMemoryPressure();
            memoryPressureTimer.Start();
        }

        #region RAM info

        DispatcherTimer ramTimer = null;
        private void ToggleRAMInfoOverlay() {
            if (Settings.ShowRAMUsage) {
                UpdateRAMUsageInfo();
                if (ramTimer == null) {
                    ramTimer = new DispatcherTimer {
                        Interval = TimeSpan.FromMilliseconds(500)
                    };
                    ramTimer.Tick += (a, b) => UpdateRAMUsageInfo();
                }
                ramTimer.Start();
            } else {
                if (ramTimer != null) {
                    ramTimer.Stop();
                }
            }
        }

        private void UpdateRAMUsageInfo() {
            Process process = Process.GetCurrentProcess();
            double workingSetMb = process.WorkingSet64 / 1048576d;
            double privateMb = process.PrivateMemorySize64 / 1048576d;
            BitmapCacheSnapshot cache = BitmapManager.GetCacheSnapshot();
            double cacheMb = cache.SizeBytes / 1048576d;
            double cacheLimitMb = cache.LimitBytes / 1048576d;
            RAMInfo.Text = $"{ChatViewModel.Instances} chats | {MessageViewModel.Instances} msgs | {ELOR.VKAPILib.Objects.Message.Instances} API msgs | WS {Math.Round(workingSetMb, 0)} / P {Math.Round(privateMb, 0)} Mb | img {Math.Round(cacheMb, 1)}/{Math.Round(cacheLimitMb, 0)} Mb ({cache.EntryCount}, bg {cache.BackgroundCount}, load {cache.LoadingCount})";
        }

        #endregion

        #region Adaptivity and convsview / chatview navigation

        bool isWide = false;
        bool isRightSideDisplaying = false;

        private void ApplyChatListWidth(double width) {
            Root.ColumnDefinitions[ChatListColumnIndex].Width = new GridLength(Math.Clamp(width, ChatListMinWidth, ChatListMaxWidth));
        }

        private void RefreshAccountRail() {
            AccountRailList.ItemsSource = null;
            AccountRailList.ItemsSource = VKSession.Sessions;
            ApplyAccountRailVisibility();
        }

        private void ApplyAccountRailVisibility() {
            bool visible = Settings.ShowAccountRail && isWide && VKSession.Sessions.Count > 1;
            AccountRail.IsVisible = visible;
            Root.ColumnDefinitions[AccountRailColumnIndex].Width = visible ? GridLength.Auto : new GridLength(0);
        }

        private void AccountRailButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e) {
            if ((sender as Control)?.DataContext is VKSession session) {
                VKSession.TryOpenSessionWindow(session.Id);
            }
        }

        private void ApplyWindowIcon() {
            try {
                WindowIcon icon = AssetsManager.GetWindowIconFromUri(AssetsManager.GetAppIconUri(), "avares://laney/Assets/Logo/icon.ico");
                if (icon != null) Icon = icon;
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot apply app icon variant.");
            }
        }

        private void ChatListSplitter_PointerPressed(object sender, PointerPressedEventArgs e) {
            if (!isWide) return;
            PointerPoint point = e.GetCurrentPoint(this);
            if (!point.Properties.IsLeftButtonPressed) return;

            isChatListSplitterDragging = true;
            e.Pointer.Capture(Separator);
            e.Handled = true;
        }

        private void ChatListSplitter_PointerMoved(object sender, PointerEventArgs e) {
            if (!isChatListSplitterDragging) return;

            double railWidth = AccountRail.IsVisible ? AccountRail.Bounds.Width : 0;
            ApplyChatListWidth(e.GetPosition(Root).X - railWidth);
            e.Handled = true;
        }

        private void ChatListSplitter_PointerReleased(object sender, PointerReleasedEventArgs e) {
            if (!isChatListSplitterDragging) return;

            isChatListSplitterDragging = false;
            e.Pointer.Capture(null);
            Settings.ChatListWidth = Root.ColumnDefinitions[ChatListColumnIndex].Width.Value;
            e.Handled = true;
        }

        private void CheckAdaptivity(double width) {
            isWide = width >= 720;

            if (!isWide) {
                AccountRail.IsVisible = false;
                Grid.SetColumn(LeftNav, 0);
                Grid.SetColumnSpan(LeftNav, 4);
                Grid.SetColumn(ChatViewContainer, 0);
                Grid.SetColumnSpan(ChatViewContainer, 4);
                Separator.IsVisible = false;

                LeftNav.IsVisible = !isRightSideDisplaying;
                ChatViewContainer.IsVisible = isRightSideDisplaying;
            } else {
                ApplyAccountRailVisibility();
                Grid.SetColumn(LeftNav, ChatListColumnIndex);
                Grid.SetColumnSpan(LeftNav, 1);
                Grid.SetColumn(ChatViewContainer, ChatColumnIndex);
                Grid.SetColumnSpan(ChatViewContainer, 1);
                Separator.IsVisible = true;

                LeftNav.IsVisible = true;
                ChatViewContainer.IsVisible = true;
            }

            ChatView.ChangeBackButtonVisibility(!isWide);
            SecondaryChatView.ChangeBackButtonVisibility(!isWide || Session?.IsSplitViewOpen == true);
        }

        public void SwitchToSide(bool toRight) {
            isRightSideDisplaying = toRight;
            CheckAdaptivity(Bounds.Width);
        }

        private void ChatView_BackButtonClick(object? sender, EventArgs e) {
            if (ReferenceEquals(sender, SecondaryChatView)) {
                Session.CloseSecondaryChat();
                return;
            }

            Session.GoToChat(0);
        }

        #endregion

        #region Mini audio player

        private void MainMAP_Click(object sender, EventArgs e) {
            AudioPlayerWindow.ShowForMainInstance();
        }

        private void MainMAP_CloseButtonClick(object sender, EventArgs e) {
            AudioPlayerViewModel.CloseMainInstance();
        }

        #endregion
    }
}
