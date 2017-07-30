﻿// This is an open source non-commercial project. Dear PVS-Studio, please check it.
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
    public sealed class TailPBL : IDisposable
    {
        public delegate void NewLineFunc(Line line, int fileIndex);

        public TailPBL(NewLineFunc function)
        {
            if (function == null) throw new ArgumentNullException("function");

            NewLineCallback = function;
            FiltersShow = new List<string>();
            FiltersHide = new List<string>();
            FiltersHighlight = new List<string>();
            Truncate = false;
            ShowLineNumber = false;
            LogicalLineMarker = string.Empty;
            StartLocation = 0;
            StartLocationType = StartLocationTypes.b;
            ComparisonOptions = StringComparison.InvariantCultureIgnoreCase;
            LinesStartFrom = NumLinesStart.begin;
            LinesStartNumber = 0;
            AllFilters = false;
            Regex = false;
            Follow = false;
            Recursive = true;
            ContextBefore = -1;
            ContextAfter = -1;
            PrintLock = new object();

            FilesMonitor.Created += Created;
            FilesMonitor.Changed += Changed;
            FilesMonitor.Deleted += Deleted;
        }

        public readonly int MAX_WIDTH = Math.Max(1, Console.BufferWidth);
        private static readonly TimeSpan FORCE_DETECT_PERIOD = TimeSpan.FromMilliseconds(500);
        private static readonly int MAX_PUSH_PROCESS_COUNT = 1000;
        private static readonly string FILENAME_PRINT_FORMAT = @"==> {0} <==";
        private static readonly string HELP_VERSION_HEADER = @"VERSION";
        private static readonly string HELP_VERSION_FORMAT = @"    {0} {1} / {2}";
        private static readonly string CONTEXT_LINE_DELIMITER = @"--";

        private AutoResetEvent _processEvent = new AutoResetEvent(false);
        private ConcurrentQueue<File> _pollFilesToBeProcess = new ConcurrentQueue<File>();
        private ConcurrentQueue<File> _pushFilesToBeProcess = new ConcurrentQueue<File>();
        // hashset<File> should be used here, but where is no way to get fast a element from hashset
        private ConcurrentDictionary<string, File> _files = new ConcurrentDictionary<string, File>();
        private int _lastFileIndex = 0;
        public object PrintLock { get; private set; }
        public NewLineFunc NewLineCallback { get; private set; }
        public List<string> FiltersShow { get; private set; }
        public List<string> FiltersHide { get; private set; }
        public List<string> FiltersHighlight { get; private set; }
        public bool Truncate { get; private set; }
        public bool ShowLineNumber { get; private set; }
        public string LogicalLineMarker { get; private set; }
        public long StartLocation { get; private set; }
        public StartLocationTypes StartLocationType { get; private set; }
        public StringComparison ComparisonOptions { get; private set; }
        public bool AllFilters { get; private set; }
        public bool Regex { get; private set; }
        public bool Follow { get; private set; }
        public NumLinesStart LinesStartFrom { get; private set; }
        public int LinesStartNumber { get; private set; }
        public int ContextAfter { get; private set; }
        public int ContextBefore { get; private set; }

        private object _showFileLocker = new object();
        private bool? _showFile = null;
        public bool ShowFile
        {
            get
            {
                lock (_showFileLocker)
                {
                    return _showFile.HasValue && _showFile.Value;
                }
            }
            set
            {
                lock (_showFileLocker)
                {
                    _showFile = value;
                }
            }
        }
        public bool IsShowFileDefined
        {
            get
            {
                lock (_showFileLocker)
                {
                    return _showFile.HasValue;
                }
            }
        }

        private bool _recursive;
        public bool Recursive
        {
            get
            {
                return _recursive;
            }

            private set
            {
                _recursive = value;
                FilesMonitor.Recursive = _recursive;
            }
        }

        private object _lastFileLock = new object();
        private File _lastFile;
        public File LastFile
        {
            get
            {
                File result;
                lock(_lastFileLock)
                {
                    result = _lastFile;
                }
                return result;
            }
            set
            {
                lock(_lastFileLock)
                {
                    _lastFile = value;
                }
            }
        }
        public long LastProcessed
        {
            get
            {
                lock(_lastFileLock)
                {
                    return _lastFile == null ? 0 : _lastFile.LastPos;
                }
            }
        }
        public long TotalProcessed
        {
            get
            {
                return _files
                        .Where(x => x.Value.FileType != FileTypes.Archive)
                        .Sum(x => x.Value.LastPos);
            }
        }
        public long LastFileSize
        {
            get
            {
                lock(_lastFileLock)
                {
                    return _lastFile == null ? 0 : _lastFile.FileSize;
                }
            }
        }
        public long TotalFilesSize
        {
            get
            {
                return _files.Sum(x => x.Value.FileSize);
            }
        }
        public string LastFileName
        {
            get
            {
                lock(_lastFileLock)
                {
                    return _lastFile == null ? string.Empty : _lastFile.FileName;
                }
            }
        }
        public int FilesCount
        {
            get
            {
                return _files.Count;
            }
        }
        public int Pending
        {
            get
            {
                return _pushFilesToBeProcess
                            .Union(_pollFilesToBeProcess)
                            .Distinct()
                            .Count();
            }
        }

        private void AdjustAndCheckIndex(ref int index, int lastIndex, string arg)
        {
            if (index == lastIndex)
            {
                throw new TailPExceptionArgs(string.Format("Invalid arg {0}", arg));
            }

            ++index;
        }

        public void ParseArgs(string[] args)
        {
            if (args.Length < 1)
            {
                throw new TailPExceptionArgs("Invalid args");
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
                        throw new TailPExceptionHelp();
                    case "-c":
                    case "--location":
                        AdjustAndCheckIndex(ref i, lastIndex, arg);
                        ParseStartLocation(args[i]);
                        break;
                    case "-f":
                    case "--follow":
                        Follow = true;
                        break;
                    case "-n":
                    case "--lines":
                        AdjustAndCheckIndex(ref i, lastIndex, arg);
                        ParseNumLines(args[i]);
                        break;
                    case "-q":
                    case "--quiet":
                    case "--silent":
                        ShowFile = false;
                        break;
                    case "-v":
                    case "--verbose":
                        ShowFile = true;
                        break;
                    case "-nr":
                    case "--non-recursive":
                        Recursive = false;
                        break;
                    case "-l":
                    case "--logical-lines":
                        AdjustAndCheckIndex(ref i, lastIndex, arg);
                        LogicalLineMarker = args[i];
                        break;
                    case "-N":
                    case "--line-number":
                        ShowLineNumber = true;
                        break;
                    case "-R":
                    case "--regex":
                        Regex = true;
                        break;
                    case "-S":
                    case "--show":
                        AdjustAndCheckIndex(ref i, lastIndex, arg);
                            FiltersShow.Add(args[i]);
                        break;
                    case "-H":
                    case "--hide":
                        AdjustAndCheckIndex(ref i, lastIndex, arg);
                            FiltersHide.Add(args[i]);
                        break;
                    case "-L":
                    case "--highlight":
                        AdjustAndCheckIndex(ref i, lastIndex, arg);
                            FiltersHighlight.Add(args[i]);
                        break;
                    case "-o":
                    case "--comparison-option":
                        AdjustAndCheckIndex(ref i, lastIndex, arg);
                        ParseComparisonOption(args[i]);
                        break;
                    case "-a":
                    case "--all":
                        AllFilters = true;
                        break;
                    case "-t":
                    case "--truncate":
                        Truncate = true;
                        break;
                    case "-A":
                    case "--after-context":
                        AdjustAndCheckIndex(ref i, lastIndex, arg);
                        ContextAfter = ParseAndGetContextNumber(args[i]);
                        break;
                    case "-B":
                    case "--before-context":
                        AdjustAndCheckIndex(ref i, lastIndex, arg);
                        ContextBefore = ParseAndGetContextNumber(args[i]);
                        break;
                    case "-C":
                    case "--context":
                        AdjustAndCheckIndex(ref i, lastIndex, arg);
                        ContextAfter = ContextBefore = ParseAndGetContextNumber(args[i]);
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
                throw new TailPExceptionArgs("no files to process");
            }
        }

        private DateTime _lastForceDetect = DateTime.MinValue;
        private bool _firstTimeDetect = true;
        private void ForceDetect()
        {
            if (_lastForceDetect.Add(FORCE_DETECT_PERIOD) > DateTime.UtcNow)
            {
                return;
            }

            var count = FilesCount;

            if (_firstTimeDetect)
            {
                UpdateStatus("Searching files...");
                TaskbarProgress.SetState(TaskbarStates.Indeterminate);
                _firstTimeDetect = false;
            }

            FilesMonitor.ForceProcess();

            var actualCount = FilesCount;
            if (actualCount != count)
            {
                UpdateStatus(string.Format("Monitoring {0} files...", actualCount));
            }

            _lastForceDetect = DateTime.UtcNow;
        }

        public void StartProcess()
        {
            Tick(true);

            StartLocation = 0; // monitor new files from beginning

            if (Follow)
            {
                new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    var signalCounter = 0;
                    while (true)
                    {
                        if (_processEvent.WaitOne(FORCE_DETECT_PERIOD))
                        {
                            ++signalCounter;
                        }
                        else
                        {
                            signalCounter = 0;
                        }

                        if (signalCounter > MAX_PUSH_PROCESS_COUNT)
                        {
                            signalCounter = 0;
                        }

                        // skip force detect for push events
                        Tick(signalCounter == 0);
                    }
                }).Start();
            }
            else
            {
                // process archived files if any
                _lastForceDetect = DateTime.MinValue;
                Tick(true);
            }
        }

        private void Tick(bool forceDetect)
        {
            if (forceDetect)
            {
                ForceDetect();
            }

            // process files, giving priority to pushed one
            while (_pushFilesToBeProcess.Any() || _pollFilesToBeProcess.Any())
            {
                File file;
                // because pushed queue may growing continuously, we can never exit
                for (int i = 0;
                         i != MAX_PUSH_PROCESS_COUNT && _pushFilesToBeProcess.Any();
                         ++i)
                {
                    if (_pushFilesToBeProcess.TryDequeue(out file))
                    {
                        if (file.FileType != FileTypes.Archive)
                        {
                            file.ProcessFile();
                        }
                    }
                }

                if (_pollFilesToBeProcess.TryDequeue(out file))
                {
                    if (file.FileType != FileTypes.Archive)
                    {
                        file.ProcessFile();
                    }
                }
            }
        }

        private void Created(object sender, FilesMonitorEventArgs e)
        {
            try
            {
                File file;
                if (!_files.TryGetValue(e.File, out file))
                {
                    file = new File(e.File, this, ++_lastFileIndex,
                                    LinesStartFrom, LinesStartNumber,
                                    ContextAfter, ContextBefore);
                    _files.TryAdd(e.File, file);

                    if (!IsShowFileDefined && _files.Count > 1)
                    {
                        ShowFile = true;
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
            File file;
            _files.TryRemove(e.File, out file);
        }

        private void UpdateStatus(string s)
        {
            Console.Title = s;
        }

        private void AddFile(string pathMask)
        {
            FilesMonitor.Add(pathMask, Follow, this);

            var status = string.Format("Added {0}", pathMask);
            UpdateStatus(status);
        }

        private void ParseStartLocation(string location)
        {
            var loc = location.Trim().ToLower();
            if (loc.Length < 2)
            {
                throw new TailPExceptionArgs(
                    string.Format("invalid starting location '{0}'", location));
            }

            var type = loc.Substring(loc.Length - 1, 1);
            var start = loc.Substring(0, loc.Length - 1);

            try
            {
                StartLocationType = (StartLocationTypes)Enum.Parse(
                    typeof(StartLocationTypes), type);
                StartLocation = long.Parse(start);
            }
            catch (Exception ex)
            {
                throw new TailPExceptionArgs(
                    string.Format("invalid starting location '{0}', parse error '{1}'",
                        location, ex.Message), ex);
            }
        }

        private int ParseAndGetContextNumber(string context)
        {
            var num = context.Trim().ToLower();

            if (num.Length > 0)
            {
                int number;
                if (int.TryParse(num, out number) &&
                    number > 0)
                {
                    return number;
                }
            }

            throw new TailPExceptionArgs(
                string.Format("invalid context number '{0}'", num));
        }

        private void ParseNumLines(string numLines)
        {
            var num = numLines.Trim().ToLower();
            if (num.Length < 1)
            {
                throw new TailPExceptionArgs(
                    string.Format("invalid number lines '{0}'", num));
            }

            LinesStartFrom = num[0] == '+' ? NumLinesStart.begin : NumLinesStart.end;

            int number;
            if (int.TryParse(num, out number))
            {
                LinesStartNumber = number;
            }
            else
            {
                throw new TailPExceptionArgs(
                    string.Format("invalid number lines '{0}'", num));
            }
        }

        private void ParseComparisonOption(string option)
        {
            try
            {
                ComparisonOptions = (StringComparison)Enum.Parse(
                    typeof(StringComparison), option);
            }
            catch (ArgumentException)
            {
                throw new TailPExceptionArgs(
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
                    new Token(Types.LineNumber, CONTEXT_LINE_DELIMITER)
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
            if (ShowFile && (force || _lastPrintedFileName != fileName))
            {
                NewLineCallback(new Line()
                {
                    new Token(Types.NewLine, string.Empty),
                    new Token(Types.FileName, string.Format(
                        FILENAME_PRINT_FORMAT, Path.GetFullPath(fileName)))
                }, 0);
                _lastPrintedFileName = fileName;
            }
        }

        public string GetHelp()
        {
            return GetVersion()
                + Environment.NewLine
                + tailp.Properties.Resources.help;
        }

        private string GetVersion()
        {
            var assembly = Assembly.GetExecutingAssembly().GetName();

            return HELP_VERSION_HEADER
                + Environment.NewLine
                + string.Format(HELP_VERSION_FORMAT, assembly.Name, assembly.Version,
                    tailp.Properties.Resources.BuildDate);
        }

        public void Dispose()
        {
            _processEvent.Dispose();
        }
    }
}
