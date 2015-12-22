using Comdata.AppSupport.AppTools;
using Comdata.AppSupport.AppTools.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Comdata.AppSupport.FileSystemCleanup
{
    class FileSystemCleanup
    {
        private object _lockObj = new object(); 
        private AppTools.ILog _log;
        private ISettings _settings;

        public FileSystemCleanup(AppTools.ILog log, ISettings settings)
        {
            // TODO: Complete member initialization
            this._log = log;
            this._settings = settings;
         }

        internal void Execute()
        {
            foreach (var Folder in _settings.FoldersToCleanup)
            {
                _log.Write("Cleaning up: {0}", Folder.Folder);
                dispatchCleanup(Folder, operation.Delete);
                dispatchCleanup(Folder, operation.Archive);
                //cleanup(Folder, operation.Compress);
//                Zip.Compress(@"C:\Users\dbernhardy\Downloads\Archive\2014\08\2014-08-07"
//                           , @"C:\Users\dbernhardy\Downloads\Archive\2014\08\2014-08-07.zip");
            }
        }

        private void dispatchCleanup(FolderSetting Folder, operation operation)
        {
            var maxDate = DateTime.Today;

            switch (operation)
            {
                case operation.Archive:
                    maxDate = DateTime.Today.AddDays(-1 * Folder.ArchiveAfterDays);
                    _log.Write("Archiving files created on or before {0:MM/dd/yyyy}.", maxDate);
                    break;

                case operation.Compress:
                    maxDate = DateTime.Today.AddDays(-1 * Folder.CompressAfterDays);
                    _log.Write("Compressing files created on or before {0:MM/dd/yyyy}.", maxDate);
                    break;

                case operation.Delete:
                    maxDate = DateTime.Today.AddMonths(-1 * Folder.DeleteAfterMonths);
                    _log.Write("Deleting files created on or before {0:MM/dd/yyyy}.", maxDate);
                    break;
            }

            var di = new DirectoryInfo(Folder.Folder);
            IEnumerable<FileInfo> list;

            if (Folder.IncludeSubfolders)
                list = from f in di.GetFiles("*.*", SearchOption.AllDirectories)
                       where f.LastWriteTime.Date <= maxDate
                       orderby f.LastWriteTime.Date 
                       select f;
            else
                list = from f in di.GetFiles("*.*", SearchOption.TopDirectoryOnly)
                       where f.LastWriteTime.Date <= maxDate
                       orderby f.LastWriteTime.Date 
                       select f;

            var threadLists = split(list, 4);
            Task[] tasks = new Task[4];
            tasks[0] = Task.Factory.StartNew(() => cleanup(tasks[0].Id, Folder, threadLists[0], operation));
            tasks[1] = Task.Factory.StartNew(() => cleanup(tasks[1].Id, Folder, threadLists[1], operation));
            tasks[2] = Task.Factory.StartNew(() => cleanup(tasks[2].Id, Folder, threadLists[2], operation));
            tasks[3] = Task.Factory.StartNew(() => cleanup(tasks[3].Id, Folder, threadLists[3], operation));
            Task.WaitAll(tasks);
        }

        private List<FileInfo>[] split(IEnumerable<FileInfo> list, int threads)
        {
            List<FileInfo>[] threadLists = new List<FileInfo>[threads];
             
            for (int i=0; i < threads; i++)
                threadLists[i] = new List<FileInfo>();

            var thread = 0;

            foreach (var file in list)
            { 
                threadLists[thread].Add(file);

                if (++thread > threads - 1)
                    thread = 0;
            }

            return threadLists;
        }

        private object cleanup(int threadId, FolderSetting Folder, List<FileInfo> list, operation operation)
        {
            object ob = new object();

            try
            {
                switch (operation)
                {
                    case operation.Archive: archive(threadId, Folder, list);
                        break;
                    case operation.Compress: compress(threadId, Folder, list);
                        break;
                    case operation.Delete: delete(threadId, Folder, list);
                        break;
                }
            }
            catch (Exception ex)
            {
                throw new Exception(String.Format("Exception in thread {0}", threadId), ex);
            }

            return ob;
        }

        private void archive(int threadId, FolderSetting Folder, List<FileInfo> list)
        {
            var prevLastWritetime = DateTime.Today;
            var archivePath = "";

            foreach (var File in list)
            {
                if (isExcluded(Folder, File))
                    continue;

                if (File.LastWriteTime != prevLastWritetime)
                {
                    archivePath = getArchivePath(Folder, File.FullName);
                    _log.Write(Severity.Debug, "{0}) Created directory: {1}", threadId, archivePath);
                }

                _log.Write(Severity.Debug,"{0}) Moving {1} to {2}", threadId, File.FullName, archivePath);
                File.MoveTo(Path.Combine(archivePath, File.Name));
            }
        }

        private string getArchivePath(FolderSetting Folder, string filename)
        {
            var archivePath = "";

            if (Folder.ArchivePath.StartsWith(@".\"))
            {
                archivePath = Path.GetDirectoryName(filename);
                archivePath = Path.Combine(archivePath, Folder.ArchivePath.Substring(2));
            }
            else
                if (Folder.ArchivePath.StartsWith(@"..\"))
                {
                    archivePath = Path.GetDirectoryName(filename);
                    archivePath = Directory.GetParent(archivePath).FullName;
                    archivePath = Path.Combine(archivePath, Folder.ArchivePath.Substring(3));
                }
                else
                    archivePath = Folder.ArchivePath;

            var fi = new FileInfo(filename);
            var yyyy = fi.LastWriteTime.Year.ToString();
            var MM = fi.LastWriteTime.ToString("MM");
            var yyyyMMdd = fi.LastWriteTime.ToString("yyyy-MM-dd");
            archivePath = Path.Combine(archivePath, yyyy, MM, yyyyMMdd); 

            lock (_lockObj)
            {
                if (!Directory.Exists(archivePath))
                {
                    Directory.CreateDirectory(archivePath);
                    Directory.SetCreationTime(archivePath, fi.LastWriteTime);
                    Directory.SetLastWriteTime(archivePath, fi.LastWriteTime);
                }
            }

            return archivePath;
        }

        private void compress(int threadId, FolderSetting Folder, List<FileInfo> list)
        {
            throw new NotImplementedException();
        }

        private void delete(int threadId, FolderSetting Folder, List<FileInfo> list)
        {
            foreach (var File in list)
            {
                if (isExcluded(Folder, File))
                    continue;

                _log.Write(Severity.Debug, "{0}) Deleting {1}", threadId, File.FullName);
                File.Delete();
            }
        }

        private bool isExcluded(FolderSetting Folder, FileInfo File)
        {
            var excluded = false;

            foreach (var pattern in Folder.Exclusions)
                if (excluded = Regex.IsMatch(File.Name, pattern))
                {
                    _log.Write("{0} has been excluded.", File.FullName);
                    break;
                }

            return excluded;
        }

        enum operation { Delete, Compress, Archive };
    }
}
