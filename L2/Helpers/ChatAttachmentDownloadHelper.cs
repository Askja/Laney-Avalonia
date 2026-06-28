using ELOR.Laney.Core.Network;
using ELOR.Laney.Core;
using ELOR.Laney.ViewModels;
using ELOR.Laney.ViewModels.Controls;
using ELOR.VKAPILib.Methods;
using ELOR.VKAPILib.Objects;
using Serilog;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace ELOR.Laney.Helpers {
    public static class AttachmentDownloadProfileIds {
        public const string All = "all";
        public const string Photos = "photos";
        public const string Documents = "documents";
        public const string Voice = "voice";
        public const string Video = "video";
        public const string Audio = "audio";
        public const string Stickers = "stickers";

        public static readonly string[] AllIds = [All, Photos, Documents, Voice, Video, Audio, Stickers];
    }

    public sealed class ChatAttachmentDownloadOptions {
        public string ProfileId { get; set; } = AttachmentDownloadProfileIds.All;
        public string TextFilter { get; set; }
        public long? SenderId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public ulong? MaxSizeBytes { get; set; }
        public bool IncludePhotos { get; set; } = true;
        public bool IncludeDocuments { get; set; } = true;
        public bool IncludeAudio { get; set; } = true;
        public bool IncludeVoice { get; set; } = true;
        public bool IncludeVideo { get; set; } = true;
        public bool IncludeStickers { get; set; } = true;
        public bool IncludeGraffiti { get; set; } = true;
        public bool IncludeLinkPreviews { get; set; } = true;
        public bool DedupeByHash { get; set; } = true;
        public bool WriteSidecarJson { get; set; } = true;
        public bool ResumeExisting { get; set; } = true;
        public bool FullHistoryBackfill { get; set; }
        public bool PauseAfterBackfillPage { get; set; }
        public int SpeedLimitKbPerSecond { get; set; }

        public static ChatAttachmentDownloadOptions CreateForProfile(string profileId) {
            ChatAttachmentDownloadOptions options = new ChatAttachmentDownloadOptions {
                ProfileId = String.IsNullOrWhiteSpace(profileId) ? AttachmentDownloadProfileIds.All : profileId
            };

            if (options.ProfileId == AttachmentDownloadProfileIds.All) return options;

            options.IncludePhotos = options.ProfileId == AttachmentDownloadProfileIds.Photos;
            options.IncludeDocuments = options.ProfileId == AttachmentDownloadProfileIds.Documents;
            options.IncludeAudio = options.ProfileId == AttachmentDownloadProfileIds.Audio;
            options.IncludeVoice = options.ProfileId == AttachmentDownloadProfileIds.Voice;
            options.IncludeVideo = options.ProfileId == AttachmentDownloadProfileIds.Video;
            options.IncludeStickers = options.ProfileId == AttachmentDownloadProfileIds.Stickers;
            options.IncludeGraffiti = options.ProfileId == AttachmentDownloadProfileIds.Stickers;
            options.IncludeLinkPreviews = options.ProfileId == AttachmentDownloadProfileIds.Photos;
            return options;
        }
    }

    public sealed class ChatAttachmentDownloadResult {
        public string TargetDirectory { get; set; }
        public int SourceMessages { get; set; }
        public int Found { get; set; }
        public int Downloaded { get; set; }
        public int Skipped { get; set; }
        public int Resumed { get; set; }
        public int Duplicates { get; set; }
        public int Failed { get; set; }
        public int BackfillPages { get; set; }
        public int BackfillItems { get; set; }
        public bool Paused { get; set; }
        public List<string> Errors { get; } = new List<string>();

        public string Summary {
            get {
                string paused = Paused ? " очередь на паузе;" : String.Empty;
                string backfill = BackfillPages > 0 ? $" API-страниц: {BackfillPages};" : String.Empty;
                return $"Найдено: {Found}; скачано: {Downloaded}; resume: {Resumed}; пропущено: {Skipped}; дубли: {Duplicates}; ошибок: {Failed};{backfill}{paused}";
            }
        }
    }

    public sealed class DownloadableChatAttachment {
        public Uri Uri { get; set; }
        public string Kind { get; set; }
        public string Extension { get; set; }
        public string OriginalName { get; set; }
        public ulong? DeclaredSize { get; set; }
        public long PeerId { get; set; }
        public int ConversationMessageId { get; set; }
        public int ParentConversationMessageId { get; set; }
        public long SenderId { get; set; }
        public DateTime SentTime { get; set; }
        public string AttachmentId { get; set; }
        public string SourceKey { get; set; }

        public Uri PreviewUri => IsPreviewKind ? Uri : null;
        public bool IsPreviewKind => Kind == "photo" || Kind == "link_preview" || Kind == "sticker" || Kind == "sticker_animated" || Kind == "ugc_sticker" || Kind == "graffiti";
        public string FilterKind => Kind switch {
            "photo" or "link_preview" => "photo",
            "document" => "document",
            "audio" => "audio",
            "voice" => "voice",
            "video" => "video",
            "sticker" or "sticker_animated" or "ugc_sticker" or "graffiti" => "sticker",
            _ => "other"
        };
        public string KindTitle => Kind switch {
            "photo" => "Фото",
            "link_preview" => "Preview ссылки",
            "document" => "Документ",
            "audio" => "Аудио",
            "voice" => "Голосовое",
            "video" => "Видео",
            "sticker" => "Стикер",
            "sticker_animated" => "Анимированный стикер",
            "ugc_sticker" => "UGC-стикер",
            "graffiti" => "Граффити",
            _ => "Вложение"
        };
        public string IconId => Kind switch {
            "photo" or "link_preview" => "Icon20PictureOutline",
            "document" => "Icon20DocumentOutline",
            "audio" => "Icon28MusicOutline",
            "voice" => "Icon28VoiceOutline",
            "video" => "Icon20VideoOutline",
            "sticker" or "sticker_animated" or "ugc_sticker" => "Icon20SmileOutline",
            "graffiti" => "Icon24BrushOutline",
            _ => "Icon20DocumentOutline"
        };
        public string DisplayTitle => ChatAttachmentDownloadHelper.SanitizeFileName(OriginalName, KindTitle, 96);
        public string DisplaySubtitle {
            get {
                string size = DeclaredSize != null ? ChatAttachmentDownloadHelper.FormatBytes(DeclaredSize.Value) + " · " : String.Empty;
                return $"{KindTitle} · {size}{SentTime:dd.MM.yyyy HH:mm} · from {SenderId} · cmid {ParentConversationMessageId}";
            }
        }
    }

    internal sealed class DownloadedChatAttachment {
        public string TempPath { get; set; }
        public string Sha256 { get; set; }
        public long Bytes { get; set; }
    }

    internal sealed class AttachmentResumeEntry {
        public string Path { get; set; }
        public string Sha256 { get; set; }
    }

    internal sealed class AttachmentResumeIndex {
        public Dictionary<string, AttachmentResumeEntry> BySourceKey { get; } = new Dictionary<string, AttachmentResumeEntry>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Hashes { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    internal sealed class AttachmentDownloadQueueState {
        public string Status { get; set; }
        public long PeerId { get; set; }
        public Dictionary<string, int> Offsets { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public long UpdatedAtUnix { get; set; }
    }

    internal sealed class AttachmentDownloadExecutionState {
        public HashSet<string> SeenSources { get; set; }
        public HashSet<string> SeenHashes { get; set; }
        public AttachmentResumeIndex ResumeIndex { get; set; }
    }

    public static class ChatAttachmentDownloadHelper {
        private const int BufferSize = 128 * 1024;
        private const int BackfillPageSize = 200;
        private const string QueueStateFileName = ".laney-download-queue.json";

        public static async Task<ChatAttachmentDownloadResult> DownloadAttachmentsAsync(VKSession session, ChatViewModel chat, string targetDirectory, ChatAttachmentDownloadOptions options) {
            if (options?.FullHistoryBackfill == true) return await DownloadFullHistoryAttachmentsAsync(session, chat, targetDirectory, options);
            return await DownloadLoadedAttachmentsAsync(chat, targetDirectory, options);
        }

        public static async Task<ChatAttachmentDownloadResult> DownloadLoadedAttachmentsAsync(ChatViewModel chat, string targetDirectory, ChatAttachmentDownloadOptions options) {
            if (chat == null) throw new ArgumentNullException(nameof(chat));
            if (String.IsNullOrWhiteSpace(targetDirectory)) throw new ArgumentException("Target directory must not be empty.", nameof(targetDirectory));
            options ??= new ChatAttachmentDownloadOptions();

            Directory.CreateDirectory(targetDirectory);

            List<MessageViewModel> messages = GetLoadedMessagesSnapshot(chat);
            List<DownloadableChatAttachment> attachments = ExtractAttachments(messages, options).ToList();
            ChatAttachmentDownloadResult result = new ChatAttachmentDownloadResult {
                TargetDirectory = targetDirectory,
                SourceMessages = messages.Count,
                Found = attachments.Count
            };

            AttachmentDownloadExecutionState executionState = CreateExecutionState(targetDirectory, options);
            await DownloadAttachmentsCoreAsync(attachments, targetDirectory, options, result, executionState);
            return result;
        }

        private static async Task<ChatAttachmentDownloadResult> DownloadFullHistoryAttachmentsAsync(VKSession session, ChatViewModel chat, string targetDirectory, ChatAttachmentDownloadOptions options) {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (chat == null) throw new ArgumentNullException(nameof(chat));
            if (String.IsNullOrWhiteSpace(targetDirectory)) throw new ArgumentException("Target directory must not be empty.", nameof(targetDirectory));
            options ??= new ChatAttachmentDownloadOptions();

            Directory.CreateDirectory(targetDirectory);

            ChatAttachmentDownloadResult result = new ChatAttachmentDownloadResult {
                TargetDirectory = targetDirectory,
                SourceMessages = GetLoadedMessagesSnapshot(chat).Count
            };
            AttachmentDownloadExecutionState executionState = CreateExecutionState(targetDirectory, options);
            AttachmentDownloadQueueState queueState = options.ResumeExisting
                ? LoadQueueState(targetDirectory, chat.PeerId)
                : CreateQueueState(chat.PeerId);
            int startCmid = GetBackfillStartCmid(chat);
            if (startCmid <= 0) {
                result.Errors.Add("Не найден стартовый cmid для full-history backfill.");
                result.Failed++;
                return result;
            }

            foreach (HistoryAttachmentMediaType type in GetBackfillMediaTypes(options)) {
                string typeKey = type.ToString();
                int offset = queueState.Offsets.TryGetValue(typeKey, out int savedOffset) ? Math.Max(0, savedOffset) : 0;

                while (true) {
                    if (options.PauseAfterBackfillPage && result.BackfillPages > 0) {
                        queueState.Status = "paused";
                        SaveQueueState(targetDirectory, queueState);
                        result.Paused = true;
                        return result;
                    }

                    ConversationAttachmentsResponse response = await session.API.Messages.GetHistoryAttachmentsAsync(
                        session.GroupId,
                        chat.PeerId,
                        type,
                        startCmid,
                        offset,
                        BackfillPageSize,
                        true,
                        fields: VKAPIHelper.Fields);

                    CacheManager.Add(response.Profiles);
                    CacheManager.Add(response.Groups);

                    List<ConversationAttachment> items = response.Items ?? new List<ConversationAttachment>();
                    result.BackfillPages++;
                    result.BackfillItems += items.Count;

                    List<DownloadableChatAttachment> attachments = ExtractConversationAttachments(items, chat.PeerId, options);
                    result.Found += attachments.Count;
                    await DownloadAttachmentsCoreAsync(attachments, targetDirectory, options, result, executionState);

                    offset += items.Count;
                    queueState.Offsets[typeKey] = offset;
                    queueState.Status = "running";
                    SaveQueueState(targetDirectory, queueState);

                    if (items.Count < BackfillPageSize) break;
                    await Task.Delay(250);
                }
            }

            queueState.Status = "done";
            SaveQueueState(targetDirectory, queueState);
            return result;
        }

        private static AttachmentDownloadExecutionState CreateExecutionState(string targetDirectory, ChatAttachmentDownloadOptions options) {
            HashSet<string> seenSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> seenHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AttachmentResumeIndex resumeIndex = options.ResumeExisting ? LoadResumeIndex(targetDirectory) : new AttachmentResumeIndex();
            foreach (string hash in resumeIndex.Hashes) seenHashes.Add(hash);

            return new AttachmentDownloadExecutionState {
                SeenSources = seenSources,
                SeenHashes = seenHashes,
                ResumeIndex = resumeIndex
            };
        }

        private static async Task DownloadAttachmentsCoreAsync(IEnumerable<DownloadableChatAttachment> attachments, string targetDirectory, ChatAttachmentDownloadOptions options, ChatAttachmentDownloadResult result, AttachmentDownloadExecutionState state) {
            foreach (DownloadableChatAttachment attachment in attachments) {
                string fileName = BuildFileName(attachment);
                string deterministicPath = Path.Combine(targetDirectory, fileName);
                if (options.ResumeExisting && IsAlreadyDownloaded(state.ResumeIndex, attachment, deterministicPath, out string existingHash)) {
                    if (!String.IsNullOrWhiteSpace(existingHash)) state.SeenHashes.Add(existingHash);
                    result.Skipped++;
                    result.Resumed++;
                    continue;
                }

                if (options.DedupeByHash && !state.SeenSources.Add(attachment.SourceKey)) {
                    result.Skipped++;
                    result.Duplicates++;
                    continue;
                }

                string targetPath = GetUniquePath(deterministicPath);

                try {
                    DownloadedChatAttachment downloaded = await DownloadToTempFileAsync(attachment, targetPath, options.SpeedLimitKbPerSecond);

                    if (options.DedupeByHash && !String.IsNullOrEmpty(downloaded.Sha256) && !state.SeenHashes.Add(downloaded.Sha256)) {
                        SafeDelete(downloaded.TempPath);
                        result.Skipped++;
                        result.Duplicates++;
                        continue;
                    }

                    File.Move(downloaded.TempPath, targetPath);
                    if (options.WriteSidecarJson) await WriteSidecarAsync(targetPath, attachment, downloaded);
                    await OfflineCacheStore.RegisterDownloadedAttachmentAsync(
                        targetPath,
                        attachment.Kind,
                        attachment.OriginalName,
                        attachment.Uri.AbsoluteUri,
                        attachment.PeerId,
                        attachment.ConversationMessageId,
                        attachment.ParentConversationMessageId,
                        attachment.SenderId,
                        attachment.SentTime,
                        downloaded.Bytes,
                        downloaded.Sha256);
                    result.Downloaded++;
                } catch (Exception ex) {
                    result.Failed++;
                    result.Errors.Add($"{attachment.Kind} cmid={attachment.ConversationMessageId}: {ex.Message}");
                    Log.Warning(ex, "Unable to download chat attachment {Kind} from {Uri}", attachment.Kind, attachment.Uri);
                    SafeDelete(targetPath + ".download");
                }
            }
        }

        public static List<DownloadableChatAttachment> GetLoadedAttachments(ChatViewModel chat, ChatAttachmentDownloadOptions options = null) {
            if (chat == null) return new List<DownloadableChatAttachment>();
            return ExtractAttachments(GetLoadedMessagesSnapshot(chat), options ?? new ChatAttachmentDownloadOptions());
        }

        private static List<DownloadableChatAttachment> ExtractConversationAttachments(IEnumerable<ConversationAttachment> items, long peerId, ChatAttachmentDownloadOptions options) {
            options ??= new ChatAttachmentDownloadOptions();
            List<DownloadableChatAttachment> result = new List<DownloadableChatAttachment>();
            if (items == null) return result;

            foreach (ConversationAttachment item in items) {
                if (item?.Attachment == null) continue;
                int cmid = item.CMID > 0 ? item.CMID : item.MessageId;
                long senderId = item.FromId;
                foreach (DownloadableChatAttachment attachment in ExtractDownloadableAttachments(item.Attachment, peerId, cmid, cmid, senderId, DateTime.Now, 1)) {
                    result.Add(attachment);
                }
            }

            return result
                .Where(a => MatchesOptions(a, options))
                .OrderBy(a => a.ConversationMessageId)
                .ThenBy(a => a.Kind)
                .ToList();
        }

        public static List<DownloadableChatAttachment> ExtractAttachments(IEnumerable<MessageViewModel> messages, ChatAttachmentDownloadOptions options) {
            options ??= new ChatAttachmentDownloadOptions();
            List<DownloadableChatAttachment> result = new List<DownloadableChatAttachment>();
            if (messages == null) return result;

            foreach (MessageViewModel message in messages) {
                if (message == null) continue;
                AddAttachmentsFromMessage(result, message.Attachments, message.PeerId, message.ConversationMessageId, message.ConversationMessageId, message.SenderId, message.SentTime, options);

                if (message.ForwardedMessages == null) continue;
                foreach (Message forwarded in message.ForwardedMessages) {
                    AddAttachmentsFromApiMessage(result, forwarded, message, options);
                }
            }

            return result
                .Where(a => MatchesOptions(a, options))
                .OrderBy(a => a.SentTime)
                .ThenBy(a => a.ConversationMessageId)
                .ToList();
        }

        private static int GetBackfillStartCmid(ChatViewModel chat) {
            int maxLoaded = GetLoadedMessagesSnapshot(chat)
                .Select(m => m.ConversationMessageId)
                .DefaultIfEmpty(0)
                .Max();
            return Math.Max(maxLoaded, Math.Max(chat.InRead, chat.OutRead));
        }

        private static IEnumerable<HistoryAttachmentMediaType> GetBackfillMediaTypes(ChatAttachmentDownloadOptions options) {
            if (options.IncludePhotos || options.IncludeLinkPreviews) yield return HistoryAttachmentMediaType.Photo;
            if (options.IncludeVideo) yield return HistoryAttachmentMediaType.Video;
            if (options.IncludeAudio) yield return HistoryAttachmentMediaType.Audio;
            if (options.IncludeDocuments) yield return HistoryAttachmentMediaType.Doc;
            if (options.IncludeLinkPreviews) yield return HistoryAttachmentMediaType.Share;
            if (options.IncludeGraffiti || options.IncludeStickers) yield return HistoryAttachmentMediaType.Graffiti;
            if (options.IncludeVoice) yield return HistoryAttachmentMediaType.AudioMessage;
        }

        public static string SanitizeFileName(string value, string fallback = "file", int maxLength = 120) {
            string name = String.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            name = String.Join("_", name.Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
            if (String.IsNullOrWhiteSpace(name)) name = fallback;
            if (name.Length > maxLength) name = name.Substring(0, maxLength).Trim();
            return name;
        }

        private static List<MessageViewModel> GetLoadedMessagesSnapshot(ChatViewModel chat) {
            List<MessageViewModel> snapshot = new List<MessageViewModel>();
            HashSet<int> ids = new HashSet<int>();

            AddMessages(chat.DisplayedMessages);
            AddMessages(chat.ReceivedMessages);

            return snapshot
                .OrderBy(m => m.SentTime)
                .ThenBy(m => m.ConversationMessageId)
                .ToList();

            void AddMessages(IEnumerable<MessageViewModel> messages) {
                if (messages == null) return;
                foreach (MessageViewModel message in messages) {
                    if (message == null) continue;
                    int key = message.ConversationMessageId != 0 ? message.ConversationMessageId : message.GlobalId;
                    if (!ids.Add(key)) continue;
                    snapshot.Add(message);
                }
            }
        }

        private static void AddAttachmentsFromApiMessage(List<DownloadableChatAttachment> target, Message message, MessageViewModel parent, ChatAttachmentDownloadOptions options) {
            if (message == null || parent == null) return;

            int cmid = message.ConversationMessageId > 0 ? message.ConversationMessageId : parent.ConversationMessageId;
            long peerId = message.PeerId != 0 ? message.PeerId : parent.PeerId;
            long senderId = message.FromId != 0 ? message.FromId : parent.SenderId;
            DateTime sentTime = message.DateUnix > 0 ? message.DateTime : parent.SentTime;

            AddAttachmentsFromMessage(target, message.Attachments, peerId, cmid, parent.ConversationMessageId, senderId, sentTime, options);

            if (message.ForwardedMessages == null) return;
            foreach (Message forwarded in message.ForwardedMessages) {
                AddAttachmentsFromApiMessage(target, forwarded, parent, options);
            }
        }

        private static void AddAttachmentsFromMessage(List<DownloadableChatAttachment> target, List<Attachment> attachments, long peerId, int cmid, int parentCmid, long senderId, DateTime sentTime, ChatAttachmentDownloadOptions options) {
            if (attachments == null || attachments.Count == 0) return;

            int index = 0;
            foreach (Attachment attachment in attachments) {
                index++;
                foreach (DownloadableChatAttachment item in ExtractDownloadableAttachments(attachment, peerId, cmid, parentCmid, senderId, sentTime, index)) {
                    target.Add(item);
                }
            }
        }

        private static IEnumerable<DownloadableChatAttachment> ExtractDownloadableAttachments(Attachment attachment, long peerId, int cmid, int parentCmid, long senderId, DateTime sentTime, int index) {
            if (attachment == null) yield break;

            if (TryGetPhotoUri(attachment.Photo, out Uri photoUri)) {
                yield return CreateItem(photoUri, "photo", GetExtensionFromUri(photoUri, ".jpg"), attachment.Photo.ToString(), null, peerId, cmid, parentCmid, senderId, sentTime, index);
            }

            if (attachment.Document != null && TryCreateAbsoluteUri(attachment.Document.Url, out Uri documentUri)) {
                string extension = NormalizeExtension(attachment.Document.Extension, GetExtensionFromUri(documentUri, ".bin"));
                yield return CreateItem(documentUri, "document", extension, attachment.Document.Title, attachment.Document.Size, peerId, cmid, parentCmid, senderId, sentTime, index);
            }

            if (attachment.Audio != null && TryCreateAbsoluteUri(attachment.Audio.Url, out Uri audioUri)) {
                string name = $"{attachment.Audio.Artist} - {attachment.Audio.FullSongName}".Trim(' ', '-');
                yield return CreateItem(audioUri, "audio", GetExtensionFromUri(audioUri, ".mp3"), name, null, peerId, cmid, parentCmid, senderId, sentTime, index);
            }

            if (attachment.AudioMessage != null && TryCreateAbsoluteUri(attachment.AudioMessage.Link, out Uri audioMessageUri)) {
                string name = $"voice_{attachment.AudioMessage.Duration}s";
                yield return CreateItem(audioMessageUri, "voice", GetExtensionFromUri(audioMessageUri, ".mp3"), name, null, peerId, cmid, parentCmid, senderId, sentTime, index);
            }

            if (attachment.Video != null && TryGetBestVideoUri(attachment.Video, out Uri videoUri)) {
                yield return CreateItem(videoUri, "video", GetExtensionFromUri(videoUri, ".mp4"), attachment.Video.Title, null, peerId, cmid, parentCmid, senderId, sentTime, index);
            }

            if (attachment.Graffiti != null) {
                Uri graffitiUri = TryGetGraffitiUri(attachment.Graffiti);
                if (graffitiUri != null) {
                    yield return CreateItem(graffitiUri, "graffiti", GetExtensionFromUri(graffitiUri, ".png"), attachment.Graffiti.ToString(), null, peerId, cmid, parentCmid, senderId, sentTime, index);
                }
            }

            if (attachment.Sticker != null && TryGetStickerUri(attachment.Sticker, out Uri stickerUri, out bool stickerAnimated)) {
                string extension = GetExtensionFromUri(stickerUri, stickerAnimated ? ".json" : ".png");
                yield return CreateItem(stickerUri, stickerAnimated ? "sticker_animated" : "sticker", extension, $"sticker_{attachment.Sticker.StickerId}", null, peerId, cmid, parentCmid, senderId, sentTime, index);
            }

            if (attachment.UGCSticker != null && TryGetUGCStickerUri(attachment.UGCSticker, out Uri ugcStickerUri)) {
                yield return CreateItem(ugcStickerUri, "ugc_sticker", GetExtensionFromUri(ugcStickerUri, ".png"), $"ugc_sticker_{attachment.UGCSticker.Id}", null, peerId, cmid, parentCmid, senderId, sentTime, index);
            }

            if (attachment.Link != null) {
                if (TryGetPhotoUri(attachment.Link.Photo, out Uri linkPhotoUri)) {
                    yield return CreateItem(linkPhotoUri, "link_preview", GetExtensionFromUri(linkPhotoUri, ".jpg"), attachment.Link.Title, null, peerId, cmid, parentCmid, senderId, sentTime, index);
                } else if (TryCreateAbsoluteUri(attachment.Link.PreviewUrl, out Uri previewUri) || TryCreateAbsoluteUri(attachment.Link.ImageSrc, out previewUri)) {
                    yield return CreateItem(previewUri, "link_preview", GetExtensionFromUri(previewUri, ".jpg"), attachment.Link.Title, null, peerId, cmid, parentCmid, senderId, sentTime, index);
                }
            }
        }

        private static DownloadableChatAttachment CreateItem(Uri uri, string kind, string extension, string originalName, ulong? size, long peerId, int cmid, int parentCmid, long senderId, DateTime sentTime, int index) {
            extension = NormalizeExtension(extension, ".bin");
            string attachmentId = $"{kind}_{parentCmid}_{cmid}_{index}";
            return new DownloadableChatAttachment {
                Uri = uri,
                Kind = kind,
                Extension = extension,
                OriginalName = originalName,
                DeclaredSize = size,
                PeerId = peerId,
                ConversationMessageId = cmid,
                ParentConversationMessageId = parentCmid,
                SenderId = senderId,
                SentTime = sentTime == default ? DateTime.Now : sentTime,
                AttachmentId = attachmentId,
                SourceKey = $"{kind}|{uri.AbsoluteUri}|{size}|{originalName}"
            };
        }

        private static bool MatchesOptions(DownloadableChatAttachment attachment, ChatAttachmentDownloadOptions options) {
            if (attachment == null || attachment.Uri == null) return false;
            if (options.SenderId != null && attachment.SenderId != options.SenderId.Value) return false;
            if (options.FromDate != null && attachment.SentTime.Date < options.FromDate.Value.Date) return false;
            if (options.ToDate != null && attachment.SentTime.Date > options.ToDate.Value.Date) return false;
            if (options.MaxSizeBytes != null && attachment.DeclaredSize != null && attachment.DeclaredSize.Value > options.MaxSizeBytes.Value) return false;

            if (!String.IsNullOrWhiteSpace(options.TextFilter)) {
                string filter = options.TextFilter.Trim();
                bool matched = Contains(attachment.OriginalName, filter)
                    || Contains(attachment.Kind, filter)
                    || Contains(attachment.Extension, filter)
                    || Contains(attachment.Uri.AbsoluteUri, filter);
                if (!matched) return false;
            }

            return attachment.Kind switch {
                "photo" => options.IncludePhotos,
                "document" => options.IncludeDocuments,
                "audio" => options.IncludeAudio,
                "voice" => options.IncludeVoice,
                "video" => options.IncludeVideo,
                "sticker" => options.IncludeStickers,
                "sticker_animated" => options.IncludeStickers,
                "ugc_sticker" => options.IncludeStickers,
                "graffiti" => options.IncludeGraffiti,
                "link_preview" => options.IncludeLinkPreviews,
                _ => false
            };
        }

        private static bool Contains(string source, string value) {
            return source?.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static AttachmentDownloadQueueState CreateQueueState(long peerId) {
            return new AttachmentDownloadQueueState {
                PeerId = peerId,
                Status = "new",
                UpdatedAtUnix = DateTimeOffset.Now.ToUnixTimeSeconds()
            };
        }

        private static AttachmentDownloadQueueState LoadQueueState(string targetDirectory, long peerId) {
            string path = GetQueueStatePath(targetDirectory);
            if (!File.Exists(path)) return CreateQueueState(peerId);

            try {
                AttachmentDownloadQueueState state = JsonSerializer.Deserialize<AttachmentDownloadQueueState>(File.ReadAllText(path));
                if (state == null || state.PeerId != peerId) return CreateQueueState(peerId);
                state.Offsets ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                return state;
            } catch (Exception ex) {
                Log.Debug(ex, "Cannot read attachment download queue state {Path}", path);
                return CreateQueueState(peerId);
            }
        }

        private static void SaveQueueState(string targetDirectory, AttachmentDownloadQueueState state) {
            if (state == null) return;

            state.UpdatedAtUnix = DateTimeOffset.Now.ToUnixTimeSeconds();
            string path = GetQueueStatePath(targetDirectory);
            string json = JsonSerializer.Serialize(state, new JsonSerializerOptions {
                WriteIndented = true
            });
            File.WriteAllText(path, json);
        }

        private static string GetQueueStatePath(string targetDirectory) {
            return Path.Combine(targetDirectory, QueueStateFileName);
        }

        private static AttachmentResumeIndex LoadResumeIndex(string targetDirectory) {
            AttachmentResumeIndex index = new AttachmentResumeIndex();
            if (String.IsNullOrWhiteSpace(targetDirectory) || !Directory.Exists(targetDirectory)) return index;

            foreach (string sidecarPath in Directory.EnumerateFiles(targetDirectory, "*.json", SearchOption.TopDirectoryOnly)) {
                try {
                    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(sidecarPath));
                    JsonElement root = document.RootElement;
                    string sourceKey = ReadJsonString(root, "sourceKey");
                    string sha256 = ReadJsonString(root, "sha256");
                    string savedFile = ReadJsonString(root, "savedFile");
                    string filePath = String.IsNullOrWhiteSpace(savedFile)
                        ? sidecarPath[..^5]
                        : Path.Combine(Path.GetDirectoryName(sidecarPath), savedFile);

                    if (!File.Exists(filePath)) continue;
                    if (!String.IsNullOrWhiteSpace(sha256)) index.Hashes.Add(sha256);
                    if (!String.IsNullOrWhiteSpace(sourceKey) && !index.BySourceKey.ContainsKey(sourceKey)) {
                        index.BySourceKey[sourceKey] = new AttachmentResumeEntry {
                            Path = filePath,
                            Sha256 = sha256
                        };
                    }
                } catch (Exception ex) {
                    Log.Debug(ex, "Cannot read attachment sidecar {Path}", sidecarPath);
                }
            }

            return index;
        }

        private static bool IsAlreadyDownloaded(AttachmentResumeIndex index, DownloadableChatAttachment attachment, string deterministicPath, out string sha256) {
            sha256 = null;
            if (attachment == null) return false;

            if (!String.IsNullOrWhiteSpace(attachment.SourceKey)
                && index.BySourceKey.TryGetValue(attachment.SourceKey, out AttachmentResumeEntry entry)
                && File.Exists(entry.Path)) {
                sha256 = entry.Sha256;
                return true;
            }

            if (String.IsNullOrWhiteSpace(deterministicPath) || !File.Exists(deterministicPath)) return false;

            FileInfo file = new FileInfo(deterministicPath);
            if (file.Length <= 0) return false;
            if (attachment.DeclaredSize != null && (ulong)file.Length != attachment.DeclaredSize.Value) return false;

            string sidecarPath = deterministicPath + ".json";
            if (File.Exists(sidecarPath)) {
                try {
                    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(sidecarPath));
                    JsonElement root = document.RootElement;
                    string sidecarSourceKey = ReadJsonString(root, "sourceKey");
                    if (!String.IsNullOrWhiteSpace(sidecarSourceKey)
                        && !String.Equals(sidecarSourceKey, attachment.SourceKey, StringComparison.OrdinalIgnoreCase)) {
                        return false;
                    }

                    sha256 = ReadJsonString(root, "sha256");
                } catch {
                    return false;
                }
            }

            return true;
        }

        private static string ReadJsonString(JsonElement root, string propertyName) {
            return root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty(propertyName, out JsonElement value)
                && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        private static async Task<DownloadedChatAttachment> DownloadToTempFileAsync(DownloadableChatAttachment attachment, string targetPath, int speedLimitKbPerSecond) {
            string tempPath = targetPath + ".download";
            SafeDelete(tempPath);

            using var response = await LNet.GetAsync(attachment.Uri);
            response.EnsureSuccessStatusCode();

            await using Stream source = await response.Content.ReadAsStreamAsync();
            await using FileStream destination = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, true);
            using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
            Stopwatch stopwatch = Stopwatch.StartNew();
            long bytes = 0;

            try {
                while (true) {
                    int read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length));
                    if (read == 0) break;

                    await destination.WriteAsync(buffer.AsMemory(0, read));
                    hasher.AppendData(buffer, 0, read);
                    bytes += read;
                    await ThrottleAsync(bytes, speedLimitKbPerSecond, stopwatch);
                }
            } finally {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            return new DownloadedChatAttachment {
                TempPath = tempPath,
                Bytes = bytes,
                Sha256 = Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant()
            };
        }

        private static async Task ThrottleAsync(long bytes, int speedLimitKbPerSecond, Stopwatch stopwatch) {
            if (speedLimitKbPerSecond <= 0) return;
            double expectedMs = (double)bytes / (speedLimitKbPerSecond * 1024) * 1000;
            double delayMs = expectedMs - stopwatch.Elapsed.TotalMilliseconds;
            if (delayMs > 15) await Task.Delay((int)Math.Min(delayMs, 1000));
        }

        private static async Task WriteSidecarAsync(string targetPath, DownloadableChatAttachment attachment, DownloadedChatAttachment downloaded) {
            await using FileStream stream = new FileStream(targetPath + ".json", FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
            await using Utf8JsonWriter writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

            writer.WriteStartObject();
            writer.WriteString("kind", attachment.Kind);
            writer.WriteString("attachmentId", attachment.AttachmentId);
            writer.WriteString("sourceKey", attachment.SourceKey);
            writer.WriteString("originalName", attachment.OriginalName);
            writer.WriteString("extension", attachment.Extension);
            if (attachment.DeclaredSize != null) {
                writer.WriteNumber("declaredSize", attachment.DeclaredSize.Value);
            } else {
                writer.WriteNull("declaredSize");
            }
            writer.WriteNumber("actualSize", downloaded.Bytes);
            writer.WriteString("sha256", downloaded.Sha256);
            writer.WriteString("url", attachment.Uri.AbsoluteUri);
            writer.WriteNumber("peerId", attachment.PeerId);
            writer.WriteNumber("parentConversationMessageId", attachment.ParentConversationMessageId);
            writer.WriteNumber("conversationMessageId", attachment.ConversationMessageId);
            writer.WriteNumber("senderId", attachment.SenderId);
            writer.WriteString("sentTime", attachment.SentTime);
            writer.WriteString("savedFile", Path.GetFileName(targetPath));
            writer.WriteEndObject();
            await writer.FlushAsync();
        }

        private static string BuildFileName(DownloadableChatAttachment attachment) {
            string original = SanitizeFileName(attachment.OriginalName, attachment.Kind, 70);
            string stem = $"{attachment.SentTime:yyyyMMdd_HHmmss}_peer{attachment.PeerId}_from{attachment.SenderId}_cmid{attachment.ParentConversationMessageId}_{attachment.Kind}_{original}";
            stem = SanitizeFileName(stem, attachment.AttachmentId, 170);
            return stem.EndsWith(attachment.Extension, StringComparison.OrdinalIgnoreCase) ? stem : stem + attachment.Extension;
        }

        private static string GetUniquePath(string targetPath) {
            if (!File.Exists(targetPath)) return targetPath;

            string directory = Path.GetDirectoryName(targetPath);
            string name = Path.GetFileNameWithoutExtension(targetPath);
            string extension = Path.GetExtension(targetPath);
            for (int i = 2; i < 10000; i++) {
                string candidate = Path.Combine(directory, $"{name}_{i}{extension}");
                if (!File.Exists(candidate)) return candidate;
            }
            return Path.Combine(directory, $"{name}_{Guid.NewGuid():N}{extension}");
        }

        private static bool TryGetBestVideoUri(Video video, out Uri uri) {
            uri = null;
            if (video?.Files == null) return false;

            return TryCreateAbsoluteUri(video.Files.MP4p1080, out uri)
                || TryCreateAbsoluteUri(video.Files.MP4p720, out uri)
                || TryCreateAbsoluteUri(video.Files.MP4p480, out uri)
                || TryCreateAbsoluteUri(video.Files.MP4p360, out uri)
                || TryCreateAbsoluteUri(video.Files.MP4p240, out uri);
        }

        private static bool TryGetPhotoUri(Photo photo, out Uri uri) {
            uri = null;
            if (photo?.Sizes == null || photo.Sizes.Count == 0) return false;
            PhotoSizes size = photo.Sizes
                .Where(s => s != null)
                .OrderByDescending(s => s.Width * s.Height)
                .FirstOrDefault();
            return TryCreateAbsoluteUri(size?.Url, out uri) || TryCreateAbsoluteUri(size?.Src, out uri);
        }

        private static Uri TryGetGraffitiUri(Graffiti graffiti) {
            if (graffiti == null) return null;
            if (TryCreateAbsoluteUri(graffiti.Src, out Uri src)) return src;
            if (TryCreateAbsoluteUri(graffiti.Url, out Uri url)) return url;
            return null;
        }

        private static bool TryGetStickerUri(Sticker sticker, out Uri uri, out bool animated) {
            uri = null;
            animated = false;
            if (sticker == null) return false;

            if (TryCreateAbsoluteUri(sticker.AnimationUrl, out uri)) {
                animated = true;
                return true;
            }

            StickerImage image = sticker.ImagesWithBackground?.OrderByDescending(i => i.Width * i.Height).FirstOrDefault()
                ?? sticker.Images?.OrderByDescending(i => i.Width * i.Height).FirstOrDefault()
                ?? sticker.Render?.Images?.OrderByDescending(i => i.Width * i.Height).FirstOrDefault();

            return TryCreateAbsoluteUri(image?.Url, out uri);
        }

        private static bool TryGetUGCStickerUri(UGCSticker sticker, out Uri uri) {
            uri = null;
            StickerImage image = sticker?.Images?.OrderByDescending(i => i.Width * i.Height).FirstOrDefault();
            return TryCreateAbsoluteUri(image?.Url, out uri);
        }

        private static bool TryCreateAbsoluteUri(string value, out Uri uri) {
            uri = null;
            return !String.IsNullOrWhiteSpace(value) && Uri.TryCreate(value, UriKind.Absolute, out uri);
        }

        private static string GetExtensionFromUri(Uri uri, string fallback) {
            string extension = uri == null ? null : Path.GetExtension(uri.AbsolutePath);
            return NormalizeExtension(extension, fallback);
        }

        private static string NormalizeExtension(string extension, string fallback) {
            if (String.IsNullOrWhiteSpace(extension)) extension = fallback;
            extension = extension.Trim();
            if (!extension.StartsWith('.')) extension = "." + extension;
            extension = SanitizeFileName(extension, fallback, 12).ToLowerInvariant();
            return extension.StartsWith('.') ? extension : "." + extension;
        }

        private static void SafeDelete(string path) {
            try {
                if (!String.IsNullOrWhiteSpace(path) && File.Exists(path)) File.Delete(path);
            } catch {
            }
        }

        public static string FormatBytes(ulong bytes) {
            if (bytes >= 1024UL * 1024 * 1024) return $"{Math.Round(bytes / 1024d / 1024d / 1024d, 2)} GB";
            if (bytes >= 1024UL * 1024) return $"{Math.Round(bytes / 1024d / 1024d, 1)} MB";
            if (bytes >= 1024UL) return $"{Math.Round(bytes / 1024d, 1)} KB";
            return $"{bytes} B";
        }
    }
}
