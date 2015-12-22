using Comdata.AppSupport.AppTools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Comdata.AppSupport.PPOLMorningChecklist
{
    class PPOLMorningChecklist
    {
        public ILog Log { get; set; }
        public ISettings Settings { get; set; }

        public PPOLMorningChecklist (ILog log, ISettings settings)
        {
            this.Log = log;
            this.Settings = settings;
        }

        public void Execute()
        {
            var problemsFound = 0;

            problemsFound += checkCorpBalRptAndLogs();              // Step 1: Check for Current Corp Balance Report and other Daily Logs
            problemsFound += checkForFilesInHold();                 // Step 2: Check for Files in Hold
            problemsFound += checkIncommingCarholder();             // Step 3: Check Incomming Cardholder
            problemsFound += checkIncommingPayroll();               // Step 4: Check Incomming Payroll
            problemsFound += cleanupInprocess();                    // Step 5: Check files in InProcess folder
            problemsFound += checkProcessed();                      // Step 7: Check files in Processed folder
            problemsFound += checkIpmFiles();                       // Step 8: Check for IPM files in \OWS_WORK\Data\Interchange\IPM_Inc
            problemsFound += checkMdsFiles();                       // Step 9: Check for MDS files in \OWS_WORK\Data\Interchange\MDS_Inc

            Process.Start(this.Log.LogPathname);                    // Display the log
        }

        private int checkCorpBalRptAndLogs()
        {
            var problemsFound = 0;
            var files = Settings.FilesToCheck.FindAll(x => x.ChecklistStep == 1);
            var folders = Settings.FoldersToCheck.FindAll(x => x.ChecklistStep == 1);

            this.Log.Write(Severity.Info, "");
            this.Log.Write(Severity.Info, "");
            this.Log.Write(Severity.Info, "Starting Step 1: Checking for Daily Files");

            foreach (PathnameAndSchedule file in files)
            {
                if (File.Exists(file.Pathname))
                    if (File.GetLastWriteTime(file.Pathname).ToShortDateString() == DateTime.Now.ToShortDateString())
                    {
                        this.Log.Write(Severity.Debug, "{0} was created today at: {1}."
                                                     , file.Pathname
                                                     , File.GetLastWriteTime(file.Pathname));

                        var di = new DirectoryInfo(Path.GetDirectoryName(file.Pathname));
                        var fi = di.GetFiles().First(x => x.Name.ToLower() == Path.GetFileName(file.Pathname).ToLower());

                        if (fi.Length == 0)
                        {
                            this.Log.Write(Severity.Warning, "*** PROBLEM: {0} has a 0 byte length. ***"
                                                         , file.Pathname);
                            problemsFound++;
                        }
                        
                        //if (File.ReadAllText(file.Pathname).StartsWith("0 File(s) copied"))
                        //{
                        //    this.Log.Write(Severity.Info, "*** PROBLEM: {0} has 0 File(s) copied. ***"
                        //                                 , file.Pathname);
                        //    problemsFound++;
                        //}

                     }
                    else
                    {
                        this.Log.Write(Severity.Warning, "*** PROBLEM: {0} was created at: {1}. ***"
                                                     , file.Pathname
                                                     , File.GetLastWriteTime(file.Pathname));
                        problemsFound++;
                    }
                else
                { 
                    this.Log.Write(Severity.Warning, "*** PROBLEM: {0} was not created. ***"
                                             , file.Pathname
                                             , File.GetLastWriteTime(file.Pathname));
                    problemsFound++;
                }
            }

            if (problemsFound == 0)
                this.Log.Write("*** Step 1 Complete.  No problems found. ***");

            return problemsFound;
        }

        private int checkForFilesInHold()
        {
            var problemsFound = 0;
            var files = Settings.FilesToCheck.FindAll(x => x.ChecklistStep == 2);
            var folders = Settings.FoldersToCheck.FindAll(x => x.ChecklistStep == 2);

            this.Log.Write(Severity.Info, "");
            this.Log.Write(Severity.Info, "");
            this.Log.Write(Severity.Info, "Starting Step 2: Checking for Files in Hold");

            problemsFound = checkFolderForOldFiles(problemsFound, folders);

            if (problemsFound == 0)
                this.Log.Write("*** Step 2 Complete.  No problems found. ***");

            return problemsFound;
        }

        private int checkIncommingCarholder()
        {
            var problemsFound = 0;
            var files = Settings.FilesToCheck.FindAll(x => x.ChecklistStep == 3);
            var folders = Settings.FoldersToCheck.FindAll(x => x.ChecklistStep == 3);

            this.Log.Write(Severity.Info, "");
            this.Log.Write(Severity.Info, "");
            this.Log.Write(Severity.Info, "Starting Step 3: Checking for Files in Incoming Cardholder");

            problemsFound = checkFolderForOldFiles(problemsFound, folders);

            if (problemsFound == 0)
                this.Log.Write("*** Step 3 Complete.  No problems found. ***");

            return problemsFound;
        }

        private int checkIncommingPayroll()
        {
            var problemsFound = 0;
            var files = Settings.FilesToCheck.FindAll(x => x.ChecklistStep == 4);
            var folders = Settings.FoldersToCheck.FindAll(x => x.ChecklistStep == 4);

            this.Log.Write(Severity.Info, "");
            this.Log.Write(Severity.Info, "");
            this.Log.Write(Severity.Info, "Starting Step 4: Checking for Files in Incoming Payroll");

            problemsFound = checkFolderForOldFiles(problemsFound, folders);

            if (problemsFound == 0)
                this.Log.Write("*** Step 4 Complete.  No problems found. ***");

            return problemsFound;
        }

        /// <summary>
        /// This method will check for files remaining InProcess directory older than one hour. It will back up any files found.
        /// For each file found, this method will check to see if the same file exists in the processed directory. If it does it 
        /// will be deleted from the Inprocess directory. If it finds that the file has not been moved to processed, 
        /// a message will be logged to go determine why the file has not been moved.
        /// </summary>
        /// <returns>The number of problems found</returns>
        private int cleanupInprocess()
        {
            var inProcessDir = this.Settings.CleanupInProcessFolder.InProcessDir;
            var processedDir = this.Settings.CleanupInProcessFolder.ProcessedDir;
            var backupDir = this.Settings.CleanupInProcessFolder.ProcessedBackupDir;
            var hourOfset = this.Settings.CleanupInProcessFolder.CheckFilesOlderThanHours;

            this.Log.Write(Severity.Info, "");
            this.Log.Write(Severity.Info, "");
            this.Log.Write(Severity.Info, "Starting Step 5: Cleaning up: {0}", inProcessDir);
            this.Log.Write(Severity.Debug, "In process Directory: {0}", inProcessDir);
            this.Log.Write(Severity.Debug, "Backup Directory:     {0}", backupDir);
            this.Log.Write(Severity.Debug, "Processed Directory:  {0}", processedDir);
            this.Log.Write(Severity.Info, "Checking for files older than {0} hour(s).", hourOfset);

            var date = DateTime.Now.ToString("_yyyyMMdd");
            var filesFound = 0;
            var filesNotMoved = 0;
            var filesDeleted = 0;
            var dest = "";
            var filename = "";
           
            if (!Directory.Exists(inProcessDir))
                throw new DirectoryNotFoundException(inProcessDir + " doesn't exist.");

            if (!Directory.Exists(processedDir))
                throw new DirectoryNotFoundException(processedDir + " doesn't exist.");

            if (!Directory.Exists(backupDir))
                throw new DirectoryNotFoundException(backupDir + " doesn't exist.");

            foreach (var file in Directory.GetFiles(inProcessDir))
            {
                FileInfo info = new FileInfo(file);

                if (info.LastWriteTime > DateTime.Now.AddHours(-1 * hourOfset))      // Only check files older than 1 hour
                    continue;

                filesFound++;
                filename = Path.GetFileName(file);
                dest = Path.Combine(backupDir, filename);

                if (File.Exists(dest))                                              // Backup File
                    dest = dest + date;  
  
                this.Log.Write(Severity.Debug, "Backing up {0} to {1}.", file, dest);
                File.Copy(file, dest);
                dest = Path.Combine(processedDir, filename);

                if (File.Exists(dest))                                              // Move or Delete File
                {
                    this.Log.Write(Severity.Debug, "Deleting: {0}.", file);
                    File.Delete(file);
                    filesDeleted++;
                }
                else
                {
                    this.Log.Write(Severity.Debug, "Moving {0} to {1}.", file, dest);
                    this.Log.Write(Severity.Warning, "Check into why {0}, created on {1:MM/dd/yyyy} at {1:HH:mm} didn't process normally."
                                                , file, info.LastWriteTime);
                    filesNotMoved++;
                }
            }

            this.Log.Write("");
            this.Log.Write(Severity.Debug, "Files older than {0} hour(s) found: {1}", hourOfset, filesFound);
            this.Log.Write(Severity.Debug, "Files older than {0} hour(s) deleted: {1}", hourOfset, filesDeleted);

            if (filesNotMoved > 0)
                this.Log.Write(Severity.Warning, "*** PROBLEM: Found {0} file(s) older than {1} hour(s) that weren't moved to {2} from {3}. ***"
                              , filesNotMoved, hourOfset, processedDir, inProcessDir);
            else
                this.Log.Write("*** Step 5 Complete.  No problems found. ***");

            return filesNotMoved;
        }

        private int checkProcessed()
        {
            var problemsFound = 0;
            var files = Settings.FilesToCheck.FindAll(x => x.ChecklistStep == 7);
            var folders = Settings.FoldersToCheck.FindAll(x => x.ChecklistStep == 7);

            this.Log.Write(Severity.Info, "");
            this.Log.Write(Severity.Info, "");
            this.Log.Write(Severity.Info, "Starting Step 7: Checking for Current files in processed");

            foreach (PathnameAndSchedule file in files)
            {
                var path = Path.GetDirectoryName(file.Pathname);
                var pattern = Path.GetFileName(file.Pathname);
                this.Log.Write(Severity.Debug, "Searching for {0} in {1}.", pattern, path);
                var di = new DirectoryInfo(path);
                var list = from f in di.GetFiles(pattern, SearchOption.TopDirectoryOnly)
                             where f.LastWriteTime.Date == DateTime.Now.Date
                             select f;

                if (list.Count() == 0)
                {
                    this.Log.Write(Severity.Warning, "*** PROBLEM: Unable to find a {0} created today.", pattern);
                    problemsFound++;
                }
                else
                    this.Log.Write(Severity.Debug, "Found {0} created at {1}", list.First().Name, list.First().LastWriteTime);
            }
            
            if (problemsFound == 0)
                this.Log.Write("*** Step 7 Complete.  No problems found. ***");

            return problemsFound;
        }

        private int checkIpmFiles()
        {
            var problemsFound = 0;
            var files = Settings.FilesToCheck.FindAll(x => x.ChecklistStep == 8);
            var folders = Settings.FoldersToCheck.FindAll(x => x.ChecklistStep == 8);

            this.Log.Write(Severity.Info, "");
            this.Log.Write(Severity.Info, "");
            this.Log.Write(Severity.Info, "Starting Step 8: Checking for Current IPM files");

            problemsFound = checkIpmAndMdsFiles(problemsFound, files);

            if (problemsFound == 0)
                this.Log.Write("*** Step 8 Complete.  No problems found. ***");

            return problemsFound;
        }

        private int checkMdsFiles()
        {
            var problemsFound = 0;
            var files = Settings.FilesToCheck.FindAll(x => x.ChecklistStep == 9);
            var folders = Settings.FoldersToCheck.FindAll(x => x.ChecklistStep == 9);

            this.Log.Write(Severity.Info, "");
            this.Log.Write(Severity.Info, "");
            this.Log.Write(Severity.Info, "Starting Step 9: Checking for Current MDS files");

            problemsFound = checkIpmAndMdsFiles(problemsFound, files);

            if (problemsFound == 0)
                this.Log.Write("*** Step 9 Complete.  No problems found. ***");

            return problemsFound;
        }

        private int checkIpmAndMdsFiles(int problemsFound, List<PathnameAndSchedule> files)
        {
            foreach (PathnameAndSchedule file in files)
            {
                var path = Path.GetDirectoryName(file.Pathname);
                var pattern = Path.GetFileName(file.Pathname);
                var minDate = DateTime.Now.Date.AddDays(-7);
                this.Log.Write(Severity.Debug, "Searching for {0} in {1} created after {2}.", pattern, path, minDate);

                var di = new DirectoryInfo(path);
                var list = from f in di.GetFiles(pattern, SearchOption.TopDirectoryOnly)
                           where f.LastWriteTime.Date >= minDate
                           select f;

                if (list.Count() > 0)
                {
                    foreach (FileInfo fi in list)
                    {
                        this.Log.Write(Severity.Warning, "*** PROBLEM: Found {0} created on {1}.", fi.Name, fi.LastWriteTime);
                        problemsFound++;
                    }
                }

                pattern = "X" + pattern.Substring(1);
                var date = DateTime.Now.Date.AddDays(-1);

                while (date >= minDate)
                {
                    if (date.DayOfWeek == DayOfWeek.Sunday && pattern.StartsWith("XPM"))
                    {                                   // we don't receive IPM files on Sunday's
                        date = date.AddDays(-1);
                        continue;
                    }

                    this.Log.Write(Severity.Debug, "Searching for {0} in {1} created on {2}.", pattern, path, date);
                    list = from f in di.GetFiles(pattern, SearchOption.TopDirectoryOnly)
                           where f.LastWriteTime.Date >= date && f.LastWriteTime <= date.AddDays(1)
                           select f;

                    if (list.Count() > 0)
                        this.Log.Write(Severity.Debug, "Found {0} created at {1}", list.First().Name, list.First().LastWriteTime);
                    else
                    {
                        this.Log.Write(Severity.Warning, "*** PROBLEM: Unable to find a {0} created on {1}.", pattern, date);
                        problemsFound++;
                    }

                    date = date.AddDays(-1);
                }
            }
            return problemsFound;
        }

        private int checkFolderForOldFiles(int problemsFound, List<PathnameAndSchedule> folders)
        {
            foreach (PathnameAndSchedule folder in folders)
            {
                if (!Directory.Exists(folder.Pathname))
                {
                    this.Log.Write(Severity.Error, "Directory: {0} not found.", folder.Pathname);
                    continue;
                }

                List<ProblemFile> problems = filterResults(folder);
                problemsFound += problems.Count();

                if (problems.Count() > 0)
                {
                    this.Log.Write(Severity.Warning, "*** PROBLEM: Files found in: {0}", folder.Pathname);

                    foreach (ProblemFile file in problems)
                        this.Log.Write(Severity.Warning, "\t{0:-20}\t{1}\t{2:5} kb\t{3} min. old"
                                                     , file.Filename, file.Timestamp, file.Size / 1024,file.Age);
                }
            }

            return problemsFound;
        }

        private List<ProblemFile> filterResults(PathnameAndSchedule folder)
        {
            List<ProblemFile> problems = new List<ProblemFile>();
            var windowStart = DateTime.Today.AddHours(folder.StartTime.Hour).AddMinutes(folder.StartTime.Minute);
            var windowEnd = DateTime.Today.AddHours(folder.EndTime.Hour).AddMinutes(folder.EndTime.Minute);
            var di = new DirectoryInfo(folder.Pathname);
            var fileInfos = di.GetFiles();
            var age = 0;

            if (folder.IntervalMins > 0)
                this.Log.Write(Severity.Debug, "Checking for files created between {0:MM/dd/yyyy hh:mm} and {0:MM/dd/yyyy hh:mm}.", windowStart, windowEnd);

            foreach (FileInfo fi in fileInfos)
            {
                if (folder.Pathname.ToLower().Contains("cardholder")
                  && fi.Name.ToLower().Contains("template"))
                    continue;

                if (folder.Pathname.ToLower().Contains(@"\hold")
                  && fi.Name.ToLower().Contains("zerodoller"))
                {
                    this.Log.Write(Severity.Info, "Moving {0} to {1}.", fi.Name, @"W:\hold\Zero Files");
                    File.Move(fi.FullName, @"W:\hold\Zero Files" + @"\" + fi.Name);
                    continue;
                }

                if (folder.IntervalMins > 0)
                    if (fi.LastWriteTime < windowStart)
                        continue;

                if (folder.IntervalMins > 0)
                    if (fi.LastWriteTime > windowEnd)
                    continue;

                age = calculateAge(folder, fi.LastWriteTime);

                if (age > folder.IntervalMins)
                    problems.Add(new ProblemFile(fi.Name, fi.LastWriteTime, fi.Length, age));
            }

            return problems;
        }

        private int calculateAge(PathnameAndSchedule folder, DateTime dateTime)
        {
            if (folder.IntervalMins == 0)
                return 99;

            TimeSpan ts = lastScheduledTime(folder).Subtract(dateTime);
            var age = ts.Days * 1440 + ts.Hours * 60 + ts.Minutes;
 
            return age;
        }

        private DateTime lastScheduledTime(PathnameAndSchedule folder)
        {
            var lastRun = DateTime.Today.AddHours(folder.StartTime.Hour);
            lastRun = lastRun.AddMinutes(folder.StartTime.Minute);

            if (DateTime.Now < lastRun)
            {
                lastRun = DateTime.Today.AddDays(-1);
                lastRun = lastRun.AddHours(folder.EndTime.Hour);
                lastRun = lastRun.AddMinutes(folder.EndTime.Minute);
                return lastRun;
            }

            while(lastRun < DateTime.Now)
                lastRun = lastRun.AddMinutes(folder.IntervalMins);

            lastRun = lastRun.AddMinutes(-1*folder.IntervalMins);
            return lastRun;
        }

        class ProblemFile
        {
            public string Filename { get; set; }
            public DateTime Timestamp { get; set; }
            public long Size { get; set; }
            public int Age { get; set; }

            public ProblemFile(string filename, DateTime timestamp, long size, int age)
            {
                this.Filename = filename;
                this.Timestamp = timestamp;
                this.Size = size;
                this.Age = age;
            }
        }
    }
}
