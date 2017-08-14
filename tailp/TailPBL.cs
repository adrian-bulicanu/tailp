// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++ and C#: http://www.viva64.com
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace TailP
{
#pragma warning disable S101 // Types should be named in camel case
    public sealed class TailPBL : IDisposable
#pragma warning restore S101 // Types should be named in camel case
    {
        public delegate void NewLineFunc(Line line, int fileIndex);

        public TailPBL(NewLineFunc function)
        {
            NewLineCallback = function ?? throw new ArgumentNullException(nameof(function));

            PrintLock = new object();

            FilesMonitor.Created += Created;
            FilesMonitor.Changed += Changed;
            FilesMonitor.Deleted += Deleted;
        }

        private readonly AutoResetEvent _processEvent = new AutoResetEvent(false);
        private readonly ConcurrentQueue<File> _pollFilesToBeProcess = new ConcurrentQueue<File>();
        private readonly ConcurrentQueue<File> _pushFilesToBeProcess = new ConcurrentQueue<File>();
        // hashset<File> should be used here, but where is no way to get fast a element from hashset
        private readonly ConcurrentDictionary<string, File> _files =
            new ConcurrentDictionary<string, File>(StringComparer.CurrentCultureIgnoreCase);
        private int _lastFileIndex = 0;
        public object PrintLock { get; private set; }
        public NewLineFunc NewLineCallback { get; private set; }

        private readonly object _lastFileLock = new object();
        private File _lastFile;
        public File LastFile
        {
            get
            {
                File result;
                lock (_lastFileLock)
                {
                    result = _lastFile;
                }
                return result;
            }
            set
            {
                lock (_lastFileLock)
                {
                    _lastFile = value;
                }
            }
        }
        public long LastProcessed
        {
            get
            {
                lock (_lastFileLock)
                {
                    return _lastFile == null ? 0 : _lastFile.LastPos;
                }
            }
        }
        public long TotalProcessed =>
            _files
                .Where(x => x.Value.FileType != FileTypes.Archive)
                .Sum(x => x.Value.LastPos);

        public long LastFileSize
        {
            get
            {
                lock (_lastFileLock)
                {
                    return _lastFile == null ? 0 : _lastFile.FileSize;
                }
            }
        }

        public long TotalFilesSize => _files.Sum(x => x.Value.FileSize);

        public string LastFileName
        {
            get
            {
                lock (_lastFileLock)
                {
                    return _lastFile == null ? string.Empty : _lastFile.FileName;
                }
            }
        }

        public int FilesCount => _files.Count;

        public int Pending =>
            _pushFilesToBeProcess
                .Union(_pollFilesToBeProcess)
                .Distinct()
                .Count();

        private void AdjustAndCheckIndex(ref int index, int lastIndex, string arg)
        {
            if (index == lastIndex)
            {
                throw new TailPArgsException(string.Format("Invalid arg {0}", arg));
            }

            ++index;
        }

        public void ParseArgs(string[] args)
        {
            if (args.Length < 1)
            {
                throw new TailPArgsException("Invalid args");
            }

            var lastIndex = args.Length - 1;
            var files = new List<string>();
            for (int i = 0; i != args.Length; ++i)
            {
                var arg = args[i];

                switch (arg)
                {
                    case "-h":
                    case "--help":
                        throw new TailPHelpException();
                    case "-c":
                    case "--location":
                        AdjustAndCheckIndex(ref i, lastIndex, arg);
                        ParseStartLocation(args[i]);
                        break;
                    case "-f":
                    case "--follow":
                        Configuration.Follow = true;
                        break;
                    case "-n":
                    case "--lines":
                        AdjustAndCheckIndex(ref i, lastIndex, arg);
                        ParseNumLines(args[i]);
                        break;
                    case "-q":
                    case "--quiet":
                    case "--silent":
                        Configuration.ShowFile = false;
                        break;
                    case "-v":
                    case "--verbose":
                        Configuration.ShowFile = true;
                        break;
                    case "-nr":
                    case "--non-recursive":
                        Configuration.Recursive = false;
                        break;
                    case "-l":
                    case "--logical-lines":
                        AdjustAndCheckIndex(ref i, lastIndex, arg);
                        Configuration.LogicalLineMarker = args[i];
                        break;
                    case "-N":
                    case "--line-number":
                        Configuration.ShowLineNumber = true;
                        break;
                    case "-R":
                    case "--regex":
                        Configuration.Regex = true;
                        break;
                    case "-S":
                    case "--show":
                        AdjustAndCheckIndex(ref i, lastIndex, arg);
                        Configuration.FiltersShow.Add(args[i]);
                        break;
                    case "-H":
                    case "--hide":
                        AdjustAndCheckIndex(ref i, lastIndex, arg);
                        Configuration.FiltersHide.Add(args[i]);
                        break;
                    case "-L":
                    case "--highlight":
                        AdjustAndCheckIndex(ref i, lastIndex, arg);
                        Configuration.FiltersHighlight.Add(args[i]);
                        break;
                    case "-o":
                    case "--comparison-option":
                        AdjustAndCheckIndex(ref i, lastIndex, arg);
                        ParseComparisonOption(args[i]);
                        break;
                    case "-a":
                    case "--all":
                        Configuration.AllFilters = true;
                        break;
                    case "-t":
                    case "--truncate":
                        Configuration.Truncate = true;
                        break;
                    case "-A":
                    case "--after-context":
                        AdjustAndCheckIndex(ref i, lastIndex, arg);
                        Configuration.ContextAfter = ParseAndGetContextNumber(args[i]);
                        break;
                    case "-B":
                    case "--before-context":
                        AdjustAndCheckIndex(ref i, lastIndex, arg);
                        Configuration.ContextBefore = ParseAndGetContextNumber(args[i]);
                        break;
                    case "-C":
                    case "--context":
                        AdjustAndCheckIndex(ref i, lastIndex, arg);
                        Configuration.ContextAfter =
                            Configuration.ContextBefore = ParseAndGetContextNumber(args[i]);
                        break;
                    default:
                        files.Add(args[i]);
                        break;
                }
            }
            if (files.Any())
            {
                files.ForEach(x => AddFile(x));
            }
            else
            {
                throw new TailPArgsException("no files to process");
            }
        }

        private DateTime _lastForceDetect = DateTime.MinValue;
        private bool _firstTimeDetect = true;
        private void ForceDetect()
        {
            if (_lastForceDetect.Add(Constants.FORCE_DETECT_PERIOD) > DateTime.UtcNow)
            {
                return;
            }

            var count = FilesCount;
            var firstTimeDetect = _firstTimeDetect;

            if (_firstTimeDetect)
            {
                UpdateStatus("Searching files...");
                TaskbarProgress.SetState(TaskbarStates.Indeterminate);
                _firstTimeDetect = false;
            }

            FilesMonitor.ForceProcess();

            var actualCount = FilesCount;
            if (actualCount != count || firstTimeDetect)
            {
                UpdateStatus(string.Format("Monitoring {0} files...", actualCount));
            }

            _lastForceDetect = DateTime.UtcNow;
        }

        public void StartProcess()
        {
            Tick();

            Configuration.StartLocation = 0; // monitor new files from beginning

            if (Configuration.Follow)
            {
                new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    while (true)
                    {
                        _processEvent.WaitOne(Constants.FORCE_DETECT_PERIOD);
                        Tick();
                    }
                }).Start();
            }
            else
            {
                // force to process archived files if any
                _lastForceDetect = DateTime.MinValue;
                Tick();
            }
        }

        private void Tick()
        {
            ForceDetect();

            // process files, giving priority to pushed one
            while (_pushFilesToBeProcess.Any() || _pollFilesToBeProcess.Any())
            {
                // because pushed queue may growing continuously, we can never exit
                for (int i = 0;
                         i != Constants.MAX_PUSH_PROCESS_COUNT && _pushFilesToBeProcess.Any();
                         ++i)
                {
                    if (_pushFilesToBeProcess.TryDequeue(out File pushFile) &&
                        pushFile.FileType != FileTypes.Archive)
                    {
                        pushFile.Process();
                    }
                }

                if (_pollFilesToBeProcess.TryDequeue(out File pollFile) &&
                    pollFile.FileType != FileTypes.Archive)
                {
                    pollFile.Process();
                }
            }
        }

        private void Created(object sender, FilesMonitorEventArgs e)
        {
            try
            {
                if (!_files.TryGetValue(e.File, out File file))
                {
                    file = new File(e.File, this, ++_lastFileIndex);
                    _files.TryAdd(e.File, file);

                    if (!Configuration.IsShowFileDefined && _files.Count > 1)
                    {
                        Configuration.ShowFile = true;
                    }

                    if (file.FileType == FileTypes.Archive)
                    {
                        AddFile(e.File);
                        return;
                    }
                }

                if (e.Sender is FileSystemWatcher)
                {
                    _pushFilesToBeProcess.Enqueue(file);
                    _processEvent.Set();
                }
                else
                {
                    _pollFilesToBeProcess.Enqueue(file);
                }
            }
            catch (Exception ex)
            {
                NewLineCallback(GetErrorLine(ex.Message), 0);
            }
        }

        private void Changed(object sender, FilesMonitorEventArgs e)
        {
            Created(this, e);
        }

        private void Deleted(object sender, FilesMonitorEventArgs e)
        {
            _files.TryRemove(e.File, out File file);
        }

        private void UpdateStatus(string s)
        {
            Console.Title = s;
        }

        private void AddFile(string pathMask)
        {
            FilesMonitor.Add(pathMask, Configuration.Follow, this);

            UpdateStatus(string.Format("Added {0}", pathMask));
        }

        private void ParseStartLocation(string location)
        {
            var loc = location.Trim().ToLower();
            if (loc.Length < 2)
            {
                throw new TailPArgsException(
                    string.Format("invalid starting location '{0}'", location));
            }

            var type = loc.Substring(loc.Length - 1, 1);
            var start = loc.Substring(0, loc.Length - 1);

            try
            {
                Configuration.StartLocationType = (StartLocationTypes)Enum.Parse(
                    typeof(StartLocationTypes), type);
                Configuration.StartLocation = long.Parse(start);
            }
            catch (Exception ex)
            {
                throw new TailPArgsException(
                    string.Format("invalid starting location '{0}', parse error '{1}'",
                        location, ex.Message), ex);
            }
        }

        private int ParseAndGetContextNumber(string context)
        {
            var num = context.Trim().ToLower();

            if (num.Length > 0 &&
                int.TryParse(num, out int number) &&
                number > 0)
            {
                return number;
            }

            throw new TailPArgsException(
                string.Format("invalid context number '{0}'", num));
        }

        private void ParseNumLines(string numLines)
        {
            var num = numLines.Trim().ToLower();
            if (num.Length < 1)
            {
                throw new TailPArgsException(
                    string.Format("invalid number lines '{0}'", num));
            }

            Configuration.LinesStartFrom =
                num[0] == '+' ?
                    NumLinesStart.begin :
                    NumLinesStart.end;

            if (int.TryParse(num, out int number))
            {
                Configuration.LinesStartNumber = number;
            }
            else
            {
                throw new TailPArgsException(
                    string.Format("invalid number lines '{0}'", num));
            }
        }

        private void ParseComparisonOption(string option)
        {
            try
            {
                Configuration.ComparisonOptions = (StringComparison)Enum.Parse(
                    typeof(StringComparison), option);
            }
            catch (ArgumentException)
            {
                throw new TailPArgsException(
                    string.Format("Invalid comparison option '{0}'", option));
            }
        }

        public static Line GetErrorLine(string message)
        {
            message = DateTime.Now + " " + message;
            return new Line
            {
                new Token(Types.NewLine, string.Empty),
                new Token(Types.Error, message),
                new Token(Types.NewLine, string.Empty)
            };
        }

        public static LogicalLine GetContextDelimiter()
        {
            return new LogicalLine
            {
                new Line
                {
                    new Token(Types.LineNumber, Constants.CONTEXT_LINE_DELIMITER)
                }
            };
        }

        public void PrintLogicalLine(LogicalLine logicalLine, int fileIndex)
        {
            if (!logicalLine.IsPrinted)
            {
                logicalLine.ForEach(x => NewLineCallback(x, fileIndex));
                logicalLine.IsPrinted = true;
            }
        }

        private string _lastPrintedFileName = string.Empty;
        public void PrintFileName(string fileName, bool force)
        {
            if (Configuration.ShowFile && (force || _lastPrintedFileName != fileName))
            {
                NewLineCallback(new Line()
                {
                    new Token(Types.NewLine, string.Empty),
                    new Token(Types.FileName, string.Format(
                        Constants.FILENAME_PRINT_FORMAT, Path.GetFullPath(fileName)))
                }, 0);
                _lastPrintedFileName = fileName;
            }
        }

        public string GetHelp() =>
            GetVersion()
          + Environment.NewLine
          + tailp.Properties.Resources.help;

        private string GetVersion()
        {
            var assembly = Assembly.GetExecutingAssembly().GetName();

            return Constants.HELP_VERSION_HEADER
                + Environment.NewLine
                + string.Format(Constants.HELP_VERSION_FORMAT, assembly.Name, assembly.Version,
                    tailp.Properties.Resources.BuildDate);
        }

        public void Dispose() => _processEvent.Dispose();
    }
}
