using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ELOR.Laney.Core {
    public static class AutoStatusRuleKinds {
        public const string Manual = "manual";
        public const string Game = "game";
        public const string Music = "music";
        public const string Calendar = "calendar";
    }

    public sealed class AutoStatusRule {
        public int LineNumber { get; set; }
        public string Id { get; set; }
        public bool Enabled { get; set; } = true;
        public string Kind { get; set; }
        public string Mode { get; set; }
        public string Reason { get; set; }
        public int StartHour { get; set; } = -1;
        public int EndHour { get; set; } = -1;
        public List<DayOfWeek> Days { get; set; } = new List<DayOfWeek>();
        public List<string> ContainsAny { get; set; } = new List<string>();
        public string SourceLine { get; set; }
    }

    public sealed class AutoStatusRuleSet {
        public IReadOnlyList<AutoStatusRule> Rules { get; init; }
        public IReadOnlyList<string> Errors { get; init; }
    }

    public static class AutoStatusRuleStore {
        private static string DirectoryPath => LocalDataProfile.GetCurrentAccountDirectory("automation");
        private static string RulesPath => Path.Combine(DirectoryPath, "auto-status.txt");

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

        public static AutoStatusRuleSet GetRuleSet() {
            return Parse(GetRulesText());
        }

        public static string GetSummary() {
            AutoStatusRuleSet ruleSet = GetRuleSet();
            int enabled = ruleSet.Rules.Count(r => r.Enabled);
            string baseText = $"Правил: {ruleSet.Rules.Count}; включено: {enabled}.";
            if (ruleSet.Errors.Count == 0) return baseText;
            return $"{baseText} Ошибок: {ruleSet.Errors.Count}. Первая: {ruleSet.Errors[0]}";
        }

        public static AutoStatusRuleSet Parse(string text) {
            List<AutoStatusRule> rules = new List<AutoStatusRule>();
            List<string> errors = new List<string>();

            string[] lines = (text ?? String.Empty).Split(['\r', '\n'], StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++) {
                string line = lines[i].Trim();
                if (String.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal)) continue;

                if (TryParseLine(line, i + 1, out AutoStatusRule rule, out string error)) {
                    rules.Add(rule);
                } else {
                    errors.Add(error);
                }
            }

            return new AutoStatusRuleSet {
                Rules = rules,
                Errors = errors
            };
        }

        private static bool TryParseLine(string line, int lineNumber, out AutoStatusRule rule, out string error) {
            rule = new AutoStatusRule {
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
            rule.Kind = NormalizeKind(Get(map, "kind", "type", "trigger"));
            string rawMode = Get(map, "mode", "status");
            rule.Mode = AutoStatusModeIds.Normalize(rawMode);
            rule.Reason = Get(map, "reason", "text", "label");
            rule.ContainsAny = SplitList(Get(map, "contains", "window", "process", "app"));
            rule.Days = ParseDays(Get(map, "days", "day"));
            ParseHours(Get(map, "hours", "time"), rule);

            if (String.IsNullOrWhiteSpace(rawMode)) {
                error = $"line {lineNumber}: mode обязателен";
                return false;
            }

            if ((rule.Kind == AutoStatusRuleKinds.Game || rule.Kind == AutoStatusRuleKinds.Music) && rule.ContainsAny.Count == 0) {
                error = $"line {lineNumber}: для game/music нужен contains/window/process";
                return false;
            }

            return true;
        }

        private static void ParseHours(string value, AutoStatusRule rule) {
            if (String.IsNullOrWhiteSpace(value)) return;

            string[] parts = value.Split('-', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2) return;

            if (TryParseHour(parts[0], out int start) && TryParseHour(parts[1], out int end)) {
                rule.StartHour = start;
                rule.EndHour = end;
            }
        }

        private static bool TryParseHour(string value, out int hour) {
            hour = 0;
            string normalized = value?.Trim();
            if (String.IsNullOrWhiteSpace(normalized)) return false;
            int colon = normalized.IndexOf(':');
            if (colon > 0) normalized = normalized[..colon];
            return int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out hour)
                && hour >= 0 && hour <= 23;
        }

        private static List<DayOfWeek> ParseDays(string value) {
            List<DayOfWeek> days = new List<DayOfWeek>();
            if (String.IsNullOrWhiteSpace(value)) return days;

            foreach (string raw in value.Split([',', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
                if (TryParseDayRange(raw, days)) continue;
                if (TryParseDay(raw, out DayOfWeek day) && !days.Contains(day)) days.Add(day);
            }

            return days;
        }

        private static bool TryParseDayRange(string value, List<DayOfWeek> days) {
            string[] parts = value.Split('-', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2) return false;
            if (!TryParseDay(parts[0], out DayOfWeek start) || !TryParseDay(parts[1], out DayOfWeek end)) return false;

            int cursor = (int)start;
            for (int guard = 0; guard < 7; guard++) {
                DayOfWeek day = (DayOfWeek)cursor;
                if (!days.Contains(day)) days.Add(day);
                if (day == end) break;
                cursor = (cursor + 1) % 7;
            }

            return true;
        }

        private static bool TryParseDay(string value, out DayOfWeek day) {
            day = DayOfWeek.Monday;
            string normalized = value?.Trim().ToLowerInvariant();
            if (String.IsNullOrWhiteSpace(normalized)) return false;

            switch (normalized) {
                case "mon":
                case "monday":
                case "пн":
                case "понедельник":
                    day = DayOfWeek.Monday;
                    return true;
                case "tue":
                case "tuesday":
                case "вт":
                case "вторник":
                    day = DayOfWeek.Tuesday;
                    return true;
                case "wed":
                case "wednesday":
                case "ср":
                case "среда":
                    day = DayOfWeek.Wednesday;
                    return true;
                case "thu":
                case "thursday":
                case "чт":
                case "четверг":
                    day = DayOfWeek.Thursday;
                    return true;
                case "fri":
                case "friday":
                case "пт":
                case "пятница":
                    day = DayOfWeek.Friday;
                    return true;
                case "sat":
                case "saturday":
                case "сб":
                case "суббота":
                    day = DayOfWeek.Saturday;
                    return true;
                case "sun":
                case "sunday":
                case "вс":
                case "воскресенье":
                    day = DayOfWeek.Sunday;
                    return true;
                default:
                    return false;
            }
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

        private static string Get(Dictionary<string, string> map, params string[] keys) {
            foreach (string key in keys) {
                if (map.TryGetValue(key, out string value)) return value;
            }

            return String.Empty;
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

        private static List<string> SplitList(string value) {
            if (String.IsNullOrWhiteSpace(value)) return new List<string>();
            return value.Split([',', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(v => !String.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(24)
                .ToList();
        }

        private static string NormalizeKind(string kind) {
            string normalized = kind?.Trim().ToLowerInvariant() ?? String.Empty;
            return normalized switch {
                "game" or "gaming" or "игра" or "игры" => AutoStatusRuleKinds.Game,
                "music" or "audio" or "song" or "музыка" => AutoStatusRuleKinds.Music,
                "calendar" or "meeting" or "worktime" or "календарь" or "созвон" or "встреча" => AutoStatusRuleKinds.Calendar,
                "manual" or "force" or "ручной" or "фикс" => AutoStatusRuleKinds.Manual,
                _ => AutoStatusRuleKinds.Manual
            };
        }

        private static void EnsureDefaultRulesFile() {
            Directory.CreateDirectory(DirectoryPath);
            if (File.Exists(RulesPath)) return;
            File.WriteAllText(RulesPath, BuildDefaultRulesText(), Encoding.UTF8);
        }

        private static string BuildDefaultRulesText() {
            return String.Join(Environment.NewLine, new[] {
                "# Laney auto-status rules. Формат: key=value; key=value",
                "# kind: manual/game/music/calendar. mode: busy/work/gaming/sleep/do_not_disturb.",
                "# contains матчится по активному окну и имени процесса; days: mon-fri или пн-пт; hours: 9-18.",
                "# Примеры выключены. Включаешь руками, потому угадайка без спроса — так себе магия.",
                "enabled=false; kind=game; contains=steam,cs2,dota,minecraft; mode=gaming; reason=игра",
                "enabled=false; kind=music; contains=spotify,yandex music,foobar2000; mode=busy; reason=музыка",
                "enabled=false; kind=calendar; days=mon-fri; hours=9-18; mode=work; reason=рабочий календарь",
                "enabled=false; kind=manual; mode=do_not_disturb; reason=ручное правило"
            }) + Environment.NewLine;
        }
    }
}
