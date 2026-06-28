using Avalonia.Controls;
using ELOR.Laney.Views.Modals;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ELOR.Laney.Core {
    public static class Launcher {
        private static readonly HashSet<string> DangerousFileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            ".appimage", ".apk", ".bat", ".cmd", ".com", ".cpl", ".deb", ".dmg", ".exe", ".jar", ".js", ".jse",
            ".lnk", ".msi", ".pif", ".pkg", ".ps1", ".reg", ".rpm", ".run", ".scr", ".sh", ".vbs", ".wsf"
        };

        public static async Task<bool> LaunchUrl(string url) {
            return await LaunchUrl(new Uri(url));
        }

        public static async Task<bool> LaunchUrl(Uri uri) {
            if (!await ConfirmSuspiciousLaunchAsync(uri)) return false;
            return await TopLevel.GetTopLevel(VKSession.Main.Window).Launcher.LaunchUriAsync(uri);
        }

        private static async Task<bool> ConfirmSuspiciousLaunchAsync(Uri uri) {
            List<string> reasons = GetSuspiciousReasons(uri).ToList();
            if (reasons.Count == 0) return true;

            Window owner = VKSession.Main?.ModalWindow ?? VKSession.Main?.Window;
            if (owner == null) return true;

            string source = uri?.AbsoluteUri ?? String.Empty;
            string text = $"Laney noticed a risky link or file before opening:\n\n{String.Join("\n", reasons.Select(r => $"- {r}"))}\n\n{source}";
            VKUIDialog dialog = new VKUIDialog("Suspicious link", text, new[] { Assets.i18n.Resources.close, Assets.i18n.Resources.open }, 2);
            int result = await dialog.ShowDialog<int>(owner);
            return result == 2;
        }

        private static IEnumerable<string> GetSuspiciousReasons(Uri uri) {
            if (uri == null) {
                yield return "Link is empty.";
                yield break;
            }

            if (!uri.IsAbsoluteUri) {
                yield return "Link is relative.";
                yield break;
            }

            if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeMailto) {
                yield return $"Unexpected scheme: {uri.Scheme}.";
            }

            if (!String.IsNullOrEmpty(uri.UserInfo)) {
                yield return "Link contains user info before host.";
            }

            if (!String.IsNullOrEmpty(uri.Host)) {
                if (IPAddress.TryParse(uri.Host, out _)) {
                    yield return "Host is an IP address, not a domain.";
                }

                if (uri.IdnHost.Contains("xn--", StringComparison.OrdinalIgnoreCase)) {
                    yield return "Host contains punycode characters.";
                }
            }

            string decodedPath = WebUtility.UrlDecode(uri.AbsolutePath);
            if (uri.AbsoluteUri.Contains("%00", StringComparison.OrdinalIgnoreCase) || decodedPath.Contains('\0')) {
                yield return "Link contains a null-byte escape.";
            }

            string extension = Path.GetExtension(decodedPath);
            if (DangerousFileExtensions.Contains(extension)) {
                yield return $"File extension can execute code: {extension}.";
            }
        }

        public static bool LaunchFolder(string path) {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                Process p = Process.Start(new ProcessStartInfo("explorer", path) { CreateNoWindow = true });
                return p != null;
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                Process p = Process.Start("xdg-open", path); // NOT TESTED!
                return p != null;
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                Process p = Process.Start("open", path); // NOT TESTED!
                return p != null;
            } else {
                return false;
            }
        }
    }
}
