using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;

namespace Haukcode
{
    public static class Log
    {
        public static LogContext Context(Logger logger, string context)
        {
            return new LogContext(logger, context);
        }
    }

    public class LogContext : IDisposable
    {
        private IDisposable ndc;
        private Logger logger;
        private System.Diagnostics.Stopwatch stopWatch;

        public LogContext(Logger logger, string context)
        {
            this.logger = logger;

            this.ndc = NestedDiagnosticsContext.Push(context);
            this.stopWatch = System.Diagnostics.Stopwatch.StartNew();
        }

        public void Dispose()
        {
            this.stopWatch.Stop();

            var logEvent = new LogEventInfo(
                LogLevel.Info,
                this.logger.Name,
                string.Format("Duration {0:N1} ms", this.stopWatch.Elapsed.TotalMilliseconds));

            logEvent.Properties["Duration"] = this.stopWatch.Elapsed.TotalMilliseconds.ToString("G1");
            this.logger.Log(logEvent);

            this.ndc.Dispose();
        }
    }
}
