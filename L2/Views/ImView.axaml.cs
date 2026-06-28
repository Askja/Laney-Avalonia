using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml.Templates;
using ELOR.Laney.Core;
using ELOR.Laney.Extensions;
using ELOR.Laney.Helpers;
using ELOR.Laney.ViewModels;
using Serilog;
using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using VKUI.Controls;

namespace ELOR.Laney.Views {
    public sealed partial class ImView : VKUI.Controls.Page {
        private VKSession Session { get { return VKSession.GetByDataContext(this); } }

        public ImView() {
            InitializeComponent();

            if (Design.IsDesignMode)
                return;

            AvatarButton.Click += (a, b) => {
                Session.ShowSessionPopup(AvatarButton);
            };
            NewConvButton.Click += async (a, b) => {
                if (DemoMode.IsEnabled) return;
                await NavigationRouter.NavigateToAsync(new ChatCreationView());
            };
            SearchButton.Click += async (a, b) => {
                if (DemoMode.IsEnabled) return;
                await NavigationRouter.NavigateToAsync(new SearchView());
            };

            ChatsList.Loaded += ChatsList_Loaded;

            ApplyChatListTemplate(false);
            Settings.SettingChanged += Settings_SettingChanged;
        }

        private void Settings_SettingChanged(string key, object value) {
            switch (key) {
                case Settings.CHAT_ITEM_MORE_ROWS:
                case Settings.CHAT_LIST_LAYOUT:
                    ApplyChatListTemplate(true);
                    break;
                case Settings.CHAT_LIST_DENSITY:
                case Settings.CHAT_LIST_AVATAR_SIZE:
                case Settings.CHAT_LIST_AVATAR_SHAPE:
                case Settings.CHAT_LIST_FONT_SIZE:
                    RebindChatsListItemsSource();
                    break;
                case Settings.STREAMER_MODE:
                    Session?.ImViewModel?.RefreshStreamerMode();
                    break;
            }
        }

        private void ApplyChatListTemplate(bool refreshItems) {
            string key = Settings.ChatListLayout switch {
                ChatListLayoutIds.Compact => "ChatItemTemplateCompact",
                ChatListLayoutIds.Telegram => "ChatItemTemplate3Row",
                ChatListLayoutIds.MediaRich => "ChatItemTemplateMediaRich",
                ChatListLayoutIds.SplitFolder => "ChatItemTemplateSplitFolder",
                _ => Settings.ChatItemMoreRows ? "ChatItemTemplate3Row" : "ChatItemTemplate2Row"
            };

            ChatsList.ItemTemplate = Resources[key] as DataTemplate;
            if (refreshItems) RebindChatsListItemsSource();
        }

        private void RebindChatsListItemsSource() {
            // Костыль для того, чтобы шаблон действительно сменился.
            ChatsList.ItemsSource = null;
            var prop = ChatsList.GetObservable(DataContextProperty)
                .OfType<VKSession>()
                .Select(v => v.ImViewModel.SortedChats);
            ChatsList.Bind(ListBox.ItemsSourceProperty, prop);
        }

        private void ChatsList_Loaded(object sender, RoutedEventArgs e) {
            // ChatsList.Loaded -= ChatsList_Loaded;
            ChatsList.SelectionChanged += ChatsList_SelectionChanged;
            new ItemsPresenterWidthFixer(ChatsList);
            new ListBoxAutoScrollHelper(ChatsList);

            Session.CurrentOpenedChatChanged += (a, b) => {
                if (b == 0) ChatsList.SelectedItem = null;
            };

            if (DemoMode.IsEnabled) return;

            new System.Action(async () => {
                bool isRegistered = false;
                while (!isRegistered) {
                    isRegistered = await TryRegisterIncrementalLoadingEventAsync();
                }
            })();
        }

        private async Task<bool> TryRegisterIncrementalLoadingEventAsync() {
            await Task.Yield();
            try {
                if (Session.ImViewModel == null) return false;
                (ChatsList?.Scroll as ScrollViewer)?.RegisterIncrementalLoadingEvent(async () => await Session.ImViewModel.LoadConversationsAsync());
                return true;
            } catch (Exception ex) {
                Log.Error(ex, $"A problem has occured while registering incremental loading event for ChatsList!");
            }
            return false;
        }

        private void ChatsList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (e.AddedItems.Count == 0 && e.RemovedItems.Count > 0) {
                ChatsList.SelectedItem = Session.CurrentOpenedChat;
                return;
            }
            var selected = ChatsList.SelectedItem as ChatViewModel;
            if (selected != null && selected.PeerId == Session.CurrentOpenedChat?.PeerId) return;

            ChatViewModel cvm = e.AddedItems[0] as ChatViewModel;
            if (cvm == null) return;
            Session.GoToChat(cvm.PeerId);
        }

        private void ChatContextRequested(object sender, ContextRequestedEventArgs e) {
            Grid g = sender as Grid;
            ChatViewModel chat = g?.DataContext as ChatViewModel;
            if (chat == null) return;

            ContextMenuHelper.ShowForChat(chat, g);
        }

        // Необходимо для того, чтобы при ПКМ не пробрасывалось
        // событие нажатия к ListBox.

        // Для мыши
        private void ChatPointerPressed(object sender, PointerPressedEventArgs e) {
            if (e.Pointer.Type == PointerType.Touch) return;
            bool isRight = !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed;
            if (isRight) {
                e.Route = RoutingStrategies.Direct;
                e.Handled = true;
            }
        }

        // Для тачскрина
        private void ChatPointerReleased(object sender, PointerReleasedEventArgs e) {
            if (e.Pointer.Type != PointerType.Touch) return;
            bool isRight = !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed;
            //if (isRight) {
            //    e.Route = RoutingStrategies.Direct;
            //    e.Handled = true;
            //}
        }
    }
}
