using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;

namespace Haukcode
{
    public static class Log
    {
        public static LogContext Context(Logger logger, string contextName)
        {
            return new LogContext(logger, contextName);
        }
    }
}
