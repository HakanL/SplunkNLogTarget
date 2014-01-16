using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Net.Http;
using System.Text;

namespace Haukcode.SplunkNLogTarget
{
    [Target("Splunk")]
    public sealed class SplunkTarget : TargetWithLayout
    {
        private HttpClient restClient;
        private string requestUrl;
        private NLog.LayoutRenderers.NdcLayoutRenderer ndcRenderer;
        private static object lockObject = new object();
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

            this.queue = new Queue<string>(MaxQueueItems);
            this.sendEvent = new ManualResetEvent(false);
            this.sendThread = new Thread(new ThreadStart(SendThread));
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
            handler.Credentials = new System.Net.NetworkCredential("x", AccessToken);

            string baseUrl = string.Format("https://{0}", Host);
            this.restClient = new HttpClient(handler);
            this.restClient.Timeout = TimeSpan.FromSeconds(15);
            this.restClient.BaseAddress = new Uri(baseUrl);

            this.requestUrl = string.Format("1/inputs/http?index={0}&sourcetype=json_predefined_timestamp&host={1}&source={2}",
                ProjectId,
                Environment.MachineName,
                Source);

            if (!string.IsNullOrEmpty(TZ))
                this.requestUrl += "&tz=" + TZ;

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

        protected override void CloseTarget()
        {
            this.sendThread.Abort();

            base.CloseTarget();

            this.restClient.Dispose();
        }

        private string SerializeLogEntry(LogEventInfo logEvent)
        {
            var sb = new StringBuilder();

            sb.Append("{");
            sb.Append("\"timestamp\":\"");
            sb.Append(logEvent.TimeStamp.ToString("yyyy-MM-ddTHH:mm:ss.fff"));
            sb.Append("\",");

            sb.Append("\"Level\":\"");
            sb.Append(logEvent.Level.Name);
            sb.Append("\",");

            sb.Append("\"ProcessId\":\"");
            sb.Append(System.Diagnostics.Process.GetCurrentProcess().Id);
            sb.Append("\",");

            sb.Append("\"ThreadId\":\"");
            sb.Append(System.Threading.Thread.CurrentThread.ManagedThreadId.ToString());
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
            if (!extraLayout.EndsWith(","))
                sb.Append(',');

            sb.Append("\"Message\":\"");
            sb.Append(logEvent.FormattedMessage
                .Replace("\"", "\\\"")
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Replace("\n", "\\n"));
            sb.Append("\"");

            sb.AppendLine("}");

            return sb.ToString();
        }

        protected override void Write(NLog.Common.AsyncLogEventInfo logEvent)
        {
            lock (lockObject)
            {
                if (this.queue.Count >= MaxQueueItems)
                    return;

                this.queue.Enqueue(SerializeLogEntry(logEvent.LogEvent));
            }
        }

        protected override void FlushAsync(AsyncContinuation asyncContinuation)
        {
            this.sendEvent.Set();

            base.FlushAsync(asyncContinuation);

            while (this.queue.Count > 0)
                Thread.Sleep(10);
        }
    }
}
