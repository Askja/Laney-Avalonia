using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using ELOR.Laney.Helpers;
using ELOR.VKAPILib.Objects;
using System.Threading.Tasks;

namespace ELOR.Laney.Controls {
    public class BotKeyboardUI : TemplatedControl {
        #region Properties

        public static readonly StyledProperty<BotKeyboard> KeyboardProperty =
            AvaloniaProperty.Register<BotKeyboardUI, BotKeyboard>(nameof(Keyboard));

        public BotKeyboard Keyboard {
            get => GetValue(KeyboardProperty);
            set => SetValue(KeyboardProperty, value);
        }

        public static readonly StyledProperty<long> PeerIdProperty =
            AvaloniaProperty.Register<BotKeyboardUI, long>(nameof(PeerId));

        public long PeerId {
            get => GetValue(PeerIdProperty);
            set => SetValue(PeerIdProperty, value);
        }

        public static readonly StyledProperty<int> MessageIdProperty =
            AvaloniaProperty.Register<BotKeyboardUI, int>(nameof(MessageId));

        public int MessageId {
            get => GetValue(MessageIdProperty);
            set => SetValue(MessageIdProperty, value);
        }

        public static readonly StyledProperty<long> AuthorIdProperty =
            AvaloniaProperty.Register<BotKeyboardUI, long>(nameof(AuthorId));

        public long AuthorId {
            get => GetValue(AuthorIdProperty);
            set => SetValue(AuthorIdProperty, value);
        }

        #endregion

        #region Template elements

        StackPanel Root;

        bool isUILoaded = false;
        protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
            base.OnApplyTemplate(e);
            Root = e.NameScope.Find<StackPanel>(nameof(Root));
            isUILoaded = true;
            Render();
        }

        #endregion

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
            base.OnPropertyChanged(change);

            if (change.Property == KeyboardProperty) {
                Render();
            }
        }

        private void Render() {
            if (!isUILoaded) return;
            Root.Children.Clear();
            if (Keyboard == null) return;
            VKAPIHelper.GenerateButtons(Root, Keyboard.Buttons, OnButtonClickAsync);
        }

        private async Task OnButtonClickAsync(BotButton button) {
            long authorId = AuthorId != 0 ? AuthorId : Keyboard?.AuthorId ?? 0;
            await BotButtonActionHandler.HandleAsync(this, button, PeerId, MessageId, authorId);
        }
    }
}
