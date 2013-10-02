using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;

namespace Haukcode.ConsoleApplication1
{
    public class Program
    {
        private static Logger log = LogManager.GetCurrentClassLogger();

        public static void Main(string[] args)
        {
            log.Info("Basic logging");

            using (Log.Context(log, "Main"))
            {
                log.Info("In context");

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

                System.Threading.Thread.Sleep(1000);

                log.Debug("Done...");
            }
        }
    }
}
