using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ELOR.Laney.Core {
    public static class QuickActionScenarioActionIds {
        public const string DailyDigest = "daily_digest";
        public const string ExtractTasks = "extract_tasks";
        public const string OcrLoadedImages = "ocr_loaded_images";
        public const string QuickActions = "quick_actions";
        public const string DownloadAttachments = "download_attachments";
        public const string LocalQuiet1h = "local_quiet_1h";
        public const string E2E = "e2e";
        public const string ArchiveToggle = "archive_toggle";
        public const string StatusWork = "status_work";
        public const string StatusDoNotDisturb = "status_dnd";
    }

    public sealed class QuickActionScenario {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string Keywords { get; set; }
        public string ActionId { get; set; }
        public bool RequiresChat { get; set; }
    }

    public static class QuickActionStore {
        private static string DirectoryPath => LocalDataProfile.GetCurrentAccountDirectory("quick-actions");
        private static string TodoPath => Path.Combine(DirectoryPath, "todo.md");
        private static string RemindersPath => Path.Combine(DirectoryPath, "reminders.md");
        private static string DailyDigestPath => Path.Combine(DirectoryPath, "daily-digest.md");
        private static string AutoRulesPath => Path.Combine(DirectoryPath, "auto-rules.md");
        private static string TagsPath => Path.Combine(DirectoryPath, "tags.md");
        private static string DownloadRequestsPath => Path.Combine(DirectoryPath, "download-requests.md");
        private static string ScenariosPath => Path.Combine(DirectoryPath, "scenarios.json");
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions {
            WriteIndented = true
        };

        public static async Task AddTodoAsync(long peerId, string text) {
            await AppendLineAsync(TodoPath, $"- [ ] {BuildPrefix(peerId)} {Normalize(text)}");
        }

        public static async Task AddReminderAsync(long peerId, string text, string due = null) {
            string duePrefix = String.IsNullOrWhiteSpace(due) ? String.Empty : $" due:{Normalize(due)}";
            await AppendLineAsync(RemindersPath, $"- {BuildPrefix(peerId)}{duePrefix} {Normalize(text)}");
        }

        public static async Task<IReadOnlyList<string>> ReadReminderLinesAsync() {
            if (!File.Exists(RemindersPath)) return Array.Empty<string>();
            return await File.ReadAllLinesAsync(RemindersPath, Encoding.UTF8);
        }

        public static async Task SaveDailyDigestAsync(string text) {
            Directory.CreateDirectory(DirectoryPath);
            await File.WriteAllTextAsync(DailyDigestPath, text + Environment.NewLine, Encoding.UTF8);
        }

        public static async Task AddAutoRuleHitAsync(long peerId, int cmid, string category, int ttlDays) {
            DateTimeOffset expires = DateTimeOffset.Now.AddDays(ttlDays);
            string line = $"- {BuildPrefix(peerId)} cmid:{cmid} category:{Normalize(category)} ttl:{expires:yyyy-MM-ddTHH:mmzzz}";
            await AppendLineAsync(AutoRulesPath, line);
        }

        public static async Task AddTagAsync(long peerId, int cmid, string tag, string text) {
            string line = $"- {BuildPrefix(peerId)} cmid:{cmid} tag:{Normalize(tag)} {Normalize(text)}";
            await AppendLineAsync(TagsPath, line);
        }

        public static async Task AddDownloadRequestAsync(long peerId, int cmid, string filter, string text) {
            string line = $"- {BuildPrefix(peerId)} cmid:{cmid} filter:{Normalize(filter)} {Normalize(text)}";
            await AppendLineAsync(DownloadRequestsPath, line);
        }

        public static IReadOnlyList<QuickActionScenario> GetScenarios() {
            try {
                EnsureDefaultScenariosFile();
                string json = File.ReadAllText(ScenariosPath, Encoding.UTF8);
                List<QuickActionScenario> scenarios = JsonSerializer.Deserialize<List<QuickActionScenario>>(json, JsonOptions);
                return NormalizeScenarios(scenarios);
            } catch {
                return BuildDefaultScenarios();
            }
        }

        private static async Task AppendLineAsync(string path, string line) {
            Directory.CreateDirectory(DirectoryPath);
            await File.AppendAllTextAsync(path, line + Environment.NewLine, Encoding.UTF8);
        }

        private static void EnsureDefaultScenariosFile() {
            Directory.CreateDirectory(DirectoryPath);
            if (File.Exists(ScenariosPath)) return;
            File.WriteAllText(ScenariosPath, JsonSerializer.Serialize(BuildDefaultScenarios(), JsonOptions), Encoding.UTF8);
        }

        private static IReadOnlyList<QuickActionScenario> NormalizeScenarios(IEnumerable<QuickActionScenario> scenarios) {
            if (scenarios == null) return BuildDefaultScenarios();

            List<QuickActionScenario> normalized = new List<QuickActionScenario>();
            HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (QuickActionScenario scenario in scenarios) {
                if (scenario == null || String.IsNullOrWhiteSpace(scenario.ActionId)) continue;

                string id = String.IsNullOrWhiteSpace(scenario.Id) ? scenario.ActionId : scenario.Id.Trim();
                if (!ids.Add(id)) continue;

                normalized.Add(new QuickActionScenario {
                    Id = id,
                    Title = String.IsNullOrWhiteSpace(scenario.Title) ? id : scenario.Title.Trim(),
                    Subtitle = scenario.Subtitle?.Trim() ?? String.Empty,
                    Keywords = scenario.Keywords?.Trim() ?? String.Empty,
                    ActionId = scenario.ActionId.Trim(),
                    RequiresChat = scenario.RequiresChat
                });
            }

            return normalized.Count == 0 ? BuildDefaultScenarios() : normalized;
        }

        private static List<QuickActionScenario> BuildDefaultScenarios() {
            return new List<QuickActionScenario> {
                new QuickActionScenario {
                    Id = "daily-digest",
                    Title = "Сценарий: сводка дня",
                    Subtitle = "Показать важное, просроченное и кандидаты на ответ",
                    Keywords = "scenario digest inbox сводка день важное ответить",
                    ActionId = QuickActionScenarioActionIds.DailyDigest
                },
                new QuickActionScenario {
                    Id = "extract-tasks",
                    Title = "Сценарий: вытащить задачи",
                    Subtitle = "Найти todo-кандидаты по загруженным сообщениям",
                    Keywords = "scenario todo tasks задачи дела извлечь",
                    ActionId = QuickActionScenarioActionIds.ExtractTasks
                },
                new QuickActionScenario {
                    Id = "ocr-loaded-images",
                    Title = "Сценарий: OCR картинок",
                    Subtitle = "Локально распознать текст в уже загруженных фото",
                    Keywords = "scenario ocr image text фото распознать",
                    ActionId = QuickActionScenarioActionIds.OcrLoadedImages
                },
                new QuickActionScenario {
                    Id = "inbox-zero",
                    Title = "Сценарий: inbox zero",
                    Subtitle = "Открыть quick actions: todo, reminder, snippets, шифрование",
                    Keywords = "scenario inbox zero quick actions todo reminder быстрые",
                    ActionId = QuickActionScenarioActionIds.QuickActions,
                    RequiresChat = true
                },
                new QuickActionScenario {
                    Id = "media-dump",
                    Title = "Сценарий: media dump",
                    Subtitle = "Скачать вложения текущего чата с фильтрами и sidecar JSON",
                    Keywords = "scenario download media attachments скачать вложения архив",
                    ActionId = QuickActionScenarioActionIds.DownloadAttachments,
                    RequiresChat = true
                },
                new QuickActionScenario {
                    Id = "focus-hour",
                    Title = "Сценарий: тихий час",
                    Subtitle = "Заглушить текущий чат локально на 1 час",
                    Keywords = "scenario mute quiet focus тишина заглушить час",
                    ActionId = QuickActionScenarioActionIds.LocalQuiet1h,
                    RequiresChat = true
                },
                new QuickActionScenario {
                    Id = "privacy-hardening",
                    Title = "Сценарий: E2E hardening",
                    Subtitle = "Открыть профиль E2E текущего чата",
                    Keywords = "scenario e2e encrypt crypto ключи приватность",
                    ActionId = QuickActionScenarioActionIds.E2E,
                    RequiresChat = true
                },
                new QuickActionScenario {
                    Id = "archive-noise",
                    Title = "Сценарий: убрать шум",
                    Subtitle = "Переключить локальный архив текущего чата",
                    Keywords = "scenario archive noise hide архив скрыть шум",
                    ActionId = QuickActionScenarioActionIds.ArchiveToggle,
                    RequiresChat = true
                },
                new QuickActionScenario {
                    Id = "status-work",
                    Title = "Сценарий: режим работа",
                    Subtitle = "Включить Laney-only автостатус «Работаю»",
                    Keywords = "scenario status work автостатус работаю",
                    ActionId = QuickActionScenarioActionIds.StatusWork
                },
                new QuickActionScenario {
                    Id = "status-dnd",
                    Title = "Сценарий: не трогать",
                    Subtitle = "Включить Laney-only автостатус DND",
                    Keywords = "scenario status dnd автостатус не трогать",
                    ActionId = QuickActionScenarioActionIds.StatusDoNotDisturb
                }
            };
        }

        private static string BuildPrefix(long peerId) {
            return $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm} peer:{peerId}";
        }

        private static string Normalize(string text) {
            if (String.IsNullOrWhiteSpace(text)) return "(пусто)";
            return text.Replace("\r", " ").Replace("\n", " ").Trim();
        }
    }
}
