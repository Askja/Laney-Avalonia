using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ELOR.Laney.Core {
    public static class AutomationRuleActionIds {
        public const string Mute = "mute";
        public const string Reminder = "remind";
        public const string Todo = "todo";
        public const string Archive = "archive";
        public const string Tag = "tag";
        public const string Download = "download";
    }

    public sealed class AutomationRule {
        public int LineNumber { get; set; }
        public string Id { get; set; }
        public bool Enabled { get; set; } = true;
        public long PeerId { get; set; }
        public long SenderId { get; set; }
        public bool MentionsOnly { get; set; }
        public bool IncludeOutgoing { get; set; }
        public List<string> ContainsAny { get; set; } = new List<string>();
        public string ActionId { get; set; }
        public string Value { get; set; }
        public int DurationMinutes { get; set; }
        public bool? SuppressNotification { get; set; }
        public bool StopProcessing { get; set; } = true;
        public string SourceLine { get; set; }
    }

    public sealed class AutomationRuleSet {
        public IReadOnlyList<AutomationRule> Rules { get; init; }
        public IReadOnlyList<string> Errors { get; init; }
    }

    public static class AutomationRuleStore {
        private static string DirectoryPath => LocalDataProfile.GetCurrentAccountDirectory("automation");
        private static string RulesPath => Path.Combine(DirectoryPath, "rules.txt");

        public static string GetRulesText() {
            EnsureDefaultRulesFile();
            return File.Exists(RulesPath) ? File.ReadAllText(RulesPath, Encoding.UTF8) : String.Empty;
        }

        public static void SetRulesText(string text) {
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(RulesPath, text ?? String.Empty, Encoding.UTF8);
        }

        public static void ResetRulesText() {
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(RulesPath, BuildDefaultRulesText(), Encoding.UTF8);
        }

        public static AutomationRuleSet GetRuleSet() {
            return Parse(GetRulesText());
        }

        public static string GetSummary() {
            AutomationRuleSet ruleSet = GetRuleSet();
            int enabled = ruleSet.Rules.Count(r => r.Enabled);
            string baseText = $"Правил: {ruleSet.Rules.Count}; включено: {enabled}.";
            if (ruleSet.Errors.Count == 0) return baseText;
            return $"{baseText} Ошибок: {ruleSet.Errors.Count}. Первая: {ruleSet.Errors[0]}";
        }

        public static AutomationRuleSet Parse(string text) {
            List<AutomationRule> rules = new List<AutomationRule>();
            List<string> errors = new List<string>();

            string[] lines = (text ?? String.Empty).Split(['\r', '\n'], StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++) {
                string line = lines[i].Trim();
                if (String.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal)) continue;

                if (TryParseLine(line, i + 1, out AutomationRule rule, out string error)) {
                    rules.Add(rule);
                } else {
                    errors.Add(error);
                }
            }

            return new AutomationRuleSet {
                Rules = rules,
                Errors = errors
            };
        }

        private static bool TryParseLine(string line, int lineNumber, out AutomationRule rule, out string error) {
            rule = new AutomationRule {
                LineNumber = lineNumber,
                Id = $"line-{lineNumber}",
                SourceLine = line
            };
            error = null;

            Dictionary<string, string> map = ParseKeyValueLine(line);
            if (map.Count == 0) {
                error = $"line {lineNumber}: нет key=value полей";
                return false;
            }

            rule.Enabled = !TryGetBool(map, "enabled", out bool enabled) || enabled;
            rule.PeerId = ParsePeerId(Get(map, "peer"));
            rule.SenderId = ParsePeerId(Get(map, "sender"));
            rule.MentionsOnly = TryGetBool(map, "mention", out bool mention) && mention;
            rule.IncludeOutgoing = TryGetBool(map, "outgoing", out bool outgoing) && outgoing;
            rule.ContainsAny = SplitList(Get(map, "contains"));
            rule.ActionId = NormalizeAction(Get(map, "action"));
            rule.Value = Get(map, "value");
            rule.DurationMinutes = ParseDurationMinutes(Get(map, "duration"));
            rule.SuppressNotification = TryGetBool(map, "suppress", out bool suppress) ? suppress : null;
            rule.StopProcessing = !TryGetBool(map, "stop", out bool stop) || stop;

            if (String.IsNullOrWhiteSpace(rule.ActionId)) {
                error = $"line {lineNumber}: action должен быть mute/remind/todo/archive/tag/download";
                return false;
            }

            if (rule.ActionId == AutomationRuleActionIds.Tag && String.IsNullOrWhiteSpace(rule.Value)) {
                error = $"line {lineNumber}: tag требует value";
                return false;
            }

            return true;
        }

        private static Dictionary<string, string> ParseKeyValueLine(string line) {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string segment in line.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
                int index = segment.IndexOf('=');
                if (index <= 0) continue;

                string key = segment[..index].Trim();
                string value = segment[(index + 1)..].Trim().Trim('"');
                if (!String.IsNullOrWhiteSpace(key)) result[key] = value;
            }

            return result;
        }

        private static string Get(Dictionary<string, string> map, string key) {
            return map.TryGetValue(key, out string value) ? value : String.Empty;
        }

        private static bool TryGetBool(Dictionary<string, string> map, string key, out bool value) {
            value = false;
            string raw = Get(map, key);
            if (String.IsNullOrWhiteSpace(raw)) return false;

            string normalized = raw.Trim().ToLowerInvariant();
            value = normalized is "1" or "true" or "yes" or "on" or "да" or "вкл";
            if (value) return true;

            if (normalized is "0" or "false" or "no" or "off" or "нет" or "выкл") {
                value = false;
                return true;
            }

            return false;
        }

        private static long ParsePeerId(string value) {
            if (String.IsNullOrWhiteSpace(value) || value.Trim() == "*") return 0;
            if (long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long id)) return id;
            return 0;
        }

        private static List<string> SplitList(string value) {
            if (String.IsNullOrWhiteSpace(value)) return new List<string>();
            return value.Split([',', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(v => !String.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(16)
                .ToList();
        }

        private static string NormalizeAction(string action) {
            string normalized = action?.Trim().ToLowerInvariant() ?? String.Empty;
            return normalized switch {
                "mute" or "quiet" or "silence" => AutomationRuleActionIds.Mute,
                "remind" or "reminder" => AutomationRuleActionIds.Reminder,
                "todo" or "task" => AutomationRuleActionIds.Todo,
                "archive" => AutomationRuleActionIds.Archive,
                "tag" => AutomationRuleActionIds.Tag,
                "download" or "export" => AutomationRuleActionIds.Download,
                _ => String.Empty
            };
        }

        public static int ParseDurationMinutes(string value) {
            if (String.IsNullOrWhiteSpace(value)) return 0;

            string normalized = value.Trim().ToLowerInvariant();
            char unit = normalized[^1];
            string numberPart = Char.IsLetter(unit) ? normalized[..^1] : normalized;
            if (!double.TryParse(numberPart, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) || number <= 0) return 0;

            TimeSpan duration = unit switch {
                'm' => TimeSpan.FromMinutes(number),
                'h' => TimeSpan.FromHours(number),
                'd' => TimeSpan.FromDays(number),
                _ when Char.IsDigit(unit) => TimeSpan.FromMinutes(number),
                _ => TimeSpan.Zero
            };

            return duration <= TimeSpan.Zero ? 0 : Math.Clamp((int)Math.Round(duration.TotalMinutes), 1, 60 * 24 * 365);
        }

        private static void EnsureDefaultRulesFile() {
            Directory.CreateDirectory(DirectoryPath);
            if (File.Exists(RulesPath)) return;
            File.WriteAllText(RulesPath, BuildDefaultRulesText(), Encoding.UTF8);
        }

        private static string BuildDefaultRulesText() {
            return String.Join(Environment.NewLine, new[] {
                "# Laney automation rules. Формат: key=value; key=value",
                "# peer=* или peer=2000000001; sender=123; contains=чек,оплата; mention=true",
                "# action: mute/remind/todo/archive/tag/download. duration: 15m/2h/1d. suppress=true гасит toast.",
                "# Примеры ниже выключены. Убери enabled=false, когда правило нужно.",
                "enabled=false; peer=*; contains=чек,оплата,заказ; action=mute; duration=2h; value=money-noise",
                "enabled=false; peer=*; contains=договор,акт,pdf; action=todo; value=docs",
                "enabled=false; peer=*; contains=фото,скрин,видео; action=download; value=media last 30d",
                "enabled=false; peer=*; contains=срочно,дедлайн; action=tag; value=urgent; suppress=false"
            }) + Environment.NewLine;
        }
    }
}
