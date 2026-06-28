using Avalonia.Controls;
using ELOR.Laney.Core;
using ELOR.VKAPILib.Objects;
using Serilog;
using System;
using System.Threading.Tasks;

namespace ELOR.Laney.Helpers {
    internal static class BotButtonActionHandler {
        public static async Task HandleAsync(Control owner, BotButton button, long peerId = 0, int messageId = 0, long authorId = 0) {
            if (button?.Action == null) return;
            await HandleAsync(owner, button.Action, peerId, messageId, authorId);
        }

        public static async Task HandleAsync(Control owner, BotButtonAction action, long peerId = 0, int messageId = 0, long authorId = 0) {
            if (action == null) return;

            VKSession session = VKSession.GetByDataContext(owner);
            if (session == null) return;

            try {
                await HandleCoreAsync(session, action, peerId, messageId, authorId);
            } catch (Exception ex) {
                Log.Error(ex, "Unable to handle bot button action {ActionType}.", action.Type);
                Window ownerWindow = session.ModalWindow ?? session.Window;
                if (ownerWindow != null) await ExceptionHelper.ShowErrorDialogAsync(ownerWindow, ex, true);
            }
        }

        private static async Task HandleCoreAsync(VKSession session, BotButtonAction action, long peerId, int messageId, long authorId) {
            switch (action.Type) {
                case BotButtonType.Text:
                    await SendTextButtonAsync(session, action, peerId);
                    break;
                case BotButtonType.Callback:
                    await SendCallbackAsync(session, action, peerId, messageId, authorId);
                    break;
                case BotButtonType.OpenLink:
                    await LaunchLinkAsync(action.Link);
                    break;
                case BotButtonType.OpenApp:
                    await LaunchOpenAppAsync(action);
                    break;
                default:
                    ExceptionHelper.ShowNotImplementedDialog(session.ModalWindow ?? session.Window);
                    break;
            }
        }

        private static async Task SendTextButtonAsync(VKSession session, BotButtonAction action, long peerId) {
            long targetPeerId = ResolvePeerId(session, peerId);
            if (targetPeerId == 0 || String.IsNullOrWhiteSpace(action.Label)) return;

            await session.API.Messages.SendAsync(
                session.GroupId,
                targetPeerId,
                Random.Shared.Next(Int32.MinValue, Int32.MaxValue),
                action.Label,
                0,
                0,
                null,
                String.Empty,
                0,
                payload: action.Payload
            );
        }

        private static async Task SendCallbackAsync(VKSession session, BotButtonAction action, long peerId, int messageId, long authorId) {
            long targetPeerId = ResolvePeerId(session, peerId);
            if (targetPeerId == 0) return;

            long keyboardAuthorId = messageId > 0 ? 0 : authorId;
            await session.API.Messages.SendMessageEventAsync(
                targetPeerId,
                action.Payload ?? "{}",
                messageId,
                keyboardAuthorId,
                session.GroupId
            );
        }

        private static async Task LaunchLinkAsync(string link) {
            if (String.IsNullOrWhiteSpace(link)) return;
            await Launcher.LaunchUrl(link);
        }

        private static async Task LaunchOpenAppAsync(BotButtonAction action) {
            if (action.AppId <= 0) return;

            string owner = action.OwnerId != 0 ? $"_{Math.Abs(action.OwnerId)}" : String.Empty;
            string hash = String.IsNullOrEmpty(action.Hash) ? String.Empty : $"#{Uri.EscapeDataString(action.Hash)}";
            await Launcher.LaunchUrl($"https://vk.com/app{action.AppId}{owner}{hash}");
        }

        private static long ResolvePeerId(VKSession session, long peerId) {
            return peerId != 0 ? peerId : session.CurrentOpenedChat?.PeerId ?? 0;
        }
    }
}
