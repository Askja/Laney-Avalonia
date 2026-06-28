using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace ELOR.Laney.Core {
    public sealed class AutoReplyRule {
        public int LineNumber { get; set; }
        public string Id { get; set; }
        public bool Enabled { get; set; } = true;
        public long PeerId { get; set; }
        public bool PrivateOnly { get; set; }
        public bool ChatsOnly { get; set; }
        public long SenderId { get; set; }
        public bool MentionsOnly { get; set; }
        public List<string> ContainsAny { get; set; } = new List<string>();
        public int StartHour { get; set; } = -1;
        public int EndHour { get; set; } = -1;
        public int CooldownMinutes { get; set; } = 120;
        public string Text { get; set; }
        public string SourceLine { get; set; }
    }

    public sealed class AutoReplyRuleSet {
        public IReadOnlyList<AutoReplyRule> Rules { get; init; }
        public IReadOnlyList<string> Errors { get; init; }
    }

    public static class AutoReplyRuleStore {
        private static string DirectoryPath => LocalDataProfile.GetCurrentAccountDirectory("automation");
        private static string RulesPath => Path.Combine(DirectoryPath, "auto-replies.txt");
        private static string StatePath => Path.Combine(DirectoryPath, "auto-replies-state.json");

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

        public static AutoReplyRuleSet GetRuleSet() {
            return Parse(GetRulesText());
        }

        public static string GetSummary() {
            AutoReplyRuleSet ruleSet = GetRuleSet();
            int enabled = ruleSet.Rules.Count(r => r.Enabled);
            string baseText = $"Правил: {ruleSet.Rules.Count}; включено: {enabled}.";
            if (ruleSet.Errors.Count == 0) return baseText;
            return $"{baseText} Ошибок: {ruleSet.Errors.Count}. Первая: {ruleSet.Errors[0]}";
        }

        public static bool TryReserveSend(AutoReplyRule rule, long peerId, long senderId, DateTimeOffset now) {
            if (rule == null) return false;

            Dictionary<string, long> state = LoadState();
            string key = $"{rule.Id}:{peerId}:{senderId}";
            if (state.TryGetValue(key, out long lastUnix)) {
                DateTimeOffset last = DateTimeOffset.FromUnixTimeSeconds(lastUnix);
                if (now - last < TimeSpan.FromMinutes(rule.CooldownMinutes)) return false;
            }

            state[key] = now.ToUnixTimeSeconds();
            SaveState(state);
            return true;
        }

        public static AutoReplyRuleSet Parse(string text) {
            List<AutoReplyRule> rules = new List<AutoReplyRule>();
            List<string> errors = new List<string>();

            string[] lines = (text ?? String.Empty).Split(['\r', '\n'], StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++) {
                string line = lines[i].Trim();
                if (String.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal)) continue;

                if (TryParseLine(line, i + 1, out AutoReplyRule rule, out string error)) {
                    rules.Add(rule);
                } else {
                    errors.Add(error);
                }
            }

            return new AutoReplyRuleSet {
                Rules = rules,
                Errors = errors
            };
        }

        private static bool TryParseLine(string line, int lineNumber, out AutoReplyRule rule, out string error) {
            rule = new AutoReplyRule {
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
            ApplyPeerSelector(rule, Get(map, "peer"));
            rule.SenderId = ParseId(Get(map, "sender"));
            rule.MentionsOnly = TryGetBool(map, "mention", out bool mention) && mention;
            rule.ContainsAny = SplitList(Get(map, "contains"));
            rule.CooldownMinutes = Math.Max(1, AutomationRuleStore.ParseDurationMinutes(Get(map, "cooldown")));
            if (rule.CooldownMinutes == 1 && String.IsNullOrWhiteSpace(Get(map, "cooldown"))) rule.CooldownMinutes = 120;
            ParseHours(Get(map, "hours"), rule);
            rule.Text = Get(map, "text");

            if (String.IsNullOrWhiteSpace(rule.Text)) {
                error = $"line {lineNumber}: text обязателен";
                return false;
            }

            return true;
        }

        private static void ApplyPeerSelector(AutoReplyRule rule, string value) {
            string normalized = value?.Trim().ToLowerInvariant();
            if (String.IsNullOrWhiteSpace(normalized) || normalized == "*") return;

            if (normalized is "private" or "user" or "личка") {
                rule.PrivateOnly = true;
                return;
            }

            if (normalized is "chat" or "group_chat" or "чат") {
                rule.ChatsOnly = true;
                return;
            }

            rule.PeerId = ParseId(normalized);
        }

        private static void ParseHours(string value, AutoReplyRule rule) {
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

        private static long ParseId(string value) {
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

        private static Dictionary<string, long> LoadState() {
            try {
                if (!File.Exists(StatePath)) return new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                return JsonSerializer.Deserialize<Dictionary<string, long>>(File.ReadAllText(StatePath, Encoding.UTF8))
                    ?? new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            } catch {
                return new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static void SaveState(Dictionary<string, long> state) {
            Directory.CreateDirectory(DirectoryPath);
            long keepAfter = DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeSeconds();
            Dictionary<string, long> compacted = state
                .Where(kv => kv.Value >= keepAfter)
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
            File.WriteAllText(StatePath, JsonSerializer.Serialize(compacted), Encoding.UTF8);
        }

        private static void EnsureDefaultRulesFile() {
            Directory.CreateDirectory(DirectoryPath);
            if (File.Exists(RulesPath)) return;
            File.WriteAllText(RulesPath, BuildDefaultRulesText(), Encoding.UTF8);
        }

        private static string BuildDefaultRulesText() {
            return String.Join(Environment.NewLine, new[] {
                "# Laney auto-replies. Формат: key=value; key=value",
                "# peer=* / private / chat / 2000000001; sender=123; contains=заказ; mention=true; hours=22-8; cooldown=2h",
                "# text поддерживает {sender}, {chat}, {text}. Примеры выключены — включай руками, без цирка.",
                "enabled=false; peer=private; hours=22-8; cooldown=2h; text=Сейчас оффлайн, отвечу позже.",
                "enabled=false; peer=chat; mention=true; cooldown=30m; text={sender}, вижу упоминание. Вернусь и отвечу.",
                "enabled=false; sender=123456789; contains=прайс,цена; cooldown=1h; text=Кинь детали одним сообщением, я заберу позже."
            }) + Environment.NewLine;
        }
    }
}
