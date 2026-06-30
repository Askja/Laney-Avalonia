using ELOR.Laney.Helpers;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace ELOR.Laney.Core {
    public enum StickerAnimationMode {
        Always = 0,
        Hover = 1,
        Click = 2,
        Never = 3
    }

    public static class LocalStickerSendModeIds {
        public const string Auto = "auto";
        public const string Graffiti = "graffiti";
        public const string Image = "image";
        public const string File = "file";

        public static readonly string[] All = [Auto, Graffiti, Image, File];

        public static string Normalize(string mode) {
            if (String.IsNullOrWhiteSpace(mode)) return Auto;

            string normalized = mode.Trim().ToLowerInvariant();
            return All.Contains(normalized) ? normalized : Auto;
        }
    }

    public static class InterfaceProfileIds {
        public const string Custom = "custom";
        public const string Compact = "compact";
        public const string Balanced = "balanced";
        public const string Touch = "touch";
        public const string Work = "work";
        public const string Night = "night";
        public const string LowRam = "low_ram";
        public const string Streamer = "streamer";
    }

    public static class ChatListDensityIds {
        public const string Small = "small";
        public const string Medium = "medium";
        public const string Large = "large";
    }

    public static class ChatListLayoutIds {
        public const string Classic = "classic";
        public const string Compact = "compact";
        public const string Telegram = "telegram";
        public const string MediaRich = "media_rich";
        public const string SplitFolder = "split_folder";
    }

    public static class AvatarShapeIds {
        public const string Circle = "circle";
        public const string Squircle = "squircle";
        public const string Rounded = "rounded";
        public const string Square = "square";
    }

    public static class AvatarSizeIds {
        public const string Auto = "auto";
        public const string Small = "small";
        public const string Medium = "medium";
        public const string Large = "large";
    }

    public static class TextSizeIds {
        public const string Auto = "auto";
        public const string Small = "small";
        public const string Medium = "medium";
        public const string Large = "large";
    }

    public static class BubbleWidthIds {
        public const string Narrow = "narrow";
        public const string Medium = "medium";
        public const string Wide = "wide";
        public const string Full = "full";
    }

    public static class BubbleDensityIds {
        public const string Compact = "compact";
        public const string Normal = "normal";
        public const string Air = "air";
        public const string LegacyDefault = "default";
        public const string LegacyRelaxed = "relaxed";
    }

    public static class BubbleStyleIds {
        public const string Vk = "default";
        public const string Telegram = "telegram";
        public const string Minimal = "minimal";
        public const string Outline = "outline";
        public const string Flat = "flat";
        public const string LegacySharp = "sharp";
        public const string LegacyRound = "round";
    }

    public static class MessageCheckmarkStyleIds {
        public const string Vk = "vk";
        public const string Compact = "compact";
        public const string Minimal = "minimal";
        public const string Hidden = "hidden";

        public static readonly string[] All = [Vk, Compact, Minimal, Hidden];

        public static string Normalize(string style) {
            if (String.IsNullOrWhiteSpace(style)) return Vk;

            string normalized = style.Trim().ToLowerInvariant();
            return All.Contains(normalized) ? normalized : Vk;
        }
    }

    public static class ChatOpenBehaviorIds {
        public const string Bottom = "bottom";
        public const string FirstUnread = "first_unread";

        public static readonly string[] All = [Bottom, FirstUnread];

        public static string Normalize(string behavior) {
            if (String.IsNullOrWhiteSpace(behavior)) return Bottom;

            string normalized = behavior.Trim().ToLowerInvariant();
            return All.Contains(normalized) ? normalized : Bottom;
        }
    }

    public static class AppIconVariantIds {
        public const string Auto = "auto";
        public const string VkClassic = "vk_classic";
        public const string VkColor = "vk_color";
        public const string VkBlue = "vk_blue";
        public const string VkWhite = "vk_white";
        public const string AnimeStar = "anime_star";
        public const string AnimeAi = "anime_ai";
        public const string AnimeAkane = "anime_akane";

        public static readonly string[] All = [Auto, VkClassic, VkColor, VkBlue, VkWhite, AnimeStar, AnimeAi, AnimeAkane];

        public static string Normalize(string icon) {
            if (String.IsNullOrWhiteSpace(icon)) return Auto;

            string normalized = icon.Trim().ToLowerInvariant();
            return All.Contains(normalized) ? normalized : Auto;
        }
    }

    public static class AutoStatusModeIds {
        public const string Busy = "busy";
        public const string Work = "work";
        public const string Gaming = "gaming";
        public const string Sleep = "sleep";
        public const string DoNotDisturb = "do_not_disturb";

        public static readonly string[] All = [Busy, Work, Gaming, Sleep, DoNotDisturb];

        public static string Normalize(string mode) {
            if (String.IsNullOrWhiteSpace(mode)) return Work;

            string normalized = mode.Trim().ToLowerInvariant();
            return All.Contains(normalized) ? normalized : Work;
        }
    }

    public static class NotificationDeliveryModeIds {
        public const string Custom = "custom";
        public const string System = "system";
        public const string Both = "both";

        public static readonly string[] All = [Custom, System, Both];

        public static string Normalize(string mode) {
            if (String.IsNullOrWhiteSpace(mode)) return Custom;

            string normalized = mode.Trim().ToLowerInvariant();
            return All.Contains(normalized) ? normalized : Custom;
        }
    }

    public static class NotificationPositionIds {
        public const string BottomRight = "bottom_right";
        public const string BottomLeft = "bottom_left";
        public const string TopRight = "top_right";
        public const string TopLeft = "top_left";

        public static readonly string[] All = [BottomRight, BottomLeft, TopRight, TopLeft];

        public static string Normalize(string position) {
            if (String.IsNullOrWhiteSpace(position)) return BottomRight;

            string normalized = position.Trim().ToLowerInvariant();
            return All.Contains(normalized) ? normalized : BottomRight;
        }
    }

    public static class AudioDspModeIds {
        public const string Off = "off";
        public const string Flat = "flat";
        public const string Normalize = "normalize";
        public const string VoiceClarity = "voice";
        public const string Night = "night";
        public const string BassBoost = "bass";

        public static readonly string[] All = [Off, Flat, Normalize, VoiceClarity, Night, BassBoost];

        public static string NormalizeMode(string mode) {
            if (String.IsNullOrWhiteSpace(mode)) return Off;

            string normalized = mode.Trim().ToLowerInvariant();
            return All.Contains(normalized) ? normalized : Off;
        }
    }

    public static class EmojiPackIds {
        public const string Inherit = "inherit";
        public const string System = "system";
        public const string Vk = "vk";
        public const string TelegramLike = "telegram_like";
        public const string Noto = "noto";
        public const string Twemoji = "twemoji";
        public const string Fallback = "fallback";
        public const string Custom = "custom";

        public static readonly string[] All = [System, Vk, TelegramLike, Noto, Twemoji, Fallback, Custom];
        public static readonly string[] AllWithInherit = [Inherit, System, Vk, TelegramLike, Noto, Twemoji, Fallback, Custom];

        public static string Normalize(string packId, bool allowInherit = false) {
            if (String.IsNullOrWhiteSpace(packId)) return allowInherit ? Inherit : System;

            string normalized = packId.Trim().ToLowerInvariant();
            string[] allowed = allowInherit ? AllWithInherit : All;
            return allowed.Contains(normalized) ? normalized : allowInherit ? Inherit : System;
        }
    }

    [Flags]
    public enum ShadowBannedAttachmentKinds {
        None = 0,
        Voice = 1,
        Link = 2,
        Sticker = 4,
        Graffiti = 8,
        Forwarded = 16
    }

    public sealed class SelfDestructMessageSchedule {
        public int ConversationMessageId { get; set; }
        public long HideAtUnix { get; set; }
        public bool BestEffortDelete { get; set; }
    }

    public sealed class ScheduledMessageItem {
        public string Id { get; set; }
        public long SessionId { get; set; }
        public long GroupId { get; set; }
        public long PeerId { get; set; }
        public string Text { get; set; }
        public long NextSendUnix { get; set; }
        public int RepeatIntervalMinutes { get; set; }
        public long CreatedAtUnix { get; set; }
    }

    public sealed class PeerDraftHistoryItem {
        public string Text { get; set; }
        public long UpdatedAtUnix { get; set; }
    }

    public sealed class PeerLocalNoteHistoryItem {
        public string Text { get; set; }
        public long UpdatedAtUnix { get; set; }
    }

    public sealed class AudioPlaybackHistoryItem {
        public string Key { get; set; }
        public string Type { get; set; }
        public long OwnerId { get; set; }
        public int Id { get; set; }
        public string Title { get; set; }
        public string Performer { get; set; }
        public long DurationMs { get; set; }
        public long PositionMs { get; set; }
        public long UpdatedAtUnix { get; set; }
    }

    public static class Settings {
        private const string VaultTokenMarker = "vault:v1";
        private const int PeerLocalNoteHistoryLimit = 12;
        private const int AudioPlaybackHistoryLimit = 100;
        private static Dictionary<string, object> _settings = new Dictionary<string, object>();
        private static FileStream _file;
        public static string FilePath { get; private set; }

        public delegate void SettingChangedDelegate(string key, object value);
        public static event SettingChangedDelegate SettingChanged;

        #region Initialization

        public static void Initialize() {
            FilePath = Path.Combine(App.LocalDataPath, "settings.xml");
            _file = new FileStream(FilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 4096);

            if (!OperatingSystem.IsMacOS()) _file.Lock(0, 0);

            byte[] fileBytes = new byte[_file.Length];
            _file.ReadExactly(fileBytes);

            UTF8Encoding enc = new UTF8Encoding(true);
            string content = enc.GetString(fileBytes);

            if (content.Length == 0) return;
            try {
                _settings = ElorPrefs.Deserialize(content);
            } catch (Exception ex) {
                Log.Error(ex, "An error occured while reading settings file!");
            }
        }

        private static readonly SemaphoreSlim fileWriteSemaphore = new SemaphoreSlim(1, 1);
        private static void UpdateFile() {
            string content = ElorPrefs.SerializeToXML(_settings);
            byte[] bytes = Encoding.UTF8.GetBytes(content);

            new Action(async () => {
                await fileWriteSemaphore.WaitAsync();
                try {
                    _file.Position = 0;
                    _file.SetLength(bytes.Length);
                    await _file.WriteAsync(bytes);
                    await _file.FlushAsync();
                } finally {
                    fileWriteSemaphore.Release();
                }
            })();
        }

        public static void UnlockSettingsFile(bool doNotUpdateFile = false) {
            if (!doNotUpdateFile) UpdateFile();
            _file.Close();
            _file.Dispose();
        }

        #endregion

        #region Getter/setter

        public static T Get<T>(string key, T defaultValue = default) {
            if (!_settings.ContainsKey(key)) return defaultValue;
            try {
                object v = _settings[key];
                return v != null ? (T)_settings[key] : defaultValue;
            } catch {
                return defaultValue;
            }
        }

        public static void Set(string key, object value) {
            AddOrReplace(key, value);
            UpdateFile();
            SettingChanged?.Invoke(key, value);
        }

        public static void SetBatch(Dictionary<string, object> settings) {
            foreach (var setting in settings) {
                AddOrReplace(setting.Key, setting.Value);
                SettingChanged?.Invoke(setting.Key, setting.Value);
            }
            UpdateFile();
        }

        public static string ExportClientSettingsToJson() {
            Dictionary<string, object> snapshot = _settings
                .Where(s => IsExportableClientSetting(s.Key))
                .OrderBy(s => s.Key)
                .ToDictionary(s => s.Key, s => s.Value);

            Dictionary<string, object> document = new Dictionary<string, object> {
                { "app", "Laney" },
                { "version", 1 },
                { "exportedAtUtc", DateTimeOffset.UtcNow.ToString("O") },
                { "settings", snapshot }
            };

            return JsonSerializer.Serialize(document, new JsonSerializerOptions {
                WriteIndented = true
            });
        }

        public static int ImportClientSettingsFromJson(string json) {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            JsonElement settingsElement = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("settings", out JsonElement wrappedSettings)
                ? wrappedSettings
                : root;

            if (settingsElement.ValueKind != JsonValueKind.Object) {
                throw new FormatException("Settings JSON must contain an object.");
            }

            Dictionary<string, object> imported = new Dictionary<string, object>();
            foreach (JsonProperty property in settingsElement.EnumerateObject()) {
                if (!IsExportableClientSetting(property.Name)) continue;

                object value = ConvertJsonValue(property.Value);
                if (value == null) continue;
                imported[property.Name] = value;
            }

            if (imported.Count > 0) SetBatch(imported);
            return imported.Count;
        }

        public static string GetVkAccessToken() {
            string token = Get<string>(VK_TOKEN);
            if (String.IsNullOrWhiteSpace(token)) return null;

            if (String.Equals(token, VaultTokenMarker, StringComparison.Ordinal)) {
                return SecureVault.GetSecret(VK_TOKEN);
            }

            string nonce = Get<string>(VK_TOKEN + "1");
            string tag = Get<string>(VK_TOKEN + "2");

            try {
                string accessToken;
                if (String.IsNullOrEmpty(nonce) && tag == null) {
                    accessToken = token;
                } else {
                    accessToken = Encryption.Decrypt(GetLegacyTokenKey(), token, nonce, tag);
                }

                if (!String.IsNullOrWhiteSpace(accessToken)) {
                    AddOrReplace(VK_TOKEN, accessToken);
                    UpdateFile();
                }

                return accessToken;
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot read VK access token from legacy settings storage.");
                return null;
            }
        }

        private static void AddOrReplace(string key, object value) {
            if (key == VK_TOKEN) {
                AddOrReplaceVkToken(value);
                return;
            }

            if (value == null) {
                if (_settings.ContainsKey(key)) {
                    _settings.Remove(key);
                }
                return;
            }

            if (_settings.ContainsKey(key)) {
                _settings[key] = value;
            } else {
                _settings.Add(key, value);
            }
        }

        private static void AddOrReplaceVkToken(object value) {
            _settings.Remove(VK_TOKEN + "1");
            _settings.Remove(VK_TOKEN + "2");

            if (value == null) {
                _settings.Remove(VK_TOKEN);
                SecureVault.DeleteSecret(VK_TOKEN);
                return;
            }

            SecureVault.SetSecret(VK_TOKEN, (string)value);
            if (_settings.ContainsKey(VK_TOKEN)) {
                _settings[VK_TOKEN] = VaultTokenMarker;
            } else {
                _settings.Add(VK_TOKEN, VaultTokenMarker);
            }
        }

        private static byte[] GetLegacyTokenKey() {
            return AssetsManager.BinaryPayload.Skip(576).Take(32).OrderDescending().ToArray();
        }

        private static object ConvertJsonValue(JsonElement value) {
            return value.ValueKind switch {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number when value.TryGetInt32(out int intValue) => intValue,
                JsonValueKind.Number when value.TryGetInt64(out long longValue) => longValue,
                JsonValueKind.Number when value.TryGetDouble(out double doubleValue) => doubleValue,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }

        private static bool IsExportableClientSetting(string key) {
            if (String.IsNullOrWhiteSpace(key)) return false;
            if (key == VK_TOKEN || key == VK_TOKEN + "1" || key == VK_TOKEN + "2") return false;
            if (key.StartsWith(PEER_LOCAL_E2E_STATE_PREFIX, StringComparison.Ordinal)) return false;
            if (key.StartsWith(PEER_CURRENT_DRAFT_PREFIX, StringComparison.Ordinal)) return false;
            if (key.StartsWith(PEER_DRAFT_HISTORY_PREFIX, StringComparison.Ordinal)) return false;
            if (key.StartsWith(PEER_LOCAL_HIDDEN_MESSAGES_PREFIX, StringComparison.Ordinal)) return false;
            if (key.StartsWith(PEER_LOCAL_SELF_DESTRUCT_PREFIX, StringComparison.Ordinal)) return false;
            if (key.StartsWith(PEER_LOCAL_MESSAGE_REACTIONS_PREFIX, StringComparison.Ordinal)) return false;
            if (key.StartsWith(PEER_LOCAL_NOTE_PREFIX, StringComparison.Ordinal)) return false;
            if (key.StartsWith(PEER_LOCAL_NOTE_HISTORY_PREFIX, StringComparison.Ordinal)) return false;
            if (key == SCHEDULED_MESSAGES) return false;

            return ExportableClientSettingKeys.Contains(key)
                || ExportableClientSettingPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.Ordinal));
        }

        #endregion

        #region Constants

        public const string TEST_STRING = "test_string";

        public const string VK_USER_ID = "user_id";
        public const string VK_TOKEN = "access_token";
        public const string GROUPS = "groups";
        public const string GROUPS_BACKGROUND_LONGPOLL_LIMIT = "groups_background_longpoll_limit";
        public const string LOCAL_ARCHIVED_PEERS = "local_archived_peers";
        public const string LOCAL_BACKUP_DIRECTORY = "local_backup_directory";
        public const string AUTOSTART_ENABLED = "autostart_enabled";
        public const string AUTOSTART_MINIMIZED = "autostart_minimized";

        public const string WIN_SIZE_W = "winw";
        public const string WIN_SIZE_H = "winh";
        public const string WIN_POS_X = "winx";
        public const string WIN_POS_Y = "winy";
        public const string WIN_MAXIMIZED = "winm";

        public const string LANGUAGE = "lang";
        public const string SEND_VIA_ENTER = "sent_via_enter";
        public const string DONT_PARSE_LINKS = "dont_parse_liks";
        public const string DISABLE_MENTIONS = "disable_mentions";
        public const string STICKERS_SUGGEST = "suggest_stickers";
        public const string STICKERS_ANIMATE = "animate_stickers";
        public const string STICKERS_ANIMATION_MODE = "sticker_animation_mode";
        public const string LOCAL_STICKER_SEND_MODE = "local_sticker_send_mode";
        public const string EMOJI_PACK = "emoji_pack";
        public const string EMOJI_CUSTOM_PACK_PATH = "emoji_custom_pack_path";
        public const string LOCAL_VOICE_TRANSCRIPTION_ENABLED = "local_voice_transcription_enabled";
        public const string LOCAL_VOICE_TRANSCRIPTION_MODEL_PATH = "local_voice_transcription_model_path";
        public const string LOCAL_VOICE_TRANSCRIPTION_LANGUAGE = "local_voice_transcription_language";
        public const string LOCAL_OCR_ENABLED = "local_ocr_enabled";
        public const string LOCAL_OCR_TESSERACT_PATH = "local_ocr_tesseract_path";
        public const string LOCAL_OCR_LANGUAGE = "local_ocr_language";
        public const string FIRST_RUN_ONBOARDING_DONE = "first_run_onboarding_done";
        public const string INTERFACE_PROFILE = "interface_profile";
        public const string STREAMER_MODE = "streamer_mode";
        public const string LOW_MOTION_MODE = "low_motion_mode";
        public const string LOW_MEMORY_MODE = "low_memory_mode";
        public const string LOW_TRAFFIC_MODE = "low_traffic_mode";
        public const string AUTO_BLOCK_BLACKLISTED_USERS = "auto_block_blacklisted_users";
        public const string AUTO_LOCK_ENABLED = "auto_lock_enabled";
        public const string AUTO_LOCK_IDLE_MINUTES = "auto_lock_idle_minutes";
        public const string PANIC_LOCK_CLEAR_CLIPBOARD = "panic_lock_clear_clipboard";
        public const string AUTO_LOCK_CLEAR_CLIPBOARD = "auto_lock_clear_clipboard";

        public const string THEME = "theme";
        public const string ACCENT_COLOR = "accent_color";
        public const string APP_FONT_FAMILY = "app_font_family";
        public const string APP_ICON_VARIANT = "app_icon_variant";
        public const string CHAT_BACKGROUND = "chat_background";
        public const string CHAT_BACKGROUND_IMAGE = "chat_background_image";
        public const string CHAT_ITEM_MORE_ROWS = "chat_item_more_rows";
        public const string CHAT_LIST_WIDTH = "chat_list_width";
        public const string CHAT_LIST_DENSITY = "chat_list_density";
        public const string CHAT_LIST_LAYOUT = "chat_list_layout";
        public const string CHAT_LIST_AVATAR_SIZE = "chat_list_avatar_size";
        public const string CHAT_LIST_AVATAR_SHAPE = "chat_list_avatar_shape";
        public const string CHAT_LIST_FONT_SIZE = "chat_list_font_size";
        public const string MESSAGE_AVATAR_SIZE = "message_avatar_size";
        public const string MESSAGE_FONT_SIZE = "message_font_size";
        public const string MESSAGE_BUBBLE_WIDTH = "message_bubble_width";
        public const string MESSAGE_BUBBLE_DENSITY = "message_bubble_density";
        public const string MESSAGE_BUBBLE_STYLE = "message_bubble_style";
        public const string MESSAGE_BUBBLE_OPACITY = "message_bubble_opacity";
        public const string MESSAGE_BUBBLE_AUTO_COLOR = "message_bubble_auto_color";
        public const string MESSAGE_CHECKMARK_STYLE = "message_checkmark_style";
        public const string CHAT_OPEN_BEHAVIOR = "chat_open_behavior";
        public const string SHOW_ACCOUNT_RAIL = "show_account_rail";
        public const string COMPOSER_ACTION_FORMATTING = "composer_action_formatting";
        public const string COMPOSER_ACTION_QUICK = "composer_action_quick";
        public const string COMPOSER_ACTION_GROUP_TEMPLATES = "composer_action_group_templates";
        public const string COMPOSER_ACTION_STICKERS = "composer_action_stickers";
        public const string CHAT_HEADER_ACTION_SEARCH = "chat_header_action_search";
        public const string CHAT_HEADER_ACTION_PROFILE = "chat_header_action_profile";
        public const string CHAT_HEADER_ACTION_MORE = "chat_header_action_more";
        public const string KEYMAP_COMMAND_PALETTE = "keymap_command_palette";
        public const string KEYMAP_GLOBAL_SEARCH = "keymap_global_search";
        public const string KEYMAP_CHAT_SEARCH = "keymap_chat_search";
        public const string KEYMAP_FOCUS_COMPOSER = "keymap_focus_composer";
        public const string KEYMAP_ATTACHMENTS = "keymap_attachments";
        public const string KEYMAP_STICKERS = "keymap_stickers";
        public const string KEYMAP_SETTINGS = "keymap_settings";
        public const string KEYMAP_PANIC_LOCK = "keymap_panic_lock";
        public const string KEYMAP_BACK = "keymap_back";

        public const string NOTIF_PRIVATE = "notifications_private";
        public const string NOTIF_PRIVATE_SOUND = "notifications_private_sound";
        public const string NOTIF_GCHAT = "notifications_group_chat";
        public const string NOTIF_GCHAT_SOUND = "notifications_group_chat_sound";
        public const string NOTIF_DONT_ANNOY_ME = "notifications_dont_annoy_me";
        public const string NOTIF_DONT_ANNOY_ME_START_HOUR = "notifications_dont_annoy_me_start_hour";
        public const string NOTIF_DONT_ANNOY_ME_END_HOUR = "notifications_dont_annoy_me_end_hour";
        public const string NOTIF_DONT_ANNOY_ME_ALLOW_MENTIONS = "notifications_dont_annoy_me_allow_mentions";
        public const string NOTIF_DONT_ANNOY_ME_ALLOW_IMPORTANT = "notifications_dont_annoy_me_allow_important";
        public const string NOTIF_DONT_ANNOY_ME_KEYWORDS = "notifications_dont_annoy_me_keywords";
        public const string NOTIF_DELIVERY_MODE = "notifications_delivery_mode";
        public const string NOTIF_CUSTOM_POSITION = "notifications_custom_position";
        public const string NOTIF_CUSTOM_STACK_LIMIT = "notifications_custom_stack_limit";
        public const string NOTIF_CUSTOM_TIMEOUT_SECONDS = "notifications_custom_timeout_seconds";
        public const string NOTIF_CUSTOM_FAST_ACTIONS = "notifications_custom_fast_actions";
        public const string NOTIF_CUSTOM_SHOW_AVATARS = "notifications_custom_show_avatars";
        public const string NOTIF_CUSTOM_SHOW_IMAGES = "notifications_custom_show_images";
        public const string AUTO_STATUS_ENABLED = "auto_status_enabled";
        public const string AUTO_STATUS_MODE = "auto_status_mode";
        public const string AUTO_STATUS_SCHEDULE_ENABLED = "auto_status_schedule_enabled";
        public const string AUTO_STATUS_SCHEDULE_START_HOUR = "auto_status_schedule_start_hour";
        public const string AUTO_STATUS_SCHEDULE_END_HOUR = "auto_status_schedule_end_hour";
        public const string AUTO_STATUS_SCHEDULE_MODE = "auto_status_schedule_mode";
        public const string AUTO_STATUS_IDLE_ENABLED = "auto_status_idle_enabled";
        public const string AUTO_STATUS_IDLE_MINUTES = "auto_status_idle_minutes";
        public const string AUTO_STATUS_IDLE_MODE = "auto_status_idle_mode";

        public const string AUDIO_PLAYER_LOOP = "audio_player_loop";
        public const string AUDIO_PLAYER_VOLUME = "audio_player_volume";
        public const string AUDIO_PLAYER_TRACK_RATE = "audio_player_track_rate_x100";
        public const string AUDIO_PLAYER_PODCAST_RATE = "audio_player_podcast_rate_x100";
        public const string AUDIO_PLAYER_VOICE_RATE = "audio_player_voice_rate_x100";
        public const string AUDIO_PLAYER_SEEK_SECONDS = "audio_player_seek_seconds";
        public const string AUDIO_PLAYER_HISTORY = "audio_player_history";
        public const string AUDIO_DSP_MODE = "audio_dsp_mode";
        public const string VOICE_MESSAGE_RESUME_ENABLED = "voice_message_resume_enabled";
        public const string VOICE_MESSAGE_SKIP_SILENCE = "voice_message_skip_silence";
        public const string VOICE_MESSAGE_RESUME_POSITION_PREFIX = "voice_message_resume_position_";

        public const string DEBUG_LOGS_CORE = "log_to_file_core";
        public const string DEBUG_LOGS_LP = "log_to_file_lp";
        public const string DEBUG_LOGS_BITMAPMANAGER = "log_to_file_bm";
        public const string DEBUG_LOGS_LNET = "log_to_file_lnet";
        public const string DEBUG_LOGS_MESSAGERENDERING = "log_to_file_msgui";

        public const string DEBUG_MARK_AS_READ_OFF = "no_mark_as_read";

        public const string DEBUG_FPS = "dbg_fps";
        public const string DEBUG_COUNTERS_CHAT = "dbg_counters_chat";
        public const string DEBUG_COUNTERS_RAM = "dbg_counters_ram";
        public const string DEBUG_CONTEXT_MENU = "dbg_dev_context_menu";
        public const string DEBUG_GALLERY = "dbg_gallery";
        public const string DEBUG_LOAD_IMAGES_SEQUENTIAL = "dbg_load_images_sequential";
        public const string DEBUG_API_MONITOR = "dbg_api_monitor";
        public const string API_DOMAIN = "api_domain";
        public const string API_VERSION = "api_version";
        public const string PROXY_ENABLED = "proxy_enabled";
        public const string PROXY_URI = "proxy_uri";
        public const string PROXY_BYPASS_LOCAL = "proxy_bypass_local";
        public const string INVISIBLE_MODE = "invisible_mode";
        public const string INVISIBLE_DISABLE_SET_ONLINE = "invisible_disable_set_online";
        public const string INVISIBLE_DISABLE_READ_RECEIPTS = "invisible_disable_read_receipts";
        public const string INVISIBLE_DISABLE_VOICE_LISTENED = "invisible_disable_voice_listened";
        public const string INVISIBLE_DISABLE_STORY_VIEWED = "invisible_disable_story_viewed";
        public const string INVISIBLE_DISABLE_TYPING_STATUS = "invisible_disable_typing_status";

        public const string IMAGE_CACHE_DEFAULT_TTL_MINUTES = "image_cache_default_ttl_minutes";
        public const string IMAGE_CACHE_AVATAR_TTL_MINUTES = "image_cache_avatar_ttl_minutes";
        public const string IMAGE_CACHE_ATTACHMENT_TTL_MINUTES = "image_cache_attachment_ttl_minutes";
        public const string IMAGE_CACHE_E2E_TTL_MINUTES = "image_cache_e2e_ttl_minutes";
        public const string IMAGE_CACHE_RAM_LIMIT_MB = "image_cache_ram_limit_mb";
        public const string MEDIA_MEMORY_BUDGET_MB = "media_memory_budget_mb";

        public const string PEER_LOCAL_NOTE_PREFIX = "peer_local_note_";
        public const string PEER_LOCAL_NOTE_HISTORY_PREFIX = "peer_local_note_history_";
        public const string PEER_LOCAL_ALIAS_PREFIX = "peer_local_alias_";
        public const string PEER_LOCAL_AVATAR_PREFIX = "peer_local_avatar_";
        public const string PEER_LOCAL_TAGS_PREFIX = "peer_local_tags_";
        public const string PEER_LOCAL_THEME_PREFIX = "peer_local_theme_";
        public const string PEER_LOCAL_BACKGROUND_IMAGE_PREFIX = "peer_local_background_image_";
        public const string PEER_LOCAL_BACKGROUND_DIM_PREFIX = "peer_local_background_dim_";
        public const string PEER_LOCAL_BACKGROUND_BLUR_PREFIX = "peer_local_background_blur_";
        public const string PEER_LOCAL_BACKGROUND_BRIGHTNESS_PREFIX = "peer_local_background_brightness_";
        public const string PEER_LOCAL_ACCENT_PREFIX = "peer_local_accent_";
        public const string PEER_LOCAL_DENSITY_PREFIX = "peer_local_density_";
        public const string PEER_LOCAL_FONT_PREFIX = "peer_local_font_";
        public const string PEER_LOCAL_BUBBLE_COLOR_PREFIX = "peer_local_bubble_color_";
        public const string PEER_LOCAL_BUBBLE_STYLE_PREFIX = "peer_local_bubble_style_";
        public const string PEER_LOCAL_EMOJI_PACK_PREFIX = "peer_local_emoji_pack_";
        public const string PEER_QUICK_REPLIES_PREFIX = "peer_quick_replies_";
        public const string PEER_QUICK_REACTIONS_PREFIX = "peer_quick_reactions_";
        public const string PERSON_QUICK_REPLIES_PREFIX = "person_quick_replies_";
        public const string CHAT_FILTER_QUICK_REPLIES_PREFIX = "chat_filter_quick_replies_";
        public const string PEER_PINNED_QUICK_REPLY_PREFIX = "peer_pinned_quick_reply_";
        public const string SCHEDULED_MESSAGES = "scheduled_messages";
        public const string PEER_CURRENT_DRAFT_PREFIX = "peer_current_draft_";
        public const string PEER_DRAFT_HISTORY_PREFIX = "peer_draft_history_";
        public const string PEER_LOCAL_HIDDEN_MESSAGES_PREFIX = "peer_local_hidden_messages_";
        public const string PEER_LOCAL_QUIET_UNTIL_PREFIX = "peer_local_quiet_until_";
        public const string PEER_LOCAL_QUIET_TEXT_RULES_PREFIX = "peer_local_quiet_text_rules_";
        public const string PEER_LOCAL_ANTISPAM_ENABLED_PREFIX = "peer_local_antispam_enabled_";
        public const string PEER_LOCAL_MUTED_SENDERS_PREFIX = "peer_local_muted_senders_";
        public const string PEER_LOCAL_SHADOW_BANNED_SENDERS_PREFIX = "peer_local_shadow_banned_senders_";
        public const string PEER_LOCAL_SHADOW_BANNED_TEXT_RULES_PREFIX = "peer_local_shadow_banned_text_rules_";
        public const string PEER_LOCAL_SHADOW_BANNED_ATTACHMENT_KINDS_PREFIX = "peer_local_shadow_banned_attachment_kinds_";
        public const string PEER_LOCAL_SELF_DESTRUCT_PREFIX = "peer_local_self_destruct_";
        public const string PEER_LOCAL_MESSAGE_REACTIONS_PREFIX = "peer_local_message_reactions_";
        public const string PEER_LOCAL_E2E_STATE_PREFIX = "peer_local_e2e_state_";
        public const string ACCOUNT_ACCENT_PREFIX = "account_accent_";
        public const string FEATURE_FLAG_PREFIX = "feature_flag_";

        private static readonly HashSet<string> ExportableClientSettingKeys = new HashSet<string> {
            GROUPS_BACKGROUND_LONGPOLL_LIMIT,
            WIN_SIZE_W,
            WIN_SIZE_H,
            WIN_POS_X,
            WIN_POS_Y,
            WIN_MAXIMIZED,
            LANGUAGE,
            SEND_VIA_ENTER,
            DONT_PARSE_LINKS,
            DISABLE_MENTIONS,
            STICKERS_SUGGEST,
            STICKERS_ANIMATE,
            STICKERS_ANIMATION_MODE,
            LOCAL_STICKER_SEND_MODE,
            EMOJI_PACK,
            EMOJI_CUSTOM_PACK_PATH,
            LOCAL_VOICE_TRANSCRIPTION_ENABLED,
            LOCAL_VOICE_TRANSCRIPTION_MODEL_PATH,
            LOCAL_VOICE_TRANSCRIPTION_LANGUAGE,
            LOCAL_OCR_ENABLED,
            LOCAL_OCR_TESSERACT_PATH,
            LOCAL_OCR_LANGUAGE,
            AUTOSTART_ENABLED,
            AUTOSTART_MINIMIZED,
            INTERFACE_PROFILE,
            STREAMER_MODE,
            LOW_MOTION_MODE,
            LOW_MEMORY_MODE,
            LOW_TRAFFIC_MODE,
            AUTO_BLOCK_BLACKLISTED_USERS,
            AUTO_LOCK_ENABLED,
            AUTO_LOCK_IDLE_MINUTES,
            PANIC_LOCK_CLEAR_CLIPBOARD,
            AUTO_LOCK_CLEAR_CLIPBOARD,
            THEME,
            ACCENT_COLOR,
            APP_FONT_FAMILY,
            APP_ICON_VARIANT,
            CHAT_BACKGROUND,
            CHAT_BACKGROUND_IMAGE,
            CHAT_ITEM_MORE_ROWS,
            CHAT_LIST_WIDTH,
            CHAT_LIST_DENSITY,
            CHAT_LIST_LAYOUT,
            CHAT_LIST_AVATAR_SIZE,
            CHAT_LIST_AVATAR_SHAPE,
            CHAT_LIST_FONT_SIZE,
            MESSAGE_AVATAR_SIZE,
            MESSAGE_FONT_SIZE,
            MESSAGE_BUBBLE_WIDTH,
            MESSAGE_BUBBLE_DENSITY,
            MESSAGE_BUBBLE_STYLE,
            MESSAGE_BUBBLE_OPACITY,
            MESSAGE_BUBBLE_AUTO_COLOR,
            MESSAGE_CHECKMARK_STYLE,
            CHAT_OPEN_BEHAVIOR,
            SHOW_ACCOUNT_RAIL,
            COMPOSER_ACTION_FORMATTING,
            COMPOSER_ACTION_QUICK,
            COMPOSER_ACTION_GROUP_TEMPLATES,
            COMPOSER_ACTION_STICKERS,
            CHAT_HEADER_ACTION_SEARCH,
            CHAT_HEADER_ACTION_PROFILE,
            CHAT_HEADER_ACTION_MORE,
            KEYMAP_COMMAND_PALETTE,
            KEYMAP_GLOBAL_SEARCH,
            KEYMAP_CHAT_SEARCH,
            KEYMAP_FOCUS_COMPOSER,
            KEYMAP_ATTACHMENTS,
            KEYMAP_STICKERS,
            KEYMAP_SETTINGS,
            KEYMAP_PANIC_LOCK,
            KEYMAP_BACK,
            NOTIF_PRIVATE,
            NOTIF_PRIVATE_SOUND,
            NOTIF_GCHAT,
            NOTIF_GCHAT_SOUND,
            NOTIF_DONT_ANNOY_ME,
            NOTIF_DONT_ANNOY_ME_START_HOUR,
            NOTIF_DONT_ANNOY_ME_END_HOUR,
            NOTIF_DONT_ANNOY_ME_ALLOW_MENTIONS,
            NOTIF_DONT_ANNOY_ME_ALLOW_IMPORTANT,
            NOTIF_DONT_ANNOY_ME_KEYWORDS,
            NOTIF_DELIVERY_MODE,
            NOTIF_CUSTOM_POSITION,
            NOTIF_CUSTOM_STACK_LIMIT,
            NOTIF_CUSTOM_TIMEOUT_SECONDS,
            NOTIF_CUSTOM_FAST_ACTIONS,
            NOTIF_CUSTOM_SHOW_AVATARS,
            NOTIF_CUSTOM_SHOW_IMAGES,
            AUTO_STATUS_ENABLED,
            AUTO_STATUS_MODE,
            AUTO_STATUS_SCHEDULE_ENABLED,
            AUTO_STATUS_SCHEDULE_START_HOUR,
            AUTO_STATUS_SCHEDULE_END_HOUR,
            AUTO_STATUS_SCHEDULE_MODE,
            AUTO_STATUS_IDLE_ENABLED,
            AUTO_STATUS_IDLE_MINUTES,
            AUTO_STATUS_IDLE_MODE,
            AUDIO_PLAYER_LOOP,
            AUDIO_PLAYER_VOLUME,
            AUDIO_PLAYER_TRACK_RATE,
            AUDIO_PLAYER_PODCAST_RATE,
            AUDIO_PLAYER_VOICE_RATE,
            AUDIO_PLAYER_SEEK_SECONDS,
            AUDIO_DSP_MODE,
            VOICE_MESSAGE_RESUME_ENABLED,
            VOICE_MESSAGE_SKIP_SILENCE,
            DEBUG_LOGS_CORE,
            DEBUG_LOGS_LP,
            DEBUG_LOGS_BITMAPMANAGER,
            DEBUG_LOGS_LNET,
            DEBUG_LOGS_MESSAGERENDERING,
            DEBUG_MARK_AS_READ_OFF,
            DEBUG_FPS,
            DEBUG_COUNTERS_CHAT,
            DEBUG_COUNTERS_RAM,
            DEBUG_CONTEXT_MENU,
            DEBUG_GALLERY,
            DEBUG_LOAD_IMAGES_SEQUENTIAL,
            DEBUG_API_MONITOR,
            API_DOMAIN,
            API_VERSION,
            PROXY_ENABLED,
            PROXY_URI,
            PROXY_BYPASS_LOCAL,
            INVISIBLE_MODE,
            INVISIBLE_DISABLE_SET_ONLINE,
            INVISIBLE_DISABLE_READ_RECEIPTS,
            INVISIBLE_DISABLE_VOICE_LISTENED,
            INVISIBLE_DISABLE_STORY_VIEWED,
            INVISIBLE_DISABLE_TYPING_STATUS,
            IMAGE_CACHE_DEFAULT_TTL_MINUTES,
            IMAGE_CACHE_AVATAR_TTL_MINUTES,
            IMAGE_CACHE_ATTACHMENT_TTL_MINUTES,
            IMAGE_CACHE_E2E_TTL_MINUTES,
            IMAGE_CACHE_RAM_LIMIT_MB,
            MEDIA_MEMORY_BUDGET_MB
        };

        private static readonly string[] ExportableClientSettingPrefixes = {
            ACCOUNT_ACCENT_PREFIX,
            PEER_LOCAL_ALIAS_PREFIX,
            PEER_LOCAL_AVATAR_PREFIX,
            PEER_LOCAL_TAGS_PREFIX,
            PEER_LOCAL_THEME_PREFIX,
            PEER_LOCAL_BACKGROUND_IMAGE_PREFIX,
            PEER_LOCAL_BACKGROUND_DIM_PREFIX,
            PEER_LOCAL_BACKGROUND_BLUR_PREFIX,
            PEER_LOCAL_BACKGROUND_BRIGHTNESS_PREFIX,
            PEER_LOCAL_ACCENT_PREFIX,
            PEER_LOCAL_DENSITY_PREFIX,
            PEER_LOCAL_FONT_PREFIX,
            PEER_LOCAL_BUBBLE_COLOR_PREFIX,
            PEER_LOCAL_BUBBLE_STYLE_PREFIX,
            PEER_LOCAL_EMOJI_PACK_PREFIX,
            PEER_LOCAL_QUIET_UNTIL_PREFIX,
            PEER_LOCAL_QUIET_TEXT_RULES_PREFIX,
            PEER_LOCAL_ANTISPAM_ENABLED_PREFIX,
            PEER_LOCAL_MUTED_SENDERS_PREFIX,
            PEER_LOCAL_SHADOW_BANNED_SENDERS_PREFIX,
            PEER_LOCAL_SHADOW_BANNED_TEXT_RULES_PREFIX,
            PEER_LOCAL_SHADOW_BANNED_ATTACHMENT_KINDS_PREFIX,
            FEATURE_FLAG_PREFIX
        };

        #endregion

        #region Settings with defaults

        public static bool SentViaEnter {
            get => Get(SEND_VIA_ENTER, true);
            set => Set(SEND_VIA_ENTER, value);
        }

        public static bool DontParseLinks {
            get => Get(DONT_PARSE_LINKS, false);
            set => Set(DONT_PARSE_LINKS, value);
        }

        public static bool DisableMentions {
            get => Get(DISABLE_MENTIONS, false);
            set => Set(DISABLE_MENTIONS, value);
        }

        public static bool SuggestStickers {
            get => Get(STICKERS_SUGGEST, true);
            set => Set(STICKERS_SUGGEST, value);
        }

        public static string EmojiPack {
            get => EmojiPackIds.Normalize(Get(EMOJI_PACK, EmojiPackIds.System));
            set => Set(EMOJI_PACK, EmojiPackIds.Normalize(value));
        }

        public static string EmojiCustomPackPath {
            get => Get(EMOJI_CUSTOM_PACK_PATH, String.Empty);
            set => Set(EMOJI_CUSTOM_PACK_PATH, value?.Trim() ?? String.Empty);
        }

        public static bool LocalVoiceTranscriptionEnabled {
            get => Get(LOCAL_VOICE_TRANSCRIPTION_ENABLED, false);
            set => Set(LOCAL_VOICE_TRANSCRIPTION_ENABLED, value);
        }

        public static string LocalVoiceTranscriptionModelPath {
            get => Get(LOCAL_VOICE_TRANSCRIPTION_MODEL_PATH, String.Empty);
            set => Set(LOCAL_VOICE_TRANSCRIPTION_MODEL_PATH, value?.Trim() ?? String.Empty);
        }

        public static string LocalVoiceTranscriptionLanguage {
            get => Get(LOCAL_VOICE_TRANSCRIPTION_LANGUAGE, "auto");
            set => Set(LOCAL_VOICE_TRANSCRIPTION_LANGUAGE, String.IsNullOrWhiteSpace(value) ? "auto" : value.Trim().ToLowerInvariant());
        }

        public static bool LocalOcrEnabled {
            get => Get(LOCAL_OCR_ENABLED, false);
            set => Set(LOCAL_OCR_ENABLED, value);
        }

        public static string LocalOcrTesseractPath {
            get => Get(LOCAL_OCR_TESSERACT_PATH, String.Empty);
            set => Set(LOCAL_OCR_TESSERACT_PATH, value?.Trim() ?? String.Empty);
        }

        public static string LocalOcrLanguage {
            get => Get(LOCAL_OCR_LANGUAGE, "rus+eng");
            set => Set(LOCAL_OCR_LANGUAGE, String.IsNullOrWhiteSpace(value) ? "rus+eng" : value.Trim());
        }

        public static StickerAnimationMode StickerAnimation {
            get {
                int mode = Get(STICKERS_ANIMATION_MODE, -1);
                if (mode >= (int)StickerAnimationMode.Always && mode <= (int)StickerAnimationMode.Never) {
                    return (StickerAnimationMode)mode;
                }

                return Get(STICKERS_ANIMATE, true) ? StickerAnimationMode.Hover : StickerAnimationMode.Never;
            }
            set {
                SetBatch(new Dictionary<string, object> {
                    { STICKERS_ANIMATION_MODE, (int)value },
                    { STICKERS_ANIMATE, value != StickerAnimationMode.Never }
                });
            }
        }

        public static bool AnimateStickers {
            get => StickerAnimation != StickerAnimationMode.Never;
            set => StickerAnimation = value ? StickerAnimationMode.Always : StickerAnimationMode.Never;
        }

        public static string LocalStickerSendMode {
            get => LocalStickerSendModeIds.Normalize(Get(LOCAL_STICKER_SEND_MODE, LocalStickerSendModeIds.Auto));
            set => Set(LOCAL_STICKER_SEND_MODE, LocalStickerSendModeIds.Normalize(value));
        }

        public static string InterfaceProfile {
            get => Get(INTERFACE_PROFILE, InterfaceProfileIds.Custom);
            set => Set(INTERFACE_PROFILE, String.IsNullOrWhiteSpace(value) ? InterfaceProfileIds.Custom : value);
        }

        public static bool FirstRunOnboardingDone {
            get => Get(FIRST_RUN_ONBOARDING_DONE, false);
            set => Set(FIRST_RUN_ONBOARDING_DONE, value);
        }

        public static bool AutostartEnabled {
            get => Get(AUTOSTART_ENABLED, false);
            set => Set(AUTOSTART_ENABLED, value);
        }

        public static bool AutostartMinimized {
            get => Get(AUTOSTART_MINIMIZED, true);
            set => Set(AUTOSTART_MINIMIZED, value);
        }

        public static bool StreamerMode {
            get => Get(STREAMER_MODE, false);
            set => Set(STREAMER_MODE, value);
        }

        public static bool LowMotionMode {
            get => Get(LOW_MOTION_MODE, false);
            set {
                if (value) {
                    SetBatch(new Dictionary<string, object> {
                        { LOW_MOTION_MODE, true },
                        { STICKERS_ANIMATION_MODE, (int)StickerAnimationMode.Never },
                        { STICKERS_ANIMATE, false }
                    });
                } else {
                    Set(LOW_MOTION_MODE, false);
                }
            }
        }

        public static bool LowMemoryMode {
            get => Get(LOW_MEMORY_MODE, false);
            set {
                if (value) {
                    SetBatch(new Dictionary<string, object> {
                        { LOW_MEMORY_MODE, true },
                        { DEBUG_LOAD_IMAGES_SEQUENTIAL, true },
                        { MEDIA_MEMORY_BUDGET_MB, 80 },
                        { IMAGE_CACHE_RAM_LIMIT_MB, 32 },
                        { IMAGE_CACHE_DEFAULT_TTL_MINUTES, 60 },
                        { IMAGE_CACHE_AVATAR_TTL_MINUTES, 240 },
                        { IMAGE_CACHE_ATTACHMENT_TTL_MINUTES, 30 },
                        { IMAGE_CACHE_E2E_TTL_MINUTES, 5 },
                        { DEBUG_COUNTERS_RAM, false }
                    });
                } else {
                    Set(LOW_MEMORY_MODE, false);
                }
            }
        }

        public static bool LowTrafficMode {
            get => Get(LOW_TRAFFIC_MODE, false);
            set {
                if (value) {
                    SetBatch(new Dictionary<string, object> {
                        { LOW_TRAFFIC_MODE, true },
                        { GROUPS_BACKGROUND_LONGPOLL_LIMIT, 0 },
                        { IMAGE_CACHE_DEFAULT_TTL_MINUTES, 1440 },
                        { IMAGE_CACHE_AVATAR_TTL_MINUTES, 10080 },
                        { IMAGE_CACHE_ATTACHMENT_TTL_MINUTES, 1440 },
                        { IMAGE_CACHE_E2E_TTL_MINUTES, 60 }
                    });
                } else {
                    Set(LOW_TRAFFIC_MODE, false);
                }
            }
        }

        public static string LocalBackupDirectory {
            get => Get(LOCAL_BACKUP_DIRECTORY, String.Empty);
            set => Set(LOCAL_BACKUP_DIRECTORY, value?.Trim() ?? String.Empty);
        }

        public static bool GetFeatureFlag(string id, bool defaultValue = false) {
            return Get($"{FEATURE_FLAG_PREFIX}{NormalizeFeatureFlagId(id)}", defaultValue);
        }

        public static void SetFeatureFlag(string id, bool enabled) {
            Set($"{FEATURE_FLAG_PREFIX}{NormalizeFeatureFlagId(id)}", enabled);
        }

        public static bool AutoBlockBlacklistedUsers {
            get => Get(AUTO_BLOCK_BLACKLISTED_USERS, false);
            set => Set(AUTO_BLOCK_BLACKLISTED_USERS, value);
        }

        public static bool AutoLockEnabled {
            get => Get(AUTO_LOCK_ENABLED, false);
            set => Set(AUTO_LOCK_ENABLED, value);
        }

        public static int AutoLockIdleMinutes {
            get => Math.Clamp(Get(AUTO_LOCK_IDLE_MINUTES, 15), 1, 240);
            set => Set(AUTO_LOCK_IDLE_MINUTES, Math.Clamp(value, 1, 240));
        }

        public static bool PanicLockClearClipboard {
            get => Get(PANIC_LOCK_CLEAR_CLIPBOARD, true);
            set => Set(PANIC_LOCK_CLEAR_CLIPBOARD, value);
        }

        public static bool AutoLockClearClipboard {
            get => Get(AUTO_LOCK_CLEAR_CLIPBOARD, false);
            set => Set(AUTO_LOCK_CLEAR_CLIPBOARD, value);
        }

        // Appearance

        public static int AppTheme {
            get => Get(THEME, Constants.DefaultTheme);
            set => Set(THEME, value);
        }

        public static string AccentColor {
            get => Get(ACCENT_COLOR, AppearanceManager.DefaultAccentId);
            set => Set(ACCENT_COLOR, String.IsNullOrWhiteSpace(value) ? AppearanceManager.DefaultAccentId : value);
        }

        public static string AppFontFamily {
            get => NormalizeFontFamily(Get(APP_FONT_FAMILY, "Segoe UI"));
            set => Set(APP_FONT_FAMILY, NormalizeFontFamily(value));
        }

        public static string AppIconVariant {
            get => AppIconVariantIds.Normalize(Get(APP_ICON_VARIANT, AppIconVariantIds.Auto));
            set => Set(APP_ICON_VARIANT, AppIconVariantIds.Normalize(value));
        }

        public static string ChatBackground {
            get => Get(CHAT_BACKGROUND, AppearanceManager.DefaultChatBackgroundId);
            set => Set(CHAT_BACKGROUND, String.IsNullOrWhiteSpace(value) ? AppearanceManager.DefaultChatBackgroundId : value);
        }

        public static string ChatBackgroundImage {
            get => Get(CHAT_BACKGROUND_IMAGE, String.Empty);
            set => Set(CHAT_BACKGROUND_IMAGE, NormalizePathOrUri(value));
        }

        public static Uri ChatBackgroundImageUri {
            get => GetImageUri(ChatBackgroundImage);
        }

        public static bool ChatItemMoreRows {
            get => Get(CHAT_ITEM_MORE_ROWS, false);
            set => Set(CHAT_ITEM_MORE_ROWS, value);
        }

        public static double ChatListWidth {
            get => Math.Clamp(Get(CHAT_LIST_WIDTH, 380d), 280d, 560d);
            set => Set(CHAT_LIST_WIDTH, Math.Clamp(value, 280d, 560d));
        }

        public static string ChatListDensity {
            get => Get(CHAT_LIST_DENSITY, ChatListDensityIds.Medium);
            set => Set(CHAT_LIST_DENSITY, String.IsNullOrWhiteSpace(value) ? ChatListDensityIds.Medium : value);
        }

        public static string ChatListLayout {
            get => Get(CHAT_LIST_LAYOUT, ChatListLayoutIds.Classic);
            set => Set(CHAT_LIST_LAYOUT, String.IsNullOrWhiteSpace(value) ? ChatListLayoutIds.Classic : value);
        }

        public static string ChatListAvatarSize {
            get => Get(CHAT_LIST_AVATAR_SIZE, AvatarSizeIds.Auto);
            set => Set(CHAT_LIST_AVATAR_SIZE, NormalizeAvatarSize(value, AvatarSizeIds.Auto));
        }

        public static string ChatListAvatarShape {
            get => Get(CHAT_LIST_AVATAR_SHAPE, AvatarShapeIds.Circle);
            set => Set(CHAT_LIST_AVATAR_SHAPE, String.IsNullOrWhiteSpace(value) ? AvatarShapeIds.Circle : value);
        }

        public static string ChatListFontSize {
            get => Get(CHAT_LIST_FONT_SIZE, TextSizeIds.Auto);
            set => Set(CHAT_LIST_FONT_SIZE, NormalizeTextSize(value, TextSizeIds.Auto));
        }

        public static string MessageAvatarSize {
            get => Get(MESSAGE_AVATAR_SIZE, AvatarSizeIds.Medium);
            set => Set(MESSAGE_AVATAR_SIZE, NormalizeAvatarSize(value, AvatarSizeIds.Medium));
        }

        public static string MessageFontSize {
            get => Get(MESSAGE_FONT_SIZE, TextSizeIds.Medium);
            set => Set(MESSAGE_FONT_SIZE, NormalizeTextSize(value, TextSizeIds.Medium));
        }

        public static string MessageBubbleWidth {
            get => NormalizeBubbleWidth(Get(MESSAGE_BUBBLE_WIDTH, BubbleWidthIds.Medium));
            set => Set(MESSAGE_BUBBLE_WIDTH, NormalizeBubbleWidth(value));
        }

        public static string MessageBubbleDensity {
            get => NormalizeBubbleDensity(Get(MESSAGE_BUBBLE_DENSITY, BubbleDensityIds.Normal));
            set => Set(MESSAGE_BUBBLE_DENSITY, NormalizeBubbleDensity(value));
        }

        public static string MessageBubbleStyle {
            get => NormalizeBubbleStyle(Get(MESSAGE_BUBBLE_STYLE, BubbleStyleIds.Vk));
            set => Set(MESSAGE_BUBBLE_STYLE, NormalizeBubbleStyle(value));
        }

        public static int MessageBubbleOpacity {
            get => NormalizePercent(Get(MESSAGE_BUBBLE_OPACITY, 100), 40, 100);
            set => Set(MESSAGE_BUBBLE_OPACITY, NormalizePercent(value, 40, 100));
        }

        public static bool MessageBubbleAutoColor {
            get => Get(MESSAGE_BUBBLE_AUTO_COLOR, false);
            set => Set(MESSAGE_BUBBLE_AUTO_COLOR, value);
        }

        public static string MessageCheckmarkStyle {
            get => MessageCheckmarkStyleIds.Normalize(Get(MESSAGE_CHECKMARK_STYLE, MessageCheckmarkStyleIds.Vk));
            set => Set(MESSAGE_CHECKMARK_STYLE, MessageCheckmarkStyleIds.Normalize(value));
        }

        public static string ChatOpenBehavior {
            get => ChatOpenBehaviorIds.Normalize(Get(CHAT_OPEN_BEHAVIOR, ChatOpenBehaviorIds.Bottom));
            set => Set(CHAT_OPEN_BEHAVIOR, ChatOpenBehaviorIds.Normalize(value));
        }

        public static bool ShowAccountRail {
            get => Get(SHOW_ACCOUNT_RAIL, false);
            set => Set(SHOW_ACCOUNT_RAIL, value);
        }

        public static bool ComposerActionFormatting {
            get => Get(COMPOSER_ACTION_FORMATTING, true);
            set => Set(COMPOSER_ACTION_FORMATTING, value);
        }

        public static bool ComposerActionQuick {
            get => Get(COMPOSER_ACTION_QUICK, true);
            set => Set(COMPOSER_ACTION_QUICK, value);
        }

        public static bool ComposerActionGroupTemplates {
            get => Get(COMPOSER_ACTION_GROUP_TEMPLATES, true);
            set => Set(COMPOSER_ACTION_GROUP_TEMPLATES, value);
        }

        public static bool ComposerActionStickers {
            get => Get(COMPOSER_ACTION_STICKERS, true);
            set => Set(COMPOSER_ACTION_STICKERS, value);
        }

        public static bool ChatHeaderActionSearch {
            get => Get(CHAT_HEADER_ACTION_SEARCH, true);
            set => Set(CHAT_HEADER_ACTION_SEARCH, value);
        }

        public static bool ChatHeaderActionProfile {
            get => Get(CHAT_HEADER_ACTION_PROFILE, false);
            set => Set(CHAT_HEADER_ACTION_PROFILE, value);
        }

        public static bool ChatHeaderActionMore {
            get => Get(CHAT_HEADER_ACTION_MORE, true);
            set => Set(CHAT_HEADER_ACTION_MORE, value);
        }

        public static string KeymapCommandPalette {
            get => Get(KEYMAP_COMMAND_PALETTE, "Ctrl+P");
            set => Set(KEYMAP_COMMAND_PALETTE, NormalizeShortcut(value, "Ctrl+P"));
        }

        public static string KeymapGlobalSearch {
            get => Get(KEYMAP_GLOBAL_SEARCH, "Ctrl+Shift+F");
            set => Set(KEYMAP_GLOBAL_SEARCH, NormalizeShortcut(value, "Ctrl+Shift+F"));
        }

        public static string KeymapChatSearch {
            get => Get(KEYMAP_CHAT_SEARCH, "Ctrl+F");
            set => Set(KEYMAP_CHAT_SEARCH, NormalizeShortcut(value, "Ctrl+F"));
        }

        public static string KeymapFocusComposer {
            get => Get(KEYMAP_FOCUS_COMPOSER, "Ctrl+L");
            set => Set(KEYMAP_FOCUS_COMPOSER, NormalizeShortcut(value, "Ctrl+L"));
        }

        public static string KeymapAttachments {
            get => Get(KEYMAP_ATTACHMENTS, "Ctrl+Shift+A");
            set => Set(KEYMAP_ATTACHMENTS, NormalizeShortcut(value, "Ctrl+Shift+A"));
        }

        public static string KeymapStickers {
            get => Get(KEYMAP_STICKERS, "Ctrl+Shift+E");
            set => Set(KEYMAP_STICKERS, NormalizeShortcut(value, "Ctrl+Shift+E"));
        }

        public static string KeymapSettings {
            get => Get(KEYMAP_SETTINGS, "Ctrl+,");
            set => Set(KEYMAP_SETTINGS, NormalizeShortcut(value, "Ctrl+,"));
        }

        public static string KeymapPanicLock {
            get => Get(KEYMAP_PANIC_LOCK, "Ctrl+Shift+P");
            set => Set(KEYMAP_PANIC_LOCK, NormalizeShortcut(value, "Ctrl+Shift+P"));
        }

        public static string KeymapBack {
            get => Get(KEYMAP_BACK, "Alt+Left;Esc");
            set => Set(KEYMAP_BACK, NormalizeShortcut(value, "Alt+Left;Esc"));
        }

        public static int GroupsBackgroundLongPollLimit {
            get => Math.Max(0, Get(GROUPS_BACKGROUND_LONGPOLL_LIMIT, 3));
            set => Set(GROUPS_BACKGROUND_LONGPOLL_LIMIT, Math.Max(0, value));
        }

        public static HashSet<long> GetArchivedPeerIds() {
            string value = Get(LOCAL_ARCHIVED_PEERS, String.Empty);
            HashSet<long> ids = new HashSet<long>();
            if (String.IsNullOrWhiteSpace(value)) return ids;

            foreach (string rawId in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
                if (long.TryParse(rawId, out long id) && id != 0) ids.Add(id);
            }

            return ids;
        }

        public static bool IsPeerArchived(long peerId) {
            return GetArchivedPeerIds().Contains(peerId);
        }

        public static void SetPeerArchived(long peerId, bool archived) {
            HashSet<long> ids = GetArchivedPeerIds();
            if (archived) {
                ids.Add(peerId);
            } else {
                ids.Remove(peerId);
            }

            Set(LOCAL_ARCHIVED_PEERS, ids.Count == 0 ? null : String.Join(",", ids.OrderBy(id => id)));
        }

        // Notifications

        public static bool NotificationsPrivate {
            get => Get(NOTIF_PRIVATE, true);
            set => Set(NOTIF_PRIVATE, value);
        }

        public static bool NotificationsPrivateSound {
            get => Get(NOTIF_PRIVATE_SOUND, true);
            set => Set(NOTIF_PRIVATE_SOUND, value);
        }

        public static bool NotificationsGroupChat {
            get => Get(NOTIF_GCHAT, true);
            set => Set(NOTIF_GCHAT, value);
        }

        public static bool NotificationsGroupChatSound {
            get => Get(NOTIF_GCHAT_SOUND, true);
            set => Set(NOTIF_GCHAT_SOUND, value);
        }

        public static bool DontAnnoyMeMode {
            get => Get(NOTIF_DONT_ANNOY_ME, false);
            set => Set(NOTIF_DONT_ANNOY_ME, value);
        }

        public static int DontAnnoyMeStartHour {
            get => NormalizeHour(Get(NOTIF_DONT_ANNOY_ME_START_HOUR, 22));
            set => Set(NOTIF_DONT_ANNOY_ME_START_HOUR, NormalizeHour(value));
        }

        public static int DontAnnoyMeEndHour {
            get => NormalizeHour(Get(NOTIF_DONT_ANNOY_ME_END_HOUR, 8));
            set => Set(NOTIF_DONT_ANNOY_ME_END_HOUR, NormalizeHour(value));
        }

        public static bool DontAnnoyMeAllowMentions {
            get => Get(NOTIF_DONT_ANNOY_ME_ALLOW_MENTIONS, true);
            set => Set(NOTIF_DONT_ANNOY_ME_ALLOW_MENTIONS, value);
        }

        public static bool DontAnnoyMeAllowImportant {
            get => Get(NOTIF_DONT_ANNOY_ME_ALLOW_IMPORTANT, true);
            set => Set(NOTIF_DONT_ANNOY_ME_ALLOW_IMPORTANT, value);
        }

        public static string DontAnnoyMeKeywords {
            get => Get(NOTIF_DONT_ANNOY_ME_KEYWORDS, String.Empty);
            set => Set(NOTIF_DONT_ANNOY_ME_KEYWORDS, value ?? String.Empty);
        }

        public static string NotificationDeliveryMode {
            get => NotificationDeliveryModeIds.Normalize(Get(NOTIF_DELIVERY_MODE, NotificationDeliveryModeIds.Custom));
            set => Set(NOTIF_DELIVERY_MODE, NotificationDeliveryModeIds.Normalize(value));
        }

        public static string CustomNotificationPosition {
            get => NotificationPositionIds.Normalize(Get(NOTIF_CUSTOM_POSITION, NotificationPositionIds.BottomRight));
            set => Set(NOTIF_CUSTOM_POSITION, NotificationPositionIds.Normalize(value));
        }

        public static int CustomNotificationStackLimit {
            get => Math.Clamp(Get(NOTIF_CUSTOM_STACK_LIMIT, 4), 1, 10);
            set => Set(NOTIF_CUSTOM_STACK_LIMIT, Math.Clamp(value, 1, 10));
        }

        public static int CustomNotificationTimeoutSeconds {
            get => Math.Clamp(Get(NOTIF_CUSTOM_TIMEOUT_SECONDS, 8), 2, 60);
            set => Set(NOTIF_CUSTOM_TIMEOUT_SECONDS, Math.Clamp(value, 2, 60));
        }

        public static bool CustomNotificationFastActions {
            get => Get(NOTIF_CUSTOM_FAST_ACTIONS, true);
            set => Set(NOTIF_CUSTOM_FAST_ACTIONS, value);
        }

        public static bool CustomNotificationShowAvatars {
            get => Get(NOTIF_CUSTOM_SHOW_AVATARS, true);
            set => Set(NOTIF_CUSTOM_SHOW_AVATARS, value);
        }

        public static bool CustomNotificationShowImages {
            get => Get(NOTIF_CUSTOM_SHOW_IMAGES, true);
            set => Set(NOTIF_CUSTOM_SHOW_IMAGES, value);
        }

        public static bool AutoStatusEnabled {
            get => Get(AUTO_STATUS_ENABLED, false);
            set => Set(AUTO_STATUS_ENABLED, value);
        }

        public static string AutoStatusMode {
            get => Get(AUTO_STATUS_MODE, AutoStatusModeIds.Work);
            set => Set(AUTO_STATUS_MODE, AutoStatusModeIds.Normalize(value));
        }

        public static bool AutoStatusScheduleEnabled {
            get => Get(AUTO_STATUS_SCHEDULE_ENABLED, false);
            set => Set(AUTO_STATUS_SCHEDULE_ENABLED, value);
        }

        public static int AutoStatusScheduleStartHour {
            get => NormalizeHour(Get(AUTO_STATUS_SCHEDULE_START_HOUR, 23));
            set => Set(AUTO_STATUS_SCHEDULE_START_HOUR, NormalizeHour(value));
        }

        public static int AutoStatusScheduleEndHour {
            get => NormalizeHour(Get(AUTO_STATUS_SCHEDULE_END_HOUR, 7));
            set => Set(AUTO_STATUS_SCHEDULE_END_HOUR, NormalizeHour(value));
        }

        public static string AutoStatusScheduleMode {
            get => Get(AUTO_STATUS_SCHEDULE_MODE, AutoStatusModeIds.Sleep);
            set => Set(AUTO_STATUS_SCHEDULE_MODE, AutoStatusModeIds.Normalize(value));
        }

        public static bool AutoStatusIdleEnabled {
            get => Get(AUTO_STATUS_IDLE_ENABLED, false);
            set => Set(AUTO_STATUS_IDLE_ENABLED, value);
        }

        public static int AutoStatusIdleMinutes {
            get => Math.Clamp(Get(AUTO_STATUS_IDLE_MINUTES, 15), 1, 240);
            set => Set(AUTO_STATUS_IDLE_MINUTES, Math.Clamp(value, 1, 240));
        }

        public static string AutoStatusIdleMode {
            get => Get(AUTO_STATUS_IDLE_MODE, AutoStatusModeIds.DoNotDisturb);
            set => Set(AUTO_STATUS_IDLE_MODE, AutoStatusModeIds.Normalize(value));
        }

        public static bool IsDontAnnoyMeActive(DateTime now) {
            if (!DontAnnoyMeMode) return false;

            int startHour = DontAnnoyMeStartHour;
            int endHour = DontAnnoyMeEndHour;
            int currentHour = NormalizeHour(now.Hour);

            if (startHour == endHour) return true;
            if (startHour < endHour) return currentHour >= startHour && currentHour < endHour;

            return currentHour >= startHour || currentHour < endHour;
        }

        public static bool AudioPlayerLoop {
            get => Get(AUDIO_PLAYER_LOOP, false);
            set => Set(AUDIO_PLAYER_LOOP, value);
        }

        public static int AudioPlayerVolume {
            get => Math.Clamp(Get(AUDIO_PLAYER_VOLUME, 90), 0, 100);
            set => Set(AUDIO_PLAYER_VOLUME, Math.Clamp(value, 0, 100));
        }

        public static int AudioPlayerTrackRate {
            get => NormalizeAudioRate(Get(AUDIO_PLAYER_TRACK_RATE, 100));
            set => Set(AUDIO_PLAYER_TRACK_RATE, NormalizeAudioRate(value));
        }

        public static int AudioPlayerPodcastRate {
            get => NormalizeAudioRate(Get(AUDIO_PLAYER_PODCAST_RATE, 100));
            set => Set(AUDIO_PLAYER_PODCAST_RATE, NormalizeAudioRate(value));
        }

        public static int AudioPlayerVoiceRate {
            get => NormalizeAudioRate(Get(AUDIO_PLAYER_VOICE_RATE, 100));
            set => Set(AUDIO_PLAYER_VOICE_RATE, NormalizeAudioRate(value));
        }

        public static int AudioPlayerSeekSeconds {
            get => Math.Clamp(Get(AUDIO_PLAYER_SEEK_SECONDS, 15), 5, 60);
            set => Set(AUDIO_PLAYER_SEEK_SECONDS, Math.Clamp(value, 5, 60));
        }

        public static string AudioDspMode {
            get => AudioDspModeIds.NormalizeMode(Get(AUDIO_DSP_MODE, AudioDspModeIds.Off));
            set => Set(AUDIO_DSP_MODE, AudioDspModeIds.NormalizeMode(value));
        }

        public static IReadOnlyList<AudioPlaybackHistoryItem> GetAudioPlaybackHistory() {
            string value = Get(AUDIO_PLAYER_HISTORY, String.Empty);
            if (String.IsNullOrWhiteSpace(value)) return Array.Empty<AudioPlaybackHistoryItem>();

            try {
                return NormalizeAudioPlaybackHistory(JsonSerializer.Deserialize<List<AudioPlaybackHistoryItem>>(value));
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot read audio playback history.");
                return Array.Empty<AudioPlaybackHistoryItem>();
            }
        }

        public static void AddOrUpdateAudioPlaybackHistory(AudioPlaybackHistoryItem item) {
            if (item == null) return;

            List<AudioPlaybackHistoryItem> items = GetAudioPlaybackHistory().ToList();
            string key = NormalizeAudioHistoryKey(item.Key);
            if (String.IsNullOrWhiteSpace(key)) return;

            items.RemoveAll(i => String.Equals(NormalizeAudioHistoryKey(i.Key), key, StringComparison.OrdinalIgnoreCase));
            items.Insert(0, new AudioPlaybackHistoryItem {
                Key = key,
                Type = NormalizeAudioHistoryText(item.Type, 32),
                OwnerId = item.OwnerId,
                Id = item.Id,
                Title = NormalizeAudioHistoryText(item.Title, 256),
                Performer = NormalizeAudioHistoryText(item.Performer, 256),
                DurationMs = Math.Max(0, item.DurationMs),
                PositionMs = Math.Max(0, item.PositionMs),
                UpdatedAtUnix = item.UpdatedAtUnix > 0 ? item.UpdatedAtUnix : DateTimeOffset.Now.ToUnixTimeSeconds()
            });

            SetAudioPlaybackHistory(items);
        }

        public static void ClearAudioPlaybackHistory() {
            Set(AUDIO_PLAYER_HISTORY, null);
        }

        public static bool VoiceMessageResumeEnabled {
            get => Get(VOICE_MESSAGE_RESUME_ENABLED, true);
            set => Set(VOICE_MESSAGE_RESUME_ENABLED, value);
        }

        public static bool VoiceMessageSkipSilence {
            get => Get(VOICE_MESSAGE_SKIP_SILENCE, false);
            set => Set(VOICE_MESSAGE_SKIP_SILENCE, value);
        }

        public static long GetVoiceMessageResumePositionMs(long ownerId, int id) {
            return Math.Max(0, Get(BuildVoiceMessageResumePositionKey(ownerId, id), 0L));
        }

        public static void SetVoiceMessageResumePositionMs(long ownerId, int id, long positionMs) {
            Set(BuildVoiceMessageResumePositionKey(ownerId, id), Math.Max(0, positionMs));
        }

        private static string BuildVoiceMessageResumePositionKey(long ownerId, int id) {
            return $"{VOICE_MESSAGE_RESUME_POSITION_PREFIX}{ownerId}_{id}";
        }

        private static int NormalizeAudioRate(int rateX100) {
            return Math.Clamp(rateX100, 50, 300);
        }

        private static void SetAudioPlaybackHistory(IEnumerable<AudioPlaybackHistoryItem> items) {
            List<AudioPlaybackHistoryItem> normalized = NormalizeAudioPlaybackHistory(items);
            Set(AUDIO_PLAYER_HISTORY, normalized.Count == 0 ? null : JsonSerializer.Serialize(normalized));
        }

        private static List<AudioPlaybackHistoryItem> NormalizeAudioPlaybackHistory(IEnumerable<AudioPlaybackHistoryItem> items) {
            return items?
                .Where(item => item != null)
                .Select(item => new AudioPlaybackHistoryItem {
                    Key = NormalizeAudioHistoryKey(item.Key),
                    Type = NormalizeAudioHistoryText(item.Type, 32),
                    OwnerId = item.OwnerId,
                    Id = item.Id,
                    Title = NormalizeAudioHistoryText(item.Title, 256),
                    Performer = NormalizeAudioHistoryText(item.Performer, 256),
                    DurationMs = Math.Max(0, item.DurationMs),
                    PositionMs = Math.Max(0, item.PositionMs),
                    UpdatedAtUnix = item.UpdatedAtUnix > 0 ? item.UpdatedAtUnix : DateTimeOffset.Now.ToUnixTimeSeconds()
                })
                .Where(item => !String.IsNullOrWhiteSpace(item.Key))
                .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(item => item.UpdatedAtUnix).First())
                .OrderByDescending(item => item.UpdatedAtUnix)
                .Take(AudioPlaybackHistoryLimit)
                .ToList() ?? new List<AudioPlaybackHistoryItem>();
        }

        private static string NormalizeAudioHistoryKey(string key) {
            if (String.IsNullOrWhiteSpace(key)) return String.Empty;

            string normalized = key.Trim();
            return normalized.Length <= 128 ? normalized : normalized[..128];
        }

        private static string NormalizeAudioHistoryText(string text, int maxLength) {
            if (String.IsNullOrWhiteSpace(text)) return String.Empty;

            string normalized = text.Trim();
            return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
        }

        // Debug

        public static bool EnableLogs {
            get => Get(DEBUG_LOGS_CORE, true);
            set => Set(DEBUG_LOGS_CORE, value);
        }

        public static bool EnableLongPollLogs {
            get => Get(DEBUG_LOGS_LP, false);
            set => Set(DEBUG_LOGS_LP, value);
        }

        public static bool BitmapManagerLogs {
            get => Get(DEBUG_LOGS_BITMAPMANAGER, false);
            set => Set(DEBUG_LOGS_BITMAPMANAGER, value);
        }

        public static bool LNetLogs {
            get => Get(DEBUG_LOGS_LNET, false);
            set => Set(DEBUG_LOGS_LNET, value);
        }

        public static bool MessageRenderingLogs {
            get => Get(DEBUG_LOGS_MESSAGERENDERING, false);
            set => Set(DEBUG_LOGS_MESSAGERENDERING, value);
        }

        public static bool ShowFPS {
            get => Get(DEBUG_FPS, false);
            set => Set(DEBUG_FPS, value);
        }

        public static bool ShowDebugCounters {
            get => Get(DEBUG_COUNTERS_CHAT, false);
            set => Set(DEBUG_COUNTERS_CHAT, value);
        }

        public static bool ShowRAMUsage {
            get => Get(DEBUG_COUNTERS_RAM, false);
            set => Set(DEBUG_COUNTERS_RAM, value);
        }

        public static bool ShowDevItemsInContextMenus {
            get => Get(DEBUG_CONTEXT_MENU, false);
            set => Set(DEBUG_CONTEXT_MENU, value);
        }

        public static bool DisableMarkingMessagesAsRead {
            get => Get(DEBUG_MARK_AS_READ_OFF, false) || ShouldSuppressReadReceipts;
            set => Set(DEBUG_MARK_AS_READ_OFF, value);
        }

        public static bool ShowDebugInfoInGallery {
            get => Get(DEBUG_GALLERY, false);
            set => Set(DEBUG_GALLERY, value);
        }

        public static bool LoadImagesSequential {
            get => Get(DEBUG_LOAD_IMAGES_SEQUENTIAL, true);
            set => Set(DEBUG_LOAD_IMAGES_SEQUENTIAL, value);
        }

        public static bool ApiDebugMonitorEnabled {
            get => Get(DEBUG_API_MONITOR, true);
            set => Set(DEBUG_API_MONITOR, value);
        }

        public static string ApiDomain {
            get => NormalizeApiDomain(Get(API_DOMAIN, ELOR.VKAPILib.VKAPI.DefaultDomain));
            set => Set(API_DOMAIN, NormalizeApiDomain(value));
        }

        public static string ApiVersion {
            get => NormalizeApiVersion(Get(API_VERSION, ELOR.VKAPILib.VKAPI.BundledVersion));
            set => Set(API_VERSION, NormalizeApiVersion(value));
        }

        public static bool ProxyEnabled {
            get => Get(PROXY_ENABLED, false);
            set => Set(PROXY_ENABLED, value);
        }

        public static string ProxyUri {
            get => NormalizeProxyUri(Get(PROXY_URI, String.Empty));
            set => Set(PROXY_URI, NormalizeProxyUri(value));
        }

        public static bool ProxyBypassLocal {
            get => Get(PROXY_BYPASS_LOCAL, true);
            set => Set(PROXY_BYPASS_LOCAL, value);
        }

        public static bool InvisibleMode {
            get => Get(INVISIBLE_MODE, false);
            set => Set(INVISIBLE_MODE, value);
        }

        public static bool InvisibleDisableSetOnline {
            get => Get(INVISIBLE_DISABLE_SET_ONLINE, true);
            set => Set(INVISIBLE_DISABLE_SET_ONLINE, value);
        }

        public static bool InvisibleDisableReadReceipts {
            get => Get(INVISIBLE_DISABLE_READ_RECEIPTS, true);
            set => Set(INVISIBLE_DISABLE_READ_RECEIPTS, value);
        }

        public static bool InvisibleDisableVoiceListened {
            get => Get(INVISIBLE_DISABLE_VOICE_LISTENED, true);
            set => Set(INVISIBLE_DISABLE_VOICE_LISTENED, value);
        }

        public static bool InvisibleDisableStoryViewed {
            get => Get(INVISIBLE_DISABLE_STORY_VIEWED, true);
            set => Set(INVISIBLE_DISABLE_STORY_VIEWED, value);
        }

        public static bool InvisibleDisableTypingStatus {
            get => Get(INVISIBLE_DISABLE_TYPING_STATUS, true);
            set => Set(INVISIBLE_DISABLE_TYPING_STATUS, value);
        }

        public static bool ShouldSuppressSetOnline {
            get => InvisibleMode && InvisibleDisableSetOnline;
        }

        public static bool ShouldSuppressReadReceipts {
            get => InvisibleMode && InvisibleDisableReadReceipts;
        }

        public static bool ShouldSuppressVoiceListened {
            get => InvisibleMode && InvisibleDisableVoiceListened;
        }

        public static bool ShouldSuppressStoryViewed {
            get => InvisibleMode && InvisibleDisableStoryViewed;
        }

        public static bool ShouldSuppressTypingStatus {
            get => InvisibleMode && InvisibleDisableTypingStatus;
        }

        // Image cache

        public static int ImageCacheDefaultTtlMinutes {
            get => Math.Max(0, Get(IMAGE_CACHE_DEFAULT_TTL_MINUTES, 240));
            set => Set(IMAGE_CACHE_DEFAULT_TTL_MINUTES, Math.Max(0, value));
        }

        public static int ImageCacheAvatarTtlMinutes {
            get => Math.Max(0, Get(IMAGE_CACHE_AVATAR_TTL_MINUTES, 10080));
            set => Set(IMAGE_CACHE_AVATAR_TTL_MINUTES, Math.Max(0, value));
        }

        public static int ImageCacheAttachmentTtlMinutes {
            get => Math.Max(0, Get(IMAGE_CACHE_ATTACHMENT_TTL_MINUTES, 120));
            set => Set(IMAGE_CACHE_ATTACHMENT_TTL_MINUTES, Math.Max(0, value));
        }

        public static int ImageCacheE2ETtlMinutes {
            get => Math.Max(0, Get(IMAGE_CACHE_E2E_TTL_MINUTES, 10));
            set => Set(IMAGE_CACHE_E2E_TTL_MINUTES, Math.Max(0, value));
        }

        public static int ImageCacheRamLimitMb {
            get => Math.Clamp(Get(IMAGE_CACHE_RAM_LIMIT_MB, 32), 16, 256);
            set => Set(IMAGE_CACHE_RAM_LIMIT_MB, Math.Clamp(value, 16, 256));
        }

        public static int MediaMemoryBudgetMb {
            get => Math.Clamp(Get(MEDIA_MEMORY_BUDGET_MB, 80), 64, 1024);
            set => Set(MEDIA_MEMORY_BUDGET_MB, Math.Clamp(value, 64, 1024));
        }

        public static string GetPeerLocalNote(long peerId) {
            return Get($"{PEER_LOCAL_NOTE_PREFIX}{peerId}", String.Empty);
        }

        public static void SetPeerLocalNote(long peerId, string note) {
            string key = $"{PEER_LOCAL_NOTE_PREFIX}{peerId}";
            Set(key, String.IsNullOrWhiteSpace(note) ? null : note);
        }

        public static void SetPeerLocalNoteWithHistory(long peerId, string note) {
            string noteKey = $"{PEER_LOCAL_NOTE_PREFIX}{peerId}";
            string historyKey = $"{PEER_LOCAL_NOTE_HISTORY_PREFIX}{peerId}";
            note = note?.Trim();

            Dictionary<string, object> batch = new Dictionary<string, object> {
                { noteKey, String.IsNullOrWhiteSpace(note) ? null : note }
            };

            if (!String.IsNullOrWhiteSpace(note)) {
                List<PeerLocalNoteHistoryItem> items = GetPeerLocalNoteHistory(peerId).ToList();
                string normalizedNote = NormalizeHistoryText(note);
                items.RemoveAll(i => String.Equals(NormalizeHistoryText(i.Text), normalizedNote, StringComparison.Ordinal));
                items.Insert(0, new PeerLocalNoteHistoryItem {
                    Text = note,
                    UpdatedAtUnix = DateTimeOffset.Now.ToUnixTimeSeconds()
                });
                batch.Add(historyKey, BuildPeerLocalNoteHistoryValue(items));
            }

            SetBatch(batch);
        }

        public static IReadOnlyList<PeerLocalNoteHistoryItem> GetPeerLocalNoteHistory(long peerId) {
            string value = Get($"{PEER_LOCAL_NOTE_HISTORY_PREFIX}{peerId}", String.Empty);
            if (String.IsNullOrWhiteSpace(value)) return Array.Empty<PeerLocalNoteHistoryItem>();

            List<PeerLocalNoteHistoryItem> items = new List<PeerLocalNoteHistoryItem>();
            foreach (string line in value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
                int separator = line.IndexOf('|');
                if (separator <= 0 || separator >= line.Length - 1) continue;

                string unixText = line.Substring(0, separator);
                string encodedText = line.Substring(separator + 1);
                if (!Int64.TryParse(unixText, out long unix)) continue;

                try {
                    string text = Encoding.UTF8.GetString(FromBase64Url(encodedText));
                    if (!String.IsNullOrWhiteSpace(text)) {
                        items.Add(new PeerLocalNoteHistoryItem {
                            Text = text,
                            UpdatedAtUnix = unix
                        });
                    }
                } catch (Exception ex) {
                    Log.Debug(ex, "Cannot parse local note history item for peer {PeerId}", peerId);
                }
            }

            return NormalizePeerLocalNoteHistory(items);
        }

        public static void AddPeerLocalNoteHistory(long peerId, string note) {
            note = note?.Trim();
            if (String.IsNullOrWhiteSpace(note)) return;

            List<PeerLocalNoteHistoryItem> items = GetPeerLocalNoteHistory(peerId).ToList();
            string normalizedNote = NormalizeHistoryText(note);
            items.RemoveAll(i => String.Equals(NormalizeHistoryText(i.Text), normalizedNote, StringComparison.Ordinal));
            items.Insert(0, new PeerLocalNoteHistoryItem {
                Text = note,
                UpdatedAtUnix = DateTimeOffset.Now.ToUnixTimeSeconds()
            });

            SetPeerLocalNoteHistory(peerId, items);
        }

        public static void ClearPeerLocalNoteHistory(long peerId) {
            Set($"{PEER_LOCAL_NOTE_HISTORY_PREFIX}{peerId}", null);
        }

        private static void SetPeerLocalNoteHistory(long peerId, IEnumerable<PeerLocalNoteHistoryItem> items) {
            List<PeerLocalNoteHistoryItem> normalized = NormalizePeerLocalNoteHistory(items);
            string key = $"{PEER_LOCAL_NOTE_HISTORY_PREFIX}{peerId}";
            Set(key, BuildPeerLocalNoteHistoryValue(normalized));
        }

        private static string BuildPeerLocalNoteHistoryValue(IEnumerable<PeerLocalNoteHistoryItem> items) {
            List<PeerLocalNoteHistoryItem> normalized = NormalizePeerLocalNoteHistory(items);
            if (normalized.Count == 0) return null;

            return String.Join(
                "\n",
                normalized.Select(i => $"{i.UpdatedAtUnix}|{ToBase64Url(Encoding.UTF8.GetBytes(i.Text))}"));
        }

        private static List<PeerLocalNoteHistoryItem> NormalizePeerLocalNoteHistory(IEnumerable<PeerLocalNoteHistoryItem> items) {
            List<PeerLocalNoteHistoryItem> normalized = new List<PeerLocalNoteHistoryItem>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            if (items == null) return normalized;

            foreach (PeerLocalNoteHistoryItem item in items
                .Where(i => !String.IsNullOrWhiteSpace(i?.Text))
                .OrderByDescending(i => i.UpdatedAtUnix)) {
                string text = item.Text.Trim();
                string key = NormalizeHistoryText(text);
                if (!seen.Add(key)) continue;

                normalized.Add(new PeerLocalNoteHistoryItem {
                    Text = text.Length > 4000 ? text.Substring(0, 4000) : text,
                    UpdatedAtUnix = item.UpdatedAtUnix <= 0 ? DateTimeOffset.Now.ToUnixTimeSeconds() : item.UpdatedAtUnix
                });

                if (normalized.Count >= PeerLocalNoteHistoryLimit) break;
            }

            return normalized;
        }

        private static string NormalizeHistoryText(string text) {
            return String.Join("\n", (text ?? String.Empty).Split(['\r', '\n'], StringSplitOptions.TrimEntries));
        }

        public static string GetPeerLocalAlias(long peerId) {
            return Get($"{PEER_LOCAL_ALIAS_PREFIX}{peerId}", String.Empty);
        }

        public static void SetPeerLocalAlias(long peerId, string alias) {
            string key = $"{PEER_LOCAL_ALIAS_PREFIX}{peerId}";
            Set(key, String.IsNullOrWhiteSpace(alias) ? null : alias.Trim());
        }

        public static string GetPeerLocalAvatar(long peerId) {
            return Get($"{PEER_LOCAL_AVATAR_PREFIX}{peerId}", String.Empty);
        }

        public static Uri GetPeerLocalAvatarUri(long peerId) {
            string value = GetPeerLocalAvatar(peerId);
            if (String.IsNullOrWhiteSpace(value)) return null;
            if (Uri.TryCreate(value, UriKind.Absolute, out Uri uri)) return uri;
            if (Path.IsPathFullyQualified(value)) return new Uri(value);
            return null;
        }

        public static void SetPeerLocalAvatar(long peerId, string avatar) {
            string key = $"{PEER_LOCAL_AVATAR_PREFIX}{peerId}";
            Set(key, String.IsNullOrWhiteSpace(avatar) ? null : avatar.Trim());
        }

        public static IReadOnlyList<string> GetPeerLocalTags(long peerId) {
            string value = Get($"{PEER_LOCAL_TAGS_PREFIX}{peerId}", String.Empty);
            if (String.IsNullOrWhiteSpace(value)) return Array.Empty<string>();

            return NormalizePeerLocalTags(value.Split([',', ';', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        public static string GetPeerLocalTagsText(long peerId) {
            return String.Join(", ", GetPeerLocalTags(peerId));
        }

        public static void SetPeerLocalTags(long peerId, string tags) {
            SetPeerLocalTags(peerId, tags?.Split([',', ';', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        public static void SetPeerLocalTags(long peerId, IEnumerable<string> tags) {
            List<string> normalized = NormalizePeerLocalTags(tags);
            string key = $"{PEER_LOCAL_TAGS_PREFIX}{peerId}";
            Set(key, normalized.Count == 0 ? null : String.Join(",", normalized));
        }

        public static bool PeerHasLocalTag(long peerId, string tag) {
            if (String.IsNullOrWhiteSpace(tag)) return false;
            string normalizedTag = NormalizePeerLocalTag(tag);
            return GetPeerLocalTags(peerId).Any(t => String.Equals(t, normalizedTag, StringComparison.OrdinalIgnoreCase));
        }

        public static IReadOnlyList<string> GetKnownPeerLocalTags() {
            return _settings
                .Where(s => s.Key.StartsWith(PEER_LOCAL_TAGS_PREFIX, StringComparison.Ordinal))
                .SelectMany(s => (s.Value?.ToString() ?? String.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Select(NormalizePeerLocalTag)
                .Where(t => !String.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(t => t)
                .Take(48)
                .ToList();
        }

        public static IReadOnlyList<string> GetPeerQuickReplies(long peerId) {
            string value = Get($"{PEER_QUICK_REPLIES_PREFIX}{peerId}", String.Empty);
            if (String.IsNullOrWhiteSpace(value)) return Array.Empty<string>();

            try {
                List<string> replies = JsonSerializer.Deserialize<List<string>>(value) ?? new List<string>();
                return NormalizeQuickReplies(replies);
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot read quick replies for peer {PeerId}", peerId);
                return Array.Empty<string>();
            }
        }

        public static void SetPeerQuickReplies(long peerId, IEnumerable<string> replies) {
            List<string> normalized = NormalizeQuickReplies(replies);
            string key = $"{PEER_QUICK_REPLIES_PREFIX}{peerId}";
            Set(key, normalized.Count == 0 ? null : JsonSerializer.Serialize(normalized));
        }

        public static bool AddPeerQuickReply(long peerId, string reply) {
            string normalized = NormalizeQuickReply(reply);
            if (String.IsNullOrWhiteSpace(normalized)) return false;

            List<string> replies = GetPeerQuickReplies(peerId).ToList();
            if (replies.Any(r => String.Equals(r, normalized, StringComparison.OrdinalIgnoreCase))) return false;

            replies.Insert(0, normalized);
            SetPeerQuickReplies(peerId, replies);
            return true;
        }

        public static IReadOnlyList<int> GetPeerQuickReactionIds(long peerId) {
            string value = Get($"{PEER_QUICK_REACTIONS_PREFIX}{peerId}", String.Empty);
            if (String.IsNullOrWhiteSpace(value)) return Array.Empty<int>();

            try {
                List<int> reactions = JsonSerializer.Deserialize<List<int>>(value) ?? new List<int>();
                return NormalizeReactionIds(reactions);
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot read quick reactions for peer {PeerId}", peerId);
                return Array.Empty<int>();
            }
        }

        public static void SetPeerQuickReactionIds(long peerId, IEnumerable<int> reactions) {
            List<int> normalized = NormalizeReactionIds(reactions);
            string key = $"{PEER_QUICK_REACTIONS_PREFIX}{peerId}";
            Set(key, normalized.Count == 0 ? null : JsonSerializer.Serialize(normalized));
        }

        public static void PromotePeerQuickReactionId(long peerId, int reactionId, IEnumerable<int> fallback = null) {
            if (reactionId <= 0) return;

            List<int> reactions = GetPeerQuickReactionIds(peerId).ToList();
            if (reactions.Count == 0 && fallback != null) reactions.AddRange(fallback);

            reactions.RemoveAll(id => id == reactionId);
            reactions.Insert(0, reactionId);
            SetPeerQuickReactionIds(peerId, reactions);
        }

        public static IReadOnlyList<string> GetPersonQuickReplies(long ownerId, long senderId) {
            if (senderId == 0) return Array.Empty<string>();

            string value = Get(BuildPersonQuickRepliesKey(ownerId, senderId), String.Empty);
            if (String.IsNullOrWhiteSpace(value)) return Array.Empty<string>();

            try {
                List<string> replies = JsonSerializer.Deserialize<List<string>>(value) ?? new List<string>();
                return NormalizeQuickReplies(replies);
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot read quick replies for owner {OwnerId} and sender {SenderId}", ownerId, senderId);
                return Array.Empty<string>();
            }
        }

        public static void SetPersonQuickReplies(long ownerId, long senderId, IEnumerable<string> replies) {
            if (senderId == 0) return;

            List<string> normalized = NormalizeQuickReplies(replies);
            Set(BuildPersonQuickRepliesKey(ownerId, senderId), normalized.Count == 0 ? null : JsonSerializer.Serialize(normalized));
        }

        public static bool AddPersonQuickReply(long ownerId, long senderId, string reply) {
            if (senderId == 0) return false;

            string normalized = NormalizeQuickReply(reply);
            if (String.IsNullOrWhiteSpace(normalized)) return false;

            List<string> replies = GetPersonQuickReplies(ownerId, senderId).ToList();
            if (replies.Any(r => String.Equals(r, normalized, StringComparison.OrdinalIgnoreCase))) return false;

            replies.Insert(0, normalized);
            SetPersonQuickReplies(ownerId, senderId, replies);
            return true;
        }

        public static IReadOnlyList<string> GetChatFilterQuickReplies(long ownerId, string filterId) {
            string value = Get(BuildChatFilterQuickRepliesKey(ownerId, filterId), String.Empty);
            if (String.IsNullOrWhiteSpace(value)) return Array.Empty<string>();

            try {
                List<string> replies = JsonSerializer.Deserialize<List<string>>(value) ?? new List<string>();
                return NormalizeQuickReplies(replies);
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot read quick replies for owner {OwnerId} and filter {FilterId}", ownerId, filterId);
                return Array.Empty<string>();
            }
        }

        public static void SetChatFilterQuickReplies(long ownerId, string filterId, IEnumerable<string> replies) {
            List<string> normalized = NormalizeQuickReplies(replies);
            Set(BuildChatFilterQuickRepliesKey(ownerId, filterId), normalized.Count == 0 ? null : JsonSerializer.Serialize(normalized));
        }

        public static bool AddChatFilterQuickReply(long ownerId, string filterId, string reply) {
            string normalized = NormalizeQuickReply(reply);
            if (String.IsNullOrWhiteSpace(normalized)) return false;

            List<string> replies = GetChatFilterQuickReplies(ownerId, filterId).ToList();
            if (replies.Any(r => String.Equals(r, normalized, StringComparison.OrdinalIgnoreCase))) return false;

            replies.Insert(0, normalized);
            SetChatFilterQuickReplies(ownerId, filterId, replies);
            return true;
        }

        private static string BuildChatFilterQuickRepliesKey(long ownerId, string filterId) {
            return $"{CHAT_FILTER_QUICK_REPLIES_PREFIX}{ownerId}_{NormalizeSettingsKeyPart(filterId)}";
        }

        private static string BuildPersonQuickRepliesKey(long ownerId, long senderId) {
            return $"{PERSON_QUICK_REPLIES_PREFIX}{ownerId}_{senderId}";
        }

        public static string GetPeerPinnedQuickReply(long peerId) {
            return Get($"{PEER_PINNED_QUICK_REPLY_PREFIX}{peerId}", String.Empty);
        }

        public static void SetPeerPinnedQuickReply(long peerId, string reply) {
            string normalized = NormalizeQuickReply(reply);
            Set($"{PEER_PINNED_QUICK_REPLY_PREFIX}{peerId}", String.IsNullOrWhiteSpace(normalized) ? null : normalized);
        }

        private static string NormalizeSettingsKeyPart(string value) {
            if (String.IsNullOrWhiteSpace(value)) return "default";

            Span<char> buffer = stackalloc char[Math.Min(value.Length, 80)];
            int length = 0;
            foreach (char c in value) {
                if (length >= buffer.Length) break;
                buffer[length++] = Char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_';
            }

            return length == 0 ? "default" : new string(buffer[..length]);
        }

        private static List<string> NormalizeQuickReplies(IEnumerable<string> replies) {
            if (replies == null) return new List<string>();

            return replies
                .Select(NormalizeQuickReply)
                .Where(reply => !String.IsNullOrWhiteSpace(reply))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(32)
                .ToList();
        }

        private static List<int> NormalizeReactionIds(IEnumerable<int> reactions) {
            if (reactions == null) return new List<int>();

            return reactions
                .Where(id => id > 0)
                .Distinct()
                .Take(8)
                .ToList();
        }

        private static List<string> NormalizePeerLocalTags(IEnumerable<string> tags) {
            if (tags == null) return new List<string>();

            return tags
                .Select(NormalizePeerLocalTag)
                .Where(t => !String.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(16)
                .ToList();
        }

        private static string NormalizePeerLocalTag(string tag) {
            if (String.IsNullOrWhiteSpace(tag)) return String.Empty;

            string normalized = tag.Trim().Trim('#').ToLowerInvariant();
            normalized = String.Join("-", normalized.Split([' ', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            if (normalized.Length > 32) normalized = normalized[..32];
            return normalized;
        }

        private static string NormalizeQuickReply(string reply) {
            if (String.IsNullOrWhiteSpace(reply)) return String.Empty;
            return reply.Trim().Length <= 512 ? reply.Trim() : reply.Trim()[..512];
        }

        public static string GetPeerCurrentDraft(long peerId) {
            return Get($"{PEER_CURRENT_DRAFT_PREFIX}{peerId}", String.Empty);
        }

        public static void ClearPeerCurrentDraft(long peerId) {
            Set($"{PEER_CURRENT_DRAFT_PREFIX}{peerId}", null);
        }

        public static IReadOnlyList<PeerDraftHistoryItem> GetPeerDraftHistory(long peerId) {
            string value = Get($"{PEER_DRAFT_HISTORY_PREFIX}{peerId}", String.Empty);
            if (String.IsNullOrWhiteSpace(value)) return Array.Empty<PeerDraftHistoryItem>();

            try {
                return NormalizeDraftHistory(JsonSerializer.Deserialize<List<PeerDraftHistoryItem>>(value));
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot read draft history for peer {PeerId}", peerId);
                return Array.Empty<PeerDraftHistoryItem>();
            }
        }

        public static void SavePeerDraftSnapshot(long peerId, string text) {
            string normalized = NormalizeDraftText(text);
            if (String.IsNullOrWhiteSpace(normalized)) {
                ClearPeerCurrentDraft(peerId);
                return;
            }

            Set($"{PEER_CURRENT_DRAFT_PREFIX}{peerId}", normalized);

            List<PeerDraftHistoryItem> history = GetPeerDraftHistory(peerId).ToList();
            history.RemoveAll(item => String.Equals(item.Text, normalized, StringComparison.OrdinalIgnoreCase));
            history.Insert(0, new PeerDraftHistoryItem {
                Text = normalized,
                UpdatedAtUnix = DateTimeOffset.Now.ToUnixTimeSeconds()
            });

            SetPeerDraftHistory(peerId, history);
        }

        public static void SetPeerDraftHistory(long peerId, IEnumerable<PeerDraftHistoryItem> history) {
            List<PeerDraftHistoryItem> normalized = NormalizeDraftHistory(history);
            Set($"{PEER_DRAFT_HISTORY_PREFIX}{peerId}", normalized.Count == 0 ? null : JsonSerializer.Serialize(normalized));
        }

        private static List<PeerDraftHistoryItem> NormalizeDraftHistory(IEnumerable<PeerDraftHistoryItem> history) {
            return history?
                .Where(item => item != null)
                .Select(item => new PeerDraftHistoryItem {
                    Text = NormalizeDraftText(item.Text),
                    UpdatedAtUnix = item.UpdatedAtUnix > 0 ? item.UpdatedAtUnix : DateTimeOffset.Now.ToUnixTimeSeconds()
                })
                .Where(item => !String.IsNullOrWhiteSpace(item.Text))
                .GroupBy(item => item.Text, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(item => item.UpdatedAtUnix).First())
                .OrderByDescending(item => item.UpdatedAtUnix)
                .Take(20)
                .ToList() ?? new List<PeerDraftHistoryItem>();
        }

        private static string NormalizeDraftText(string text) {
            if (String.IsNullOrWhiteSpace(text)) return String.Empty;
            string normalized = text.Trim();
            return normalized.Length <= 2048 ? normalized : normalized[..2048];
        }

        public static IReadOnlyList<ScheduledMessageItem> GetScheduledMessages() {
            string value = Get(SCHEDULED_MESSAGES, String.Empty);
            if (String.IsNullOrWhiteSpace(value)) return Array.Empty<ScheduledMessageItem>();

            try {
                return NormalizeScheduledMessages(JsonSerializer.Deserialize<List<ScheduledMessageItem>>(value));
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot read scheduled messages.");
                return Array.Empty<ScheduledMessageItem>();
            }
        }

        public static void AddScheduledMessage(ScheduledMessageItem item) {
            if (item == null) return;

            List<ScheduledMessageItem> items = GetScheduledMessages().ToList();
            if (String.IsNullOrWhiteSpace(item.Id)) item.Id = Guid.NewGuid().ToString("N");
            if (item.CreatedAtUnix <= 0) item.CreatedAtUnix = DateTimeOffset.Now.ToUnixTimeSeconds();
            items.RemoveAll(existing => existing.Id == item.Id);
            items.Add(item);
            SetScheduledMessages(items);
        }

        public static void RemoveScheduledMessage(string id) {
            if (String.IsNullOrWhiteSpace(id)) return;

            List<ScheduledMessageItem> items = GetScheduledMessages().Where(item => item.Id != id).ToList();
            SetScheduledMessages(items);
        }

        public static void SetScheduledMessages(IEnumerable<ScheduledMessageItem> items) {
            List<ScheduledMessageItem> normalized = NormalizeScheduledMessages(items);
            Set(SCHEDULED_MESSAGES, normalized.Count == 0 ? null : JsonSerializer.Serialize(normalized));
        }

        private static List<ScheduledMessageItem> NormalizeScheduledMessages(IEnumerable<ScheduledMessageItem> items) {
            return items?
                .Where(item => item != null
                    && item.SessionId != 0
                    && item.PeerId != 0
                    && item.NextSendUnix > 0
                    && !String.IsNullOrWhiteSpace(item.Text))
                .Select(item => new ScheduledMessageItem {
                    Id = String.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N") : item.Id,
                    SessionId = item.SessionId,
                    GroupId = item.GroupId,
                    PeerId = item.PeerId,
                    Text = NormalizeDraftText(item.Text),
                    NextSendUnix = item.NextSendUnix,
                    RepeatIntervalMinutes = Math.Clamp(item.RepeatIntervalMinutes, 0, 60 * 24 * 30),
                    CreatedAtUnix = item.CreatedAtUnix > 0 ? item.CreatedAtUnix : DateTimeOffset.Now.ToUnixTimeSeconds()
                })
                .Take(128)
                .ToList() ?? new List<ScheduledMessageItem>();
        }

        public static string GetPeerLocalTheme(long peerId) {
            return Get($"{PEER_LOCAL_THEME_PREFIX}{peerId}", String.Empty);
        }

        public static void SetPeerLocalTheme(long peerId, string theme) {
            string key = $"{PEER_LOCAL_THEME_PREFIX}{peerId}";
            Set(key, String.IsNullOrWhiteSpace(theme)
                || theme == AppearanceManager.InheritChatBackgroundId
                || theme == AppearanceManager.DefaultChatBackgroundId
                ? null
                : theme.Trim());
        }

        public static string GetPeerLocalBackgroundImage(long peerId) {
            return Get($"{PEER_LOCAL_BACKGROUND_IMAGE_PREFIX}{peerId}", String.Empty);
        }

        public static Uri GetPeerLocalBackgroundImageUri(long peerId) {
            return GetImageUri(GetPeerLocalBackgroundImage(peerId));
        }

        public static void SetPeerLocalBackgroundImage(long peerId, string image) {
            string key = $"{PEER_LOCAL_BACKGROUND_IMAGE_PREFIX}{peerId}";
            Set(key, String.IsNullOrWhiteSpace(image) ? null : image.Trim());
        }

        public static int GetPeerLocalBackgroundDim(long peerId) {
            return NormalizePercent(Get($"{PEER_LOCAL_BACKGROUND_DIM_PREFIX}{peerId}", 0), 0, 80);
        }

        public static void SetPeerLocalBackgroundDim(long peerId, int dim) {
            string key = $"{PEER_LOCAL_BACKGROUND_DIM_PREFIX}{peerId}";
            int normalized = NormalizePercent(dim, 0, 80);
            Set(key, normalized == 0 ? null : normalized);
        }

        public static int GetPeerLocalBackgroundBlur(long peerId) {
            return Math.Clamp(Get($"{PEER_LOCAL_BACKGROUND_BLUR_PREFIX}{peerId}", 0), 0, 16);
        }

        public static void SetPeerLocalBackgroundBlur(long peerId, int blur) {
            string key = $"{PEER_LOCAL_BACKGROUND_BLUR_PREFIX}{peerId}";
            int normalized = Math.Clamp(blur, 0, 16);
            Set(key, normalized == 0 ? null : normalized);
        }

        public static int GetPeerLocalBackgroundBrightness(long peerId) {
            return Math.Clamp(Get($"{PEER_LOCAL_BACKGROUND_BRIGHTNESS_PREFIX}{peerId}", 0), -40, 40);
        }

        public static void SetPeerLocalBackgroundBrightness(long peerId, int brightness) {
            string key = $"{PEER_LOCAL_BACKGROUND_BRIGHTNESS_PREFIX}{peerId}";
            int normalized = Math.Clamp(brightness, -40, 40);
            Set(key, normalized == 0 ? null : normalized);
        }

        public static string GetPeerLocalAccent(long peerId) {
            return Get($"{PEER_LOCAL_ACCENT_PREFIX}{peerId}", String.Empty);
        }

        public static void SetPeerLocalAccent(long peerId, string accent) {
            SetPeerLocalAppearanceValue(PEER_LOCAL_ACCENT_PREFIX, peerId, accent);
        }

        public static string GetAccountAccent(long accountId) {
            return accountId == 0 ? String.Empty : Get(BuildAccountAccentKey(accountId), String.Empty);
        }

        public static void SetAccountAccent(long accountId, string accent) {
            if (accountId == 0) return;
            string key = BuildAccountAccentKey(accountId);
            Set(key, String.IsNullOrWhiteSpace(accent) || accent == AppearanceManager.InheritChatBackgroundId ? null : accent.Trim());
        }

        public static string BuildAccountAccentKey(long accountId) {
            return $"{ACCOUNT_ACCENT_PREFIX}{accountId}";
        }

        public static string GetPeerLocalDensity(long peerId) {
            return Get($"{PEER_LOCAL_DENSITY_PREFIX}{peerId}", String.Empty);
        }

        public static void SetPeerLocalDensity(long peerId, string density) {
            SetPeerLocalAppearanceValue(PEER_LOCAL_DENSITY_PREFIX, peerId, density);
        }

        public static string GetPeerLocalFont(long peerId) {
            return Get($"{PEER_LOCAL_FONT_PREFIX}{peerId}", String.Empty);
        }

        public static void SetPeerLocalFont(long peerId, string font) {
            SetPeerLocalAppearanceValue(PEER_LOCAL_FONT_PREFIX, peerId, font);
        }

        public static string GetPeerLocalBubbleStyle(long peerId) {
            return Get($"{PEER_LOCAL_BUBBLE_STYLE_PREFIX}{peerId}", String.Empty);
        }

        public static void SetPeerLocalBubbleStyle(long peerId, string bubbleStyle) {
            SetPeerLocalAppearanceValue(PEER_LOCAL_BUBBLE_STYLE_PREFIX, peerId, bubbleStyle);
        }

        public static string GetPeerLocalBubbleColor(long peerId) {
            return Get($"{PEER_LOCAL_BUBBLE_COLOR_PREFIX}{peerId}", String.Empty);
        }

        public static void SetPeerLocalBubbleColor(long peerId, string bubbleColor) {
            SetPeerLocalAppearanceValue(PEER_LOCAL_BUBBLE_COLOR_PREFIX, peerId, bubbleColor);
        }

        public static string GetPeerLocalEmojiPack(long peerId) {
            return EmojiPackIds.Normalize(Get($"{PEER_LOCAL_EMOJI_PACK_PREFIX}{peerId}", EmojiPackIds.Inherit), true);
        }

        public static string ResolvePeerEmojiPack(long peerId) {
            string local = GetPeerLocalEmojiPack(peerId);
            return local == EmojiPackIds.Inherit ? EmojiPack : EmojiPackIds.Normalize(local);
        }

        public static void SetPeerLocalEmojiPack(long peerId, string packId) {
            string normalized = EmojiPackIds.Normalize(packId, true);
            string key = $"{PEER_LOCAL_EMOJI_PACK_PREFIX}{peerId}";
            Set(key, normalized == EmojiPackIds.Inherit ? null : normalized);
        }

        private static void SetPeerLocalAppearanceValue(string prefix, long peerId, string value) {
            string key = $"{prefix}{peerId}";
            Set(key, String.IsNullOrWhiteSpace(value) || value == AppearanceManager.InheritChatBackgroundId ? null : value.Trim());
        }

        public static HashSet<int> GetLocallyHiddenMessageIds(long peerId) {
            string value = Get($"{PEER_LOCAL_HIDDEN_MESSAGES_PREFIX}{peerId}", String.Empty);
            HashSet<int> ids = new HashSet<int>();
            if (String.IsNullOrWhiteSpace(value)) return ids;

            foreach (string rawId in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
                if (int.TryParse(rawId, out int id) && id > 0) ids.Add(id);
            }

            return ids;
        }

        public static void HideMessagesLocally(long peerId, IEnumerable<int> messageIds) {
            HashSet<int> ids = GetLocallyHiddenMessageIds(peerId);
            foreach (int messageId in messageIds) {
                if (messageId > 0) ids.Add(messageId);
            }

            SetLocallyHiddenMessageIds(peerId, ids);
        }

        public static void UnhideMessagesLocally(long peerId, IEnumerable<int> messageIds) {
            HashSet<int> ids = GetLocallyHiddenMessageIds(peerId);
            foreach (int messageId in messageIds) {
                ids.Remove(messageId);
            }

            SetLocallyHiddenMessageIds(peerId, ids);
        }

        private static void SetLocallyHiddenMessageIds(long peerId, HashSet<int> ids) {
            string key = $"{PEER_LOCAL_HIDDEN_MESSAGES_PREFIX}{peerId}";
            Set(key, ids.Count == 0 ? null : String.Join(",", ids.OrderBy(id => id)));
        }

        public static DateTimeOffset? GetPeerQuietUntil(long peerId) {
            long unix = Get($"{PEER_LOCAL_QUIET_UNTIL_PREFIX}{peerId}", 0L);
            if (unix <= 0) return null;
            return DateTimeOffset.FromUnixTimeSeconds(unix);
        }

        public static bool IsPeerQuietNow(long peerId, DateTimeOffset now) {
            DateTimeOffset? until = GetPeerQuietUntil(peerId);
            if (until == null) return false;
            if (until.Value <= now) {
                SetPeerQuietUntil(peerId, null);
                return false;
            }

            return true;
        }

        public static void SetPeerQuietUntil(long peerId, DateTimeOffset? until) {
            string key = $"{PEER_LOCAL_QUIET_UNTIL_PREFIX}{peerId}";
            Set(key, until == null || until.Value <= DateTimeOffset.Now ? null : until.Value.ToUnixTimeSeconds());
        }

        public static string GetPeerQuietTextRules(long peerId) {
            return Get($"{PEER_LOCAL_QUIET_TEXT_RULES_PREFIX}{peerId}", String.Empty);
        }

        public static void SetPeerQuietTextRules(long peerId, string rules) {
            string key = $"{PEER_LOCAL_QUIET_TEXT_RULES_PREFIX}{peerId}";
            Set(key, String.IsNullOrWhiteSpace(rules) ? null : rules.Trim());
        }

        public static bool IsPeerAntiSpamEnabled(long peerId) {
            return Get($"{PEER_LOCAL_ANTISPAM_ENABLED_PREFIX}{peerId}", false);
        }

        public static void SetPeerAntiSpamEnabled(long peerId, bool enabled) {
            string key = $"{PEER_LOCAL_ANTISPAM_ENABLED_PREFIX}{peerId}";
            Set(key, enabled ? true : null);
        }

        public static IReadOnlyDictionary<int, string> GetLocalMessageReactions(long peerId) {
            string value = Get($"{PEER_LOCAL_MESSAGE_REACTIONS_PREFIX}{peerId}", String.Empty);
            if (String.IsNullOrWhiteSpace(value)) return new Dictionary<int, string>();

            try {
                Dictionary<int, string> reactions = JsonSerializer.Deserialize<Dictionary<int, string>>(value) ?? new Dictionary<int, string>();
                return reactions
                    .Where(pair => pair.Key > 0 && !String.IsNullOrWhiteSpace(pair.Value))
                    .ToDictionary(pair => pair.Key, pair => NormalizeLocalMessageReaction(pair.Value));
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot read local message reactions for peer {PeerId}", peerId);
                return new Dictionary<int, string>();
            }
        }

        public static string GetLocalMessageReaction(long peerId, int conversationMessageId) {
            if (conversationMessageId <= 0) return String.Empty;
            IReadOnlyDictionary<int, string> reactions = GetLocalMessageReactions(peerId);
            return reactions.TryGetValue(conversationMessageId, out string reaction) ? reaction : String.Empty;
        }

        public static void SetLocalMessageReaction(long peerId, int conversationMessageId, string reaction) {
            if (conversationMessageId <= 0) return;

            Dictionary<int, string> reactions = GetLocalMessageReactions(peerId).ToDictionary(pair => pair.Key, pair => pair.Value);
            string normalized = NormalizeLocalMessageReaction(reaction);
            if (String.IsNullOrWhiteSpace(normalized)) {
                reactions.Remove(conversationMessageId);
            } else {
                reactions[conversationMessageId] = normalized;
            }

            string key = $"{PEER_LOCAL_MESSAGE_REACTIONS_PREFIX}{peerId}";
            Set(key, reactions.Count == 0 ? null : JsonSerializer.Serialize(reactions));
        }

        private static string NormalizeLocalMessageReaction(string reaction) {
            if (String.IsNullOrWhiteSpace(reaction)) return String.Empty;

            string normalized = reaction.Trim();
            return normalized.Length <= 16 ? normalized : normalized[..16];
        }

        public static HashSet<long> GetMutedSenderIds(long peerId) {
            string value = Get($"{PEER_LOCAL_MUTED_SENDERS_PREFIX}{peerId}", String.Empty);
            HashSet<long> ids = new HashSet<long>();
            if (String.IsNullOrWhiteSpace(value)) return ids;

            foreach (string rawId in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
                if (long.TryParse(rawId, out long id) && id != 0) ids.Add(id);
            }

            return ids;
        }

        public static bool IsSenderMutedLocally(long peerId, long senderId) {
            return GetMutedSenderIds(peerId).Contains(senderId);
        }

        public static void SetMutedSenderIds(long peerId, IEnumerable<long> senderIds) {
            HashSet<long> ids = senderIds?
                .Where(id => id != 0)
                .ToHashSet() ?? new HashSet<long>();
            string key = $"{PEER_LOCAL_MUTED_SENDERS_PREFIX}{peerId}";
            Set(key, ids.Count == 0 ? null : String.Join(",", ids.OrderBy(id => id)));
        }

        public static void MuteSenderLocally(long peerId, long senderId) {
            if (senderId == 0) return;

            HashSet<long> ids = GetMutedSenderIds(peerId);
            ids.Add(senderId);
            SetMutedSenderIds(peerId, ids);
        }

        public static void UnmuteSenderLocally(long peerId, long senderId) {
            HashSet<long> ids = GetMutedSenderIds(peerId);
            ids.Remove(senderId);
            SetMutedSenderIds(peerId, ids);
        }

        public static HashSet<long> GetShadowBannedSenderIds(long peerId) {
            string value = Get($"{PEER_LOCAL_SHADOW_BANNED_SENDERS_PREFIX}{peerId}", String.Empty);
            HashSet<long> ids = new HashSet<long>();
            if (String.IsNullOrWhiteSpace(value)) return ids;

            foreach (string rawId in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
                if (long.TryParse(rawId, out long id) && id != 0) ids.Add(id);
            }

            return ids;
        }

        public static bool IsSenderShadowBanned(long peerId, long senderId) {
            return GetShadowBannedSenderIds(peerId).Contains(senderId);
        }

        public static void SetShadowBannedSenderIds(long peerId, IEnumerable<long> senderIds) {
            HashSet<long> ids = senderIds?
                .Where(id => id != 0)
                .ToHashSet() ?? new HashSet<long>();
            string key = $"{PEER_LOCAL_SHADOW_BANNED_SENDERS_PREFIX}{peerId}";
            Set(key, ids.Count == 0 ? null : String.Join(",", ids.OrderBy(id => id)));
        }

        public static void ShadowBanSenderLocally(long peerId, long senderId) {
            if (senderId == 0) return;

            HashSet<long> ids = GetShadowBannedSenderIds(peerId);
            ids.Add(senderId);
            SetShadowBannedSenderIds(peerId, ids);
        }

        public static void UnshadowBanSenderLocally(long peerId, long senderId) {
            HashSet<long> ids = GetShadowBannedSenderIds(peerId);
            ids.Remove(senderId);
            SetShadowBannedSenderIds(peerId, ids);
        }

        public static string GetShadowBannedTextRules(long peerId) {
            return Get($"{PEER_LOCAL_SHADOW_BANNED_TEXT_RULES_PREFIX}{peerId}", String.Empty);
        }

        public static void SetShadowBannedTextRules(long peerId, string rules) {
            string key = $"{PEER_LOCAL_SHADOW_BANNED_TEXT_RULES_PREFIX}{peerId}";
            Set(key, String.IsNullOrWhiteSpace(rules) ? null : rules.Trim());
        }

        public static ShadowBannedAttachmentKinds GetShadowBannedAttachmentKinds(long peerId) {
            int raw = Get($"{PEER_LOCAL_SHADOW_BANNED_ATTACHMENT_KINDS_PREFIX}{peerId}", 0);
            ShadowBannedAttachmentKinds all = ShadowBannedAttachmentKinds.Voice
                | ShadowBannedAttachmentKinds.Link
                | ShadowBannedAttachmentKinds.Sticker
                | ShadowBannedAttachmentKinds.Graffiti
                | ShadowBannedAttachmentKinds.Forwarded;
            return (ShadowBannedAttachmentKinds)raw & all;
        }

        public static void SetShadowBannedAttachmentKinds(long peerId, ShadowBannedAttachmentKinds kinds) {
            string key = $"{PEER_LOCAL_SHADOW_BANNED_ATTACHMENT_KINDS_PREFIX}{peerId}";
            Set(key, kinds == ShadowBannedAttachmentKinds.None ? null : (int)kinds);
        }

        public static List<SelfDestructMessageSchedule> GetSelfDestructMessages(long peerId) {
            string value = Get($"{PEER_LOCAL_SELF_DESTRUCT_PREFIX}{peerId}", String.Empty);
            if (String.IsNullOrWhiteSpace(value)) return new List<SelfDestructMessageSchedule>();

            try {
                return JsonSerializer.Deserialize<List<SelfDestructMessageSchedule>>(value) ?? new List<SelfDestructMessageSchedule>();
            } catch (Exception ex) {
                Log.Warning(ex, "Cannot read self-destruct schedule for peer {PeerId}", peerId);
                return new List<SelfDestructMessageSchedule>();
            }
        }

        public static void ScheduleSelfDestructMessages(long peerId, IEnumerable<int> messageIds, DateTimeOffset hideAt, bool bestEffortDelete) {
            List<SelfDestructMessageSchedule> schedules = GetSelfDestructMessages(peerId);
            HashSet<int> ids = messageIds?.Where(id => id > 0).ToHashSet() ?? new HashSet<int>();
            if (ids.Count == 0) return;

            schedules.RemoveAll(s => ids.Contains(s.ConversationMessageId));
            schedules.AddRange(ids.Select(id => new SelfDestructMessageSchedule {
                ConversationMessageId = id,
                HideAtUnix = hideAt.ToUnixTimeSeconds(),
                BestEffortDelete = bestEffortDelete
            }));

            SetSelfDestructMessages(peerId, schedules);
        }

        public static List<SelfDestructMessageSchedule> GetExpiredSelfDestructMessages(long peerId, DateTimeOffset now) {
            long unix = now.ToUnixTimeSeconds();
            return GetSelfDestructMessages(peerId)
                .Where(s => s.ConversationMessageId > 0 && s.HideAtUnix <= unix)
                .ToList();
        }

        public static void ClearSelfDestructMessages(long peerId, IEnumerable<int> messageIds) {
            HashSet<int> ids = messageIds?.Where(id => id > 0).ToHashSet() ?? new HashSet<int>();
            if (ids.Count == 0) return;

            List<SelfDestructMessageSchedule> schedules = GetSelfDestructMessages(peerId);
            schedules.RemoveAll(s => ids.Contains(s.ConversationMessageId));
            SetSelfDestructMessages(peerId, schedules);
        }

        private static void SetSelfDestructMessages(long peerId, List<SelfDestructMessageSchedule> schedules) {
            string key = $"{PEER_LOCAL_SELF_DESTRUCT_PREFIX}{peerId}";
            List<SelfDestructMessageSchedule> valid = schedules?
                .Where(s => s.ConversationMessageId > 0 && s.HideAtUnix > 0)
                .OrderBy(s => s.HideAtUnix)
                .ThenBy(s => s.ConversationMessageId)
                .ToList() ?? new List<SelfDestructMessageSchedule>();

            Set(key, valid.Count == 0 ? null : JsonSerializer.Serialize(valid));
        }

        private static string ToBase64Url(byte[] data) {
            return Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private static byte[] FromBase64Url(string value) {
            string base64 = value.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4) {
                case 2:
                    base64 += "==";
                    break;
                case 3:
                    base64 += "=";
                    break;
            }

            return Convert.FromBase64String(base64);
        }

        private static int NormalizeHour(int hour) {
            if (hour < 0) return 0;
            if (hour > 23) return 23;
            return hour;
        }

        private static string NormalizeApiDomain(string domain) {
            if (String.IsNullOrWhiteSpace(domain)) return ELOR.VKAPILib.VKAPI.DefaultDomain;

            string normalized = domain.Trim().Trim('/');
            if (Uri.TryCreate(normalized, UriKind.Absolute, out Uri uri) && !String.IsNullOrWhiteSpace(uri.Host)) {
                normalized = uri.Host;
            }

            return String.IsNullOrWhiteSpace(normalized) ? ELOR.VKAPILib.VKAPI.DefaultDomain : normalized;
        }

        private static string NormalizeApiVersion(string version) {
            if (String.IsNullOrWhiteSpace(version)) return ELOR.VKAPILib.VKAPI.BundledVersion;

            string normalized = version.Trim();
            return normalized.Length > 16 ? normalized[..16] : normalized;
        }

        private static string NormalizeFontFamily(string fontFamily) {
            if (String.IsNullOrWhiteSpace(fontFamily)) return "Segoe UI";

            string normalized = fontFamily.Trim();
            return normalized.Length > 96 ? normalized[..96] : normalized;
        }

        private static string NormalizePathOrUri(string value) {
            if (String.IsNullOrWhiteSpace(value)) return null;

            string normalized = Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));
            return normalized.Length > 1024 ? normalized[..1024] : normalized;
        }

        private static Uri GetImageUri(string value) {
            if (String.IsNullOrWhiteSpace(value)) return null;
            if (Uri.TryCreate(value, UriKind.Absolute, out Uri uri)) return uri;
            if (Path.IsPathFullyQualified(value)) return new Uri(value);
            return null;
        }

        private static string NormalizeProxyUri(string value) {
            if (String.IsNullOrWhiteSpace(value)) return String.Empty;

            string normalized = value.Trim();
            if (!normalized.Contains("://", StringComparison.Ordinal)) normalized = $"http://{normalized}";
            return normalized.Length > 512 ? normalized[..512] : normalized;
        }

        private static string NormalizeAvatarSize(string size, string defaultValue) {
            if (String.IsNullOrWhiteSpace(size)) return defaultValue;

            string normalized = size.Trim().ToLowerInvariant();
            return normalized switch {
                AvatarSizeIds.Auto => AvatarSizeIds.Auto,
                AvatarSizeIds.Small => AvatarSizeIds.Small,
                AvatarSizeIds.Medium => AvatarSizeIds.Medium,
                AvatarSizeIds.Large => AvatarSizeIds.Large,
                _ => defaultValue
            };
        }

        private static string NormalizeTextSize(string size, string defaultValue) {
            if (String.IsNullOrWhiteSpace(size)) return defaultValue;

            string normalized = size.Trim().ToLowerInvariant();
            return normalized switch {
                TextSizeIds.Auto => TextSizeIds.Auto,
                TextSizeIds.Small => TextSizeIds.Small,
                TextSizeIds.Medium => TextSizeIds.Medium,
                TextSizeIds.Large => TextSizeIds.Large,
                _ => defaultValue
            };
        }

        private static string NormalizeBubbleWidth(string width) {
            if (String.IsNullOrWhiteSpace(width)) return BubbleWidthIds.Medium;

            string normalized = width.Trim().ToLowerInvariant();
            return normalized switch {
                BubbleWidthIds.Narrow => BubbleWidthIds.Narrow,
                BubbleWidthIds.Medium => BubbleWidthIds.Medium,
                BubbleWidthIds.Wide => BubbleWidthIds.Wide,
                BubbleWidthIds.Full => BubbleWidthIds.Full,
                _ => BubbleWidthIds.Medium
            };
        }

        private static string NormalizeBubbleDensity(string density) {
            if (String.IsNullOrWhiteSpace(density)) return BubbleDensityIds.Normal;

            string normalized = density.Trim().ToLowerInvariant();
            return normalized switch {
                BubbleDensityIds.Compact => BubbleDensityIds.Compact,
                BubbleDensityIds.Normal => BubbleDensityIds.Normal,
                BubbleDensityIds.Air => BubbleDensityIds.Air,
                BubbleDensityIds.LegacyDefault => BubbleDensityIds.Normal,
                BubbleDensityIds.LegacyRelaxed => BubbleDensityIds.Air,
                _ => BubbleDensityIds.Normal
            };
        }

        private static string NormalizeBubbleStyle(string style) {
            if (String.IsNullOrWhiteSpace(style)) return BubbleStyleIds.Vk;

            string normalized = style.Trim().ToLowerInvariant();
            return normalized switch {
                BubbleStyleIds.Vk => BubbleStyleIds.Vk,
                BubbleStyleIds.Telegram => BubbleStyleIds.Telegram,
                BubbleStyleIds.Minimal => BubbleStyleIds.Minimal,
                BubbleStyleIds.Outline => BubbleStyleIds.Outline,
                BubbleStyleIds.Flat => BubbleStyleIds.Flat,
                BubbleStyleIds.LegacySharp => BubbleStyleIds.Minimal,
                BubbleStyleIds.LegacyRound => BubbleStyleIds.Telegram,
                _ => BubbleStyleIds.Vk
            };
        }

        private static int NormalizePercent(int value, int min, int max) {
            return Math.Clamp(value, min, max);
        }

        private static string NormalizeShortcut(string value, string defaultValue) {
            return String.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
        }

        private static string NormalizeFeatureFlagId(string id) {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentException("Feature flag id is empty.", nameof(id));

            string normalized = id.Trim().ToLowerInvariant();
            if (normalized.Any(c => !Char.IsLetterOrDigit(c) && c != '.' && c != '_' && c != '-')) {
                throw new ArgumentException($"Feature flag id contains unsupported characters: {id}", nameof(id));
            }

            return normalized;
        }

        #endregion
    }
}
