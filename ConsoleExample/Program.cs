using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;

namespace Haukcode.ConsoleExample
{
    public class Program
    {
        private static Logger log = LogManager.GetLogger("TheName");

        public static void Main(string[] args)
        {
            NLog.MappedDiagnosticsContext.Set("SessionId", Guid.NewGuid().ToString("n"));
            NLog.MappedDiagnosticsContext.Set("MessageId", Guid.NewGuid().ToString("n"));

            log.Info("Basic logging");

            using (Log.Context(log, "Main"))
            {
                log.Info("In Main context");

                LogManager.Flush();
                TestFunction("TestArg");
            }

            LogManager.Flush();

            Console.WriteLine("Press ENTER to quit...");
            Console.ReadLine();
        }

        private static void TestFunction(string arg1)
        {
            using (Log.Context(log, "TestFunction"))
            {
                log.Info("Argument {0}", arg1);

                log.Warn("Warning caution!");

                System.Threading.Thread.Sleep(1000 + (new Random()).Next(1000));

                log.Debug("Done...");
            }
        }
    }
}
