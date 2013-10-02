using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;

namespace Haukcode
{
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

            logEvent.Properties["DurationMS"] = this.stopWatch.Elapsed.TotalMilliseconds.ToString("F1");
            this.logger.Log(logEvent);

            this.ndc.Dispose();
        }
    }
}
