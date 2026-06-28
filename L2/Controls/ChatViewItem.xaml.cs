using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml.Templates;
using ELOR.Laney.Controls.Attachments;
using ELOR.Laney.Core;
using ELOR.Laney.Extensions;
using ELOR.Laney.ViewModels.Controls;
using ELOR.VKAPILib.Objects;
using Serilog;
using System.ComponentModel;
using System.Runtime.InteropServices;
using VKUI.Controls;

namespace ELOR.Laney.Controls {
    public class ChatViewItem : TemplatedControl {
        #region Properties

        public static readonly StyledProperty<MessageViewModel> MessageProperty =
            AvaloniaProperty.Register<ChatViewItem, MessageViewModel>(nameof(Message));

        public MessageViewModel Message {
            get => GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        #endregion

        #region Template elements

        StackPanel Root;
        MessageViewModel subscribedMessage;

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
#if RELEASE
#elif BETA
#else
            if (Settings.MessageRenderingLogs && Message == null) {
                Log.Verbose("ChatViewItem OnApplyTemplate exec.");
            } else if (Settings.MessageRenderingLogs) {
                Log.Verbose($"ChatViewItem OnApplyTemplate exec. ({Message.PeerId}_{Message.ConversationMessageId})");
            }
#endif

            base.OnApplyTemplate(e);
            Root = e.NameScope.Find<StackPanel>(nameof(Root));

            Root.Children.Clear();
            if (Message != null) {
                RenderContent(Message);
                AttachMessageEvents(Message);
            }
        }

        #endregion

        public ChatViewItem() {
            if (Settings.MessageRenderingLogs && Message == null) {
                Log.Verbose($"ChatViewItem init.");
            } else if (Settings.MessageRenderingLogs) {
                Log.Verbose($"ChatViewItem init. ({Message.PeerId}_{Message.ConversationMessageId})");
            }

            PointerPressed += ChatViewItem_PointerPressed;
            PointerReleased += ChatViewItem_PointerReleased;
            Unloaded += ChatViewItem_Unloaded;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
            base.OnPropertyChanged(change);
            if (change.Property == MessageProperty) {
                MessageViewModel old = change.OldValue as MessageViewModel;

                DetachMessageEvents(old);

                MessageViewModel newm = change.NewValue as MessageViewModel;
                AttachMessageEvents(newm);

                if (Root == null) return;
                if (change.NewValue == null) {
                    Root.Children.Clear();
                    return;
                }

                if (old != null && newm.ConversationMessageId == old.ConversationMessageId && newm.PeerId == old.PeerId) return;

                Root.Children.Clear();
                RenderContent(newm);
            }
        }

        private void AttachMessageEvents(MessageViewModel message) {
            if (message == null || ReferenceEquals(subscribedMessage, message)) return;
            DetachMessageEvents(subscribedMessage);
            subscribedMessage = message;
            subscribedMessage.PropertyChanged += MessagePropertyChanged;
        }

        private void DetachMessageEvents(MessageViewModel message) {
            if (message == null || !ReferenceEquals(subscribedMessage, message)) return;
            subscribedMessage.PropertyChanged -= MessagePropertyChanged;
            subscribedMessage = null;
        }

        private void RenderContent(MessageViewModel message) {
            if (Settings.MessageRenderingLogs) Log.Verbose($"ChatViewItem > RenderContent started.");
            // Date
            if (message.IsDateBetweenVisible) {
                DataTemplate template = App.GetResource<DataTemplate>("DateUnderTitleTemplate");
                var dateUI = template.Build(message);
                // Check template in ChatViewItem.xaml
                ((dateUI as Border).Child as TextBlock).Text = message.SentTime.ToHumanizedDateString();
                Root.Children.Add(dateUI);
            }

            // Service message
            if (message.Action != null) {
                DataTemplate template = App.GetResource<DataTemplate>("TemporaryServiceMessageTemplate");
                var serviceUI = template.Build(message);
                if (Settings.StreamerMode && serviceUI is Border serviceBorder && serviceBorder.Child is TextBlock serviceText) {
                    serviceText.Text = PrivacyMask.HiddenMessage;
                }
                Root.Children.Add(serviceUI);
            }

            // Expired message
            if (message.IsExpired) {
                DataTemplate template = App.GetResource<DataTemplate>("ExpiredMessageTemplate");
                var expiredUI = template.Build(message);
                Root.Children.Add(expiredUI);
            }

            // Message bubble
            if (message.CanShowInUI) {
                var messageUI = new MessageBubble { Message = message };
                Root.Children.Add(messageUI);
            }

            // Carousel
            if (message.Template != null) {
                if (message.Template.Type == BotTemplateType.Carousel) {
                    StackPanel items = new StackPanel {
                        Spacing = 6,
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                    };

                    foreach (CarouselElement item in CollectionsMarshal.AsSpan(message.Template.Elements)) {
                        CarouselElementUI cui = new CarouselElementUI {
                            Element = item,
                            PeerId = message.PeerId,
                            MessageId = message.GlobalId,
                            AuthorId = message.SenderId,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
                        };
                        items.Children.Add(cui);
                    }

                    CarouselEx cex = new CarouselEx {
                        ScrollPixels = 240,
                        MaxWidth = 984,
                        Margin = new Thickness(12, 0),
                        Content = new ScrollViewer {
                            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                            Content = items
                        }
                    };
                    Root.Children.Add(cex);
                }
            }

            if (Settings.MessageRenderingLogs) Log.Verbose($"ChatViewItem > RenderContent finished.");
        }

        private void MessagePropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (Root == null) return;
            switch (e.PropertyName) {
                case nameof(MessageViewModel.Action):
                case nameof(MessageViewModel.IsDateBetweenVisible):
                case nameof(MessageViewModel.IsExpired):
                case nameof(MessageViewModel.CanShowInUI):
                case nameof(MessageViewModel.Template):
                case nameof(MessageViewModel.DisplayPreviewText):
                    Root.Children.Clear();
                    RenderContent(Message);
                    break;
            }
        }

        // Необходимо для того, чтобы при ПКМ не пробрасывалось
        // событие нажатия к ListBox и не выделялось сообщение,
        // а при ЛКМ проверить, можно ли выделить сообщение
        // (например, оно не сервисное)

        // Для мыши
        private void ChatViewItem_PointerPressed(object sender, Avalonia.Input.PointerPressedEventArgs e) {
            if (e.Pointer.Type == Avalonia.Input.PointerType.Touch) return;
            bool isRight = !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed;
            if (isRight) {
                e.Handled = true;
            } else {
                if (Message == null || Message.Action != null || Message.IsExpired || Message.TTL > 0) e.Handled = true;
            }
        }

        // Для тачскрина
        private void ChatViewItem_PointerReleased(object sender, Avalonia.Input.PointerReleasedEventArgs e) {
            if (e.Pointer.Type != Avalonia.Input.PointerType.Touch) return;
            bool isRight = !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed;
            if (isRight) {
                e.Handled = true;
            } else {
                if (Message == null || Message.Action != null || Message.IsExpired || Message.TTL > 0) e.Handled = true;
            }
        }

        private void ChatViewItem_Unloaded(object sender, Avalonia.Interactivity.RoutedEventArgs e) {
            PointerPressed -= ChatViewItem_PointerPressed;
            PointerReleased -= ChatViewItem_PointerReleased;
            Unloaded -= ChatViewItem_Unloaded;
            DetachMessageEvents(subscribedMessage);
            Root?.Children.Clear();
        }

    }
}
