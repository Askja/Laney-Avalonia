using ELOR.Laney.Helpers;
using ELOR.Laney.Views.Modals;
using ELOR.VKAPILib.Objects;
using Serilog;
using System;
using System.Threading.Tasks;

namespace ELOR.Laney.Core {
    public enum VKLinkType {
        Unknown, User, Group, Wall, Poll, ConversationInvite, WriteVkMe, Write, StickerPack, ScreenName
    }

    public static class Router {
        #region Regex and parsing VK links

        public static VKLinkType GetLinkType(string url) {
            if (CompiledRegularExpressions.User().IsMatch(url)) return VKLinkType.User;
            if (CompiledRegularExpressions.Group().IsMatch(url)) return VKLinkType.Group;
            if (CompiledRegularExpressions.Wall().IsMatch(url)) return VKLinkType.Wall;
            if (CompiledRegularExpressions.Poll().IsMatch(url)) return VKLinkType.Poll;
            if (CompiledRegularExpressions.ConvoInvite().IsMatch(url)) return VKLinkType.ConversationInvite;
            if (CompiledRegularExpressions.VKME().IsMatch(url)) return VKLinkType.WriteVkMe;
            if (CompiledRegularExpressions.Write().IsMatch(url)) return VKLinkType.Write;
            if (CompiledRegularExpressions.Stickers().IsMatch(url)) return VKLinkType.StickerPack;
            if (CompiledRegularExpressions.VK().IsMatch(url)) return VKLinkType.ScreenName;
            return VKLinkType.Unknown;
        }

        public static async Task<Tuple<VKLinkType, string>> LaunchLink(this VKSession session, Uri uri) {
            return await LaunchLink(session, uri.AbsoluteUri);
        }

        public static async Task<Tuple<VKLinkType, string>> LaunchLink(this VKSession session, string url) {
            VKLinkType type = GetLinkType(url);
            string id = null;

            var ids = CompiledRegularExpressions.IDs().Matches(url);
            var snm = CompiledRegularExpressions.ScreenName().Matches(url);
            var spm = CompiledRegularExpressions.Stickers().Matches(url);

            Log.Information($"Trying to launch VK link. Type: {type}, link: {url}, ids: {String.Join(',', ids)}, snm: {String.Join(',', snm)}, ");

            switch (type) {
                case VKLinkType.User:
                    id = ids[0].Value;
                    await OpenPeerProfileAsync(session, Int64.Parse(id));
                    break;
                case VKLinkType.Group:
                    id = ids[0].Value;
                    await OpenPeerProfileAsync(session, Int64.Parse(id) * -1);
                    break;
                case VKLinkType.Wall: // TODO: Wallpost viewer in app
                    id = $"{ids[0].Value}_{ids[1].Value}";
                    await Launcher.LaunchUrl(url); // Remove after implementation
                    break;
                case VKLinkType.Poll:
                    await OpenPollViewerAsync(session, Int64.Parse(ids[0].Value), Int32.Parse(ids[1].Value));
                    break;
                case VKLinkType.ConversationInvite:
                    id = url;
                    if (session.GroupId != 0) {
                        await Launcher.LaunchUrl(EnsureAbsoluteUrl(url));
                        break;
                    }
                    await OpenChatPreviewAsync(session, url);
                    break;
                case VKLinkType.WriteVkMe:
                    id = snm[0].Value;
                    await TryResolveScreenNameAndOpenConvAsync(session, id, url);
                    break;
                case VKLinkType.Write:
                    var wr = CompiledRegularExpressions.Write().Match(url);
                    id = wr.Value;
                    session.GoToChat(Int64.Parse(id));
                    break;
                case VKLinkType.StickerPack:
                    string packName = spm[0].Groups[4].Value;
                    await OpenStickerPackPreviewAsync(session, packName);
                    break;
                case VKLinkType.ScreenName:
                    id = snm[0].Value;
                    await TryResolveScreenNameAndOpenProfileAsync(session, id, url);
                    break;
                case VKLinkType.Unknown:
                    id = url;
                    await Launcher.LaunchUrl(url);
                    break;
            }
            return new Tuple<VKLinkType, string>(type, id);
        }

        #endregion

        public static async Task OpenPeerProfileAsync(VKSession session, long peerId) {
            if (DemoMode.IsEnabled) return;
            PeerProfile pp = new PeerProfile(session, peerId);
            await pp.ShowDialog(session.ModalWindow);
        }

        public static async Task OpenPollViewerAsync(VKSession session, long ownerId, int id) {
            await PollDialogHelper.ShowPollByIdAsync(session, ownerId, id);
        }

        public static async Task OpenChatPreviewAsync(VKSession session, string url) {
            string link = EnsureAbsoluteUrl(url);
            VKUIDialog confirm = new VKUIDialog(
                Assets.i18n.Resources.chatlink_title,
                link,
                [Assets.i18n.Resources.close, Assets.i18n.Resources.add],
                2
            );

            if (await confirm.ShowDialog<int>(session.ModalWindow) != 2) return;

            try {
                var response = await session.API.Messages.JoinChatByInviteLinkAsync(link);
                long peerId = response.ChatId >= 2000000000 ? response.ChatId : 2000000000 + response.ChatId;
                session.GoToChat(peerId);
            } catch (Exception ex) {
                await ExceptionHelper.ShowErrorDialogAsync(session.ModalWindow, ex, true);
            }
        }

        public static async Task OpenStickerPackPreviewAsync(VKSession session, string packName) {
            await StickerPackDialogHelper.ShowAsync(session, packName);
        }

        public static async Task TryResolveScreenNameAndOpenProfileAsync(VKSession session, string name, string fallbackUrl) {
            ResolveScreenNameResult resolved = await ResolveScreenNameAsync(session, name, fallbackUrl);
            if (resolved == null) return;

            switch (resolved.Type) {
                case ScreenNameType.User:
                    await OpenPeerProfileAsync(session, resolved.ObjectId);
                    break;
                case ScreenNameType.Group:
                    await OpenPeerProfileAsync(session, -resolved.ObjectId);
                    break;
                default:
                    await Launcher.LaunchUrl(EnsureAbsoluteUrl(fallbackUrl));
                    break;
            }
        }

        public static async Task TryResolveScreenNameAndOpenConvAsync(VKSession session, string name, string fallbackUrl) {
            ResolveScreenNameResult resolved = await ResolveScreenNameAsync(session, name, fallbackUrl);
            if (resolved == null) return;

            switch (resolved.Type) {
                case ScreenNameType.User:
                    session.GoToChat(resolved.ObjectId);
                    break;
                case ScreenNameType.Group:
                    session.GoToChat(-resolved.ObjectId);
                    break;
                default:
                    await Launcher.LaunchUrl(EnsureAbsoluteUrl(fallbackUrl));
                    break;
            }
        }

        private static async Task<ResolveScreenNameResult> ResolveScreenNameAsync(VKSession session, string name, string fallbackUrl) {
            try {
                ResolveScreenNameResult resolved = await session.API.Utils.ResolveScreenNameAsync(name);
                if (resolved != null && resolved.ObjectId != 0) return resolved;

                await Launcher.LaunchUrl(EnsureAbsoluteUrl(fallbackUrl));
            } catch (Exception ex) {
                await ExceptionHelper.ShowErrorDialogAsync(session.ModalWindow, ex, true);
            }

            return null;
        }

        private static string EnsureAbsoluteUrl(string url) {
            return Uri.TryCreate(url, UriKind.Absolute, out _) ? url : $"https://{url}";
        }
    }
}
