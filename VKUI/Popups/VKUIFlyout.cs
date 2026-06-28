using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VKUI.Controls;

namespace VKUI.Popups {
    public sealed class VKUIFlyout : PopupFlyoutBase {
        public VKUIFlyout() {
            ShowMode = FlyoutShowMode.Standard;
            OverlayDismissEventPassThrough = false;
        }

        private VKUIFlyoutPresenter presenter;
        private TopLevel ownerTopLevel;

        public static readonly StyledProperty<object> ContentProperty =
            AvaloniaProperty.Register<VKUIFlyout, object>(nameof(Content));

        public static readonly StyledProperty<Thickness> PaddingProperty =
            AvaloniaProperty.Register<VKUIFlyout, Thickness>(nameof(Padding));

        [Content]
        public object Content {
            get => GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        public Thickness Padding {
            get => GetValue(PaddingProperty);
            set => SetValue(PaddingProperty, value);
        }

        protected override Control CreatePresenter() {
            presenter = new VKUIFlyoutPresenter {
                [!ContentControl.ContentProperty] = this[!ContentProperty],
                [!PaddingProperty] = this[!PaddingProperty],
                ParentFlyout = this
            };
            return presenter;
        }

        protected override void OnOpened() {
            base.OnOpened();
            presenter?.AddHandler(InputElement.KeyDownEvent, Presenter_KeyDown, RoutingStrategies.Tunnel, true);
            presenter?.AddHandler(InputElement.LostFocusEvent, Presenter_LostFocus, RoutingStrategies.Bubble, true);

            ownerTopLevel = TopLevel.GetTopLevel(presenter);
            ownerTopLevel?.AddHandler(InputElement.PointerPressedEvent, OwnerTopLevel_PointerPressed, RoutingStrategies.Tunnel, true);
        }

        protected override void OnClosed() {
            base.OnClosed();
            presenter?.RemoveHandler(InputElement.KeyDownEvent, Presenter_KeyDown);
            presenter?.RemoveHandler(InputElement.LostFocusEvent, Presenter_LostFocus);
            ownerTopLevel?.RemoveHandler(InputElement.PointerPressedEvent, OwnerTopLevel_PointerPressed);
            ownerTopLevel = null;
        }

        private void Presenter_KeyDown(object? sender, KeyEventArgs e) {
            if (e.Key != Key.Escape) return;

            Hide();
            e.Handled = true;
        }

        private void OwnerTopLevel_PointerPressed(object? sender, PointerPressedEventArgs e) {
            if (IsInsidePresenter(e.Source as Visual)) return;
            Hide();
        }

        private void Presenter_LostFocus(object? sender, FocusChangedEventArgs e) {
            if (IsInsidePresenter(e.NewFocusedElement as Visual)) return;

            Dispatcher.UIThread.Post(() => {
                IInputElement focused = ownerTopLevel?.FocusManager?.GetFocusedElement();
                if (!IsInsidePresenter(focused as Visual)) Hide();
            }, DispatcherPriority.Background);
        }

        private bool IsInsidePresenter(Visual visual) {
            if (presenter == null || visual == null) return false;
            return visual == presenter || presenter.IsVisualAncestorOf(visual);
        }
    }
}
