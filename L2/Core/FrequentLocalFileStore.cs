using Avalonia.Platform.Storage;
using ELOR.Laney.DataModels;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ELOR.Laney.Core {
    public static class FrequentLocalFileStore {
        private const int MaxItems = 80;

        private static string DirectoryPath => LocalDataProfile.GetCurrentAccountDirectory("frequent-files");
        private static string LibraryPath => Path.Combine(DirectoryPath, "library.json");

        public static List<FrequentLocalFile> GetTop(int limit = 20) {
            return Load()
                .Where(f => !String.IsNullOrWhiteSpace(f.FilePath) && File.Exists(f.FilePath))
                .OrderByDescending(f => f.UseCount)
                .ThenByDescending(f => f.LastUsedAt)
                .ThenBy(f => f.DisplayTitle)
                .Take(Math.Max(1, limit))
                .ToList();
        }

        public static void MarkUsed(IEnumerable<IStorageFile> files, int uploadType) {
            if (files == null) return;

            List<FrequentLocalFile> items = Load();
            bool changed = false;
            foreach (IStorageFile file in files) {
                changed |= MarkUsed(items, file, uploadType);
            }

            if (changed) Save(items);
        }

        public static void MarkUsed(FrequentLocalFile item) {
            if (item == null || String.IsNullOrWhiteSpace(item.FilePath)) return;

            List<FrequentLocalFile> items = Load();
            FrequentLocalFile existing = items.FirstOrDefault(i => IsSameItem(i, item.FilePath, item.UploadType));
            if (existing == null) {
                if (!File.Exists(item.FilePath)) return;
                existing = CreateItem(item.FilePath, item.DisplayTitle, item.UploadType);
                items.Add(existing);
            }

            existing.UseCount++;
            existing.LastUsedAt = DateTimeOffset.UtcNow;
            Save(items);
        }

        public static void Clear() {
            try {
                if (File.Exists(LibraryPath)) File.Delete(LibraryPath);
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot clear frequent local files.");
            }
        }

        public static string FormatBytes(long bytes) {
            if (bytes < 1024) return $"{bytes} B";
            double kb = bytes / 1024.0;
            if (kb < 1024) return $"{kb:0.#} KB";
            double mb = kb / 1024.0;
            if (mb < 1024) return $"{mb:0.##} MB";
            return $"{mb / 1024.0:0.##} GB";
        }

        private static bool MarkUsed(List<FrequentLocalFile> items, IStorageFile file, int uploadType) {
            string path = TryGetLocalPath(file);
            if (String.IsNullOrWhiteSpace(path) || IsLaneyTempPath(path) || !File.Exists(path)) return false;

            FrequentLocalFile existing = items.FirstOrDefault(i => IsSameItem(i, path, uploadType));
            if (existing == null) {
                existing = CreateItem(path, file.Name, uploadType);
                items.Add(existing);
            } else {
                existing.Name = file.Name;
                existing.Extension = Path.GetExtension(path);
                existing.Size = TryGetSize(path);
            }

            existing.UseCount++;
            existing.LastUsedAt = DateTimeOffset.UtcNow;
            return true;
        }

        private static FrequentLocalFile CreateItem(string path, string name, int uploadType) {
            return new FrequentLocalFile {
                FilePath = path,
                Name = String.IsNullOrWhiteSpace(name) ? Path.GetFileName(path) : name,
                Extension = Path.GetExtension(path),
                Size = TryGetSize(path),
                UploadType = uploadType,
                UseCount = 0,
                LastUsedAt = DateTimeOffset.UtcNow
            };
        }

        private static string TryGetLocalPath(IStorageFile file) {
            try {
                Uri uri = file?.Path;
                return uri?.IsFile == true ? uri.LocalPath : null;
            } catch {
                return null;
            }
        }

        private static bool IsLaneyTempPath(string path) {
            string tempLaney = Path.Combine(Path.GetTempPath(), "Laney");
            return path.StartsWith(tempLaney, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSameItem(FrequentLocalFile item, string filePath, int uploadType) {
            return item != null
                && item.UploadType == uploadType
                && String.Equals(item.FilePath, filePath, StringComparison.OrdinalIgnoreCase);
        }

        private static long TryGetSize(string path) {
            try {
                return new FileInfo(path).Length;
            } catch {
                return 0;
            }
        }

        private static List<FrequentLocalFile> Load() {
            try {
                if (!File.Exists(LibraryPath)) return new List<FrequentLocalFile>();

                using FileStream input = File.OpenRead(LibraryPath);
                using JsonDocument document = JsonDocument.Parse(input);
                if (document.RootElement.ValueKind != JsonValueKind.Array) return new List<FrequentLocalFile>();

                List<FrequentLocalFile> result = new List<FrequentLocalFile>();
                foreach (JsonElement element in document.RootElement.EnumerateArray()) {
                    FrequentLocalFile item = ReadItem(element);
                    if (item != null) result.Add(item);
                }

                return result;
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot read frequent local files.");
                return new List<FrequentLocalFile>();
            }
        }

        private static FrequentLocalFile ReadItem(JsonElement element) {
            if (element.ValueKind != JsonValueKind.Object) return null;

            string filePath = ReadString(element, "filePath");
            if (String.IsNullOrWhiteSpace(filePath)) return null;

            return new FrequentLocalFile {
                FilePath = filePath,
                Name = ReadString(element, "name"),
                Extension = ReadString(element, "extension"),
                Size = ReadInt64(element, "size"),
                UploadType = ReadInt32(element, "uploadType"),
                UseCount = ReadInt32(element, "useCount"),
                LastUsedAt = ReadDateTimeOffset(element, "lastUsedAt")
            };
        }

        private static void Save(List<FrequentLocalFile> items) {
            try {
                Directory.CreateDirectory(DirectoryPath);
                List<FrequentLocalFile> normalized = items
                    .Where(i => !String.IsNullOrWhiteSpace(i.FilePath) && File.Exists(i.FilePath))
                    .OrderByDescending(i => i.UseCount)
                    .ThenByDescending(i => i.LastUsedAt)
                    .Take(MaxItems)
                    .ToList();

                using FileStream output = File.Create(LibraryPath);
                using Utf8JsonWriter writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = true });
                writer.WriteStartArray();
                foreach (FrequentLocalFile item in normalized) {
                    writer.WriteStartObject();
                    writer.WriteString("filePath", item.FilePath);
                    writer.WriteString("name", item.Name);
                    writer.WriteString("extension", item.Extension);
                    writer.WriteNumber("size", item.Size);
                    writer.WriteNumber("uploadType", item.UploadType);
                    writer.WriteNumber("useCount", item.UseCount);
                    writer.WriteString("lastUsedAt", item.LastUsedAt);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot save frequent local files.");
            }
        }

        private static string ReadString(JsonElement element, string propertyName) {
            return element.TryGetProperty(propertyName, out JsonElement property) && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
        }

        private static int ReadInt32(JsonElement element, string propertyName) {
            return element.TryGetProperty(propertyName, out JsonElement property) && property.TryGetInt32(out int value)
                ? value
                : 0;
        }

        private static long ReadInt64(JsonElement element, string propertyName) {
            return element.TryGetProperty(propertyName, out JsonElement property) && property.TryGetInt64(out long value)
                ? value
                : 0;
        }

        private static DateTimeOffset ReadDateTimeOffset(JsonElement element, string propertyName) {
            string value = ReadString(element, propertyName);
            return DateTimeOffset.TryParse(value, out DateTimeOffset parsed) ? parsed : DateTimeOffset.MinValue;
        }
    }
}
