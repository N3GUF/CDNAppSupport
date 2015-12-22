using Comdata.AppSupport.AppTools;
using Microsoft.Practices.Unity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Comdata.AppSupport.PPOLMorningChecklist
{
    class Program
    {
        static ILog Log = null;
        static ISettings Settings = null;

        static void Main(string[] args)
        {

            try
            {
             /*   var container = new UnityContainer();
                container.RegisterType(typeof(ISettings), typeof(ChecklistSettings));
                container.RegisterType(typeof(ILog), typeof(TextLogger));
                Settings = container.Resolve<ISettings>(@".\config.xml");
                Log = container.Resolve<ILog>(Settings.LogPath); */

                Settings = new ChecklistSettings(@".\config.xml");
                Log = new TextLogger(Settings.LogPath, Settings.LoggingSeverityLevel);
                Log.LogUpdated += Log_LogUpdated;
                PPOLMorningChecklist checklist = new PPOLMorningChecklist(Log, Settings);

                Log.Write("Starting PPOL Morning Checklist...");
                checklist.Execute();
                Log.Write("Finished PPOL Morning Checklist.");
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                if (Log != null)
                {
                    Log.Write("PPOL Morning Checklist has failed.");
                    Utilities.ReportException(ex, Log);
                    Environment.Exit(1);
                }
                else
                {
                    Console.WriteLine("PPOL Morning Checklist has failed.");
                    Utilities.ReportException(ex);
                    Console.WriteLine("Press any key...");
                    Console.Read();
                    Environment.Exit(1);
                }
            }
        }

        static void Log_LogUpdated(object sender, LogUpdatedEventArgs e)
        {
            Console.WriteLine(e.Mwssage);
        }
   }
}
