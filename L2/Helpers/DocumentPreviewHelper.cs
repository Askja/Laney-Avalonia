using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using ELOR.Laney.Core;
using ELOR.Laney.Core.Network;
using ELOR.Laney.Extensions;
using ELOR.Laney.Views.Modals;
using ELOR.VKAPILib.Objects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ELOR.Laney.Helpers {
    public static class DocumentPreviewHelper {
        private const int MaxAutoPreviewBytes = 512 * 1024;
        private const int MaxDisplayedChars = 64 * 1024;
        private const int ReadBufferSize = 16 * 1024;

        private static readonly HashSet<string> TextExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            ".txt", ".md", ".markdown", ".log", ".json", ".xml", ".csv", ".tsv", ".yml", ".yaml",
            ".ini", ".cfg", ".conf", ".cs", ".xaml", ".js", ".ts", ".css", ".scss", ".html",
            ".htm", ".php", ".py", ".sql", ".sh", ".bat", ".ps1", ".env"
        };

        public static async Task ShowAsync(VKSession session, Document document) {
            if (session == null || document == null || String.IsNullOrWhiteSpace(document.Url)) return;

            Uri uri = document.Uri;
            DocumentPreviewModel model = new DocumentPreviewModel {
                Title = String.IsNullOrWhiteSpace(document.Title) ? "Документ" : document.Title,
                Uri = uri,
                Extension = NormalizeExtension(document.Extension, uri),
                DeclaredSize = document.Size > 0 ? document.Size : null,
                Meta = BuildDocumentMeta(document)
            };

            await ShowAsync(session, model);
        }

        public static async Task ShowAsync(VKSession session, DownloadableChatAttachment attachment) {
            if (session == null || attachment?.Uri == null) return;

            DocumentPreviewModel model = new DocumentPreviewModel {
                Title = attachment.DisplayTitle,
                Uri = attachment.Uri,
                Extension = NormalizeExtension(attachment.Extension, attachment.Uri),
                DeclaredSize = attachment.DeclaredSize,
                Meta = BuildGalleryMeta(attachment)
            };

            await ShowAsync(session, model);
        }

        private static async Task ShowAsync(VKSession session, DocumentPreviewModel model) {
            DocumentPreviewLoadResult preview = await TryLoadTextPreviewAsync(model);
            StackPanel content = BuildContent(model, preview);

            VKUIDialog dialog = new VKUIDialog("Preview документа", preview.Summary, ["Открыть", "Скопировать", "Закрыть"], 3) {
                DialogContent = content
            };

            int result = await dialog.ShowDialog<int>(session.ModalWindow);
            if (result == 1) {
                await Launcher.LaunchUrl(model.Uri);
            } else if (result == 2) {
                await CopyAsync(session, model.Uri.AbsoluteUri);
            }
        }

        private static async Task<DocumentPreviewLoadResult> TryLoadTextPreviewAsync(DocumentPreviewModel model) {
            if (!IsTextPreviewCandidate(model)) {
                return DocumentPreviewLoadResult.MetadataOnly("Быстрый просмотр доступен для текстовых файлов. Для этого формата открываю метаданные, без цирка с загрузкой бинарника в память.");
            }

            if (model.DeclaredSize > MaxAutoPreviewBytes) {
                return DocumentPreviewLoadResult.MetadataOnly($"Файл больше лимита preview: {ChatAttachmentDownloadHelper.FormatBytes(model.DeclaredSize.Value)}.");
            }

            try {
                using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                using HttpResponseMessage response = await LNet.GetAsync(model.Uri, cts: cts);
                response.EnsureSuccessStatusCode();

                long? contentLength = response.Content.Headers.ContentLength;
                if (contentLength > MaxAutoPreviewBytes) {
                    return DocumentPreviewLoadResult.MetadataOnly($"Сервер заявил размер больше лимита preview: {ChatAttachmentDownloadHelper.FormatBytes((ulong)contentLength.Value)}.");
                }

                await using Stream stream = await response.Content.ReadAsStreamAsync(cts.Token);
                byte[] bytes = await ReadLimitedAsync(stream, MaxAutoPreviewBytes + 1, cts.Token);
                if (bytes.Length > MaxAutoPreviewBytes) {
                    return DocumentPreviewLoadResult.MetadataOnly($"Preview обрезан по лимиту {ChatAttachmentDownloadHelper.FormatBytes(MaxAutoPreviewBytes)}.");
                }

                string text = Encoding.UTF8.GetString(bytes);
                if (!LooksLikeText(text)) {
                    return DocumentPreviewLoadResult.MetadataOnly("Файл похож на бинарник, текстовый preview отключен.");
                }

                string trimmed = text.Length > MaxDisplayedChars
                    ? text.Substring(0, MaxDisplayedChars) + "\n\n... preview обрезан ..."
                    : text;

                return new DocumentPreviewLoadResult {
                    Summary = "Текстовый preview загружен локально. Никуда не сохраняю.",
                    Text = trimmed
                };
            } catch (Exception ex) {
                return DocumentPreviewLoadResult.MetadataOnly($"Preview не загрузился: {ex.Message}");
            }
        }

        private static async Task<byte[]> ReadLimitedAsync(Stream stream, int limit, CancellationToken token) {
            using MemoryStream memory = new MemoryStream(Math.Min(limit, MaxAutoPreviewBytes));
            byte[] buffer = new byte[ReadBufferSize];
            while (memory.Length < limit) {
                int toRead = Math.Min(buffer.Length, limit - (int)memory.Length);
                int read = await stream.ReadAsync(buffer.AsMemory(0, toRead), token);
                if (read == 0) break;
                memory.Write(buffer, 0, read);
            }

            return memory.ToArray();
        }

        private static StackPanel BuildContent(DocumentPreviewModel model, DocumentPreviewLoadResult preview) {
            StackPanel panel = new StackPanel {
                Spacing = 10,
                MinWidth = 420,
                MaxWidth = 760
            };

            panel.Children.Add(new TextBlock {
                Text = model.Title,
                FontWeight = FontWeight.SemiBold,
                Foreground = GetBrush("VKTextPrimaryBrush"),
                TextWrapping = TextWrapping.Wrap
            });

            panel.Children.Add(new TextBlock {
                Text = model.Meta,
                Foreground = GetBrush("VKTextSecondaryBrush"),
                TextWrapping = TextWrapping.Wrap
            });

            panel.Children.Add(new TextBox {
                Text = preview.Text ?? "Текстовый preview недоступен для этого документа.",
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 160,
                MaxHeight = 360,
                HorizontalAlignment = HorizontalAlignment.Stretch
            });

            return panel;
        }

        private static IBrush GetBrush(string key) {
            if (Avalonia.Application.Current?.TryGetResource(key, null, out object resource) == true && resource is IBrush brush) {
                return brush;
            }

            return Brushes.Gray;
        }

        private static async Task CopyAsync(VKSession session, string text) {
            IClipboard clipboard = session.ModalWindow?.Clipboard ?? session.Window?.Clipboard;
            if (clipboard != null) await clipboard.SetTextAsync(text);
        }

        private static bool IsTextPreviewCandidate(DocumentPreviewModel model) {
            return !String.IsNullOrWhiteSpace(model.Extension) && TextExtensions.Contains(model.Extension);
        }

        private static string NormalizeExtension(string extension, Uri uri) {
            string normalized = extension;
            if (String.IsNullOrWhiteSpace(normalized) && uri != null) normalized = Path.GetExtension(uri.LocalPath);
            if (String.IsNullOrWhiteSpace(normalized)) return String.Empty;
            normalized = normalized.Trim();
            return normalized.StartsWith('.') ? normalized : "." + normalized;
        }

        private static bool LooksLikeText(string text) {
            if (String.IsNullOrEmpty(text)) return true;
            int checkedChars = Math.Min(text.Length, 4096);
            int controlChars = 0;
            for (int i = 0; i < checkedChars; i++) {
                char c = text[i];
                if (c == '\0') return false;
                if (Char.IsControl(c) && c != '\r' && c != '\n' && c != '\t') controlChars++;
            }

            return controlChars <= checkedChars / 100;
        }

        private static string BuildDocumentMeta(Document document) {
            string extension = String.IsNullOrWhiteSpace(document.Extension) ? "без расширения" : document.Extension.ToUpperInvariant();
            string size = document.Size > 0 ? document.Size.ToFileSize() : "размер неизвестен";
            return $"{extension} · {size} · {document.Type}";
        }

        private static string BuildGalleryMeta(DownloadableChatAttachment attachment) {
            string extension = String.IsNullOrWhiteSpace(attachment.Extension) ? "без расширения" : attachment.Extension.ToUpperInvariant();
            string size = attachment.DeclaredSize != null ? ChatAttachmentDownloadHelper.FormatBytes(attachment.DeclaredSize.Value) : "размер неизвестен";
            return $"{extension} · {size} · cmid {attachment.ParentConversationMessageId} · from {attachment.SenderId}";
        }

        private sealed class DocumentPreviewModel {
            public string Title { get; set; }
            public Uri Uri { get; set; }
            public string Extension { get; set; }
            public ulong? DeclaredSize { get; set; }
            public string Meta { get; set; }
        }

        private sealed class DocumentPreviewLoadResult {
            public string Summary { get; set; }
            public string Text { get; set; }

            public static DocumentPreviewLoadResult MetadataOnly(string summary) {
                return new DocumentPreviewLoadResult {
                    Summary = summary,
                    Text = null
                };
            }
        }
    }
}
