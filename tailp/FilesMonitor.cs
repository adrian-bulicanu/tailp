// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++ and C#: http://www.viva64.com
using System.Collections.Generic;
using System.Linq;

namespace TailP
{
    /// <summary>
    /// </summary>
    public static class FilesMonitor
    {
        private static object _monitorEntriesLock = new object();
        private static HashSet<FilesMonitorEntry> _monitorEntries = new HashSet<FilesMonitorEntry>();
        public static bool Recursive { get; set; }

#pragma warning disable S3264 // Events should be invoked
        public static event FilesMonitorEntryHandler Created;
        public static event FilesMonitorEntryHandler Deleted;
        public static event FilesMonitorEntryHandler Changed;
#pragma warning restore S3264 // Events should be invoked

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

        public static void Add(string path, bool follow, TailPBL bl)
        {
            var entry = new FilesMonitorEntry(path, Recursive, bl);

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
