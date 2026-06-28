using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ELOR.Laney.Core {
    public sealed class LocalBackupSyncResult {
        public string TargetDirectory { get; set; }
        public int CopiedFiles { get; set; }
        public int SkippedFiles { get; set; }
        public int FailedFiles { get; set; }
        public long CopiedBytes { get; set; }
        public DateTimeOffset StartedAtUtc { get; set; }
        public DateTimeOffset FinishedAtUtc { get; set; }
        public List<string> Errors { get; set; } = new List<string>();

        public string Summary {
            get {
                string size = FormatBytes(CopiedBytes);
                string failed = FailedFiles > 0 ? $", ошибок: {FailedFiles}" : String.Empty;
                return $"Файлов скопировано: {CopiedFiles}, пропущено: {SkippedFiles}{failed}. Записано: {size}.";
            }
        }

        private static string FormatBytes(long bytes) {
            if (bytes < 1024) return $"{bytes} B";
            double kb = bytes / 1024d;
            if (kb < 1024) return $"{kb:0.##} KB";
            double mb = kb / 1024d;
            if (mb < 1024) return $"{mb:0.##} MB";
            return $"{mb / 1024d:0.##} GB";
        }
    }

    public static class LocalBackupSyncManager {
        private const string BackupRootName = "LaneyBackup";

        private static readonly string[] AccountDirectories = [
            "automation",
            "frequent-files",
            "local-stickers",
            "ocr-cache",
            "offline",
            "quick-actions",
            "search-index",
            "voice",
            "workspaces"
        ];

        private static readonly string[] ExcludedDirectories = [
            "cache",
            "logs",
            "vault",
            "webview2",
            "whisper"
        ];

        public static async Task<LocalBackupSyncResult> SyncCurrentAccountAsync(string userDirectory) {
            if (String.IsNullOrWhiteSpace(userDirectory)) {
                throw new ArgumentException("Папка backup не выбрана.", nameof(userDirectory));
            }

            string userRoot = Path.GetFullPath(userDirectory.Trim());
            string appRoot = Path.GetFullPath(App.LocalDataPath);
            string accountRoot = Path.GetFullPath(LocalDataProfile.CurrentAccountRoot);
            string targetRoot = Path.GetFullPath(Path.Combine(userRoot, BackupRootName, BuildAccountSegment(LocalDataProfile.CurrentAccountId)));

            if (IsSameOrChildPath(targetRoot, appRoot) || IsSameOrChildPath(targetRoot, accountRoot)) {
                throw new InvalidOperationException("Backup нельзя класть внутрь локальных данных Laney: получится рекурсивная помойка, а не резервная копия.");
            }

            Directory.CreateDirectory(targetRoot);

            LocalBackupSyncResult result = new LocalBackupSyncResult {
                TargetDirectory = targetRoot,
                StartedAtUtc = DateTimeOffset.UtcNow
            };

            await WriteTextFileAsync(Path.Combine(targetRoot, "settings.client.json"), Settings.ExportClientSettingsToJson(), result);

            foreach (string directoryName in AccountDirectories) {
                string source = Path.Combine(accountRoot, directoryName);
                if (!Directory.Exists(source)) continue;

                await CopyDirectoryAsync(source, Path.Combine(targetRoot, "account-data", directoryName), result);
            }

            string manifest = JsonSerializer.Serialize(new LocalBackupManifest {
                AccountId = LocalDataProfile.CurrentAccountId,
                SyncedAtUtc = DateTimeOffset.UtcNow,
                SourceLocalDataRoot = appRoot,
                SourceAccountRoot = accountRoot,
                TargetDirectory = targetRoot,
                IncludedAccountDirectories = AccountDirectories,
                ExcludedDirectories = ExcludedDirectories
            }, new JsonSerializerOptions { WriteIndented = true });

            await WriteTextFileAsync(Path.Combine(targetRoot, "manifest.json"), manifest, result);

            result.FinishedAtUtc = DateTimeOffset.UtcNow;
            Settings.LocalBackupDirectory = userRoot;
            return result;
        }

        private static string BuildAccountSegment(long accountId) {
            return accountId == 0 ? "global" : $"account-{accountId}";
        }

        private static async Task CopyDirectoryAsync(string source, string target, LocalBackupSyncResult result) {
            foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories)) {
                string relative = Path.GetRelativePath(source, directory);
                Directory.CreateDirectory(Path.Combine(target, relative));
            }

            foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)) {
                string relative = Path.GetRelativePath(source, file);
                string destination = Path.Combine(target, relative);

                try {
                    await CopyFileIfChangedAsync(file, destination, result);
                } catch (Exception ex) {
                    result.FailedFiles++;
                    if (result.Errors.Count < 12) result.Errors.Add($"{relative}: {ex.Message}");
                    Log.Warning(ex, "Cannot sync local backup file {Source} to {Destination}.", file, destination);
                }
            }
        }

        private static async Task CopyFileIfChangedAsync(string source, string destination, LocalBackupSyncResult result) {
            FileInfo sourceInfo = new FileInfo(source);
            FileInfo destinationInfo = new FileInfo(destination);
            if (destinationInfo.Exists && destinationInfo.Length == sourceInfo.Length && destinationInfo.LastWriteTimeUtc >= sourceInfo.LastWriteTimeUtc) {
                result.SkippedFiles++;
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            string tempPath = $"{destination}.tmp-{Guid.NewGuid():N}";

            try {
                await using FileStream sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                await using FileStream targetStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await sourceStream.CopyToAsync(targetStream);
                await targetStream.FlushAsync();

                File.SetLastWriteTimeUtc(tempPath, sourceInfo.LastWriteTimeUtc);
                File.Move(tempPath, destination, true);
                result.CopiedFiles++;
                result.CopiedBytes += sourceInfo.Length;
            } finally {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        private static async Task WriteTextFileAsync(string destination, string text, LocalBackupSyncResult result) {
            byte[] bytes = new UTF8Encoding(false).GetBytes(text ?? String.Empty);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));

            if (File.Exists(destination) && await HasSameContentAsync(destination, bytes)) {
                result.SkippedFiles++;
                return;
            }

            await File.WriteAllBytesAsync(destination, bytes);
            result.CopiedFiles++;
            result.CopiedBytes += bytes.Length;
        }

        private static async Task<bool> HasSameContentAsync(string path, byte[] bytes) {
            FileInfo info = new FileInfo(path);
            if (!info.Exists || info.Length != bytes.Length) return false;

            byte[] existing = await File.ReadAllBytesAsync(path);
            return existing.SequenceEqual(bytes);
        }

        private static bool IsSameOrChildPath(string child, string parent) {
            string normalizedChild = Path.GetFullPath(child).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedParent = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return String.Equals(normalizedChild, normalizedParent, StringComparison.OrdinalIgnoreCase)
                || normalizedChild.StartsWith(normalizedParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private sealed class LocalBackupManifest {
            public long AccountId { get; set; }
            public DateTimeOffset SyncedAtUtc { get; set; }
            public string SourceLocalDataRoot { get; set; }
            public string SourceAccountRoot { get; set; }
            public string TargetDirectory { get; set; }
            public IReadOnlyList<string> IncludedAccountDirectories { get; set; }
            public IReadOnlyList<string> ExcludedDirectories { get; set; }
        }
    }
}
