// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++ and C#: http://www.viva64.com
using SharpCompress.Archives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TailP
{
    public sealed class ArchiveSupport
    {
        private static HashSet<string> SupportedExtensions = new HashSet<string>()
        {
            ".zip", ".rar", ".7z"
        };

        private static object _lock = new object();

        public static bool TryGetArchivePath(string path, out string archive, out string file)
        {
            foreach(var extension in SupportedExtensions)
            {
                var split = path.Split(new string[] { extension }, 2, StringSplitOptions.None);
                if (split.Length == 2)
                {
                    archive = split[0] + extension;
                    file = split[1];
                    while(Path.IsPathRooted(file))
                    {
                        file = file.Substring(1);
                    }

                    lock (_lock)
                    {
                        return _archives.ContainsKey(archive)
                            || System.IO.File.Exists(archive);
                    }
                }
            }

            archive = string.Empty;
            file = string.Empty;
            return false;
        }

        public static bool IsValidArchive(string archive)
        {
            lock (_lock)
            {
                try
                {
                    if (!_archives.ContainsKey(archive))
                    {
                        GetArchive(archive);
                    }
                    return true;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }
        }

        public class EntryInfo
        {
            public string Path { get; private set; }
            public long Size { get; private set; }
            public DateTime CreatedTime { get; private set; }

            public EntryInfo(IArchiveEntry entry)
            {
                Path = entry.Key;
                Size = entry.Size;
                CreatedTime = entry.CreatedTime.GetValueOrDefault();
            }

            public override int GetHashCode() => Path.GetHashCode();

            public override bool Equals(object obj)
            {
                if (obj == null) return false;

                if (obj is EntryInfo e)
                {
                    return Path.Equals(e.Path, StringComparison.InvariantCultureIgnoreCase);
                }

                if (obj is string s)
                {
                    return Path.Equals(s, StringComparison.InvariantCultureIgnoreCase);
                }

                return false;
            }
        }

        public static IEnumerable<string> EnumerateFiles(string path)
        {
            if (!TryGetArchivePath(path, out string archive, out string file))
            {
                throw new TailPArchiveException(string.Format("Invalid archive path {0}", path));
            }

            lock (_lock)
            {
                return GetArchiveEntries(archive)
                    .Values
                    .Where(x => Utils.IsMatchMask(x.Key, file))
                    .Select(x => x.Key)
                    .ToList();
            }
        }

        /// <summary>
        /// Returns uncompressed file size and creation time
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static EntryInfo GetArchivedFileInfo(string path)
        {
            if (!TryGetArchivePath(path, out string archive, out string file))
            {
                throw new TailPArchiveException(string.Format("Invalid archive path {0}", path));
            }

            lock (_lock)
            {
                if (GetArchiveEntries(archive).TryGetValue(file, out IArchiveEntry entry))
                {
                    return new EntryInfo(entry);
                }
                else
                {
                    throw new TailPArchiveException(string.Format("File not found {0}", file));
                }
            }
        }

        
#pragma warning disable S1135 // Track uses of "TODO" tags
// TODO: to dispose after inactivity timeout
        private static Dictionary<string, IArchive> _archives =
#pragma warning restore S1135 // Track uses of "TODO" tags
            new Dictionary<string, IArchive>(StringComparer.CurrentCultureIgnoreCase);
        // archive path / file path inside archive / entry
        private static Dictionary<string, Dictionary<string, IArchiveEntry>> _archivesEntries = 
            new Dictionary<string, Dictionary<string, IArchiveEntry>>(StringComparer.CurrentCultureIgnoreCase);

        private static IArchive GetArchive(string archive)
        {
            lock(_lock)
            {
                if (!_archives.TryGetValue(archive, out IArchive arch))
                {
                    var fs = new FileStream(archive, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    arch = ArchiveFactory.Open(fs);
                    _archives.Add(archive, arch);
                }

                return arch;
            }
        }

        private static Dictionary<string, IArchiveEntry> GetArchiveEntries(string archive)
        {
            var arch = GetArchive(archive);

            lock (_lock)
            {
                if (!_archivesEntries.TryGetValue(archive,
                    out Dictionary<string, IArchiveEntry> archiveEntries))
                {
                    archiveEntries = arch.Entries
                        .Where(x => !x.IsDirectory)
                        .ToDictionary(x => x.Key);
                    _archivesEntries.Add(archive, archiveEntries);
                }

                return archiveEntries;
            }
        }

        public static Stream GetFileStream(string path)
        {
            if (!TryGetArchivePath(path, out string archive, out string file))
            {
                throw new TailPArchiveException(string.Format("Invalid archive path {0}", path));
            }

            try
            {
                if (GetArchiveEntries(archive).TryGetValue(file, out IArchiveEntry entry))
                {
                    return entry.OpenEntryStream();
                }
                else
                {
                    throw new TailPArchiveException(string.Format("File not found {0}", path));
                }
            }
            catch (InvalidOperationException ex)
            {
                throw new TailPArchiveException(ex.Message, ex);
            }
        }
    }
}
