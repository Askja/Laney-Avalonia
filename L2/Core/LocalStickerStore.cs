using Avalonia.Platform.Storage;
using ELOR.Laney.DataModels;
using ELOR.Laney.Core.Network;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ELOR.Laney.Core {
    public sealed class LocalStickerImportResult {
        public string Source { get; set; }
        public string PackName { get; set; }
        public string Title { get; set; }
        public int Imported { get; set; }
        public int Skipped { get; set; }
        public int Total { get; set; }

        public string Summary => $"{Title ?? PackName}: импортировано {Imported}, пропущено {Skipped}, всего {Total}";
    }

    public sealed class LocalStickerPackInfo {
        public string Key { get; set; }
        public string Title { get; set; }
        public string Source { get; set; }
        public int Count { get; set; }
        public bool IsEnabled { get; set; }
        public int SortOrder { get; set; }
    }

    public static class LocalStickerStore {
        public const string TelegramBotTokenSecretName = "telegram.bot_api_token";
        private const int TelegramMaxStickersPerPack = 120;
        private const long MaxStickerFileBytes = 8L * 1024L * 1024L;
        private static readonly Regex TelegramPackNameRegex = new Regex("^[A-Za-z0-9_]{1,128}$", RegexOptions.Compiled);
        private static readonly HashSet<string> SupportedStickerExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            ".png", ".jpg", ".jpeg", ".webp", ".gif", ".webm", ".tgs"
        };
        private static string DirectoryPath => LocalDataProfile.GetCurrentAccountDirectory("local-stickers");
        private static string FilesPath => Path.Combine(DirectoryPath, "files");
        private static string LibraryPath => Path.Combine(DirectoryPath, "library.json");

        public static List<LocalSticker> GetAll(bool includeDisabled = false) {
            List<LocalSticker> stickers = ReadLibrary();
            if (!includeDisabled) stickers = stickers.Where(s => !s.IsDisabled).ToList();

            return stickers
                .OrderBy(s => s.SortOrder)
                .ThenBy(s => s.Title)
                .ThenBy(s => s.Id)
                .ToList();
        }

        private static List<LocalSticker> ReadLibrary() {
            try {
                if (!File.Exists(LibraryPath)) return new List<LocalSticker>();

                string json = File.ReadAllText(LibraryPath);
                List<LocalSticker> stickers = JsonSerializer.Deserialize<List<LocalSticker>>(json) ?? new List<LocalSticker>();
                return stickers.Where(s => !String.IsNullOrWhiteSpace(s.FilePath) && File.Exists(s.FilePath)).ToList();
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot read local sticker library.");
                return new List<LocalSticker>();
            }
        }

        public static List<LocalSticker> Search(string query) {
            IEnumerable<LocalSticker> stickers = GetAll();
            string normalizedQuery = query?.Trim().ToLowerInvariant();

            if (!String.IsNullOrWhiteSpace(normalizedQuery)) {
                stickers = stickers.Where(s =>
                    ContainsNormalized(s.Title, normalizedQuery) ||
                    ContainsNormalized(s.Tags, normalizedQuery) ||
                    ContainsNormalized(s.Extension, normalizedQuery));
            }

            return stickers
                .OrderBy(s => s.SortOrder)
                .ThenByDescending(s => s.IsFavorite)
                .ThenByDescending(s => s.UseCount)
                .ThenByDescending(s => s.LastUsedAt)
                .ThenBy(s => s.Title)
                .ToList();
        }

        public static List<LocalSticker> GetRecent(int max = 32) {
            return GetAll()
                .Where(s => s.LastUsedAt > DateTimeOffset.MinValue || s.UseCount > 0)
                .GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(s => s.LastUsedAt)
                    .ThenByDescending(s => s.UseCount)
                    .First())
                .OrderByDescending(s => s.LastUsedAt)
                .ThenByDescending(s => s.UseCount)
                .ThenBy(s => s.Title)
                .Take(Math.Clamp(max, 1, 128))
                .ToList();
        }

        public static List<LocalStickerPackInfo> GetPacks() {
            return ReadLibrary()
                .GroupBy(GetPackKey)
                .Select(group => new LocalStickerPackInfo {
                    Key = group.Key,
                    Title = GetPackTitle(group.Key, group),
                    Source = group.FirstOrDefault(s => !String.IsNullOrWhiteSpace(s.Source))?.Source ?? "local",
                    Count = group.Count(),
                    IsEnabled = group.Any(s => !s.IsDisabled),
                    SortOrder = group.Min(s => s.SortOrder)
                })
                .OrderBy(p => p.SortOrder)
                .ThenBy(p => p.Title)
                .ToList();
        }

        public static void SetPackEnabled(string packKey, bool isEnabled) {
            UpdatePack(packKey, sticker => sticker.IsDisabled = !isEnabled);
        }

        public static void MovePack(string packKey, int delta) {
            if (String.IsNullOrWhiteSpace(packKey) || delta == 0) return;

            List<LocalSticker> stickers = ReadLibrary();
            List<string> packKeys = stickers
                .GroupBy(GetPackKey)
                .Select(group => new {
                    Key = group.Key,
                    Title = GetPackTitle(group.Key, group),
                    SortOrder = group.Min(s => s.SortOrder)
                })
                .OrderBy(p => p.SortOrder)
                .ThenBy(p => p.Title)
                .Select(p => p.Key)
                .ToList();

            int index = packKeys.IndexOf(packKey);
            if (index < 0) return;

            int target = Math.Clamp(index + delta, 0, packKeys.Count - 1);
            if (target == index) return;

            (packKeys[index], packKeys[target]) = (packKeys[target], packKeys[index]);
            for (int i = 0; i < packKeys.Count; i++) {
                foreach (LocalSticker sticker in stickers.Where(s => GetPackKey(s) == packKeys[i])) {
                    sticker.SortOrder = i;
                }
            }

            Save(stickers);
        }

        public static void SetStickerTags(string stickerId, string tags) {
            UpdateSticker(stickerId, sticker => sticker.Tags = NormalizeTags(tags));
        }

        public static void MarkUsed(string stickerId) {
            UpdateSticker(stickerId, sticker => {
                sticker.UseCount++;
                sticker.LastUsedAt = DateTimeOffset.UtcNow;
            });
        }

        public static void ToggleFavorite(string stickerId) {
            UpdateSticker(stickerId, sticker => sticker.IsFavorite = !sticker.IsFavorite);
        }

        public static async Task<int> ImportAsync(IEnumerable<IStorageFile> files) {
            if (files == null) return 0;

            Directory.CreateDirectory(FilesPath);
            List<LocalSticker> stickers = GetAll();
            int imported = 0;

            foreach (IStorageFile file in files) {
                string extension = Path.GetExtension(file.Name);
                if (String.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase)) {
                    imported += await ImportZipAsync(file, stickers);
                } else if (IsSupportedStickerExtension(extension)) {
                    LocalSticker sticker = await ImportFileAsync(file.Name, extension, async output => {
                        using Stream input = await file.OpenReadAsync();
                        await input.CopyToAsync(output);
                    });
                    stickers.Add(sticker);
                    imported++;
                }
            }

            if (imported > 0) Save(stickers);
            return imported;
        }

        public static async Task<LocalStickerImportResult> ImportTelegramPackLinkAsync(string linkOrName, string botToken) {
            if (!TryExtractTelegramPackName(linkOrName, out string packName)) {
                throw new ArgumentException("Нужна ссылка вида https://t.me/addstickers/pack_name или имя пака.");
            }

            if (String.IsNullOrWhiteSpace(botToken)) {
                throw new ArgumentException("Нужен Telegram Bot API token. Без него официальный API не отдаёт файлы стикеров.");
            }

            Directory.CreateDirectory(FilesPath);
            List<LocalSticker> stickers = GetAll();
            HashSet<string> knownSourceIds = stickers
                .Where(s => String.Equals(s.Source, "telegram", StringComparison.OrdinalIgnoreCase) && !String.IsNullOrWhiteSpace(s.SourceId))
                .Select(s => s.SourceId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            JsonElement stickerSet = await GetTelegramStickerSetAsync(packName, botToken.Trim(), cts);
            string title = ReadString(stickerSet, "title") ?? packName;
            JsonElement stickersElement = stickerSet.GetProperty("stickers");
            int total = stickersElement.GetArrayLength();
            int imported = 0;
            int skipped = 0;

            foreach (JsonElement stickerElement in stickersElement.EnumerateArray().Take(TelegramMaxStickersPerPack)) {
                string fileId = ReadString(stickerElement, "file_id");
                string uniqueId = ReadString(stickerElement, "file_unique_id") ?? fileId;
                if (String.IsNullOrWhiteSpace(fileId)) {
                    skipped++;
                    continue;
                }

                if (!String.IsNullOrWhiteSpace(uniqueId) && knownSourceIds.Contains(uniqueId)) {
                    skipped++;
                    continue;
                }

                string filePath = await GetTelegramFilePathAsync(fileId, botToken.Trim(), cts);
                if (String.IsNullOrWhiteSpace(filePath)) {
                    skipped++;
                    continue;
                }

                string extension = NormalizeExtension(Path.GetExtension(filePath), GetTelegramStickerFallbackExtension(stickerElement));
                if (!IsSupportedStickerExtension(extension)) {
                    skipped++;
                    continue;
                }

                LocalSticker sticker = await ImportTelegramFileAsync(botToken.Trim(), filePath, extension, packName, title, stickerElement, cts);
                stickers.Add(sticker);
                if (!String.IsNullOrWhiteSpace(sticker.SourceId)) knownSourceIds.Add(sticker.SourceId);
                imported++;
            }

            if (imported > 0) Save(stickers);

            return new LocalStickerImportResult {
                Source = "telegram",
                PackName = packName,
                Title = title,
                Imported = imported,
                Skipped = skipped + Math.Max(0, total - TelegramMaxStickersPerPack),
                Total = total
            };
        }

        private static async Task<int> ImportZipAsync(IStorageFile file, List<LocalSticker> stickers) {
            int imported = 0;

            using Stream stream = await file.OpenReadAsync();
            using ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read, false);

            foreach (ZipArchiveEntry entry in archive.Entries) {
                if (String.IsNullOrWhiteSpace(entry.Name)) continue;

                string extension = Path.GetExtension(entry.Name);
                if (!IsSupportedStickerExtension(extension)) continue;

                LocalSticker sticker = await ImportFileAsync(entry.Name, extension, async output => {
                    using Stream input = entry.Open();
                    await input.CopyToAsync(output);
                });
                stickers.Add(sticker);
                imported++;
            }

            return imported;
        }

        private static async Task<JsonElement> GetTelegramStickerSetAsync(string packName, string botToken, CancellationTokenSource cts) {
            Uri uri = new Uri($"https://api.telegram.org/bot{botToken}/getStickerSet?name={Uri.EscapeDataString(packName)}");
            using HttpResponseMessage response = await LNet.GetAsync(uri, cts: cts);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync(cts.Token);
            using JsonDocument document = JsonDocument.Parse(json);
            ThrowIfTelegramError(document.RootElement, "getStickerSet");
            return document.RootElement.GetProperty("result").Clone();
        }

        private static async Task<string> GetTelegramFilePathAsync(string fileId, string botToken, CancellationTokenSource cts) {
            Uri uri = new Uri($"https://api.telegram.org/bot{botToken}/getFile?file_id={Uri.EscapeDataString(fileId)}");
            using HttpResponseMessage response = await LNet.GetAsync(uri, cts: cts);
            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync(cts.Token);
            using JsonDocument document = JsonDocument.Parse(json);
            ThrowIfTelegramError(document.RootElement, "getFile");
            return ReadString(document.RootElement.GetProperty("result"), "file_path");
        }

        private static async Task<LocalSticker> ImportTelegramFileAsync(string botToken, string telegramFilePath, string extension, string packName, string title, JsonElement stickerElement, CancellationTokenSource cts) {
            string id = Guid.NewGuid().ToString("N");
            string targetPath = Path.Combine(FilesPath, $"{id}{extension.ToLowerInvariant()}");
            Uri fileUri = new Uri($"https://api.telegram.org/file/bot{botToken}/{telegramFilePath}");

            using HttpResponseMessage response = await LNet.GetAsync(fileUri, cts: cts);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaxStickerFileBytes) {
                throw new InvalidDataException($"Telegram sticker is too large: {response.Content.Headers.ContentLength} bytes.");
            }

            await using (Stream input = await response.Content.ReadAsStreamAsync(cts.Token))
            await using (FileStream output = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) {
                await CopyLimitedAsync(input, output, MaxStickerFileBytes + 1, cts.Token);
            }

            string emoji = ReadString(stickerElement, "emoji");
            string uniqueId = ReadString(stickerElement, "file_unique_id") ?? ReadString(stickerElement, "file_id");
            string sourceUrl = $"https://t.me/addstickers/{Uri.EscapeDataString(packName)}";
            (string fallbackPath, string fallbackExtension) = await TryDownloadTelegramStickerFallbackAsync(botToken, id, extension, stickerElement, cts);

            return new LocalSticker {
                Id = id,
                Title = String.IsNullOrWhiteSpace(emoji) ? $"{title} {id[..6]}" : $"{emoji} {title}",
                FilePath = targetPath,
                Extension = extension.ToLowerInvariant(),
                FallbackFilePath = fallbackPath,
                FallbackExtension = fallbackExtension,
                Tags = BuildTelegramTags(packName, title, emoji, stickerElement),
                Source = "telegram",
                SourcePack = packName,
                SourceUrl = sourceUrl,
                SourceId = uniqueId
            };
        }

        private static async Task<(string Path, string Extension)> TryDownloadTelegramStickerFallbackAsync(string botToken, string id, string primaryExtension, JsonElement stickerElement, CancellationTokenSource cts) {
            if (!NeedsRasterFallback(primaryExtension)) return (null, null);

            string thumbnailFileId = TryGetTelegramThumbnailFileId(stickerElement);
            if (String.IsNullOrWhiteSpace(thumbnailFileId)) return (null, null);

            try {
                string filePath = await GetTelegramFilePathAsync(thumbnailFileId, botToken, cts);
                string extension = NormalizeExtension(Path.GetExtension(filePath), ".webp");
                if (!IsRasterFallbackExtension(extension)) extension = ".webp";

                string targetPath = Path.Combine(FilesPath, $"{id}.fallback{extension}");
                Uri fileUri = new Uri($"https://api.telegram.org/file/bot{botToken}/{filePath}");

                using HttpResponseMessage response = await LNet.GetAsync(fileUri, cts: cts);
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength > MaxStickerFileBytes) return (null, null);

                await using Stream input = await response.Content.ReadAsStreamAsync(cts.Token);
                await using FileStream output = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                await CopyLimitedAsync(input, output, MaxStickerFileBytes + 1, cts.Token);
                return (targetPath, extension);
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot download Telegram sticker raster fallback.");
                return (null, null);
            }
        }

        private static async Task<LocalSticker> ImportFileAsync(string sourceName, string extension, Func<FileStream, Task> copyAsync) {
            string id = Guid.NewGuid().ToString("N");
            string targetPath = Path.Combine(FilesPath, $"{id}{extension.ToLowerInvariant()}");

            using (FileStream output = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) {
                await copyAsync(output);
            }

            return new LocalSticker {
                Id = id,
                Title = Path.GetFileNameWithoutExtension(sourceName),
                FilePath = targetPath,
                Extension = extension.ToLowerInvariant(),
                Tags = BuildTags(sourceName)
            };
        }

        private static bool IsSupportedStickerExtension(string extension) {
            return !String.IsNullOrWhiteSpace(extension) && SupportedStickerExtensions.Contains(extension);
        }

        private static bool NeedsRasterFallback(string extension) {
            string ext = extension?.ToLowerInvariant();
            return ext == ".webm" || ext == ".tgs";
        }

        private static bool IsRasterFallbackExtension(string extension) {
            string ext = extension?.ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".webp" || ext == ".gif";
        }

        private static string BuildTags(string sourceName) {
            string name = Path.GetFileNameWithoutExtension(sourceName) ?? String.Empty;
            return String.Join(" ", name.Split([' ', '_', '-', '.'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        private static string NormalizeTags(string tags) {
            if (String.IsNullOrWhiteSpace(tags)) return String.Empty;

            return String.Join(" ", tags
                .Split([' ', ',', ';', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private static string BuildTelegramTags(string packName, string title, string emoji, JsonElement stickerElement) {
            List<string> tags = new List<string> {
                "telegram",
                "tg",
                packName,
                title,
                emoji,
                ReadString(stickerElement, "type")
            };

            if (ReadBool(stickerElement, "is_animated")) tags.Add("animated");
            if (ReadBool(stickerElement, "is_video")) tags.Add("video");

            return String.Join(" ", tags
                .Where(t => !String.IsNullOrWhiteSpace(t))
                .SelectMany(t => t.Split([' ', '_', '-', '.'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private static string GetTelegramStickerFallbackExtension(JsonElement stickerElement) {
            if (ReadBool(stickerElement, "is_video")) return ".webm";
            if (ReadBool(stickerElement, "is_animated")) return ".tgs";
            return ".webp";
        }

        private static string NormalizeExtension(string extension, string fallback) {
            extension = String.IsNullOrWhiteSpace(extension) ? fallback : extension;
            if (!extension.StartsWith('.')) extension = $".{extension}";
            return extension.ToLowerInvariant();
        }

        private static string TryGetTelegramThumbnailFileId(JsonElement stickerElement) {
            if (TryReadNestedString(stickerElement, "thumbnail", "file_id", out string thumbnailFileId)) return thumbnailFileId;
            if (TryReadNestedString(stickerElement, "thumb", "file_id", out string legacyThumbFileId)) return legacyThumbFileId;
            return null;
        }

        private static bool TryReadNestedString(JsonElement element, string objectProperty, string valueProperty, out string value) {
            value = null;
            if (element.ValueKind != JsonValueKind.Object
                || !element.TryGetProperty(objectProperty, out JsonElement nested)
                || nested.ValueKind != JsonValueKind.Object) {
                return false;
            }

            value = ReadString(nested, valueProperty);
            return !String.IsNullOrWhiteSpace(value);
        }

        private static async Task CopyLimitedAsync(Stream input, Stream output, long limit, CancellationToken token) {
            byte[] buffer = new byte[81920];
            long total = 0;

            while (true) {
                int read = await input.ReadAsync(buffer, token);
                if (read == 0) break;

                total += read;
                if (total > limit) throw new InvalidDataException("Telegram sticker file is larger than allowed.");
                await output.WriteAsync(buffer.AsMemory(0, read), token);
            }
        }

        private static void ThrowIfTelegramError(JsonElement root, string method) {
            if (root.TryGetProperty("ok", out JsonElement ok) && ok.ValueKind == JsonValueKind.True) return;

            string description = ReadString(root, "description") ?? "unknown Telegram API error";
            throw new InvalidOperationException($"Telegram {method} failed: {description}");
        }

        private static string ReadString(JsonElement element, string property) {
            return element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty(property, out JsonElement value)
                && value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : null;
        }

        private static bool ReadBool(JsonElement element, string property) {
            return element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty(property, out JsonElement value)
                && value.ValueKind == JsonValueKind.True;
        }

        private static bool TryExtractTelegramPackName(string value, out string packName) {
            packName = null;
            if (String.IsNullOrWhiteSpace(value)) return false;

            string trimmed = value.Trim();
            if (TelegramPackNameRegex.IsMatch(trimmed)) {
                packName = trimmed;
                return true;
            }

            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri uri)) return false;

            if ((uri.Scheme == "http" || uri.Scheme == "https")
                && (String.Equals(uri.Host, "t.me", StringComparison.OrdinalIgnoreCase) || String.Equals(uri.Host, "telegram.me", StringComparison.OrdinalIgnoreCase))) {
                string[] segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 2 && String.Equals(segments[0], "addstickers", StringComparison.OrdinalIgnoreCase) && TelegramPackNameRegex.IsMatch(segments[1])) {
                    packName = segments[1];
                    return true;
                }
            }

            if (uri.Scheme == "tg" && String.Equals(uri.Host, "addstickers", StringComparison.OrdinalIgnoreCase)) {
                string query = uri.Query.TrimStart('?');
                foreach (string part in query.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
                    string[] pair = part.Split('=', 2);
                    if (pair.Length == 2 && String.Equals(pair[0], "set", StringComparison.OrdinalIgnoreCase)) {
                        string decoded = Uri.UnescapeDataString(pair[1]);
                        if (TelegramPackNameRegex.IsMatch(decoded)) {
                            packName = decoded;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool ContainsNormalized(string source, string query) {
            return !String.IsNullOrWhiteSpace(source) && source.ToLowerInvariant().Contains(query);
        }

        private static string GetPackKey(LocalSticker sticker) {
            if (sticker == null) return "local:loose";
            if (!String.IsNullOrWhiteSpace(sticker.SourcePack)) return $"{sticker.Source ?? "local"}:{sticker.SourcePack}";
            if (!String.IsNullOrWhiteSpace(sticker.Source)) return $"{sticker.Source}:loose";
            return "local:loose";
        }

        private static string GetPackTitle(string packKey, IEnumerable<LocalSticker> stickers) {
            LocalSticker first = stickers.FirstOrDefault();
            if (first == null) return packKey;

            if (!String.IsNullOrWhiteSpace(first.SourcePack)) {
                string prefix = String.Equals(first.Source, "telegram", StringComparison.OrdinalIgnoreCase) ? "TG" : first.Source;
                return String.IsNullOrWhiteSpace(prefix) ? first.SourcePack : $"{prefix}: {first.SourcePack}";
            }

            return String.Equals(first.Source, "telegram", StringComparison.OrdinalIgnoreCase) ? "TG: без пака" : "Локальные";
        }

        private static void UpdateSticker(string stickerId, Action<LocalSticker> update) {
            if (String.IsNullOrWhiteSpace(stickerId) || update == null) return;

            List<LocalSticker> stickers = ReadLibrary();
            LocalSticker sticker = stickers.FirstOrDefault(s => s.Id == stickerId);
            if (sticker == null) return;

            update(sticker);
            Save(stickers);
        }

        private static void UpdatePack(string packKey, Action<LocalSticker> update) {
            if (String.IsNullOrWhiteSpace(packKey) || update == null) return;

            List<LocalSticker> stickers = ReadLibrary();
            foreach (LocalSticker sticker in stickers.Where(s => GetPackKey(s) == packKey)) {
                update(sticker);
            }

            Save(stickers);
        }

        private static void Save(List<LocalSticker> stickers) {
            Directory.CreateDirectory(DirectoryPath);
            string json = JsonSerializer.Serialize(stickers.OrderBy(s => s.Title).ThenBy(s => s.Id).ToList());
            File.WriteAllText(LibraryPath, json);
        }
    }
}
