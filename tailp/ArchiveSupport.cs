// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++ and C#: http://www.viva64.com
using SharpCompress.Archives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TailP
{
    public static class ArchiveSupport
    {
        private static readonly HashSet<string> SupportedExtensions = new HashSet<string>()
        {
            ".zip", ".rar", ".7z"
        };

        private static readonly object Lock = new object();

        public static bool TryGetArchivePath(string path, out string archive, out string file)
        {
            foreach (var extension in SupportedExtensions)
            {
                var split = path.Split(new[] { extension }, 2, StringSplitOptions.None);
                if (split.Length == 2)
                {
                    archive = split[0] + extension;
                    file = split[1];
                    while (Path.IsPathRooted(file))
                    {
                        file = file.Substring(1);
                    }

                    lock (Lock)
                    {
                        return Archives.ContainsKey(archive)
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
            lock (Lock)
            {
                try
                {
                    if (!Archives.ContainsKey(archive))
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
            private string Path { get; }
            public long Size { get; }
            public DateTime CreatedTime { get; }

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
            if (!TryGetArchivePath(path, out var archive, out var file))
            {
                throw new TailPArchiveException($"Invalid archive path {path}");
            }

            lock (Lock)
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
            if (!TryGetArchivePath(path, out var archive, out var file))
            {
                throw new TailPArchiveException($"Invalid archive path {path}");
            }

            lock (Lock)
            {
                if (GetArchiveEntries(archive).TryGetValue(file, out var entry))
                {
                    return new EntryInfo(entry);
                }
                else
                {
                    throw new TailPArchiveException($"File not found {file}");
                }
            }
        }

        // TODO: to dispose after inactivity timeout
        private static readonly Dictionary<string, IArchive> Archives =
            new Dictionary<string, IArchive>(StringComparer.CurrentCultureIgnoreCase);

        // archive path / file path inside archive / entry
        private static readonly Dictionary<string, Dictionary<string, IArchiveEntry>> ArchivesEntries =
            new Dictionary<string, Dictionary<string, IArchiveEntry>>(StringComparer.CurrentCultureIgnoreCase);

        private static IArchive GetArchive(string archive)
        {
            lock (Lock)
            {
                if (!Archives.TryGetValue(archive, out var arch))
                {
                    var fs = new FileStream(archive, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    arch = ArchiveFactory.Open(fs);
                    Archives.Add(archive, arch);
                }

                return arch;
            }
        }

        private static Dictionary<string, IArchiveEntry> GetArchiveEntries(string archive)
        {
            var arch = GetArchive(archive);

            lock (Lock)
            {
                if (!ArchivesEntries.TryGetValue(archive,
                    out var archiveEntries))
                {
                    archiveEntries = arch.Entries
                        .Where(x => !x.IsDirectory)
                        .ToDictionary(x => x.Key);
                    ArchivesEntries.Add(archive, archiveEntries);
                }

                return archiveEntries;
            }
        }

        public static Stream GetFileStream(string path)
        {
            if (!TryGetArchivePath(path, out var archive, out var file))
            {
                throw new TailPArchiveException($"Invalid archive path {path}");
            }

            try
            {
                if (GetArchiveEntries(archive).TryGetValue(file, out var entry))
                {
                    return entry.OpenEntryStream();
                }
                else
                {
                    throw new TailPArchiveException($"File not found {path}");
                }
            }
            catch (InvalidOperationException ex)
            {
                throw new TailPArchiveException(ex.Message, ex);
            }
        }
    }
}