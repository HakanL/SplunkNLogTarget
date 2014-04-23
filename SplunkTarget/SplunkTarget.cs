using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using NLog;
using NLog.Common;
using NLog.Config;
using NLog.Targets;

namespace Haukcode.SplunkNLogTarget
{
    [Target("Splunk")]
    public class SplunkTarget : TargetWithLayout
    {
        private static object lockObject = new object();
        private HttpClient restClient;
        private string requestUrl;
        private NLog.LayoutRenderers.NdcLayoutRenderer ndcRenderer;
        private Queue<string> queue;
        private ManualResetEvent sendEvent;
        private Thread sendThread;

        public SplunkTarget()
        {
            this.MaxQueueItems = 1000;

            this.ndcRenderer = new NLog.LayoutRenderers.NdcLayoutRenderer
            {
                Separator = "/"
            };

            this.queue = new Queue<string>(this.MaxQueueItems);
            this.sendEvent = new ManualResetEvent(false);
            this.sendThread = new Thread(new ThreadStart(this.SendThread));
        }

        [RequiredParameter]
        public string Host { get; set; }

        [RequiredParameter]
        public string ProjectId { get; set; }

        [RequiredParameter]
        public string AccessToken { get; set; }

        public string Source { get; set; }

        public int MaxQueueItems { get; set; }

        [RequiredParameter]
        public string TZ { get; set; }

        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            var handler = new HttpClientHandler();
            handler.Credentials = new System.Net.NetworkCredential("x", this.AccessToken);

            string baseUrl = string.Format(CultureInfo.InvariantCulture, "https://{0}", this.Host);
            this.restClient = new HttpClient(handler);
            this.restClient.Timeout = TimeSpan.FromSeconds(15);
            this.restClient.BaseAddress = new Uri(baseUrl);

            this.requestUrl = string.Format(
                CultureInfo.InvariantCulture,
                "1/inputs/http?index={0}&sourcetype=json_predefined_timestamp&host={1}&source={2}",
                this.ProjectId,
                Environment.MachineName,
                this.Source);

            if (!string.IsNullOrEmpty(this.TZ))
                this.requestUrl += "&tz=" + this.TZ;

            System.Net.ServicePointManager.ServerCertificateValidationCallback +=
                (sender, certificate, chain, sslPolicyErrors) =>
                {
                    if (sslPolicyErrors == System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch)
                    {
                        if (certificate.Subject.StartsWith("CN=*.splunkstorm.com,"))
                            return true;
                    }

                    return sslPolicyErrors == System.Net.Security.SslPolicyErrors.None;
                };

            this.sendThread.Start();
        }

        protected override void CloseTarget()
        {
            this.sendThread.Abort();

            base.CloseTarget();

            this.restClient.Dispose();
        }

        protected override void Write(NLog.Common.AsyncLogEventInfo logEvent)
        {
            lock (lockObject)
            {
                if (this.queue.Count >= this.MaxQueueItems)
                    return;

                this.queue.Enqueue(this.SerializeLogEntry(logEvent.LogEvent));
            }
        }

        protected override void FlushAsync(AsyncContinuation asyncContinuation)
        {
            this.sendEvent.Set();

            base.FlushAsync(asyncContinuation);

            while (this.queue.Count > 0)
                Thread.Sleep(10);
        }

        private static string EscapeMultilineMessage(string input)
        {
            return input
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Replace("\n", "\\n");
        }

        private void SendThread()
        {
            System.Threading.Thread.CurrentThread.IsBackground = true;

            while (true)
            {
                try
                {
                    var logEntries = new List<string>();

                    lock (lockObject)
                    {
                        while (this.queue.Count > 0 && logEntries.Count <= 100)
                            logEntries.Add(this.queue.Dequeue());
                    }

                    if (logEntries.Any())
                    {
                        var sb = new StringBuilder();

                        foreach (var logEntry in logEntries)
                            sb.Append(logEntry);

                        var content = new StringContent(sb.ToString(), Encoding.UTF8, "application/json");
                        var response = this.restClient.PostAsync(this.requestUrl, content).Result;
                        response.EnsureSuccessStatusCode();
                    }
                    else
                    {
                        if (this.sendEvent.WaitOne(30000))
                            this.sendEvent.Reset();
                    }
                }
                catch (ThreadAbortException)
                {
                    // Ignore
                }
                catch (Exception ex)
                {
                    InternalLogger.Error("Exception in SendThread: {0}", ex.Message);
                }
            }
        }

        private string SerializeLogEntry(LogEventInfo logEvent)
        {
            var sb = new StringBuilder();

            sb.Append("{");
            sb.Append("\"timestamp\":\"");
            sb.Append(logEvent.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture));
            sb.Append("\",");

            sb.Append("\"Level\":\"");
            sb.Append(logEvent.Level.Name);
            sb.Append("\",");

            sb.Append("\"ProcessId\":\"");
            sb.Append(System.Diagnostics.Process.GetCurrentProcess().Id);
            sb.Append("\",");

            sb.Append("\"ThreadId\":\"");
            sb.Append(System.Threading.Thread.CurrentThread.ManagedThreadId.ToString(CultureInfo.InvariantCulture));
            sb.Append("\",");

            sb.Append("\"Logger\":\"");
            sb.Append(logEvent.LoggerName);
            sb.Append("\",");

            if (logEvent.Properties.ContainsKey("DurationMS"))
            {
                sb.Append("\"DurationMS\":\"");
                sb.Append(logEvent.Properties["DurationMS"].ToString());
                sb.Append("\",");
            }

            string path = this.ndcRenderer.Render(logEvent);
            if (!string.IsNullOrEmpty(path))
            {
                sb.Append("\"Path\":\"");
                sb.Append(path);
                sb.Append("\",");
            }

            // Add anything extra, must be json-format
            string extraLayout = this.Layout.Render(logEvent).Trim().Replace('\'', '"');
            sb.Append(extraLayout);
            if (!extraLayout.EndsWith(",", StringComparison.Ordinal))
                sb.Append(',');

            if (logEvent.Exception != null)
            {
                sb.Append("\"Exception\":\"");
                sb.Append(EscapeMultilineMessage(logEvent.Exception.ToString()));
                sb.Append("\",");

                sb.Append("\"ExceptionType\":\"");
                sb.Append(logEvent.Exception.GetType().Name);
                sb.Append("\",");

                sb.Append("\"ExceptionMessage\":\"");
                sb.Append(EscapeMultilineMessage(logEvent.Exception.Message));
                sb.Append("\",");

                if (logEvent.Exception.StackTrace != null)
                {
                    sb.Append("\"ExceptionStack\":\"");
                    sb.Append(EscapeMultilineMessage(logEvent.Exception.StackTrace));
                    sb.Append("\",");
                }
            }

            sb.Append("\"Message\":\"");
            sb.Append(EscapeMultilineMessage(logEvent.FormattedMessage));
            sb.Append("\"");

            sb.AppendLine("}");

            return sb.ToString();
        }
    }
}
