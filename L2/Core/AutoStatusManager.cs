using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace ELOR.Laney.Core {
    public sealed class AutoStatusState {
        public string Mode { get; init; }
        public string Title { get; init; }
        public string Reason { get; init; }
        public bool IsActive { get; init; }
    }

    public sealed class AutoStatusSignal {
        public string ActiveWindowTitle { get; init; }
        public string ActiveProcessName { get; init; }

        public string SearchText => $"{ActiveWindowTitle} {ActiveProcessName}";
    }

    public static class AutoStatusManager {
        public static readonly IReadOnlyList<string> Modes = AutoStatusModeIds.All;

        public static AutoStatusState GetCurrent(DateTimeOffset lastUserActivity, DateTime now) {
            if (!Settings.AutoStatusEnabled || Settings.StreamerMode) return Inactive();

            AutoStatusState ruleState = GetRuleState(now);
            if (ruleState.IsActive) return ruleState;

            TimeSpan idle = new DateTimeOffset(now) - lastUserActivity;
            if (Settings.AutoStatusIdleEnabled && idle >= TimeSpan.FromMinutes(Settings.AutoStatusIdleMinutes)) {
                return Build(Settings.AutoStatusIdleMode, $"простой {Math.Floor(idle.TotalMinutes)} мин");
            }

            if (Settings.AutoStatusScheduleEnabled && IsScheduleActive(now, Settings.AutoStatusScheduleStartHour, Settings.AutoStatusScheduleEndHour)) {
                return Build(Settings.AutoStatusScheduleMode, "по расписанию");
            }

            return Build(Settings.AutoStatusMode, "вручную");
        }

        public static string GetTitle(string mode) {
            return AutoStatusModeIds.Normalize(mode) switch {
                AutoStatusModeIds.Busy => "Занят",
                AutoStatusModeIds.Work => "Работаю",
                AutoStatusModeIds.Gaming => "Играю",
                AutoStatusModeIds.Sleep => "Сплю",
                AutoStatusModeIds.DoNotDisturb => "Не трогать",
                _ => "Работаю"
            };
        }

        public static string GetSubtitle(string mode) {
            return AutoStatusModeIds.Normalize(mode) switch {
                AutoStatusModeIds.Busy => "Фокус, отвечаю с задержкой",
                AutoStatusModeIds.Work => "Рабочий режим",
                AutoStatusModeIds.Gaming => "Играю, не дергать по мелочи",
                AutoStatusModeIds.Sleep => "Сплю или оффлайн",
                AutoStatusModeIds.DoNotDisturb => "Жесткий DND",
                _ => "Рабочий режим"
            };
        }

        public static bool IsAutoStatusSettingKey(string key) {
            return key != null && AllSettingKeys.Contains(key);
        }

        public static bool IsScheduleActive(DateTime now, int startHour, int endHour) {
            int currentHour = ((now.Hour % 24) + 24) % 24;
            startHour = ((startHour % 24) + 24) % 24;
            endHour = ((endHour % 24) + 24) % 24;

            if (startHour == endHour) return true;
            if (startHour < endHour) return currentHour >= startHour && currentHour < endHour;
            return currentHour >= startHour || currentHour < endHour;
        }

        private static AutoStatusState Build(string mode, string reason) {
            string normalized = AutoStatusModeIds.Normalize(mode);
            return new AutoStatusState {
                IsActive = true,
                Mode = normalized,
                Title = GetTitle(normalized),
                Reason = reason
            };
        }

        private static AutoStatusState Inactive() {
            return new AutoStatusState {
                IsActive = false,
                Mode = AutoStatusModeIds.Work,
                Title = String.Empty,
                Reason = String.Empty
            };
        }

        private static readonly HashSet<string> AllSettingKeys = new HashSet<string> {
            Settings.AUTO_STATUS_ENABLED,
            Settings.AUTO_STATUS_MODE,
            Settings.AUTO_STATUS_SCHEDULE_ENABLED,
            Settings.AUTO_STATUS_SCHEDULE_START_HOUR,
            Settings.AUTO_STATUS_SCHEDULE_END_HOUR,
            Settings.AUTO_STATUS_SCHEDULE_MODE,
            Settings.AUTO_STATUS_IDLE_ENABLED,
            Settings.AUTO_STATUS_IDLE_MINUTES,
            Settings.AUTO_STATUS_IDLE_MODE,
            Settings.STREAMER_MODE
        };

        private static AutoStatusState GetRuleState(DateTime now) {
            AutoStatusRuleSet ruleSet = AutoStatusRuleStore.GetRuleSet();
            if (ruleSet.Rules.Count == 0) return Inactive();

            AutoStatusSignal signal = null;
            foreach (AutoStatusRule rule in ruleSet.Rules) {
                if (!MatchesRule(rule, now, ref signal)) continue;
                return Build(rule.Mode, BuildRuleReason(rule, signal));
            }

            return Inactive();
        }

        private static bool MatchesRule(AutoStatusRule rule, DateTime now, ref AutoStatusSignal signal) {
            if (rule == null || !rule.Enabled) return false;
            if (rule.Days.Count > 0 && !rule.Days.Contains(now.DayOfWeek)) return false;
            if (rule.StartHour >= 0 && rule.EndHour >= 0 && !IsScheduleActive(now, rule.StartHour, rule.EndHour)) return false;

            if (rule.ContainsAny.Count == 0) return true;

            signal ??= CaptureSignal();
            string searchText = signal.SearchText ?? String.Empty;
            return rule.ContainsAny.Any(token => searchText.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string BuildRuleReason(AutoStatusRule rule, AutoStatusSignal signal) {
            if (!String.IsNullOrWhiteSpace(rule.Reason)) return rule.Reason.Trim();

            string source = CleanSignalTitle(signal);
            return rule.Kind switch {
                AutoStatusRuleKinds.Game => String.IsNullOrWhiteSpace(source) ? "игра" : $"игра: {source}",
                AutoStatusRuleKinds.Music => String.IsNullOrWhiteSpace(source) ? "музыка" : $"музыка: {source}",
                AutoStatusRuleKinds.Calendar => "календарь",
                _ => "ручное правило"
            };
        }

        private static string CleanSignalTitle(AutoStatusSignal signal) {
            string value = !String.IsNullOrWhiteSpace(signal?.ActiveProcessName)
                ? signal.ActiveProcessName
                : signal?.ActiveWindowTitle;
            if (String.IsNullOrWhiteSpace(value)) return String.Empty;

            value = value.Replace("\r", " ").Replace("\n", " ").Trim();
            return value.Length <= 40 ? value : value[..40] + "...";
        }

        private static AutoStatusSignal CaptureSignal() {
            if (!OperatingSystem.IsWindows()) {
                return new AutoStatusSignal {
                    ActiveWindowTitle = String.Empty,
                    ActiveProcessName = String.Empty
                };
            }

            try {
                IntPtr handle = GetForegroundWindow();
                if (handle == IntPtr.Zero) return EmptySignal();

                StringBuilder title = new StringBuilder(256);
                GetWindowText(handle, title, title.Capacity);
                GetWindowThreadProcessId(handle, out uint processId);

                string processName = String.Empty;
                if (processId > 0) {
                    try {
                        using Process process = Process.GetProcessById((int)processId);
                        processName = process.ProcessName;
                    } catch {
                        processName = String.Empty;
                    }
                }

                return new AutoStatusSignal {
                    ActiveWindowTitle = title.ToString(),
                    ActiveProcessName = processName
                };
            } catch {
                return EmptySignal();
            }
        }

        private static AutoStatusSignal EmptySignal() {
            return new AutoStatusSignal {
                ActiveWindowTitle = String.Empty,
                ActiveProcessName = String.Empty
            };
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    }
}
