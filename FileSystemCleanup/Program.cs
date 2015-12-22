using Comdata.AppSupport.AppTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Comdata.AppSupport.FileSystemCleanup
{
    class Program
    {
        static ILog Log = null;
        static ISettings Settings = null;

        static void Main(string[] args)
        {
            try
            {
                Settings = new FileSystemCleanupSettings(@".\FileSystemCleanupConfig.xml");
                Log = new TextLogger(Settings.LogPath, Settings.LoggingSeverityLevel);
                FileSystemCleanup cleanup = new FileSystemCleanup(Log, Settings);

                Log.Write("Starting FileSystemCleanup...");
                cleanup.Execute();
                Log.Write("Finished FileSystemCleanup.");
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                if (Log != null)
                {
                    Log.Write("FileSystemCleanup has failed.");
                    Utilities.ReportException(ex, Log);
                    Environment.Exit(1);
                }
                else
                {
                    Console.WriteLine("FileSystemCleanup has failed.");
                    Utilities.ReportException(ex);
                    Console.WriteLine("Press any key...");
                    Console.Read();
                    Environment.Exit(1);
                }
            }
        }
    }
}
