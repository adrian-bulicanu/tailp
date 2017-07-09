using System.Collections.Generic;
using System.Linq;

namespace TailP
{
    /// <summary>
    /// </summary>
    public class FilesMonitor
    {
        private static object _monitorEntriesLock = new object();
        private static HashSet<FilesMonitorEntry> _monitorEntries = new HashSet<FilesMonitorEntry>();
        public static bool Recursive { get; set; }

        public static event FilesMonitorEntryHandler Created;
        public static event FilesMonitorEntryHandler Deleted;
        public static event FilesMonitorEntryHandler Changed;

        /// <summary>
        /// check all files and force created event
        /// may throws exceptions, see Directory.EnumerateFiles
        /// </summary>
        public static void ForceProcess()
        {
            List<FilesMonitorEntry> entries;
            lock (_monitorEntriesLock)
            {
                entries = _monitorEntries.ToList();
            }
            entries.ForEach(x => x.ForceProcess());
        }

        public static void Add(string path, bool follow)
        {
            var entry = new FilesMonitorEntry(path, Recursive);

            var added = false;
            lock (_monitorEntriesLock)
            {
                added = _monitorEntries.Add(entry);
            }

            if (added)
            {
                entry.Created += Created;
                entry.Deleted += Deleted;
                entry.Changed += Changed;

                if (follow)
                {
                    entry.BeginMonitor();
                }
            }
        }
    }
}
