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

                string x = "MultiLine dslfj sdlkfj sdljfksd kjfkls djsd" + "\n" + "And the next line lsdfjkl dsfjkld sjlfkdsjf lksd\n";
                x += "MultiLine dslfj sdlkfj sdljfksd kjfkls djsd" + "\n" + "And the next line lsdfjkl dsfjkld sjlfkdsjf lksd\n";
                x += "MultiLine dslfj sdlkfj sdljfksd kjfkls djsd" + "\n" + "And the next line lsdfjkl dsfjkld sjlfkdsjf lksd\n";
                x += "MultiLine dslfj sdlkfj sdljfksd kjfkls djsd" + "\n" + "And the next line lsdfjkl dsfjkld sjlfkdsjf lksd\n";
                x += "MultiLine dslfj sdlkfj sdljfksd kjfkls djsd" + "\n" + "And the next line lsdfjkl dsfjkld sjlfkdsjf lksd\n";
                x += "MultiLine dslfj sdlkfj sdljfksd kjfkls djsd" + "\n" + "And the next line lsdfjkl dsfjkld sjlfkdsjf lksd\n";
                x += "MultiLine dslfj sdlkfj sdljfksd kjfkls djsd" + "\n" + "And the next line lsdfjkl dsfjkld sjlfkdsjf lksd\n";
                log.Info(x);

                try
                {
                    throw new InvalidOperationException("Not correct operation");
                }
                catch (Exception ex)
                {
                    log.ErrorException("Test of exception", ex);
                }

                try
                {
                    throw new AggregateException(new DivideByZeroException("Divide by 0"));
                }
                catch (Exception ex)
                {
                    log.WarnException("Test of inner exception", ex);
                }

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
