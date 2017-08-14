// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++ and C#: http://www.viva64.com
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TailP
{
    public class FilesMonitorEventArgs : EventArgs
    {
        public string File { get; private set; }
        public object Sender { get; private set; }

        public FilesMonitorEventArgs(object sender, string file)
        {
            Sender = sender;
            File = file;
        }

        public override string ToString() =>
            string.Format("file: {0}, sender: {1}", File, Sender);
    }

    public delegate void FilesMonitorEntryHandler(object sender, FilesMonitorEventArgs e);

    public sealed class FilesMonitorEntry : IDisposable
    {
        public string Folder { get; private set; }
        public string Mask { get; private set; }
        public FileTypes FileType { get; private set; }

        private readonly object _filesLock = new object();
        private readonly HashSet<string> _files =
            new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);

        public event FilesMonitorEntryHandler Created;
        public event FilesMonitorEntryHandler Deleted;
        public event FilesMonitorEntryHandler Changed;

        private readonly object _watcherLock = new object();
        private FileSystemWatcher _watcher = null;
        private readonly TailPBL _bl;

        public FilesMonitorEntry(string path, TailPBL bl)
        {
            if (ArchiveSupport.TryGetArchivePath(path, out string archive, out string file))
            {
                FileType = FileTypes.Archive;
                Folder = archive;
                Mask = file;
            }
            else
            {
                Folder = Path.GetDirectoryName(path);
                Mask = Path.GetFileName(path);
                FileType = IsWildcard ? FileTypes.Wildcard : FileTypes.Regular;
            }

            if (string.IsNullOrEmpty(Folder.Trim()))
            {
                Folder = ".";
            }

            _bl = bl;
        }

        private void ForceProcessRegular() =>
            InternalCreatedOrChanged(this, Path.Combine(Folder, Mask));

        private bool IsExceptionIgnored(Exception ex) =>
            ex is UnauthorizedAccessException ||
            ex is PathTooLongException ||
            ex is System.Security.SecurityException ||
            ex is IOException;

        private readonly HashSet<string> _invalidPathes =
            new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        private readonly object _invalidPathesLock = new object();
        private void PrintErrorOnlyFirstTime(string path, string error)
        {
            bool isNewError;
            lock (_invalidPathesLock)
            {
                isNewError = _invalidPathes.Add(path);
            }
            if (isNewError)
            {
                _bl.NewLineCallback(TailPBL.GetErrorLine(error), 0);
            }
        }

        // the same as Directory.EnumerateFiles, but ignores security errors
        // inspired by https://stackoverflow.com/questions/5098011/directory-enumeratefiles-unauthorizedaccessexception
        private IEnumerable<string> DirectoryEnumerateFiles(string path, string searchPattern, SearchOption searchOption)
        {
            var foundFiles = new List<string>();

            if (searchOption == SearchOption.AllDirectories)
            {
                try
                {
                    Directory.EnumerateDirectories(path)
                        .ToList()
                        .ForEach(x => foundFiles.AddRange(
                            DirectoryEnumerateFiles(x, searchPattern, searchOption)));
                }
                catch (Exception ex)
                {
                    if (IsExceptionIgnored(ex))
                    {
                        PrintErrorOnlyFirstTime(path, ex.Message);
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            try
            {
                foundFiles.AddRange(Directory.EnumerateFiles(path, searchPattern));
            }
            catch (Exception ex)
            {
                if (IsExceptionIgnored(ex))
                {
                    PrintErrorOnlyFirstTime(path, ex.Message);
                }
                else
                {
                    throw;
                }
            }

            return foundFiles;
        }

        private void ForceProcessWildcard()
        {
            var actualFiles = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            lock (_filesLock)
            {
                actualFiles.UnionWith(_files);
            }

            foreach (var f in DirectoryEnumerateFiles(Folder, Mask,
                Configuration.Recursive ?
                    SearchOption.AllDirectories :
                    SearchOption.TopDirectoryOnly))
            {
                actualFiles.Remove(f);
                InternalCreatedOrChanged(this, f);
            }

            // garbage removed files
            foreach (var f in actualFiles)
            {
                InternalRemoved(this, f);
            }
        }

        private void ForceProcessArchive()
        {
            foreach (var f in ArchiveSupport.EnumerateFiles(Path.Combine(Folder, Mask)))
            {
                InternalCreatedOrChanged(this, Path.Combine(Folder, f));
            }
        }

        /// <summary>
        /// check all files and force created event
        /// may throws exceptions, see Directory.EnumerateFiles, ArchiveSupport
        /// </summary>
        public void ForceProcess()
        {
            switch (FileType)
            {
                case FileTypes.Regular:
                    ForceProcessRegular();
                    break;
                case FileTypes.Wildcard:
                    ForceProcessWildcard();
                    break;
                case FileTypes.Archive:
                    ForceProcessArchive();
                    break;
                default:
                    throw new InvalidOperationException(
                        string.Format("Unknown FileType {0}", FileType));
            }
        }

        private bool IsWildcard => Mask.IndexOfAny(new char[] { '?', '*' }) != -1;

        private void InternalCreatedOrChanged(object sender, string file)
        {
            if (!Utils.IsMatchMask(file, Mask))
            {
                return;
            }

            var added = false;
            lock (_filesLock)
            {
                added = _files.Add(file);
            }

            if (added)
            {
                Created?.Invoke(this, new FilesMonitorEventArgs(sender, file));
            }
            else
            {
                Changed?.Invoke(this, new FilesMonitorEventArgs(sender, file));
            }
        }

        private void InternalRemoved(object sender, string file)
        {
            var removed = false;
            lock (_filesLock)
            {
                removed = _files.Remove(file);
            }

            if (removed)
            {
                Deleted?.Invoke(this, new FilesMonitorEventArgs(sender, file));
            }
        }

        public void BeginMonitor()
        {
            if (FileType == FileTypes.Archive)
            {
                return;
            }

            lock (_watcherLock)
            {
                if (_watcher != null)
                {
                    return;
                }

                _watcher = new FileSystemWatcher(Folder, Mask);

                _watcher.Created += (s, e) => InternalCreatedOrChanged(_watcher, e.FullPath);
                _watcher.Changed += (s, e) => InternalCreatedOrChanged(_watcher, e.FullPath);
                _watcher.Deleted += (s, e) => InternalRemoved(_watcher, e.FullPath);
                _watcher.Renamed += (s, e) =>
                {
                    InternalRemoved(_watcher, e.OldFullPath);
                    InternalCreatedOrChanged(_watcher, e.FullPath);
                };

                _watcher.Error += (s, e) =>
                {
                    Task.Delay(Constants.WAIT_ON_ERROR).ContinueWith((t) =>
                    {
                        lock (_watcherLock)
                        {
                            DeleteWatcher();
                            BeginMonitor();
                        }
                    });
                };

                _watcher.IncludeSubdirectories = Configuration.Recursive;
                _watcher.EnableRaisingEvents = true;
            }
        }

        private void DeleteWatcher()
        {
            lock (_watcherLock)
            {
                if (_watcher != null)
                {
                    _watcher.Dispose();
                    _watcher = null;
                }
            }
        }

        public void Dispose()
        {
            DeleteWatcher();
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;

            var f = obj as FilesMonitorEntry;
            if (f == null) return false;

            return Mask.Equals(f.Mask, StringComparison.InvariantCultureIgnoreCase)
                && Folder.Equals(f.Folder, StringComparison.InvariantCultureIgnoreCase);
        }

        public override int GetHashCode() => Mask.GetHashCode() ^ Folder.GetHashCode();
    }
}
