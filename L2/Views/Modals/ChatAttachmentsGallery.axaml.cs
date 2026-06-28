using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using ELOR.Laney.Core;
using ELOR.Laney.Core.Localization;
using ELOR.Laney.Helpers;
using ELOR.Laney.ViewModels;
using ELOR.Laney.ViewModels.Modals;
using System;
using System.Threading.Tasks;
using VKUI.Controls;
using VKUI.Popups;
using VKUI.Windows;

namespace ELOR.Laney.Views.Modals {
    public partial class ChatAttachmentsGallery : DialogWindow {
        private readonly VKSession session;
        private readonly ChatViewModel chat;
        private ChatAttachmentsGalleryViewModel ViewModel => DataContext as ChatAttachmentsGalleryViewModel;

        public ChatAttachmentsGallery() {
            InitializeComponent();
            if (!Design.IsDesignMode) throw new ArgumentException();
        }

        public ChatAttachmentsGallery(VKSession session, ChatViewModel chat) {
            InitializeComponent();

            this.session = session;
            this.chat = chat;
            Tag = session;
            DataContext = new ChatAttachmentsGalleryViewModel(chat);

#if LINUX
            TitleBar.IsVisible = false;
#endif
        }

        private void FilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            ViewModel?.SetFilterIndex(FilterBox.SelectedIndex);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) {
            ViewModel?.SetSearchText(SearchBox.Text);
        }

        private async void OnItemClick(object sender, Avalonia.Interactivity.RoutedEventArgs e) {
            if ((sender as Control)?.DataContext is DownloadableChatAttachment item) {
                await OpenAttachmentAsync(item);
            }
        }

        private void OnItemContextRequested(object sender, ContextRequestedEventArgs e) {
            if ((sender as Control)?.DataContext is not DownloadableChatAttachment item) return;

            ActionSheet ash = new ActionSheet();
            ActionSheetItem preview = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20ViewOutline },
                Header = Localizer.Get("cm_attachment_preview")
            };
            ActionSheetItem open = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon12ArrowUpRight },
                Header = Localizer.Get("cm_attachment_open_original")
            };
            ActionSheetItem copy = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20LinkCircleOutline },
                Header = Localizer.Get("cm_attachment_copy_link")
            };
            ActionSheetItem goToMessage = new ActionSheetItem {
                Before = new VKIcon { Id = VKIconNames.Icon20MessageArrowRightOutline },
                Header = Assets.i18n.Resources.go_to_message
            };

            preview.Click += (a, b) => new System.Action(async () => await DocumentPreviewHelper.ShowAsync(session, item))();
            open.Click += (a, b) => new System.Action(async () => await ELOR.Laney.Core.Launcher.LaunchUrl(item.Uri))();
            copy.Click += (a, b) => new System.Action(async () => {
                if (Clipboard != null) await Clipboard.SetTextAsync(item.Uri.AbsoluteUri);
            })();
            goToMessage.Click += (a, b) => {
                session.GoToChat(chat.PeerId, item.ParentConversationMessageId);
                Close();
            };

            if (IsDocument(item)) ash.Items.Add(preview);
            ash.Items.Add(open);
            ash.Items.Add(copy);
            ash.Items.Add(goToMessage);
            ash.ShowAt(sender as Control, true);
        }

        private static bool IsDocument(DownloadableChatAttachment item) {
            return item?.Kind == "document";
        }

        private async Task OpenAttachmentAsync(DownloadableChatAttachment item) {
            if (IsDocument(item)) {
                await DocumentPreviewHelper.ShowAsync(session, item);
                return;
            }

            await ELOR.Laney.Core.Launcher.LaunchUrl(item.Uri);
        }
    }
}
