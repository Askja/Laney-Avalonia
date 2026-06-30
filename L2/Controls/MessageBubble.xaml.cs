using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ELOR.Laney.Controls.Attachments;
using ELOR.Laney.Core;
using ELOR.Laney.Core.Localization;
using ELOR.Laney.Extensions;
using ELOR.Laney.Helpers;
using ELOR.Laney.ViewModels.Controls;
using ELOR.Laney.Views.Modals;
using ELOR.VKAPILib.Objects;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using VKUI.Controls;

namespace ELOR.Laney.Controls {
    public class MessageBubble : TemplatedControl {

        #region Properties

        public static readonly StyledProperty<MessageViewModel> MessageProperty =
            AvaloniaProperty.Register<MessageBubble, MessageViewModel>(nameof(Message));

        public MessageViewModel Message {
            get => GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        #endregion

        #region Internal

        bool IsOutgoing => Message.IsOutgoing;
        bool IsChat => Message.PeerId.IsChat();

#if RELEASE
#elif BETA
#else
        public MessageBubble() {
            if (Settings.MessageRenderingLogs && Message == null) {
                Log.Verbose($"> MessageBubble init.");
            } else if (Settings.MessageRenderingLogs) {
                Log.Verbose($"> MessageBubble init. ({Message.PeerId}_{Message.ConversationMessageId})");
            }
        }
#endif

        #endregion

        #region Constants

        const string BACKGROUND_INCOMING = "IncomingMessageBackground";
        const string BACKGROUND_OUTGOING = "OutgoingMessageBackground";
        const string BACKGROUND_GIFT = "GiftMessageBackground";
        const string BACKGROUND_TRANSPARENT = "TransparentMessageBackground";
        const string BACKGROUND_MENTIONED_ME = "MentionedMeMessageBackground";
        const string BACKGROUND_FAVORITE_MENTION = "FavoriteMentionMessageBackground";
        const string BACKGROUND_KEYWORD = "KeywordMessageBackground";

        const string MSG_INCOMING = "Incoming";
        const string MSG_OUTGOING = "Outgoing";

        const string INDICATOR_DEFAULT = "DefaultIndicator";
        const string INDICATOR_IMAGE = "ImageIndicator";
        const string INDICATOR_GIFT = "GiftIndicator";
        const string INDICATOR_COMPLEX_IMAGE = "ComplexImageIndicator";
        const string INDICATOR_OUTGOING = "OutgoingIndicator";

        const int MAX_DISPLAYED_FORWARDED_MESSAGES = 2;

        public const double STORY_WIDTH = 200;
        public const double BUBBLE_FIXED_WIDTH = 320;
        public const double STICKER_WIDTH = 168; // 168 в макете figma vk ipad, 176 — в vk ios, 
                                                 // 184 — android, 148 — android with reply

        #endregion

        #region Template elements

        Grid BubbleRoot;
        Border BubbleBackground;
        Button AvatarButton;
        Avatar SenderAvatar;
        TextBlock SenderName;
        Button ReplyMessageButton;
        GiftUI Gift;
        Grid MessageTextHost;
        SelectableTextBlock SelectableMessageText;
        TextBlock RichMessageText;
        AttachmentsContainer MessageAttachments;
        Rectangle Map;
        Border ForwardedMessagesContainer;
        StackPanel ForwardedMessagesStack;
        BotKeyboardUI BotKeyboard;
        Border ReactionsContainer;
        ItemsControl ReactionsList;
        Border IndicatorContainer;
        TextBlock LocalReactionIndicator;
        TextBlock TimeIndicator;
        VKIcon StateIndicator;
        Ellipse ReadIndicator;

        bool isUILoaded = false;
        protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
            if (Settings.MessageRenderingLogs) Log.Verbose($"> MessageBubble OnApplyTemplate exec. ({Message.PeerId}_{Message.ConversationMessageId})");
            if (Settings.MessageRenderingLogs) Debug.WriteLine($"Msg bubble {Message?.PeerId}_{Message?.ConversationMessageId}");

            base.OnApplyTemplate(e);
            BubbleRoot = e.NameScope.Find<Grid>(nameof(BubbleRoot));
            BubbleBackground = e.NameScope.Find<Border>(nameof(BubbleBackground));
            AvatarButton = e.NameScope.Find<Button>(nameof(AvatarButton));
            SenderAvatar = e.NameScope.Find<Avatar>(nameof(SenderAvatar));
            SenderName = e.NameScope.Find<TextBlock>(nameof(SenderName));
            ReplyMessageButton = e.NameScope.Find<Button>(nameof(ReplyMessageButton));
            Gift = e.NameScope.Find<GiftUI>(nameof(Gift));
            MessageTextHost = e.NameScope.Find<Grid>(nameof(MessageTextHost));
            SelectableMessageText = e.NameScope.Find<SelectableTextBlock>(nameof(SelectableMessageText));
            RichMessageText = e.NameScope.Find<TextBlock>(nameof(RichMessageText));
            MessageAttachments = e.NameScope.Find<AttachmentsContainer>(nameof(MessageAttachments));
            Map = e.NameScope.Find<Rectangle>(nameof(Map));
            ForwardedMessagesContainer = e.NameScope.Find<Border>(nameof(ForwardedMessagesContainer));
            ForwardedMessagesStack = e.NameScope.Find<StackPanel>(nameof(ForwardedMessagesStack));
            BotKeyboard = e.NameScope.Find<BotKeyboardUI>(nameof(BotKeyboard));
            ReactionsContainer = e.NameScope.Find<Border>(nameof(ReactionsContainer));
            ReactionsList = e.NameScope.Find<ItemsControl>(nameof(ReactionsList));
            IndicatorContainer = e.NameScope.Find<Border>(nameof(IndicatorContainer));
            LocalReactionIndicator = e.NameScope.Find<TextBlock>(nameof(LocalReactionIndicator));
            TimeIndicator = e.NameScope.Find<TextBlock>(nameof(TimeIndicator));
            StateIndicator = e.NameScope.Find<VKIcon>(nameof(StateIndicator));
            ReadIndicator = e.NameScope.Find<Ellipse>(nameof(ReadIndicator));

            double mapWidth = BUBBLE_FIXED_WIDTH - 8;
            Map.Width = mapWidth;
            Map.Height = mapWidth / 2;

            IndicatorContainer.SizeChanged += BubbleRoot_SizeChanged;
            ReactionsList.Tag = new RelayCommand(SendOrDeleteReaction);

            AvatarButton.Click += AvatarButton_Click;
            ReplyMessageButton.Click += ReplyMessageButton_Click;

            AvatarButton.PointerPressed += SuppressClickEvent;
            ReplyMessageButton.PointerPressed += SuppressClickEvent;
            MessageAttachments.PointerPressed += SuppressClickEvent;
            Map.PointerPressed += SuppressClickEvent;

            isUILoaded = true;
            Settings.SettingChanged += Settings_SettingChanged;
            RenderElement();

            Unloaded += MessageBubble_Unloaded;
        }

        private void MessageBubble_Unloaded(object sender, Avalonia.Interactivity.RoutedEventArgs e) {
            if (Message != null) {
                Message.PropertyChanged -= Message_PropertyChanged;
                Message.MessageEdited -= Message_MessageEdited;

                if (Settings.MessageRenderingLogs) Debug.WriteLine($"Message bubble UI for {Message.PeerId}_{Message.ConversationMessageId} is unloaded");
            } else {
                if (Settings.MessageRenderingLogs) Debug.WriteLine($"Message bubble UI is unloaded");
            }

            AvatarButton.Click -= AvatarButton_Click;
            ReplyMessageButton.Click -= ReplyMessageButton_Click;

            AvatarButton.PointerPressed -= SuppressClickEvent;
            ReplyMessageButton.PointerPressed -= SuppressClickEvent;
            MessageAttachments.PointerPressed -= SuppressClickEvent;
            Map.PointerPressed -= SuppressClickEvent;
            Settings.SettingChanged -= Settings_SettingChanged;
            Unloaded -= MessageBubble_Unloaded;

            Message = null;
            BubbleRoot.Children.Clear();
        }

        private void Settings_SettingChanged(string key, object value) {
            if (Message == null) return;

            if (key == $"{Settings.PEER_LOCAL_MESSAGE_REACTIONS_PREFIX}{Message.PeerId}") {
                Dispatcher.UIThread.Post(UpdateLocalReactionIndicator);
                return;
            }

            if (key == Settings.MESSAGE_CHECKMARK_STYLE) {
                Dispatcher.UIThread.Post(ChangeUI);
                return;
            }

            if (IsMessageAppearanceSettingKey(key)) {
                Dispatcher.UIThread.Post(RenderElement);
            }
        }

        private bool IsMessageAppearanceSettingKey(string key) {
            long peerId = Message?.PeerId ?? 0;
            return key == Settings.STREAMER_MODE
                || key == Settings.APP_FONT_FAMILY
                || key == Settings.MESSAGE_FONT_SIZE
                || key == Settings.MESSAGE_BUBBLE_WIDTH
                || key == Settings.MESSAGE_BUBBLE_DENSITY
                || key == Settings.MESSAGE_BUBBLE_STYLE
                || key == Settings.MESSAGE_BUBBLE_OPACITY
                || key == Settings.MESSAGE_BUBBLE_AUTO_COLOR
                || key == Settings.EMOJI_PACK
                || key == Settings.EMOJI_CUSTOM_PACK_PATH
                || key == $"{Settings.PEER_LOCAL_DENSITY_PREFIX}{peerId}"
                || key == $"{Settings.PEER_LOCAL_FONT_PREFIX}{peerId}"
                || key == $"{Settings.PEER_LOCAL_BUBBLE_COLOR_PREFIX}{peerId}"
                || key == $"{Settings.PEER_LOCAL_BUBBLE_STYLE_PREFIX}{peerId}"
                || key == $"{Settings.PEER_LOCAL_EMOJI_PACK_PREFIX}{peerId}";
        }

        // Это чтобы событие нажатия не доходили до родителей (особенно к ListBox)
        private void SuppressClickEvent(object sender, Avalonia.Input.PointerPressedEventArgs e) {
            e.Handled = true;
        }

        #endregion

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
            base.OnPropertyChanged(change);

            if (change.Property == MessageProperty) {
                if (change.OldValue is MessageViewModel oldm) {
                    oldm.PropertyChanged -= Message_PropertyChanged;
                    oldm.MessageEdited -= Message_MessageEdited;
                }
                if (Message == null) {
                    IsVisible = false;
                    return;
                }
                IsVisible = true;
                Message.PropertyChanged += Message_PropertyChanged;
                Message.MessageEdited += Message_MessageEdited;
                RenderElement();
            }
        }

        private void Message_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(MessageViewModel.Text):
                case nameof(MessageViewModel.DisplayText):
                    if (isUILoaded && Message.CanShowInUI) {
                        if (Settings.MessageRenderingLogs) Log.Verbose($">> MessageBubble: {Message.PeerId}_{Message.ConversationMessageId} Message.Text prop changed.");
                        UpdateText();
                        UpdateBubbleHighlightClasses();
                        if (Settings.MessageRenderingLogs) Log.Verbose($"<< MessageBubble: {Message.PeerId}_{Message.ConversationMessageId} Message.Text prop changed.");
                    }
                    break;
                case nameof(MessageViewModel.State):
                case nameof(MessageViewModel.IsImportant):
                case nameof(MessageViewModel.EditTime):
                case nameof(MessageViewModel.IsSenderNameVisible):
                case nameof(MessageViewModel.IsSenderAvatarVisible):
                    if (Settings.MessageRenderingLogs) Log.Verbose($">> MessageBubble: {Message.PeerId}_{Message.ConversationMessageId} Message.IsSenderAvatarVisible prop changed.");
                    ChangeUI();
                    if (Settings.MessageRenderingLogs) Log.Verbose($"<< MessageBubble: {Message.PeerId}_{Message.ConversationMessageId} Message.IsSenderAvatarVisible prop changed.");
                    break;
            }
        }

        private void Message_MessageEdited(object sender, EventArgs e) {
            RenderElement();
        }

        private void RenderElement() {
            if (!isUILoaded || !Message.CanShowInUI) return;

            if (Settings.MessageRenderingLogs) Log.Verbose($">> MessageBubble: {Message.PeerId}_{Message.ConversationMessageId} rendering...");
            var sw = Stopwatch.StartNew();

            // Outgoing
            BubbleRoot.HorizontalAlignment = IsOutgoing ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            BubbleRoot.Classes.RemoveAll([MSG_INCOMING, MSG_OUTGOING]);
            BubbleRoot.Classes.Add(IsOutgoing ? MSG_OUTGOING : MSG_INCOMING);
            MessageAttachments.IsOutgoing = IsOutgoing;

            bool streamerMode = Settings.StreamerMode;
            MessageUIType uiType = streamerMode ? MessageUIType.Standart : Message.UIType;
            bool hasReply = !streamerMode && Message.ReplyMessage != null;
            bool singleImage = IsSingleImageLayout(uiType, hasReply);

            // Bubble background
            var bbc = BubbleBackground.Classes;
            bbc.Clear();
            if (singleImage) {
                bbc.Add(BACKGROUND_TRANSPARENT);
                //} else if ((uiType == MessageUIType.Sticker || uiType == MessageUIType.Graffiti) && hasReply) {
                //    bbc.Add(BACKGROUND_BORDER);
            } else if (uiType == MessageUIType.Gift) {
                bbc.Add(BACKGROUND_GIFT);
            } else {
                bbc.Add(IsOutgoing ? BACKGROUND_OUTGOING : BACKGROUND_INCOMING);
            }
            UpdateBubbleHighlightClasses();

            // Other classes
            var acc = MessageAttachments.Classes;
            acc.Clear();
            if (uiType == MessageUIType.Gift) {
                // acc.Add(MSG_GIFT);
            } else {
                acc.Add(IsOutgoing ? MSG_OUTGOING : MSG_INCOMING);
            }
            acc.Add(Constants.ATCHC_INBUBBLE);

            var rct = ReactionsContainer.Classes;
            rct.Clear();
            rct.Add(IsOutgoing ? MSG_OUTGOING : MSG_INCOMING);

            // Avatar
            AvatarButton.IsVisible = IsChat && !IsOutgoing && !streamerMode;

            // Sender name
            SenderName.IsVisible = !singleImage && Message.IsSenderNameVisible;

            // Message bubble width
            if (uiType == MessageUIType.Sticker) {
                // при BACKGROUND_BORDER у стикера будет отступ в 8px по сторонам.
                BubbleRoot.Width = hasReply ? STICKER_WIDTH + 16 : STICKER_WIDTH;
            } else if (uiType == MessageUIType.Story || uiType == MessageUIType.StoryWithSticker) {
                BubbleRoot.Width = STORY_WIDTH;
            } else if (uiType == MessageUIType.Graffiti) {
                // при BACKGROUND_BORDER у граффити будет отступ в 8px по сторонам.
                BubbleRoot.Width = hasReply ? BUBBLE_FIXED_WIDTH : BUBBLE_FIXED_WIDTH - 8;
            } else if (uiType == MessageUIType.Complex) {
                BubbleRoot.Width = BUBBLE_FIXED_WIDTH;
            } else {
                BubbleRoot.Width = Double.NaN;
            }

            // Attachments margin
            double amargin = 0;
            if (!hasReply) {
                if (uiType == MessageUIType.Story || uiType == MessageUIType.Sticker || uiType == MessageUIType.StoryWithSticker) {
                    amargin = -8;
                } else if (uiType == MessageUIType.SingleImage || uiType == MessageUIType.Graffiti) {
                    amargin = -4;
                }
            }
            MessageAttachments.Margin = new Thickness(amargin, 0, amargin, amargin);

            // Story UI
            MessageAttachments.NeedToShowStoryPreview = !streamerMode && (Message.UIType == MessageUIType.Story || Message.UIType == MessageUIType.StoryWithSticker);

            // Attachments
            MessageAttachments.Owner = streamerMode ? PrivacyMask.HiddenSenderName : Message.SenderName;
            MessageAttachments.Attachments = streamerMode ? new List<Attachment>() : Message.Attachments;
            MessageAttachments.IsVisible = !streamerMode && Message.Attachments.Count > 0;
            ReplyMessageButton.IsVisible = hasReply;
            BotKeyboard.IsVisible = !streamerMode && Message.Keyboard != null;

            // Forwarded messages
            ForwardedMessagesStack.Children.Clear();
            ForwardedMessagesContainer.IsVisible = !streamerMode && Message.ForwardedMessages?.Count > 0;
            var fmcmargin = ForwardedMessagesContainer.Margin;
            var fmcborder = ForwardedMessagesContainer.BorderThickness;
            var fmsmargin = ForwardedMessagesStack.Margin;
            double fmwidth = fmcmargin.Left + fmcmargin.Right + fmcborder.Left + fmsmargin.Left;

            if (!streamerMode && Message.ForwardedMessages?.Count > 0) {
                ForwardedMessagesStack.Children.Add(new ContentControl {
                    ContentTemplate = App.Current.GetCommonTemplate("ForwardedMessagesInfoTemplate"),
                    Content = Localizer.GetDeclensionFormatted(Message.ForwardedMessages.Count, "forwarded_message"),
                    Margin = new Thickness(0, 0, 0, -4),
                    Height = 16
                });
                int visibleForwardedCount = Math.Min(Message.ForwardedMessages.Count, MAX_DISPLAYED_FORWARDED_MESSAGES);
                foreach (var message in Message.ForwardedMessages.Take(visibleForwardedCount)) {
                    AddForwardedMessage(message, BubbleRoot.Width - fmwidth);
                }

                if (Message.ForwardedMessages.Count > visibleForwardedCount) {
                    int collapsedCount = Message.ForwardedMessages.Count - visibleForwardedCount;
                    Button moreButton = new Button {
                        Classes = { "Tertiary" },
                        Padding = new Thickness(0),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        ContentTemplate = App.Current.GetCommonTemplate("ForwardedMessagesInfoTemplateAccent"),
                        Content = Localizer.GetDeclensionFormatted(collapsedCount, "forwarded_message_more")
                    };
                    moreButton.Click += async (a, b) => {
                        StandaloneMessageViewer smv = new StandaloneMessageViewer(Message.OwnerSession, Message.ForwardedMessages);
                        await smv.ShowDialog(Message.OwnerSession.ModalWindow);
                    };
                    ForwardedMessagesStack.Children.Add(moreButton);
                }
            }

            // Gift
            var mtm = MessageTextHost.Margin;
            if (!streamerMode && Message.Gift != null) {
                Gift.Gift = Message.Gift;
                Gift.HorizontalAlignment = String.IsNullOrEmpty(Message.Text) ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
                Gift.Margin = new Thickness(4, 4, 4, String.IsNullOrEmpty(Message.Text) ? 12 : 0);
                Gift.IsVisible = true;
                SelectableMessageText.TextAlignment = TextAlignment.Center;
                RichMessageText.TextAlignment = TextAlignment.Center;
                MessageTextHost.Margin = new Thickness(mtm.Left, mtm.Top, mtm.Right, 12);
            } else {
                Gift.IsVisible = false;
                SelectableMessageText.TextAlignment = TextAlignment.Left;
                RichMessageText.TextAlignment = TextAlignment.Left;
                MessageTextHost.Margin = new Thickness(mtm.Left, mtm.Top, mtm.Right, 8);
            }

            // Text
            UpdateText();

            // Map
            if (!streamerMode && Message.Location != null) {
                var glong = Message.Location.Coordinates.Longitude.ToString().Replace(",", ".");
                var glat = Message.Location.Coordinates.Latitude.ToString().Replace(",", ".");
                var w = Map.Width * App.Current.DPI;
                var h = Map.Height * App.Current.DPI;
                Map.SetImageFill(new Uri($"https://static-maps.yandex.ru/1.x/?ll={glong},{glat}&size={w},{h}&z=12&lang=ru_RU&l=pmap&pt={glong},{glat},vkbkm"), Map.Width, Map.Height);
            }
            Map.IsVisible = !streamerMode && Message.Location != null;

            // Time & indicator class & reactions panel
            UpdateIndicatorsUI(uiType, hasReply);

            // UI
            ChangeUI();

            sw.Stop();
            if (Settings.MessageRenderingLogs) Debug.WriteLine($"Msg bubble {Message?.PeerId}_{Message?.ConversationMessageId} rendered!");
            if (Settings.MessageRenderingLogs) Log.Verbose($"<< MessageBubble: {Message.PeerId}_{Message.ConversationMessageId} rendered. ({sw.ElapsedMilliseconds} ms.)");
            if (sw.ElapsedMilliseconds > (1000.0 / 30.0)) {
                Log.Warning($"MessageBubble: rendering {Message.PeerId}_{Message.ConversationMessageId} took too long! ({sw.ElapsedMilliseconds} ms.)");
            }
        }

        private void AddForwardedMessage(Message message, double width) {
            ForwardedMessagesStack.Children.Add(new ForwardedMessage {
                Width = width,
                Message = message,
                SnippetMessageClick = async () => {
                    if (Message.HasMoreNestedMessage) {
                        await GetFullMessageAndShowForwardedAsync(message.PeerId, message.ConversationMessageId);
                    } else {
                        StandaloneMessageViewer smv = new StandaloneMessageViewer(Message.OwnerSession, message);
                        await smv.ShowDialog(Message.OwnerSession.ModalWindow);
                    }
                }
            });
        }

        private static bool IsSingleImageLayout(MessageUIType uiType, bool hasReply) {
            return uiType == MessageUIType.SingleImage
                || uiType == MessageUIType.Story
                || uiType == MessageUIType.StoryWithSticker
                || (uiType == MessageUIType.Sticker && !hasReply)
                || (uiType == MessageUIType.Graffiti && !hasReply);
        }

        private async Task GetFullMessageAndShowForwardedAsync(long peerId, int cmid) {
            try {
                var session = Message.OwnerSession;
                VKUIWaitDialog<MessagesList> wd = new VKUIWaitDialog<MessagesList>();
                MessagesList response = await wd.ShowAsync(session.ModalWindow, session.API.Messages.GetByConversationMessageIdAsync(0, Message.PeerId, new List<int> { Message.ConversationMessageId }));

                if (response.Items.Count > 0) {
                    FindInForwardedAndShowMessage(response.Items.FirstOrDefault(), ref peerId, ref cmid);
                }
            } catch (Exception ex) {
                await ExceptionHelper.ShowErrorDialogAsync(Message.OwnerSession.ModalWindow, ex);
            }
        }

        private void FindInForwardedAndShowMessage(Message message, ref long peerId, ref int cmid) {
            if (message.ForwardedMessages == null || message.ForwardedMessages.Count == 0) return;

            foreach (var curMsg in CollectionsMarshal.AsSpan(message.ForwardedMessages)) {
                if (curMsg.PeerId == peerId && curMsg.ConversationMessageId == cmid) {
                    new System.Action(async () => {
                        await Task.Delay(32); // required!
                        StandaloneMessageViewer smv = new StandaloneMessageViewer(Message.OwnerSession, curMsg);
                        await smv.ShowDialog(Message.OwnerSession.ModalWindow);
                    })();
                    return;
                }
                FindInForwardedAndShowMessage(curMsg, ref peerId, ref cmid);
            }
        }

        private void BubbleRoot_SizeChanged(object sender, SizeChangedEventArgs e) {
            double indicatorsWidth = IndicatorContainer.DesiredSize.Width;
            if (Settings.MessageRenderingLogs) Debug.WriteLine($"IC Width: {indicatorsWidth}");

            var rcm = ReactionsContainer.Margin;
            ReactionsContainer.Margin = new Thickness(rcm.Left, rcm.Top, indicatorsWidth, rcm.Bottom);
        }

        private void UpdateText() {
            if (Settings.StreamerMode) {
                SetText(PrivacyMask.HiddenMessage);
                return;
            }

            SetText(Message.UIType == MessageUIType.Empty ? Assets.i18n.Resources.empty_message : Message.DisplayText);
        }

        private void SetText(string text) {
            if (Settings.MessageRenderingLogs) Log.Verbose($">>> MessageBubble: {Message.PeerId}_{Message.ConversationMessageId} setting text...");
            string selectableText = TextParser.GetParsedText(text);
            MessageTextHost.IsVisible = !String.IsNullOrEmpty(selectableText);
            SelectableMessageText.Classes.Remove("Empty");
            RichMessageText.Classes.Remove("Empty");

            if (String.IsNullOrEmpty(selectableText)) {
                SelectableMessageText.ClearValue(TextBlock.FontFamilyProperty);
                SelectableMessageText.Text = String.Empty;
                SelectableMessageText.IsVisible = false;
                RichMessageText.Inlines?.Clear();
                RichMessageText.ClearValue(TextBlock.FontFamilyProperty);
                RichMessageText.Text = String.Empty;
                RichMessageText.IsVisible = false;
                return;
            }

            bool isEmptyMessage = String.Equals(text, Assets.i18n.Resources.empty_message, StringComparison.Ordinal);
            bool richEmoji = !isEmptyMessage && MessageEmojiInlineRenderer.TryApply(RichMessageText, selectableText, Message.PeerId);
            if (richEmoji) {
                SelectableMessageText.ClearValue(TextBlock.FontFamilyProperty);
                RichMessageText.ClearValue(TextBlock.FontFamilyProperty);
                RichMessageText.Text = String.Empty;
                SelectableMessageText.Text = String.Empty;
                SelectableMessageText.IsVisible = false;
                RichMessageText.IsVisible = true;
            } else {
                SelectableMessageText.ClearValue(TextBlock.FontFamilyProperty);
                RichMessageText.Inlines?.Clear();
                RichMessageText.ClearValue(TextBlock.FontFamilyProperty);
                RichMessageText.Text = String.Empty;
                RichMessageText.IsVisible = false;
                SelectableMessageText.Text = selectableText;
                SelectableMessageText.IsVisible = true;
            }

            if (isEmptyMessage) {
                SelectableMessageText.Classes.Add("Empty");
                RichMessageText.Classes.Add("Empty");
            }

            if (Settings.MessageRenderingLogs) Log.Verbose($"<<< MessageBubble: {Message.PeerId}_{Message.ConversationMessageId} text rendered.");
        }

        private void UpdateBubbleHighlightClasses() {
            if (BubbleBackground == null || Message == null) return;

            var classes = BubbleBackground.Classes;
            classes.RemoveAll([BACKGROUND_MENTIONED_ME, BACKGROUND_FAVORITE_MENTION, BACKGROUND_KEYWORD]);

            string highlightClass = GetBubbleHighlightClass();
            if (!String.IsNullOrEmpty(highlightClass)) classes.Add(highlightClass);
        }

        private string GetBubbleHighlightClass() {
            if (Settings.StreamerMode || Message.IsOutgoing) return null;

            string text = Message.DisplayText;
            long sessionId = Message.OwnerSession?.Id ?? VKSession.Main?.Id ?? 0;
            if (sessionId > 0 && ContainsUserMention(text, sessionId)) return BACKGROUND_MENTIONED_ME;
            if (ContainsFavoriteUserMention(text)) return BACKGROUND_FAVORITE_MENTION;
            if (ContainsHighlightKeyword(text)) return BACKGROUND_KEYWORD;

            return null;
        }

        private static bool ContainsUserMention(string text, long userId) {
            if (String.IsNullOrWhiteSpace(text) || userId <= 0) return false;

            foreach (var match in CompiledRegularExpressions.UserMention().Matches(text).Cast<System.Text.RegularExpressions.Match>()) {
                if (long.TryParse(match.Groups[2].Value, out long mentionedId) && mentionedId == userId) return true;
            }
            return false;
        }

        private static bool ContainsFavoriteUserMention(string text) {
            if (String.IsNullOrWhiteSpace(text)) return false;

            foreach (var match in CompiledRegularExpressions.UserMention().Matches(text).Cast<System.Text.RegularExpressions.Match>()) {
                if (!long.TryParse(match.Groups[2].Value, out long mentionedId)) continue;

                User user = CacheManager.GetUser(mentionedId);
                if (user?.IsFavorite == 1) return true;
            }
            return false;
        }

        private static bool ContainsHighlightKeyword(string text) {
            string keywords = Settings.DontAnnoyMeKeywords;
            if (String.IsNullOrWhiteSpace(text) || String.IsNullOrWhiteSpace(keywords)) return false;

            string[] tokens = keywords.Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string token in tokens) {
                if (token.Length > 0 && text.Contains(token, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private void UpdateLocalReactionIndicator() {
            if (LocalReactionIndicator == null || Message == null) return;

            string reaction = Settings.StreamerMode
                ? String.Empty
                : Settings.GetLocalMessageReaction(Message.PeerId, Message.ConversationMessageId);
            LocalReactionIndicator.Text = String.IsNullOrWhiteSpace(reaction) ? String.Empty : $"{reaction} L";
            LocalReactionIndicator.IsVisible = !String.IsNullOrWhiteSpace(reaction);
        }

        private void OnLinkClicked(string link) {
            new System.Action(async () => await Router.LaunchLink(VKSession.GetByDataContext(this), link))();
        }

        private void SendOrDeleteReaction(object obj) {
            if (obj == null || obj is not int) return;

            var session = VKSession.GetByDataContext(this);
            int picked = Convert.ToInt32(obj);
            long peerId = Message.PeerId;
            int cmid = Message.ConversationMessageId;
            bool remove = Message.SelectedReactionId == picked;

            new System.Action(async () => {
                try {
                    bool response = remove
                        ? await session.API.Messages.DeleteReactionAsync(session.GroupId, peerId, cmid)
                        : await session.API.Messages.SendReactionAsync(session.GroupId, peerId, cmid, picked);
                    if (!remove && response) Settings.PromotePeerQuickReactionId(peerId, picked, CacheManager.AvailableReactions?.Take(6));
                } catch (Exception ex) {
                    string str = remove ? "remove" : "send";
                    Log.Error(ex, $"Failed to {str} reaction to message {peerId}_{cmid}!");
                    await ExceptionHelper.ShowErrorDialogAsync(session?.Window, ex, true);
                }
            })();
        }

        // Смена некоторых частей UI сообщения, которые не влияют
        // в целом на само облачко.
        // Конечно, можно и через TemplateBinding такие вещи делать,
        // но code-behind лучше.
        private void ChangeUI() {
            if (!isUILoaded || !Message.CanShowInUI) return;

            if (Settings.MessageRenderingLogs) Log.Verbose($">>> MessageBubble: {Message.PeerId}_{Message.ConversationMessageId} exec ChangeUI...");

            bool streamerMode = Settings.StreamerMode;
            MessageUIType uiType = streamerMode ? MessageUIType.Standart : Message.UIType;
            bool hasReply = !streamerMode && Message.ReplyMessage != null;
            int attachmentsCount = streamerMode ? 0 : Message.Attachments.Count;
            int forwardedCount = streamerMode ? 0 : (Message.ForwardedMessages?.Count ?? 0);
            bool hasText = !String.IsNullOrEmpty(streamerMode ? PrivacyMask.HiddenMessage : Message.Text);

            // Avatar visibility
            SenderAvatar.Opacity = !streamerMode && Message.IsSenderAvatarVisible ? 1 : 0;
            SenderName.IsVisible = !IsSingleImageLayout(uiType, hasReply) && Message.IsSenderNameVisible;

            UpdateIndicatorsUI(uiType, hasReply);

            // Message state
            var state = Message.State;
            ReadIndicator.IsVisible = !IsOutgoing && state == MessageVMState.Unread;
            ApplyStateIndicator(state);

            // Time & is edited
            TimeIndicator.Text = Message.SentTime.ToString("H:mm");
            UpdateLocalReactionIndicator();

            // Reply msg button margin-top
            double replyTopMargin = Message.IsSenderNameVisible ? 6 : 10;
            var rmm = ReplyMessageButton.Margin;
            ReplyMessageButton.Margin = new Thickness(rmm.Left, replyTopMargin, rmm.Right, rmm.Bottom);

            // Text margin-top
            double textTopMargin = Message.IsSenderNameVisible || hasReply || (!streamerMode && Message.Gift != null) ? 2 : 8;
            bool needsIndicatorClearance = uiType == MessageUIType.Standart
                && attachmentsCount == 0
                && forwardedCount == 0
                && (Message.Reactions == null || Message.Reactions.Count == 0);
            double textBottomMargin = forwardedCount > 0 && attachmentsCount == 0 ? 4 : 8;
            if (needsIndicatorClearance) textBottomMargin = Math.Max(textBottomMargin, 28);
            var mtm = MessageTextHost.Margin;
            MessageTextHost.Margin = new Thickness(mtm.Left, textTopMargin, mtm.Right, textBottomMargin);

            // Attachments margin-top
            double atchTopMargin = 0;

            if ((uiType == MessageUIType.Complex || (uiType == MessageUIType.Standart && attachmentsCount > 0)) && !hasReply && !hasText) {
                atchTopMargin = Message.ImagesCount > 0 ? 4 : 8;
            }
            var mam = MessageAttachments.Margin;
            MessageAttachments.Margin = new Thickness(mam.Left, atchTopMargin, mam.Right, mam.Bottom);

            // Map margin-top
            double mapTopMargin = Message.IsSenderNameVisible || hasReply || hasText || attachmentsCount > 0 ? 0 : 4;
            var mapm = Map.Margin;
            Map.Margin = new Thickness(mapm.Left, mapTopMargin, mapm.Right, mapm.Bottom);

            // Forwarded messages margin-top
            double fwdTopMargin = hasText || attachmentsCount > 0 ? 0 : 8;
            double fwdBottomMargin = Message.Reactions?.Count > 0 ? 4 : 22;
            var fwm = ForwardedMessagesContainer.Margin;
            ForwardedMessagesContainer.Margin = new Thickness(fwm.Left, fwdTopMargin, fwm.Right, fwdBottomMargin);

            // Reactions margin-top
            double rtop = uiType != MessageUIType.Standart || (!streamerMode && Message.Keyboard != null) ? 8 : 0;
            double rside = uiType == MessageUIType.SingleImage || uiType == MessageUIType.Graffiti || (uiType == MessageUIType.Sticker && !hasReply) ? 0 : 12;
            var rcm = ReactionsContainer.Margin;
            ReactionsContainer.Margin = new Thickness(rside, rtop, rcm.Right, rcm.Bottom);

            if (Settings.MessageRenderingLogs) Log.Verbose($"<<< MessageBubble: {Message.PeerId}_{Message.ConversationMessageId} ChangeUI completed.");
        }

        private void ApplyStateIndicator(MessageVMState state) {
            string style = Settings.MessageCheckmarkStyle;
            StateIndicator.Margin = style == MessageCheckmarkStyleIds.Compact
                ? new Thickness(3, 0, 0, 0)
                : new Thickness(4, 0, 0, 0);

            switch (state) {
                case MessageVMState.Unread:
                    StateIndicator.IsVisible = IsOutgoing && style != MessageCheckmarkStyleIds.Hidden;
                    StateIndicator.Width = StateIndicator.Height = style == MessageCheckmarkStyleIds.Minimal ? 12 : 16;
                    StateIndicator.Id = style == MessageCheckmarkStyleIds.Compact ? VKIconNames.Icon20Check : VKIconNames.Icon16CheckOutline;
                    break;
                case MessageVMState.Read:
                    StateIndicator.IsVisible = IsOutgoing && style != MessageCheckmarkStyleIds.Hidden;
                    StateIndicator.Width = StateIndicator.Height = style == MessageCheckmarkStyleIds.Minimal ? 12 : 16;
                    StateIndicator.Id = style == MessageCheckmarkStyleIds.Compact ? VKIconNames.Icon20CheckCircleOn : VKIconNames.Icon16CheckDoubleOutline;
                    break;
                case MessageVMState.Loading:
                    StateIndicator.IsVisible = true;
                    StateIndicator.Width = StateIndicator.Height = 12;
                    StateIndicator.Id = VKIconNames.Icon16ClockOutline;
                    break;
                case MessageVMState.Deleted:
                    StateIndicator.IsVisible = true;
                    StateIndicator.Width = StateIndicator.Height = 12;
                    StateIndicator.Id = VKIconNames.Icon16DeleteOutline;
                    break;
            }
        }

        private void UpdateIndicatorsUI(MessageUIType uiType, bool hasReply) {
            IndicatorContainer.Classes.RemoveAll([INDICATOR_DEFAULT, INDICATOR_IMAGE, INDICATOR_COMPLEX_IMAGE, INDICATOR_GIFT, INDICATOR_OUTGOING]);
            if (IsOutgoing) IndicatorContainer.Classes.Add(INDICATOR_OUTGOING);
            if (uiType == MessageUIType.Gift) {
                IndicatorContainer.Classes.Add(INDICATOR_GIFT);
            } else if (uiType == MessageUIType.StoryWithSticker || uiType == MessageUIType.SingleImage || uiType == MessageUIType.Story) {
                IndicatorContainer.Classes.Add(INDICATOR_IMAGE);
            } else if (uiType == MessageUIType.Sticker || uiType == MessageUIType.Graffiti) {
                if (hasReply) {
                    if (Message.Reactions?.Count > 0) {
                        IndicatorContainer.Classes.Add(INDICATOR_DEFAULT);
                        Grid.SetRow(IndicatorContainer, 2);
                    } else {
                        IndicatorContainer.Classes.Add(INDICATOR_COMPLEX_IMAGE);
                        Grid.SetRow(IndicatorContainer, 0);
                    }
                } else {
                    IndicatorContainer.Classes.Add(INDICATOR_IMAGE);
                }
                IndicatorContainer.Classes.Add(hasReply ? INDICATOR_COMPLEX_IMAGE : INDICATOR_IMAGE);
            } else if (uiType == MessageUIType.Complex &&
                (Message.ImagesCount == Message.Attachments.Count || Message.Location != null) &&
                Message.ForwardedMessages.Count == 0) {
                if (Message.Reactions?.Count > 0) {
                    IndicatorContainer.Classes.Add(INDICATOR_DEFAULT);
                    Grid.SetRow(IndicatorContainer, 2);
                } else {
                    IndicatorContainer.Classes.Add(INDICATOR_COMPLEX_IMAGE);
                    Grid.SetRow(IndicatorContainer, 0);
                }
            } else {
                IndicatorContainer.Classes.Add(INDICATOR_DEFAULT);
                if (Message.Reactions?.Count > 0) {
                    Grid.SetRow(IndicatorContainer, 2);
                } else {
                    Grid.SetRow(IndicatorContainer, 1);
                }
            }
        }

        #region Template events

        private void AvatarButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e) {
            new System.Action(async () => await Router.OpenPeerProfileAsync(Message.OwnerSession, Message.SenderId))();
        }

        private void ReplyMessageButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e) {
            // Message.OwnerSession.CurrentOpenedChat.GoToMessage(Message.ReplyMessage);
            Message.OwnerSession.GoToMessage(Message.ReplyMessage);
        }

        #endregion
    }
}
