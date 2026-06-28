using ELOR.Laney.Core.Network;
using ELOR.Laney.DataModels;
using ELOR.VKAPILib.Objects;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ELOR.Laney.Core {
    public enum AccountHealthLevel {
        Ok,
        Warning,
        Error
    }

    public sealed class AccountHealthItem {
        public string Name { get; set; }
        public AccountHealthLevel Level { get; set; }
        public string Details { get; set; }
    }

    public sealed class AccountHealthReport {
        public long SessionId { get; set; }
        public string SessionName { get; set; }
        public DateTimeOffset CheckedAtUtc { get; set; }
        public List<AccountHealthItem> Items { get; set; } = new List<AccountHealthItem>();

        public AccountHealthLevel Level {
            get {
                if (Items.Any(i => i.Level == AccountHealthLevel.Error)) return AccountHealthLevel.Error;
                if (Items.Any(i => i.Level == AccountHealthLevel.Warning)) return AccountHealthLevel.Warning;
                return AccountHealthLevel.Ok;
            }
        }

        public string Summary => Level switch {
            AccountHealthLevel.Ok => "Все основные проверки зеленые.",
            AccountHealthLevel.Warning => "Есть предупреждения. Клиент жив, но что-то шатается.",
            _ => "Есть ошибки. Тут уже не косметика."
        };

        public string ToDetailsText() {
            IEnumerable<string> lines = Items.Select(i => $"[{FormatLevel(i.Level)}] {i.Name}: {i.Details}");
            return $"Laney account health-check{Environment.NewLine}"
                + $"Session: {SessionName} ({SessionId}){Environment.NewLine}"
                + $"Checked at: {CheckedAtUtc:O}{Environment.NewLine}"
                + $"Summary: {Summary}{Environment.NewLine}{Environment.NewLine}"
                + String.Join(Environment.NewLine, lines);
        }

        private static string FormatLevel(AccountHealthLevel level) {
            return level switch {
                AccountHealthLevel.Ok => "OK",
                AccountHealthLevel.Warning => "WARN",
                _ => "ERROR"
            };
        }
    }

    public static class AccountHealthChecker {
        public static async Task<AccountHealthReport> CheckAsync(VKSession session) {
            if (session == null) throw new ArgumentNullException(nameof(session));

            AccountHealthReport report = new AccountHealthReport {
                SessionId = session.Id,
                SessionName = session.Name,
                CheckedAtUtc = DateTimeOffset.UtcNow
            };

            report.Items.Add(await CheckNetworkAsync());
            report.Items.Add(await CheckApiProbeAsync(session));
            report.Items.Add(CheckLongPoll(session));
            report.Items.Add(CheckRecentApiPressure());
            report.Items.Add(CheckRecentApiErrors());
            report.Items.Add(CheckSessionLongPolls());
            return report;
        }

        private static async Task<AccountHealthItem> CheckNetworkAsync() {
            if (!FeatureFlags.IsEnabled(FeatureFlags.HealthCheckNetworkProbe)) {
                return new AccountHealthItem {
                    Name = "Network probe",
                    Level = AccountHealthLevel.Warning,
                    Details = "Пропущено локальным feature flag diagnostics.health.network_probe."
                };
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            try {
                using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
                using HttpResponseMessage response = await LNet.GetAsync(new Uri("https://vk.com/favicon.ico"), cts: cts);
                stopwatch.Stop();

                return new AccountHealthItem {
                    Name = "Network probe",
                    Level = response.IsSuccessStatusCode ? AccountHealthLevel.Ok : AccountHealthLevel.Warning,
                    Details = $"vk.com ответил HTTP {(int)response.StatusCode} за {stopwatch.ElapsedMilliseconds} ms."
                };
            } catch (Exception ex) {
                stopwatch.Stop();
                return new AccountHealthItem {
                    Name = "Network probe",
                    Level = AccountHealthLevel.Error,
                    Details = $"{ex.GetType().Name}: {ex.Message} ({stopwatch.ElapsedMilliseconds} ms)."
                };
            }
        }

        private static async Task<AccountHealthItem> CheckApiProbeAsync(VKSession session) {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try {
                long userId = session.UserId != 0 ? session.UserId : Math.Abs(session.Id);
                User user = await session.API.Users.GetAsync(userId, new List<string> { "online" });
                stopwatch.Stop();

                string name = user == null ? "response без user" : user.FullName;
                return new AccountHealthItem {
                    Name = "API auth probe",
                    Level = user == null ? AccountHealthLevel.Warning : AccountHealthLevel.Ok,
                    Details = $"users.get для {userId}: {name}, {stopwatch.ElapsedMilliseconds} ms."
                };
            } catch (APIException ex) {
                stopwatch.Stop();
                return new AccountHealthItem {
                    Name = "API auth probe",
                    Level = IsAuthBreakingApiError(ex.Code) ? AccountHealthLevel.Error : AccountHealthLevel.Warning,
                    Details = $"VK API error {ex.Code}: {ex.Message} ({stopwatch.ElapsedMilliseconds} ms)."
                };
            } catch (Exception ex) {
                stopwatch.Stop();
                return new AccountHealthItem {
                    Name = "API auth probe",
                    Level = AccountHealthLevel.Error,
                    Details = $"{ex.GetType().Name}: {ex.Message} ({stopwatch.ElapsedMilliseconds} ms)."
                };
            }
        }

        private static AccountHealthItem CheckLongPoll(VKSession session) {
            if (session.LongPoll == null) {
                return new AccountHealthItem {
                    Name = "Current LongPoll",
                    Level = AccountHealthLevel.Warning,
                    Details = "LongPoll еще не создан."
                };
            }

            LongPollState state = session.LongPoll.State;
            bool isGood = session.LongPoll.IsRunning && state == LongPollState.Working;
            bool isConnecting = session.LongPoll.IsRunning && (state == LongPollState.Connecting || state == LongPollState.Updating);
            return new AccountHealthItem {
                Name = "Current LongPoll",
                Level = isGood ? AccountHealthLevel.Ok : isConnecting ? AccountHealthLevel.Warning : AccountHealthLevel.Error,
                Details = $"running={session.LongPoll.IsRunning}, state={state}."
            };
        }

        private static AccountHealthItem CheckRecentApiPressure() {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            IReadOnlyList<ApiDebugCallEntry> entries = ApiDebugMonitor.GetSnapshot();
            int last10s = entries.Count(e => now - e.StartedAt <= TimeSpan.FromSeconds(10));
            int last60s = entries.Count(e => now - e.StartedAt <= TimeSpan.FromMinutes(1));

            AccountHealthLevel level = last10s > 35 || last60s > 180 ? AccountHealthLevel.Warning : AccountHealthLevel.Ok;
            return new AccountHealthItem {
                Name = "API pressure",
                Level = level,
                Details = $"{last10s} вызовов за 10s, {last60s} за 60s. Если тут сотни, VK начнет бить по рукам rate-limit'ом."
            };
        }

        private static AccountHealthItem CheckRecentApiErrors() {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            IReadOnlyList<ApiDebugCallEntry> entries = ApiDebugMonitor.GetSnapshot()
                .Where(e => now - e.StartedAt <= TimeSpan.FromMinutes(5))
                .ToList();

            int networkErrors = entries.Count(e => e.ErrorType != null);
            int httpErrors = entries.Count(e => e.StatusCode >= 500 || e.StatusCode == 429);
            AccountHealthLevel level = networkErrors + httpErrors == 0 ? AccountHealthLevel.Ok : AccountHealthLevel.Warning;
            return new AccountHealthItem {
                Name = "Recent API errors",
                Level = level,
                Details = $"{networkErrors} transport errors, {httpErrors} HTTP 429/5xx за последние 5 минут."
            };
        }

        private static AccountHealthItem CheckSessionLongPolls() {
            IReadOnlyList<VKSession> sessions = VKSession.Sessions;
            int total = sessions.Count(s => s.LongPoll != null);
            int running = sessions.Count(s => s.LongPoll?.IsRunning == true);
            int failed = sessions.Count(s => s.LongPoll != null && (s.LongPoll.State == LongPollState.Failed || s.LongPoll.State == LongPollState.NoInternet));

            AccountHealthLevel level = failed == 0 ? AccountHealthLevel.Ok : AccountHealthLevel.Warning;
            return new AccountHealthItem {
                Name = "All session LongPolls",
                Level = level,
                Details = $"created={total}, running={running}, failed/no-internet={failed}, background group limit={Settings.GroupsBackgroundLongPollLimit}."
            };
        }

        private static bool IsAuthBreakingApiError(int code) {
            return code == 5 || code == 15 || code == 17 || code == 18;
        }
    }
}
