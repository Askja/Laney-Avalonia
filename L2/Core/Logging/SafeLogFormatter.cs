using Serilog.Events;
using Serilog.Formatting;
using System;
using System.Globalization;
using System.IO;

namespace ELOR.Laney.Core.Logging {
    public sealed class SafeLogFormatter : ITextFormatter {
        public void Format(LogEvent logEvent, TextWriter output) {
            string timestamp = logEvent.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture);
            string level = GetLevel(logEvent.Level);
            string message = SensitiveLogSanitizer.Sanitize(logEvent.RenderMessage(CultureInfo.InvariantCulture));

            output.Write('[');
            output.Write(timestamp);
            output.Write(' ');
            output.Write(level);
            output.Write("] ");
            output.Write(message);
            output.WriteLine();

            if (logEvent.Exception != null) {
                output.WriteLine(SensitiveLogSanitizer.Sanitize(logEvent.Exception.ToString()));
            }
        }

        private static string GetLevel(LogEventLevel level) {
            return level switch {
                LogEventLevel.Verbose => "VRB",
                LogEventLevel.Debug => "DBG",
                LogEventLevel.Information => "INF",
                LogEventLevel.Warning => "WRN",
                LogEventLevel.Error => "ERR",
                LogEventLevel.Fatal => "FTL",
                _ => level.ToString().ToUpperInvariant()
            };
        }
    }
}
