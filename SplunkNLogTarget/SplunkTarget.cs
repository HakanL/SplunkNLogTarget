using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace Haukcode.SplunkNLogTarget
{
    [Target("SplunkTarget")]
    public sealed class SplunkTarget : TargetWithLayout
    {
        public SplunkTarget()
        {
        }

        [RequiredParameter]
        public string Host { get; set; }
    }
}
