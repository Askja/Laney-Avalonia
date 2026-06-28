using Serilog;
using System;
using System.IO;

namespace ELOR.Laney.Core {
    public static class LocalDataProfile {
        private const string AccountsDirectoryName = "accounts";

        public static long CurrentAccountId {
            get {
                long sessionId = VKSession.Main?.Id ?? 0;
                if (sessionId != 0) return sessionId;

                return Settings.Get<long>(Settings.VK_USER_ID);
            }
        }

        public static string CurrentAccountRoot {
            get {
                long accountId = CurrentAccountId;
                if (accountId == 0) return App.LocalDataPath;

                return Path.Combine(App.LocalDataPath, AccountsDirectoryName, accountId.ToString());
            }
        }

        public static string GetCurrentAccountDirectory(string directoryName) {
            string target = Path.Combine(CurrentAccountRoot, directoryName);
            TryCopyLegacyDirectory(Path.Combine(App.LocalDataPath, directoryName), target);
            return target;
        }

        public static string GetCurrentAccountPath(string directoryName, string fileName) {
            return Path.Combine(GetCurrentAccountDirectory(directoryName), fileName);
        }

        public static string BuildScopedSecretName(string name) {
            long accountId = CurrentAccountId;
            return accountId == 0 ? name : $"account.{accountId}.{name}";
        }

        private static void TryCopyLegacyDirectory(string source, string target) {
            if (String.Equals(source, target, StringComparison.OrdinalIgnoreCase)) return;
            if (!Directory.Exists(source) || Directory.Exists(target)) return;

            try {
                CopyDirectory(source, target);
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot copy legacy local data directory {Source} to {Target}.", source, target);
            }
        }

        private static void CopyDirectory(string source, string target) {
            Directory.CreateDirectory(target);

            foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories)) {
                string relative = Path.GetRelativePath(source, directory);
                Directory.CreateDirectory(Path.Combine(target, relative));
            }

            foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)) {
                string relative = Path.GetRelativePath(source, file);
                string destination = Path.Combine(target, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                if (!File.Exists(destination)) File.Copy(file, destination);
            }
        }
    }
}
