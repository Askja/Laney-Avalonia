using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;
using System.IO;

namespace ELOR.Laney.Core.Logging {
    public sealed class SafeDebugSink : ILogEventSink {
        private readonly SafeLogFormatter _formatter = new SafeLogFormatter();

        public void Emit(LogEvent logEvent) {
            using StringWriter writer = new StringWriter();
            _formatter.Format(logEvent, writer);
            Debug.Write(writer.ToString());
        }
    }
}
