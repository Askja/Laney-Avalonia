using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ELOR.Laney.Controls;
using ELOR.Laney.Core;
using ELOR.Laney.Extensions;
using ELOR.Laney.Helpers;
using ELOR.Laney.ViewModels;
using ELOR.Laney.ViewModels.Controls;
using ELOR.Laney.Views.Modals;
using ELOR.VKAPILib.Objects;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using VKUI.Controls;

namespace ELOR.Laney.Views {
    public sealed partial class ChatView : UserControl, IMainWindowRightView {
        ChatViewModel Chat { get; set; }
        Window OwnerWindow => TopLevel.GetTopLevel(this) as Window;

        ScrollViewer MessagesListScrollViewer;
        DispatcherTimer markReadTimer;
        readonly System.Action<Avalonia.Styling.ThemeVariant> themeChangedAction;
        bool autoScrollToLastMessageQueued = false;
        bool suppressHopNavUntilScrollSettled = false;
        bool frameMonitorRunning = false;
        long lastFrameTicks = 0;
        long lastVisualTreeSampleTicks = 0;
        int lastVisibleControlCount = 0;
        double lastFrameMs = 0;
        double averageFrameMs = 0;
        double maxFrameMs = 0;
        int jankFrames = 0;

        MessageViewModel FirstVisible { get => MessagesList?.GetFirstVisibleItem<MessageViewModel>(); }
        MessageViewModel LastVisible { get => MessagesList?.GetLastVisibleItem<MessageViewModel>(); }

        public ChatView() {
            InitializeComponent();
            MultiMsgContextButton.CommandParameter = MultiMsgContextButton;
            themeChangedAction = (_) => ApplyChatAppearance();
            App.Current.ThemeChangedActions.Add(themeChangedAction);
            DetachedFromVisualTree += ChatView_DetachedFromVisualTree;

            MessagesList.Loaded += (a, b) => {
                MessagesListScrollViewer = MessagesList.Scroll as ScrollViewer;
                MessagesListScrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
                MessagesListScrollViewer.PropertyChanged += MessagesListScrollViewer_PropertyChanged;

                if (!Settings.DisableMarkingMessagesAsRead) {
                    markReadTimer = new DispatcherTimer {
                        Interval = TimeSpan.FromSeconds(1)
                    };
                    markReadTimer.Tick += MarkReadTimer_Tick;
                    markReadTimer.Start();
                }

                new ItemsPresenterWidthFixer(MessagesList);

                OwnerWindow.Activated += OwnerWindow_Activated;
                if (Settings.ShowDebugCounters) StartFrameMonitor();
            };

            BackButton.Click += (a, b) => BackButtonClick?.Invoke(this, null);
            DataContextChanged += ChatView_DataContextChanged;
            LoadingSkeleton.PropertyChanged += LoadingSkeleton_PropertyChanged;

            DebugOverlay.IsVisible = Settings.ShowDebugCounters;
            Settings.SettingChanged += Settings_SettingChanged;
            UpdateHeaderActionButtonsVisibility();

            Root.AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
            DropArea.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
            DropArea.AddHandler(DragDrop.DropEvent, OnDrop);

            TopDropArea.AddHandler(DragDrop.DragEnterEvent, OnDragEnterIntoArea);
            BottomDropArea.AddHandler(DragDrop.DragEnterEvent, OnDragEnterIntoArea);
            TopDropArea.AddHandler(DragDrop.DragLeaveEvent, OnDragLeaveFromArea);
            BottomDropArea.AddHandler(DragDrop.DragLeaveEvent, OnDragLeaveFromArea);
            TopDropArea.AddHandler(DragDrop.DropEvent, OnDropOnArea);
            BottomDropArea.AddHandler(DragDrop.DropEvent, OnDropOnArea);
        }

        private void Settings_SettingChanged(string key, object value) {
            switch (key) {
                case Settings.DEBUG_COUNTERS_CHAT:
                    SetDebugOverlayVisible((bool)value);
                    break;
                case Settings.CHAT_HEADER_ACTION_SEARCH:
                case Settings.CHAT_HEADER_ACTION_PROFILE:
                case Settings.CHAT_HEADER_ACTION_MORE:
                    UpdateHeaderActionButtonsVisibility();
                    break;
                case Settings.STREAMER_MODE:
                    Chat?.RefreshStreamerMode();
                    break;
                case Settings.THEME:
                case Settings.CHAT_BACKGROUND:
                case Settings.CHAT_BACKGROUND_IMAGE:
                case Settings.APP_FONT_FAMILY:
                case Settings.MESSAGE_FONT_SIZE:
                case Settings.MESSAGE_BUBBLE_WIDTH:
                case Settings.MESSAGE_BUBBLE_DENSITY:
                case Settings.MESSAGE_BUBBLE_STYLE:
                case Settings.MESSAGE_BUBBLE_OPACITY:
                case Settings.MESSAGE_BUBBLE_AUTO_COLOR:
                    ApplyChatAppearance();
                    break;
            }

            if (Chat != null && IsCurrentChatAppearanceKey(key)) ApplyChatAppearance();
        }

        private void ChatView_DetachedFromVisualTree(object sender, VisualTreeAttachmentEventArgs e) {
            StopFrameMonitor();
            App.Current.ThemeChangedActions.Remove(themeChangedAction);
            Settings.SettingChanged -= Settings_SettingChanged;
        }

        private void SetDebugOverlayVisible(bool isVisible) {
            DebugOverlay.IsVisible = isVisible;
            if (isVisible) {
                StartFrameMonitor();
            } else {
                StopFrameMonitor();
            }
        }

        private void UpdateHeaderActionButtonsVisibility() {
            SearchInChatButton.IsVisible = Settings.ChatHeaderActionSearch;
            HeaderProfileButton.IsVisible = Settings.ChatHeaderActionProfile;
            ContextMenuInChatButton.IsVisible = Settings.ChatHeaderActionMore;
        }

        private void OwnerWindow_Activated(object sender, EventArgs e) {
            new System.Action(async () => {
                await Chat?.UpdateOnlineMembersCountAsync();
            })();
        }

        public event EventHandler BackButtonClick;
        public void ChangeBackButtonVisibility(bool isVisible) {
            BackButton.IsVisible = isVisible;
        }

        public void FocusComposer() {
            if (Chat == null || RestrictionReason.IsVisible || SelectedMessagesCommandsAreVisible()) return;
            ComposerControl.FocusMessageText();
        }

        public void OpenAttachmentPicker() {
            if (Chat == null || RestrictionReason.IsVisible || SelectedMessagesCommandsAreVisible()) return;
            ComposerControl.ShowAttachmentPicker();
        }

        public void OpenStickerPicker() {
            if (Chat == null || RestrictionReason.IsVisible || SelectedMessagesCommandsAreVisible()) return;
            ComposerControl.ShowStickerPicker();
        }

        public void OpenQuickActions() {
            if (Chat == null || RestrictionReason.IsVisible || SelectedMessagesCommandsAreVisible()) return;
            ComposerControl.ShowQuickActions();
        }

        public void SetComposerText(string text) {
            if (Chat?.Composer == null || RestrictionReason.IsVisible || SelectedMessagesCommandsAreVisible()) return;

            Chat.Composer.Text = text ?? String.Empty;
            Chat.Composer.TextSelectionStart = Chat.Composer.Text.Length;
            Chat.Composer.TextSelectionEnd = Chat.Composer.Text.Length;
            FocusComposer();
        }

        public void OpenSearchInChat() {
            if (Chat == null || DemoMode.IsEnabled) return;

            Window mainWindow = TopLevel.GetTopLevel(this) as Window;
            SearchInChatWindow window = new SearchInChatWindow(VKSession.GetByDataContext(this), Chat.PeerId, mainWindow);
            window.Show();
        }

        private bool SelectedMessagesCommandsAreVisible() {
            return MessagesCommandsRoot?.IsVisible == true;
        }

        private void ChatView_DataContextChanged(object sender, EventArgs e) {
            if (MessagesListScrollViewer != null) MessagesListScrollViewer.ScrollChanged -= ScrollViewer_ScrollChanged;
            if (Chat != null) {
                Chat.ReceivedMessages.CollectionChanged -= ReceivedMessages_CollectionChanged;
            }

            Chat = DataContext as ChatViewModel;
            if (Chat != null) {
                Chat.ReceivedMessages.CollectionChanged += ReceivedMessages_CollectionChanged;
            }
            ApplyChatAppearance();

            new System.Action(async () => {
                await Dispatcher.UIThread.InvokeAsync(() => {
                    if (MessagesListScrollViewer != null) MessagesListScrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
                });
            })();
        }

        private bool IsCurrentChatAppearanceKey(string key) {
            return key == $"{Settings.PEER_LOCAL_THEME_PREFIX}{Chat.PeerId}"
                || key == $"{Settings.PEER_LOCAL_BACKGROUND_IMAGE_PREFIX}{Chat.PeerId}"
                || key == $"{Settings.PEER_LOCAL_BACKGROUND_DIM_PREFIX}{Chat.PeerId}"
                || key == $"{Settings.PEER_LOCAL_BACKGROUND_BLUR_PREFIX}{Chat.PeerId}"
                || key == $"{Settings.PEER_LOCAL_BACKGROUND_BRIGHTNESS_PREFIX}{Chat.PeerId}"
                || key == $"{Settings.PEER_LOCAL_ACCENT_PREFIX}{Chat.PeerId}"
                || key == $"{Settings.PEER_LOCAL_DENSITY_PREFIX}{Chat.PeerId}"
                || key == $"{Settings.PEER_LOCAL_FONT_PREFIX}{Chat.PeerId}"
                || key == $"{Settings.PEER_LOCAL_BUBBLE_COLOR_PREFIX}{Chat.PeerId}"
                || key == $"{Settings.PEER_LOCAL_BUBBLE_STYLE_PREFIX}{Chat.PeerId}";
        }

        private void ApplyChatAppearance() {
            long peerId = Chat?.PeerId ?? 0;
            bool isDarkPalette = ResolveChatPaletteDark();
            ApplyChatPaletteResources(isDarkPalette);

            Avalonia.Media.IBrush chatBackground = AppearanceManager.GetChatBackgroundBrush(Chat, isDarkPalette);
            Root.Resources[AppearanceManager.ChatBackgroundResourceKey] = chatBackground;
            ChatBackgroundColor.Background = chatBackground;
            MessagesList.Background = Avalonia.Media.Brushes.Transparent;
            Uri backgroundImageUri = AppearanceManager.GetChatBackgroundImageUri(peerId);
            bool hasBackgroundImage = backgroundImageUri != null;
            ChatBackgroundImage.IsVisible = hasBackgroundImage;
            int backgroundBlur = hasBackgroundImage ? Math.Min(AppearanceManager.GetChatBackgroundBlurRadius(peerId), Settings.LowMemoryMode ? 0 : 6) : 0;
            ImageLoader.SetBackgroundBlurRadius(ChatBackgroundImage, backgroundBlur);
            ImageLoader.SetBackgroundSource(ChatBackgroundImage, backgroundImageUri);
            ChatBackgroundImage.Opacity = hasBackgroundImage ? AppearanceManager.GetChatBackgroundImageOpacity(peerId) : 0;

            double dimOpacity = AppearanceManager.GetChatBackgroundDimOpacity(peerId);
            ChatBackgroundDim.Opacity = dimOpacity;
            ChatBackgroundDim.IsVisible = dimOpacity > 0;

            double brightnessOpacity = AppearanceManager.GetChatBackgroundBrightnessOpacity(peerId);
            ChatBackgroundBrightness.Opacity = brightnessOpacity;
            ChatBackgroundBrightness.IsVisible = brightnessOpacity > 0;
            AppearanceManager.ApplyChatAccentResources(Root.Resources, peerId);
            Root.Resources[AppearanceManager.MessageOuterMarginResourceKey] = AppearanceManager.GetMessageOuterMargin(peerId);
            Root.Resources[AppearanceManager.MessageTextHostMarginResourceKey] = AppearanceManager.GetMessageTextHostMargin(peerId);
            Root.Resources[AppearanceManager.MessageTextFontSizeResourceKey] = AppearanceManager.GetMessageTextFontSize(peerId);
            Root.Resources[AppearanceManager.MessageTextLineHeightResourceKey] = AppearanceManager.GetMessageTextLineHeight(peerId);
            Root.Resources[AppearanceManager.MessageBubbleMaxWidthResourceKey] = AppearanceManager.GetMessageBubbleMaxWidth();
            Root.Resources[AppearanceManager.MessageBubbleCornerRadiusResourceKey] = AppearanceManager.GetMessageBubbleCornerRadius(peerId);
            Root.Resources[AppearanceManager.MessageBubbleBorderThicknessResourceKey] = AppearanceManager.GetMessageBubbleBorderThickness(peerId);
            Root.Resources[AppearanceManager.MessageBubbleBorderBrushResourceKey] = AppearanceManager.GetMessageBubbleBorderBrush(peerId);
            Root.Resources[AppearanceManager.MessageBubbleBackgroundOpacityResourceKey] = AppearanceManager.GetMessageBubbleBackgroundOpacity();
            Avalonia.Media.IBrush outgoingBubbleBrush = AppearanceManager.GetOutgoingBubbleBrush(Chat, isDarkPalette);
            Root.Resources[AppearanceManager.MessageBubbleOutgoingBrushResourceKey] = outgoingBubbleBrush;
            Root.Resources["MessageBubbleOutgoingTextPrimaryBrush"] = AppearanceManager.GetReadableTextBrush(outgoingBubbleBrush);
            Root.Resources["MessageBubbleOutgoingTextSecondaryBrush"] = AppearanceManager.GetReadableTextBrush(outgoingBubbleBrush, true);
        }

        private bool ResolveChatPaletteDark() {
            if (AppearanceManager.TryIsDarkBrush(BottomPanel?.Background, out bool isDark)) return isDark;
            if (AppearanceManager.TryIsDarkBrush(Root?.Background, out isDark)) return isDark;
            return AppearanceManager.IsDarkTheme();
        }

        private void ApplyChatPaletteResources(bool isDarkPalette) {
            Root.Resources["LaneySkeletonBrush"] = AppearanceManager.GetSkeletonBrush(isDarkPalette);
            Root.Resources["MessageBubbleDefaultIncomingBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(isDarkPalette ? "#D92A2D31" : "#EFFFFFFF"));
            Root.Resources["NestedMessageBackgroundBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(isDarkPalette ? "#A0223143" : "#BFE8F2FF"));
        }

        private void ReceivedMessages_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            if (MessagesList?.IsScrollOperationInProgress == true) return;
            if (e.Action != NotifyCollectionChangedAction.Add || !autoScrollToLastMessage || Chat?.DisplayedMessages == null || Chat.DisplayedMessages.Count == 0) return;
            QueueScrollToLastMessage(sender as ObservableCollection<MessageViewModel>);
        }

        private void QueueScrollToLastMessage(ObservableCollection<MessageViewModel> received) {
            if (autoScrollToLastMessageQueued) return;
            autoScrollToLastMessageQueued = true;
            Dispatcher.UIThread.Post(async () => await ScrollToLastMessageIfNeededAsync(received));
        }

        private async Task ScrollToLastMessageIfNeededAsync(ObservableCollection<MessageViewModel> received) {
            try {
                await Task.Delay(16);
                VKSession session = VKSession.GetByDataContext(this);
                if (session?.Window?.IsActive != true) return;
                if (MessagesList?.IsScrollOperationInProgress == true) return;
                if (!autoScrollToLastMessage || received == null || Chat?.DisplayedMessages == null || Chat.DisplayedMessages.Count == 0) return;

                int lastReceivedId = received.LastOrDefault()?.ConversationMessageId ?? 0;
                MessageViewModel lastDisplayed = Chat.DisplayedMessages.Last;
                int lastDisplayedId = lastDisplayed?.ConversationMessageId ?? 0;
                if (lastReceivedId == lastDisplayedId && lastDisplayedId > 0) {
                    await MessagesList.ScrollToBottomStableAsync(lastDisplayed, 8);

                    // После loading-состояния нужно ещё раз дожать вниз, когда высота bubble станет финальной.
                    if (Settings.ShowDebugCounters) Log.Information($"Need to scroll to last message again. Message id: {lastDisplayedId}");
                    if (lastDisplayed.State == MessageVMState.Loading) lastDisplayed.PropertyChanged += LastDisplayedMsgPropertyChanged;
                }
            } finally {
                autoScrollToLastMessageQueued = false;
            }
        }

        private void LastDisplayedMsgPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            new System.Action(async () => {
                if (e.PropertyName == nameof(MessageViewModel.State)) {
                    MessageViewModel msg = sender as MessageViewModel;
                    msg.PropertyChanged -= LastDisplayedMsgPropertyChanged;

                    await Task.Delay(10); // нужно, ибо без этого высота скролла старая.

                    int lastReceivedId = Chat.ReceivedMessages.LastOrDefault()?.ConversationMessageId ?? 0;
                    if (msg.ConversationMessageId == lastReceivedId) {
                        // Принудительно скроллим вниз
                        //double h = MessagesListScrollViewer.Extent.Height - MessagesListScrollViewer.DesiredSize.Height;
                        //ForceScroll(h);
                        await MessagesList.ScrollToBottomStableAsync(msg, 10);

                        Log.Information($"Scroll to message \"{msg.ConversationMessageId}\" done.");
                    }
                }
            })();
        }

        //private void ForceScroll(double y) {
        //    new System.Action(async () => {
        //        byte retries = 0;
        //        while (MessagesListScrollViewer.Offset.Y < y - 2 || MessagesListScrollViewer.Offset.Y > y + 2) {
        //            MessagesListScrollViewer.Offset = new Vector(0, y);
        //            await Task.Yield();
        //            retries++;
        //            if (retries >= 5) break;
        //        }
        //    })();
        //}

        bool autoScrollToLastMessage = false;
        bool visibleMessagesCheckQueued = false;
        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e) {
            autoScrollToLastMessage = MessagesList?.IsScrollOperationInProgress != true && IsNearBottom(96);
            QueueFirstAndLastDisplayedMessagesCheck();
        }

        private void MessagesListScrollViewer_PropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e) {
            if (e.Property == ScrollViewer.ExtentProperty) QueueFirstAndLastDisplayedMessagesCheck();
        }

        private async void HopNavButton_Click(object sender, RoutedEventArgs e) {
            suppressHopNavUntilScrollSettled = true;
            HopNavContainer.IsVisible = false;

            try {
                if (Chat != null) await Chat.GoToLastMessageAsync();
                MessageViewModel lastDisplayed = Chat?.DisplayedMessages?.LastOrDefault();
                await MessagesList.ScrollToBottomStableAsync(lastDisplayed, 16);
            } catch (Exception ex) {
                Log.Error(ex, "Failed to hop to chat bottom.");
            } finally {
                suppressHopNavUntilScrollSettled = false;
                CheckFirstAndLastDisplayedMessages();
            }
        }

        private void QueueFirstAndLastDisplayedMessagesCheck() {
            if (visibleMessagesCheckQueued) return;
            visibleMessagesCheckQueued = true;

            TopLevel topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) {
                visibleMessagesCheckQueued = false;
                CheckFirstAndLastDisplayedMessages();
                return;
            }

            topLevel.RequestAnimationFrame((_) => {
                visibleMessagesCheckQueued = false;
                CheckFirstAndLastDisplayedMessages();
            });
        }

        private void CheckFirstAndLastDisplayedMessages() {
            MessageViewModel fv = FirstVisible;
            if (Settings.ShowDebugCounters) {
                MessageViewModel lv = LastVisible;
                tmsgId.Text = fv?.ConversationMessageId.ToString() ?? "N/A";
                bmsgId.Text = lv?.ConversationMessageId.ToString() ?? "N/A";
                UpdateDebugOverlay();
            }

            UpdateDateUnderHeader(fv);
            if (suppressHopNavUntilScrollSettled || MessagesList?.IsScrollOperationInProgress == true) {
                HopNavContainer.IsVisible = false;
                return;
            }

            if (Chat?.DisplayedMessages?.Count > 0) {
                HopNavContainer.IsVisible = CanHopToBottom();
            } else {
                HopNavContainer.IsVisible = false;
            }
            try {
                if (MessagesListScrollViewer?.Extent.Height <= MessagesListScrollViewer?.DesiredSize.Height) HopNavContainer.IsVisible = false;
            } catch (Exception ex) {
                Log.Error(ex, "Failed to check messages list's ScrollViewer!");
                HopNavContainer.IsVisible = false;
            }
        }

        private bool CanHopToBottom() {
            if (Chat == null || MessagesListScrollViewer == null) return false;
            bool canScroll = !IsNearBottom(2);
            bool hasUndisplayedTail = Chat.LastMessage != null
                && Chat.DisplayedMessages?.LastOrDefault()?.ConversationMessageId != Chat.LastMessage.ConversationMessageId;

            if (Chat.UnreadMessagesCount > 0) return canScroll || hasUndisplayedTail;
            return canScroll;
        }

        private bool IsNearBottom(double tolerance) {
            if (MessagesListScrollViewer == null) return false;

            double viewportHeight = MessagesListScrollViewer.Viewport.Height;
            if (viewportHeight <= 0) viewportHeight = MessagesListScrollViewer.DesiredSize.Height;
            double remaining = MessagesListScrollViewer.Extent.Height - viewportHeight - MessagesListScrollViewer.Offset.Y;
            return remaining <= tolerance;
        }

        private void UpdateDateUnderHeader(MessageViewModel msg) {
            if (msg == null) {
                TopDateContainer.IsVisible = false;
                return;
            }

            TopDate.Text = msg.SentTime.ToHumanizedDateString();
            TopDateContainer.IsVisible = true;
        }

        private void UpdateDebugOverlay() {
            if (MessagesListScrollViewer == null) return;

            dbgScrV.Text = Math.Round(MessagesListScrollViewer.Viewport.Height).ToString();
            dbgScrO.Text = $"{Math.Round(MessagesListScrollViewer.Offset.Y)}/{Math.Round(MessagesListScrollViewer.Extent.Height)}";
            dbgScrAuto.Text = autoScrollToLastMessage ? "on" : "off";

            Process process = Process.GetCurrentProcess();
            dbgRam.Text = $"WS {Math.Round(process.WorkingSet64 / 1048576d, 0)} / P {Math.Round(process.PrivateMemorySize64 / 1048576d, 0)} Mb";
            dbgGc.Text = $"{GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)}";

            long now = Stopwatch.GetTimestamp();
            if (lastVisualTreeSampleTicks == 0 || (now - lastVisualTreeSampleTicks) * 1000.0 / Stopwatch.Frequency >= 1000) {
                List<Control> visibleControls = new List<Control>();
                MessagesList.FindVisualChildrenByType(visibleControls);
                lastVisibleControlCount = visibleControls.Count;
                lastVisualTreeSampleTicks = now;
            }
            dbgUi.Text = lastVisibleControlCount.ToString();
            dbgFrame.Text = $"{Math.Round(averageFrameMs, 1)}/{Math.Round(lastFrameMs, 1)}/{Math.Round(maxFrameMs, 1)} ms";
            dbgJank.Text = jankFrames.ToString();
        }

        private void StartFrameMonitor() {
            if (frameMonitorRunning) return;

            frameMonitorRunning = true;
            ResetFrameStats();
            ScheduleFrameSample();
        }

        private void StopFrameMonitor() {
            frameMonitorRunning = false;
            lastFrameTicks = 0;
        }

        private void ResetFrameStats() {
            lastFrameTicks = 0;
            lastVisualTreeSampleTicks = 0;
            lastVisibleControlCount = 0;
            lastFrameMs = 0;
            averageFrameMs = 0;
            maxFrameMs = 0;
            jankFrames = 0;
        }

        public async Task<ChatPerfScrollQaResult> RunPerfScrollQaAsync(TimeSpan duration) {
            if (MessagesListScrollViewer == null) return new ChatPerfScrollQaResult();

            bool wasRunning = frameMonitorRunning;
            ResetFrameStats();
            StartFrameMonitor();
            await Task.Delay(500);
            ResetFrameStats();

            Stopwatch stopwatch = Stopwatch.StartNew();
            double originalOffset = MessagesListScrollViewer.Offset.Y;
            double direction = -1;
            int samples = 0;

            while (stopwatch.Elapsed < duration) {
                double maxOffset = Math.Max(0, MessagesListScrollViewer.Extent.Height - MessagesListScrollViewer.Viewport.Height);
                double step = Math.Max(10, MessagesListScrollViewer.Viewport.Height / 60d);
                double nextOffset = MessagesListScrollViewer.Offset.Y + step * direction;
                if (nextOffset <= 0 || nextOffset >= maxOffset) {
                    direction *= -1;
                    nextOffset = Math.Clamp(MessagesListScrollViewer.Offset.Y + step * direction, 0, maxOffset);
                }

                MessagesListScrollViewer.Offset = new Vector(MessagesListScrollViewer.Offset.X, nextOffset);
                samples++;
                await Task.Delay(16);
            }

            await Task.Delay(120);

            List<Control> visibleControls = new List<Control>();
            MessagesList.FindVisualChildrenByType(visibleControls);
            double averageFps = averageFrameMs > 0 ? 1000d / averageFrameMs : 0;
            ChatPerfScrollQaResult result = new ChatPerfScrollQaResult {
                Samples = samples,
                AverageFrameMs = averageFrameMs,
                LastFrameMs = lastFrameMs,
                MaxFrameMs = maxFrameMs,
                AverageFps = averageFps,
                JankFrames = jankFrames,
                VisibleControls = visibleControls.Count,
                PrivateMemoryMb = Math.Round(Process.GetCurrentProcess().PrivateMemorySize64 / 1048576d, 2)
            };

            double boundedOriginalOffset = Math.Min(originalOffset, Math.Max(0, MessagesListScrollViewer.Extent.Height - MessagesListScrollViewer.Viewport.Height));
            MessagesListScrollViewer.Offset = new Vector(MessagesListScrollViewer.Offset.X, boundedOriginalOffset);
            if (!wasRunning && !DebugOverlay.IsVisible) StopFrameMonitor();
            return result;
        }

        public async Task<ChatHistoryBoundaryQaResult> RunHistoryBoundaryQaAsync(TimeSpan timeout) {
            ChatHistoryBoundaryQaResult result = new ChatHistoryBoundaryQaResult();
            if (MessagesListScrollViewer == null || Chat?.DisplayedMessages == null || Chat.DisplayedMessages.Count == 0) return result;

            Stopwatch readiness = Stopwatch.StartNew();
            while ((Chat.IsLoading || MessagesList.IsScrollOperationInProgress) && readiness.Elapsed < timeout) {
                await Task.Delay(50);
            }

            result.Ready = !Chat.IsLoading && !MessagesList.IsScrollOperationInProgress;
            result.HolderReady = MessagesList.IsHolderReady;
            result.HasHolder = MessagesList.HasCurrentHolder;
            result.CanChangeScroll = MessagesList.CanChangeScroll;
            result.WasPreviousLoadFlagSet = MessagesList.IsPreviousMessagesLoadTriggered;
            result.WasNextLoadFlagSet = MessagesList.IsNextMessagesLoadTriggered;
            result.InitialCount = Chat.DisplayedMessages.Count;
            result.BeforeFirstId = Chat.DisplayedMessages.First?.ConversationMessageId ?? 0;

            double viewportHeight = MessagesListScrollViewer.Viewport.Height;
            if (viewportHeight <= 0) viewportHeight = MessagesListScrollViewer.Bounds.Height;
            double maxOffset = Math.Max(0, MessagesListScrollViewer.Extent.Height - viewportHeight);
            if (maxOffset <= viewportHeight) return result;

            double stagingOffset = Math.Clamp(Math.Max(160, viewportHeight * 1.25), 0, maxOffset);
            MessagesList.SuppressIncrementalLoadingFor(1000);
            await SetMessagesScrollOffsetForQaAsync(stagingOffset, TimeSpan.FromMilliseconds(700));

            MessagesList.SuppressIncrementalLoadingFor(1000);
            await SetMessagesScrollOffsetForQaAsync(0, TimeSpan.FromMilliseconds(900));
            result.TriggerAccepted = MessagesList.TriggerPreviousPageLoadForCurrentViewport();
            result.TriggerSkipReason = MessagesList.LastPreviousTriggerSkipReason;
            Stopwatch stopwatch = Stopwatch.StartNew();
            bool started = false;

            while (stopwatch.Elapsed < timeout) {
                if (Chat.IsPreviousMessagesLoading) started = true;
                int currentFirstId = Chat.DisplayedMessages.First?.ConversationMessageId ?? 0;
                if (currentFirstId > 0 && result.BeforeFirstId > 0 && currentFirstId < result.BeforeFirstId) {
                    started = true;
                    break;
                }
                if (started && !Chat.IsPreviousMessagesLoading) break;
                await Task.Delay(50);
            }

            Stopwatch restoreWait = Stopwatch.StartNew();
            while (MessagesList.IsScrollOperationInProgress && restoreWait.Elapsed < timeout) {
                await Task.Delay(50);
            }

            await Task.Delay(160);
            result.Started = started;
            result.FinalCount = Chat.DisplayedMessages.Count;
            result.AfterFirstId = Chat.DisplayedMessages.First?.ConversationMessageId ?? 0;
            result.PreviousLoaded = result.AfterFirstId > 0 && result.BeforeFirstId > 0 && result.AfterFirstId < result.BeforeFirstId;
            result.TriggerSkipReason = String.IsNullOrWhiteSpace(MessagesList.LastPreviousTriggerSkipReason)
                ? result.TriggerSkipReason
                : MessagesList.LastPreviousTriggerSkipReason;
            result.AnchorId = MessagesList.LastPreviousRestoreAnchorId;
            result.UserOffsetDelta = MessagesList.LastPreviousLoadUserOffsetDelta;
            result.AnchorDriftPx = MessagesList.LastPreviousRestoreDrift;
            result.RestoreOldOffset = MessagesList.LastPreviousRestoreOldOffset;
            result.RestoreOldHeight = MessagesList.LastPreviousRestoreOldHeight;
            result.RestoreFinalOffset = MessagesList.LastPreviousRestoreFinalOffset;
            result.RestoreFinalHeight = MessagesList.LastPreviousRestoreFinalHeight;
            result.AnchorStable = !Double.IsNaN(result.AnchorDriftPx) && Math.Abs(result.AnchorDriftPx) <= 6;
            result.FinalOffset = MessagesListScrollViewer.Offset.Y;
            result.FinalExtent = MessagesListScrollViewer.Extent.Height;
            return result;
        }

        private async Task<bool> SetMessagesScrollOffsetForQaAsync(double targetOffset, TimeSpan timeout) {
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout) {
                MessagesListScrollViewer.Offset = new Vector(MessagesListScrollViewer.Offset.X, targetOffset);
                await Task.Delay(32);

                if (Math.Abs(MessagesListScrollViewer.Offset.Y - targetOffset) <= 2) return true;
            }

            return Math.Abs(MessagesListScrollViewer.Offset.Y - targetOffset) <= 4;
        }

        private void ScheduleFrameSample() {
            if (!frameMonitorRunning) return;

            TopLevel topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            topLevel.RequestAnimationFrame((t) => SampleFrame());
        }

        private void SampleFrame() {
            if (!frameMonitorRunning) return;

            long now = Stopwatch.GetTimestamp();
            if (lastFrameTicks != 0) {
                lastFrameMs = (now - lastFrameTicks) * 1000.0 / Stopwatch.Frequency;
                averageFrameMs = averageFrameMs <= 0 ? lastFrameMs : averageFrameMs * 0.9 + lastFrameMs * 0.1;
                maxFrameMs = Math.Max(lastFrameMs, maxFrameMs * 0.995);
                if (lastFrameMs > 34) jankFrames++;
            }

            lastFrameTicks = now;
            ScheduleFrameSample();
        }

        private void LoadingSkeleton_PropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e) {
            if (e.Property == StackPanel.IsVisibleProperty) {
                TopDateContainer.IsVisible = !LoadingSkeleton.IsVisible;
                HopNavContainer.IsVisible = !LoadingSkeleton.IsVisible;
                if (!LoadingSkeleton.IsVisible) CheckFirstAndLastDisplayedMessages();
            }
        }

        private void ChatView_SizeChanged(object sender, SizeChangedEventArgs e) {
            if (e.NewSize.Width >= 512) {
                MessagesCommandsRoot.Classes.Clear();
            } else {
                if (!MessagesCommandsRoot.Classes.Contains("CompactMsgCmd")) MessagesCommandsRoot.Classes.Add("CompactMsgCmd");
            }
        }

        #region Buttons events

        private void PinnedMessageButton_Click(object sender, RoutedEventArgs e) {
            new System.Action(async () => await Chat.GoToMessageAsync(Chat.PinnedMessage))();
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e) {
            OpenSearchInChat();
        }

        private void HeaderProfileButton_Click(object sender, RoutedEventArgs e) {
            if (Chat?.OpenProfileCommand?.CanExecute(null) == true) Chat.OpenProfileCommand.Execute(null);
        }

        private void ContextMenuInChatButton_Click(object sender, RoutedEventArgs e) {
            if (Chat != null) ContextMenuHelper.ShowForChat(Chat, sender as Control);
        }

        private void OnSuggestedStickerClicked(object? sender, RoutedEventArgs e) {
            Button button = sender as Button;
            Sticker sticker = button.DataContext as Sticker;
            if (sticker == null) return;

            new System.Action(async () => await Chat?.Composer.SendStickerAsync(sticker.StickerId))();
        }

        #endregion

        #region Mark message as read

        // TODO: execute (mark message and reactions as read in one request).
        private void MarkReadTimer_Tick(object sender, EventArgs e) {
            if (Chat == null) return;
            if (Settings.DisableMarkingMessagesAsRead) return;
            new System.Action(async () => {
                await TryMarkAsReadAsync(LastVisible);
                if (Chat?.UnreadReactions != null) await TryMarkReactionsAsReadAsync();
            })();
        }

        bool isMarking = false;
        private async Task TryMarkAsReadAsync(MessageViewModel msg) {
            if (DemoMode.IsEnabled) return;
            if (Settings.DisableMarkingMessagesAsRead) return;
            if (isMarking || msg == null || msg.IsOutgoing || msg.State != MessageVMState.Unread) return;
            isMarking = true;

            try {
                var session = VKSession.GetByDataContext(this);
                bool result = await session.API.Messages.MarkAsReadAsync(session.GroupId, Chat.PeerId, msg.ConversationMessageId);
                await Task.Delay(1000);
            } catch (Exception ex) {
                Log.Error(ex, $"Unable to mark message (peer: {msg.PeerId}; cmid: {msg.ConversationMessageId}) as read!");
            } finally {
                isMarking = false;
            }
        }

        bool isMarking2 = false;
        private async Task TryMarkReactionsAsReadAsync() {
            if (DemoMode.IsEnabled) return;
            if (Settings.DisableMarkingMessagesAsRead) return;
            if (isMarking2 || FirstVisible == null || LastVisible == null) return;
            var fid = FirstVisible.ConversationMessageId;
            var lid = LastVisible.ConversationMessageId;
            var visibleMessages = Chat.DisplayedMessages.Select(m => m.ConversationMessageId)
                .Where(id => id >= fid && id <= lid && Chat.UnreadReactions.Contains(id)).ToList();
            if (visibleMessages == null || visibleMessages.Count == 0) return;

            isMarking2 = true;
            try {
                Log.Information($"About to mark reactions in messages (cmids {String.Join(',', visibleMessages)}) as read...");
                var session = VKSession.GetByDataContext(this);
                bool result = await session.API.Messages.MarkReactionsAsReadAsync(session.GroupId, Chat.PeerId, visibleMessages);
                await Task.Delay(1000);
            } catch (Exception ex) {
                Log.Error(ex, $"Unable to mark reactions in messages (cmids {String.Join(',', visibleMessages)}) as read!");
            } finally {
                isMarking2 = false;
            }
        }

        #endregion

        #region Context menu for message

        private void ChatViewItem_ContextRequested(object sender, ContextRequestedEventArgs e) {
            ChatViewItem cvi = sender as ChatViewItem;
            MessageViewModel message = cvi?.DataContext as MessageViewModel;
            if (message == null) return;

            ContextMenuHelper.ShowForMessage(message, Chat, cvi);
        }

        #endregion

        #region Drag'n'drop

        private void OnDragEnter(object sender, DragEventArgs e) {
            DropArea.IsVisible = true;

            try {
                var files = e.DataTransfer.TryGetFiles();
                var type = files.GetFilesType();
                BottomDropArea.Tag = type;
                switch (type) {
                    case DroppedFilesType.OnlyPhotos:
                        Grid.SetRowSpan(TopDropArea, 1);
                        BottomDropArea.IsVisible = true;
                        BottomDropIcon.Id = VKIconNames.Icon56GalleryOutline;
                        BottomDropText.Text = Assets.i18n.Resources.drop_photos_quick;
                        TopDropText.Text = Assets.i18n.Resources.drop_photos_file;
                        break;
                    case DroppedFilesType.OnlyVideos:
                        Grid.SetRowSpan(TopDropArea, 1);
                        BottomDropArea.IsVisible = true;
                        BottomDropIcon.Id = VKIconNames.Icon56VideoOutline;
                        BottomDropText.Text = Assets.i18n.Resources.drop_videos_quick;
                        TopDropText.Text = Assets.i18n.Resources.drop_videos_file;
                        break;
                    case DroppedFilesType.Mixed:
                        Grid.SetRowSpan(TopDropArea, 2);
                        BottomDropArea.IsVisible = false;
                        TopDropText.Text = Assets.i18n.Resources.drop_without_compression_desc;
                        break;
                }
            } catch (Exception ex) {
                DropArea.IsVisible = false;
                new System.Action(async () => await ExceptionHelper.ShowErrorDialogAsync(VKSession.GetByDataContext(this).ModalWindow, ex, true))();
            }
        }

        private void OnDragLeave(object sender, DragEventArgs e) {
            DropArea.IsVisible = false;
        }

        private void OnDrop(object sender, DragEventArgs e) {
            DropArea.IsVisible = false;
        }

        private void OnDragEnterIntoArea(object sender, DragEventArgs e) {
            Border border = sender as Border;
            border.Classes.Add("DropTargetHover");
        }

        private void OnDragLeaveFromArea(object sender, DragEventArgs e) {
            Border border = sender as Border;
            border.Classes.Remove("DropTargetHover");
        }

        private void OnDropOnArea(object sender, DragEventArgs e) {
            VKSession session = VKSession.GetByDataContext(this);
            Border border = sender as Border;
            border.Classes.Remove("DropTargetHover");

            var files = e.DataTransfer.TryGetFiles().Take(10);
            if (border.Name == "TopDropArea") {
                new System.Action(async () => {
                    foreach (IStorageFile file in files) {
                        Chat.Composer.Attachments.Add(new OutboundAttachmentViewModel(session, file, Constants.FileUploadCommand));
                        await Task.Delay(500);
                    }
                })();
            } else if (border.Name == "BottomDropArea") {
                DroppedFilesType type = (DroppedFilesType)BottomDropArea.Tag;
                int utype = Constants.FileUploadCommand;
                switch (type) {
                    case DroppedFilesType.OnlyPhotos: utype = Constants.PhotoUploadCommand; break;
                    case DroppedFilesType.OnlyVideos: utype = Constants.VideoUploadCommand; break;
                    default: utype = Constants.FileUploadCommand; break;
                }

                new System.Action(async () => {
                    foreach (IStorageFile file in files) {
                        Chat.Composer.Attachments.Add(new OutboundAttachmentViewModel(session, file, utype));
                        await Task.Delay(500);
                    }
                })();
            }
        }

        #endregion
    }

    public sealed class ChatPerfScrollQaResult {
        public int Samples { get; set; }
        public double AverageFrameMs { get; set; }
        public double LastFrameMs { get; set; }
        public double MaxFrameMs { get; set; }
        public double AverageFps { get; set; }
        public int JankFrames { get; set; }
        public int VisibleControls { get; set; }
        public double PrivateMemoryMb { get; set; }
    }

    public sealed class ChatHistoryBoundaryQaResult {
        public bool Ready { get; set; }
        public bool HasHolder { get; set; }
        public bool HolderReady { get; set; }
        public bool CanChangeScroll { get; set; }
        public bool WasPreviousLoadFlagSet { get; set; }
        public bool WasNextLoadFlagSet { get; set; }
        public bool TriggerAccepted { get; set; }
        public string TriggerSkipReason { get; set; } = String.Empty;
        public bool Started { get; set; }
        public bool PreviousLoaded { get; set; }
        public bool AnchorStable { get; set; }
        public int InitialCount { get; set; }
        public int FinalCount { get; set; }
        public int BeforeFirstId { get; set; }
        public int AfterFirstId { get; set; }
        public int AnchorId { get; set; }
        public double UserOffsetDelta { get; set; }
        public double AnchorDriftPx { get; set; } = Double.NaN;
        public double RestoreOldOffset { get; set; } = Double.NaN;
        public double RestoreOldHeight { get; set; } = Double.NaN;
        public double RestoreFinalOffset { get; set; } = Double.NaN;
        public double RestoreFinalHeight { get; set; } = Double.NaN;
        public double FinalOffset { get; set; }
        public double FinalExtent { get; set; }
    }
}
