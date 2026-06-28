using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using ELOR.Laney.Core;
using ELOR.Laney.Extensions;
using ELOR.Laney.ViewModels.Controls;
using ELOR.VKAPILib.Objects;
using System;
using System.ComponentModel;

namespace ELOR.Laney.Controls {
    public class CompactMessage : TemplatedControl {
        #region Properties

        public static readonly StyledProperty<MessageViewModel> MessageVMProperty =
            AvaloniaProperty.Register<CompactMessage, MessageViewModel>(nameof(MessageVM));

        public MessageViewModel MessageVM {
            get => GetValue(MessageVMProperty);
            set => SetValue(MessageVMProperty, value);
        }

        public static readonly StyledProperty<Message> MessageProperty =
            AvaloniaProperty.Register<CompactMessage, Message>(nameof(Message));

        public Message Message {
            get => GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        public static readonly StyledProperty<bool> IsSentTimeVisibleProperty =
            AvaloniaProperty.Register<CompactMessage, bool>(nameof(IsSentTimeVisible));

        public bool IsSentTimeVisible {
            get => GetValue(IsSentTimeVisibleProperty);
            set => SetValue(IsSentTimeVisibleProperty, value);
        }

        #endregion

        #region Template elements

        Border ImagePreview;
        TextBlock SenderName;
        TextBlock SentTime;
        TextBlock MessagePreview;
        MessageViewModel subscribedMessageVM;

        #endregion

        bool isUILoaded = false;
        protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
            base.OnApplyTemplate(e);

            ImagePreview = e.NameScope.Find<Border>(nameof(ImagePreview));
            SenderName = e.NameScope.Find<TextBlock>(nameof(SenderName));
            SentTime = e.NameScope.Find<TextBlock>(nameof(SentTime));
            MessagePreview = e.NameScope.Find<TextBlock>(nameof(MessagePreview));

            isUILoaded = true;
            AttachMessageVM(MessageVM);
            SetData();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
            base.OnPropertyChanged(change);

            if (change.Property == MessageVMProperty) {
                DetachMessageVM(change.OldValue as MessageViewModel);
                AttachMessageVM(change.NewValue as MessageViewModel);
            }

            if (change.Property == MessageProperty || change.Property == MessageVMProperty) {
                if (Message == null && MessageVM == null) return;
                SetData();
            }
        }

        private void AttachMessageVM(MessageViewModel message) {
            if (message == null || ReferenceEquals(subscribedMessageVM, message)) return;
            DetachMessageVM(subscribedMessageVM);
            subscribedMessageVM = message;
            subscribedMessageVM.PropertyChanged += MessageVM_PropertyChanged;
        }

        private void DetachMessageVM(MessageViewModel message) {
            if (message == null || !ReferenceEquals(subscribedMessageVM, message)) return;
            subscribedMessageVM.PropertyChanged -= MessageVM_PropertyChanged;
            subscribedMessageVM = null;
        }

        private void MessageVM_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(MessageViewModel.DisplaySenderName):
                case nameof(MessageViewModel.DisplayPreviewText):
                case nameof(MessageViewModel.DisplayPreviewImageUri):
                    SetData();
                    break;
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) {
            DetachMessageVM(subscribedMessageVM);
            base.OnDetachedFromVisualTree(e);
        }

        private void SetData() {
            if (!isUILoaded) return;
            if (Message == null && MessageVM == null) {
                SenderName.Text = null;
                SentTime.Text = null;
                MessagePreview.Text = null;
                ImagePreview.IsVisible = false;
                ImagePreview.Background = null;
                return;
            }

            if (Message != null) {
                var data = Settings.StreamerMode ? null : CacheManager.GetNameAndAvatar(Message.FromId);
                SenderName.Text = Settings.StreamerMode ? PrivacyMask.HiddenSenderName : data == null ? null : String.Join(" ", new[] { data.Item1, data.Item2 });
                SentTime.Text = Message.DateTime.ToHumanizedString();
                MessagePreview.Text = Settings.StreamerMode ? PrivacyMask.HiddenMessage : Message.ToNormalString();

                Uri previewUri = Settings.StreamerMode ? null : Message.Attachments.GetPreviewImageUri();
                ImagePreview.IsVisible = previewUri != null;
                if (previewUri != null)
                    new System.Action(async () => await ImagePreview.SetImageBackgroundAsync(previewUri, ImagePreview.Width, ImagePreview.Height))();

            } else if (MessageVM != null) {
                SenderName.Text = MessageVM.DisplaySenderName;
                SentTime.Text = MessageVM.SentTime.ToHumanizedString();
                MessagePreview.Text = MessageVM.DisplayPreviewText;
                ImagePreview.IsVisible = MessageVM.DisplayPreviewImageUri != null;

                if (MessageVM.DisplayPreviewImageUri != null) {
                    new System.Action(async () => await ImagePreview.SetImageBackgroundAsync(MessageVM.DisplayPreviewImageUri, ImagePreview.Width, ImagePreview.Height))();
                }
            }
        }
    }
}
