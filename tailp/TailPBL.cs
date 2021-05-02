// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++ and C#: http://www.viva64.com

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace tailp
{
#pragma warning disable S101 // Types should be named in camel case

    public sealed class TailPbl : IDisposable
#pragma warning restore S101 // Types should be named in camel case
    {
        public delegate void NewLineFunc(Line line, int fileIndex);

        public TailPbl(NewLineFunc function)
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

        private int _lastFileIndex;
        public object PrintLock { get; }
        public NewLineFunc NewLineCallback { get; }

        private readonly object _lastFileLock = new object();
        private File _lastFile;

        public void SetLastFile(File value)
        {
            lock (_lastFileLock)
            {
                _lastFile = value;
            }
        }

        public long TotalProcessed =>
            _files
                .Where(x => x.Value.FileType != FileTypes.Archive)
                .Sum(x => x.Value.LastPos);

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

        // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
        private static void AdjustAndCheckIndex(ref int index, int lastIndex, string arg)
        {
            if (index == lastIndex)
            {
                throw new TailPArgsException($"Invalid arg {arg}");
            }

            ++index;
        }

        public void ParseArgs(string[] args)
        {
            if (args is null || args.Length < 1)
            {
                throw new TailPArgsException("Invalid args");
            }

            var lastIndex = args.Length - 1;
            var files = new List<string>();
            for (var i = 0; i != args.Length; ++i)
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
                        Configs.Follow = true;
                        break;

                    case "-n":
                    case "--lines":
                        AdjustAndCheckIndex(ref i, lastIndex, arg);
                        ParseNumLines(args[i]);
                        break;

                    case "-q":
                    case "--quiet":
                    case "--silent":
                        Configs.ShowFile = false;
                        break;

                    case "-v":
                    case "--verbose":
                        Configs.ShowFile = true;
                        break;

                    case "-nr":
                    case "--non-recursive":
                        Configs.Recursive = false;
                        break;

                    case "-l":
                    case "--logical-lines":
                        AdjustAndCheckIndex(ref i, lastIndex, arg);
                        Configs.LogicalLineMarker = args[i];
                        break;

                    case "-N":
                    case "--line-number":
                        Configs.ShowLineNumber = true;
                        break;

                    case "-R":
                    case "--regex":
                        Configs.Regex = true;
                        break;

                    case "-S":
                    case "--show":
                        AdjustAndCheckIndex(ref i, lastIndex, arg);
                        Configs.FiltersShow.Add(args[i]);
                        break;

                    case "-H":
                    case "--hide":
                        AdjustAndCheckIndex(ref i, lastIndex, arg);
                        Configs.FiltersHide.Add(args[i]);
                        break;

                    case "-L":
                    case "--highlight":
                        AdjustAndCheckIndex(ref i, lastIndex, arg);
                        Configs.FiltersHighlight.Add(args[i]);
                        break;

                    case "-o":
                    case "--comparison-option":
                        AdjustAndCheckIndex(ref i, lastIndex, arg);
                        ParseComparisonOption(args[i]);
                        break;

                    case "-a":
                    case "--all":
                        Configs.AllFilters = true;
                        break;

                    case "-t":
                    case "--truncate":
                        Configs.Truncate = true;
                        break;

                    case "-A":
                    case "--after-context":
                        AdjustAndCheckIndex(ref i, lastIndex, arg);
                        Configs.ContextAfter = ParseAndGetContextNumber(args[i]);
                        break;

                    case "-B":
                    case "--before-context":
                        AdjustAndCheckIndex(ref i, lastIndex, arg);
                        Configs.ContextBefore = ParseAndGetContextNumber(args[i]);
                        break;

                    case "-C":
                    case "--context":
                        AdjustAndCheckIndex(ref i, lastIndex, arg);
                        Configs.ContextAfter =
                            Configs.ContextBefore = ParseAndGetContextNumber(args[i]);
                        break;

                    default:
                        files.Add(args[i]);
                        break;
                }
            }
            if (Console.IsInputRedirected)
            {
                files.Add(Constants.CONSOLE_FILENAME);
            }

#pragma warning disable RCS1080 // Use 'Count/Length' property instead of 'Any' method.
            if (files.Any())
#pragma warning restore RCS1080 // Use 'Count/Length' property instead of 'Any' method.
            {
                files.ForEach(AddFile);
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
                UpdateStatus($"Monitoring {actualCount} files...");
            }

            _lastForceDetect = DateTime.UtcNow;
        }

        public void StartProcess()
        {
            Tick();

            Configs.StartLocation = 0; // monitor new files from beginning

            if (Configs.Follow)
            {
                new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    while (true)
                    {
                        _processEvent.WaitOne(Constants.FORCE_DETECT_PERIOD);
                        Tick();
                    }
                    // ReSharper disable once FunctionNeverReturns
                }).Start();
            }
            else
            {
                // force to process archived files if any
                _lastForceDetect = DateTime.MinValue;
                Tick();
            }
        }

#pragma warning disable CA2000 // Dispose objects before losing scope
        private void Tick()
        {
            ForceDetect();

            // process files, giving priority to pushed one
            while (_pushFilesToBeProcess.Any() || _pollFilesToBeProcess.Any())
            {
                // because pushed queue may growing continuously, we can never exit
                for (var i = 0;
                         i != Constants.MAX_PUSH_PROCESS_COUNT && _pushFilesToBeProcess.Any();
                         ++i)
                {
                    if (_pushFilesToBeProcess.TryDequeue(out var pushFile)
                        && pushFile.FileType != FileTypes.Archive)
                    {
                        pushFile.Process();
                    }
                }

                if (_pollFilesToBeProcess.TryDequeue(out var pollFile)
                    && pollFile.FileType != FileTypes.Archive)
                {
                    pollFile.Process();
                }
            }
        }
#pragma warning restore CA2000 // Dispose objects before losing scope

        private void Created(object sender, FilesMonitorEventArgs e)
        {
            try
            {
                if (!_files.TryGetValue(e.File, out var file))
                {
                    file = new File(e.File, this, ++_lastFileIndex);
                    _files.TryAdd(e.File, file);

                    if (!Configs.IsShowFileDefined && _files.Count > 1)
                    {
                        Configs.ShowFile = true;
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
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                NewLineCallback(GetErrorLine(ex.Message), 0);
            }
        }

        private void Changed(object sender, FilesMonitorEventArgs e) => Created(this, e);

        private void Deleted(object sender, FilesMonitorEventArgs e) =>
            _files.TryRemove(e.File, out _);

        private static void UpdateStatus(string s) => Console.Title = s;

        private void AddFile(string pathMask)
        {
            FilesMonitor.Add(pathMask, Configs.Follow, this);

            UpdateStatus($"Added {pathMask}");
        }

        private static void ParseStartLocation(string location)
        {
            var loc = location.Trim().ToUpperInvariant();
            if (loc.Length < 2)
            {
                throw new TailPArgsException(
                    $"invalid starting location '{location}'");
            }

            var type = loc.Substring(loc.Length - 1, 1);
            var start = loc[..^1];

            try
            {
                Configs.StartLocationType = (StartLocationTypes)Enum.Parse(
                    typeof(StartLocationTypes), type);
                Configs.StartLocation = long.Parse(start, CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                throw new TailPArgsException(
                    $"invalid starting location '{location}', parse error '{ex.Message}'", ex);
            }
        }

        private static int ParseAndGetContextNumber(string context)
        {
            var num = context.Trim().ToUpperInvariant();

            if (num.Length > 0
                && int.TryParse(num, out var number)
                && number > 0)
            {
                return number;
            }

            throw new TailPArgsException(
                $"invalid context number '{num}'");
        }

        private static void ParseNumLines(string numLines)
        {
            var num = numLines.Trim().ToUpperInvariant();
            if (num.Length < 1)
            {
                throw new TailPArgsException(
                    $"invalid number lines '{num}'");
            }

            Configs.LinesStartFrom =
                num[0] == '+' ?
                    NumLinesStart.Begin :
                    NumLinesStart.End;

            if (int.TryParse(num, out var number))
            {
                Configs.LinesStartNumber = number;
            }
            else
            {
                throw new TailPArgsException(
                    $"invalid number lines '{num}'");
            }
        }

        private static void ParseComparisonOption(string option)
        {
            try
            {
                Configs.ComparisonOptions = (StringComparison)Enum.Parse(
                    typeof(StringComparison), option);
            }
            catch (ArgumentException)
            {
                throw new TailPArgsException(
                    $"Invalid comparison option '{option}'");
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
            if (logicalLine is null) throw new ArgumentNullException(nameof(logicalLine));

            if (logicalLine.IsPrinted)
            {
                return;
            }

            logicalLine.ForEach(x => NewLineCallback(x, fileIndex));
            logicalLine.IsPrinted = true;
        }

        private string _lastPrintedFileName = string.Empty;

        public void PrintFileName(string fileName, bool force)
        {
            if (Configs.ShowFile && (force || _lastPrintedFileName != fileName))
            {
                NewLineCallback(new Line()
                {
                    new Token(Types.NewLine, string.Empty),
                    new Token(Types.FileName, string.Format(CultureInfo.InvariantCulture,
                        Constants.FILENAME_PRINT_FORMAT, Path.GetFullPath(fileName)))
                }, 0);
                _lastPrintedFileName = fileName;
            }
        }

        public static string GetHelp() =>
            GetVersion()
          + Environment.NewLine
          + Properties.Resources.help;

        private static string GetVersion()
        {
            if (Assembly
                .GetExecutingAssembly()
                .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)
                is AssemblyInformationalVersionAttribute[] attr && attr.Any())
            {
                return Constants.HELP_VERSION_HEADER
                       + Environment.NewLine
                       + string.Format(CultureInfo.InvariantCulture,
                           Constants.HELP_VERSION_FORMAT, attr.First().InformationalVersion);
            }

            return string.Format(CultureInfo.InvariantCulture,
                Constants.HELP_VERSION_FORMAT, "Unknown");
        }

        public void Dispose() => _processEvent.Dispose();
    }
}