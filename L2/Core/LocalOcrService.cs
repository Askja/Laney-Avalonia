using ELOR.Laney.Core.Network;
using ELOR.Laney.ViewModels;
using ELOR.Laney.ViewModels.Controls;
using ELOR.VKAPILib.Objects;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ELOR.Laney.Core {
    public sealed class LocalOcrBatchResult {
        public bool Enabled { get; set; }
        public int Found { get; set; }
        public int Cached { get; set; }
        public int Recognized { get; set; }
        public int Empty { get; set; }
        public int Failed { get; set; }
        public int SkippedByLimit { get; set; }
        public List<string> Errors { get; } = new List<string>();

        public string Summary {
            get {
                if (!Enabled) return "OCR выключен. Включи локальный OCR в настройках и укажи tesseract, если его нет в PATH.";
                return $"Картинок: {Found}; уже в cache: {Cached}; распознано: {Recognized}; пусто: {Empty}; пропущено по лимиту: {SkippedByLimit}; ошибок: {Failed}.";
            }
        }
    }

    public static class LocalOcrService {
        private const int MaxImagesPerRun = 25;
        private const long MaxImageBytes = 12 * 1024 * 1024;
        private const int TesseractTimeoutSeconds = 25;

        private static string DirectoryPath => LocalDataProfile.GetCurrentAccountDirectory("ocr-cache");
        private static string LibraryPath => Path.Combine(DirectoryPath, "library.json");
        private static Dictionary<string, LocalOcrCacheEntry> cache;
        private static string cachePath;

        public static string GetCachedText(string imageUrl) {
            if (String.IsNullOrWhiteSpace(imageUrl)) return null;
            Dictionary<string, LocalOcrCacheEntry> entries = GetCache();
            return entries.TryGetValue(imageUrl, out LocalOcrCacheEntry entry) ? entry.Text : null;
        }

        public static string GetImageSource(Attachment attachment) {
            return TryGetImageUri(attachment)?.AbsoluteUri;
        }

        public static async Task<LocalOcrBatchResult> RecognizeLoadedImagesAsync(IEnumerable<ChatViewModel> chats) {
            LocalOcrBatchResult result = new LocalOcrBatchResult {
                Enabled = Settings.LocalOcrEnabled
            };
            if (!Settings.LocalOcrEnabled || chats == null) return result;

            List<Uri> imageUris = chats
                .Where(c => c != null)
                .SelectMany(c => c.ReceivedMessages ?? Enumerable.Empty<MessageViewModel>())
                .Where(m => m != null && m.Attachments != null)
                .SelectMany(m => m.Attachments)
                .Select(TryGetImageUri)
                .Where(u => u != null)
                .DistinctBy(u => u.AbsoluteUri)
                .ToList();

            result.Found = imageUris.Count;
            Dictionary<string, LocalOcrCacheEntry> entries = GetCache();
            int processed = 0;

            foreach (Uri uri in imageUris) {
                if (entries.TryGetValue(uri.AbsoluteUri, out LocalOcrCacheEntry cached) && !String.IsNullOrWhiteSpace(cached.Text)) {
                    result.Cached++;
                    continue;
                }

                if (processed >= MaxImagesPerRun) {
                    result.SkippedByLimit++;
                    continue;
                }

                processed++;
                try {
                    string text = await RecognizeAsync(uri);
                    entries[uri.AbsoluteUri] = new LocalOcrCacheEntry {
                        Source = uri.AbsoluteUri,
                        Text = NormalizeText(text),
                        CapturedAt = DateTimeOffset.UtcNow,
                        Engine = BuildEngineLabel(),
                        Error = null
                    };

                    if (String.IsNullOrWhiteSpace(text)) {
                        result.Empty++;
                    } else {
                        result.Recognized++;
                    }
                } catch (Exception ex) {
                    result.Failed++;
                    entries[uri.AbsoluteUri] = new LocalOcrCacheEntry {
                        Source = uri.AbsoluteUri,
                        Text = null,
                        CapturedAt = DateTimeOffset.UtcNow,
                        Engine = BuildEngineLabel(),
                        Error = ex.Message
                    };
                    if (result.Errors.Count < 5) result.Errors.Add($"{Path.GetFileName(uri.LocalPath)}: {ex.Message}");
                    Log.Warning(ex, "Local OCR failed for {Uri}", uri);
                }
            }

            SaveCache(entries);
            return result;
        }

        private static async Task<string> RecognizeAsync(Uri uri) {
            string imagePath = null;
            bool deleteTemp = false;
            try {
                if (uri.IsFile) {
                    imagePath = uri.LocalPath;
                } else {
                    imagePath = await DownloadToTempAsync(uri);
                    deleteTemp = true;
                }

                return await RunTesseractAsync(imagePath);
            } finally {
                if (deleteTemp && !String.IsNullOrWhiteSpace(imagePath)) TryDelete(imagePath);
            }
        }

        private static async Task<string> DownloadToTempAsync(Uri uri) {
            using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            using HttpResponseMessage response = await LNet.GetAsync(uri, cts: cts);
            response.EnsureSuccessStatusCode();

            long? length = response.Content.Headers.ContentLength;
            if (length > MaxImageBytes) throw new InvalidOperationException($"Картинка больше OCR-лимита {MaxImageBytes / 1024 / 1024} МБ.");

            string extension = Path.GetExtension(uri.LocalPath);
            if (String.IsNullOrWhiteSpace(extension)) extension = ".jpg";

            string directory = Path.Combine(Path.GetTempPath(), "Laney", "OCR");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, $"laney-ocr-{Guid.NewGuid():N}{extension}");

            await using Stream input = await response.Content.ReadAsStreamAsync(cts.Token);
            await using FileStream output = File.Create(path);
            await CopyLimitedAsync(input, output, MaxImageBytes + 1, cts.Token);
            if (output.Length > MaxImageBytes) throw new InvalidOperationException($"Картинка больше OCR-лимита {MaxImageBytes / 1024 / 1024} МБ.");

            return path;
        }

        private static async Task CopyLimitedAsync(Stream input, Stream output, long limit, CancellationToken token) {
            byte[] buffer = new byte[128 * 1024];
            long total = 0;
            while (true) {
                int read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), token);
                if (read == 0) break;

                total += read;
                if (total > limit) throw new InvalidOperationException("OCR download limit exceeded.");
                await output.WriteAsync(buffer.AsMemory(0, read), token);
            }
        }

        private static async Task<string> RunTesseractAsync(string imagePath) {
            if (String.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath)) throw new FileNotFoundException("OCR image not found.", imagePath);

            string executable = String.IsNullOrWhiteSpace(Settings.LocalOcrTesseractPath) ? "tesseract" : Settings.LocalOcrTesseractPath;
            string language = String.IsNullOrWhiteSpace(Settings.LocalOcrLanguage) ? "rus+eng" : Settings.LocalOcrLanguage;

            using Process process = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = executable,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.StartInfo.ArgumentList.Add(imagePath);
            process.StartInfo.ArgumentList.Add("stdout");
            process.StartInfo.ArgumentList.Add("-l");
            process.StartInfo.ArgumentList.Add(language);

            try {
                process.Start();
            } catch (Exception ex) {
                throw new InvalidOperationException("Не удалось запустить tesseract. Укажи путь в настройках или добавь его в PATH.", ex);
            }

            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();
            Task waitTask = process.WaitForExitAsync();
            Task completed = await Task.WhenAny(waitTask, Task.Delay(TimeSpan.FromSeconds(TesseractTimeoutSeconds)));
            if (completed != waitTask) {
                TryKill(process);
                throw new TimeoutException($"Tesseract не уложился в {TesseractTimeoutSeconds} секунд.");
            }

            string output = await outputTask;
            string error = await errorTask;
            if (process.ExitCode != 0) throw new InvalidOperationException(String.IsNullOrWhiteSpace(error) ? $"Tesseract exit code {process.ExitCode}." : error.Trim());
            return output;
        }

        private static Uri TryGetImageUri(Attachment attachment) {
            if (attachment?.Type == AttachmentType.Photo) return TryGetPhotoUri(attachment.Photo);
            if (attachment?.Type == AttachmentType.Document && attachment.Document?.Type == DocumentType.Image) {
                return TryCreateUri(attachment.Document.Url);
            }
            return null;
        }

        private static Uri TryGetPhotoUri(Photo photo) {
            if (photo == null) return null;
            if (TryCreateUri(photo.Sizes?.OrderByDescending(s => s.Width * s.Height).FirstOrDefault(s => !String.IsNullOrWhiteSpace(s.Url))?.Url, out Uri uri)) return uri;
            if (TryCreateUri(photo.Photo200Url, out uri)) return uri;
            if (TryCreateUri(photo.Photo100Url, out uri)) return uri;
            if (TryCreateUri(photo.Photo50Url, out uri)) return uri;
            return null;
        }

        private static Uri TryCreateUri(string value) {
            return TryCreateUri(value, out Uri uri) ? uri : null;
        }

        private static bool TryCreateUri(string value, out Uri uri) {
            if (String.IsNullOrWhiteSpace(value)) {
                uri = null;
                return false;
            }

            return Uri.TryCreate(value, UriKind.Absolute, out uri);
        }

        private static string NormalizeText(string text) {
            if (String.IsNullOrWhiteSpace(text)) return null;
            return String.Join(" ", text.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Trim();
        }

        private static string BuildEngineLabel() {
            string executable = String.IsNullOrWhiteSpace(Settings.LocalOcrTesseractPath) ? "PATH:tesseract" : Settings.LocalOcrTesseractPath;
            return $"{executable}; lang={Settings.LocalOcrLanguage}";
        }

        private static Dictionary<string, LocalOcrCacheEntry> GetCache() {
            string currentPath = LibraryPath;
            if (cache != null && String.Equals(cachePath, currentPath, StringComparison.OrdinalIgnoreCase)) return cache;

            cachePath = currentPath;
            cache = LoadCache();
            return cache;
        }

        private static Dictionary<string, LocalOcrCacheEntry> LoadCache() {
            try {
                if (!File.Exists(LibraryPath)) return new Dictionary<string, LocalOcrCacheEntry>(StringComparer.OrdinalIgnoreCase);

                using FileStream input = File.OpenRead(LibraryPath);
                using JsonDocument document = JsonDocument.Parse(input);
                Dictionary<string, LocalOcrCacheEntry> result = new Dictionary<string, LocalOcrCacheEntry>(StringComparer.OrdinalIgnoreCase);
                if (document.RootElement.ValueKind != JsonValueKind.Array) return result;

                foreach (JsonElement element in document.RootElement.EnumerateArray()) {
                    LocalOcrCacheEntry entry = ReadEntry(element);
                    if (entry != null) result[entry.Source] = entry;
                }

                return result;
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot read local OCR cache.");
                return new Dictionary<string, LocalOcrCacheEntry>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static void SaveCache(Dictionary<string, LocalOcrCacheEntry> entries) {
            try {
                Directory.CreateDirectory(DirectoryPath);
                using FileStream output = File.Create(LibraryPath);
                using Utf8JsonWriter writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = true });
                writer.WriteStartArray();
                foreach (LocalOcrCacheEntry entry in entries.Values.OrderByDescending(e => e.CapturedAt).Take(5000)) {
                    writer.WriteStartObject();
                    writer.WriteString("source", entry.Source);
                    writer.WriteString("text", entry.Text);
                    writer.WriteString("capturedAt", entry.CapturedAt);
                    writer.WriteString("engine", entry.Engine);
                    writer.WriteString("error", entry.Error);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot save local OCR cache.");
            }
        }

        private static LocalOcrCacheEntry ReadEntry(JsonElement element) {
            string source = ReadString(element, "source");
            if (String.IsNullOrWhiteSpace(source)) return null;

            return new LocalOcrCacheEntry {
                Source = source,
                Text = ReadString(element, "text"),
                CapturedAt = ReadDateTimeOffset(element, "capturedAt"),
                Engine = ReadString(element, "engine"),
                Error = ReadString(element, "error")
            };
        }

        private static string ReadString(JsonElement element, string propertyName) {
            return element.TryGetProperty(propertyName, out JsonElement property) && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
        }

        private static DateTimeOffset ReadDateTimeOffset(JsonElement element, string propertyName) {
            string value = ReadString(element, propertyName);
            return DateTimeOffset.TryParse(value, out DateTimeOffset parsed) ? parsed : DateTimeOffset.MinValue;
        }

        private static void TryDelete(string path) {
            try {
                if (File.Exists(path)) File.Delete(path);
            } catch {
                // temp cleanup is best-effort
            }
        }

        private static void TryKill(Process process) {
            try {
                if (!process.HasExited) process.Kill(true);
            } catch {
                // process already gone
            }
        }

        private sealed class LocalOcrCacheEntry {
            public string Source { get; set; }
            public string Text { get; set; }
            public DateTimeOffset CapturedAt { get; set; }
            public string Engine { get; set; }
            public string Error { get; set; }
        }
    }
}
