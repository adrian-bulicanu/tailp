// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++ and C#: http://www.viva64.com

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace tailp
{
    public sealed class File : IDisposable
    {
        private class FileInfoCache // FileInfo accesses file on each method call
        {
            public long Length { get; set; }
            public DateTime CreationTime { get; }

            /// <summary>
            /// See exceptions of FileInfo class
            /// </summary>
            /// <param name="fileInfo"></param>
            public FileInfoCache(FileInfo fileInfo)
            {
                Length = fileInfo.Length;
                CreationTime = fileInfo.CreationTime;
            }

            public FileInfoCache(string archivePath)
            {
                var info = ArchiveSupport.GetArchivedFileInfo(archivePath);
                Length = info.Size;
                CreationTime = info.CreatedTime;
            }

            public FileInfoCache(long length)
            {
                Length = length;
                CreationTime = DateTime.Now;
            }
        }

        private LogicalLine _logicalLine = new LogicalLine();
        private readonly LogicalLinesHistoryQueue _logicalLinesHistoryQueue;
        private int _lineNumber;
        private bool _isFilenameNeeded = true;
        private readonly string _file;
        private readonly object _minorLock = new object();
        private long _lastPos;
        private DateTime _lastCreationTime = DateTime.MinValue;
        private readonly TailPbl _bl;
        private readonly int _fileIndex;
        private FileInfoCache _fileInfo;
        private bool _errorShown;
        private readonly FileTypes _fileType = FileTypes.Regular;
        private int _startFromNum;
        private int _afterCounter = -1;

        public FileTypes FileType
        {
            get
            {
                lock (_minorLock)
                {
                    return _fileType;
                }
            }
        }

        public long LastPos
        {
            get
            {
                lock (_minorLock)
                {
                    return _lastPos;
                }
            }

            private set
            {
                lock (_minorLock)
                {
                    _lastPos = value;
                }
            }
        }

        public long FileSize
        {
            get
            {
                lock (_minorLock)
                {
                    return _fileInfo?.Length ?? 0;
                }
            }

            private set
            {
                lock (_minorLock)
                {
                    if (_fileInfo == null)
                    {
                        _fileInfo = new FileInfoCache(value);
                    }
                    else
                    {
                        _fileInfo.Length = value;
                    }
                }
            }
        }

        private DateTime FileCreationTime
        {
            get
            {
                lock (_minorLock)
                {
                    return _fileInfo?.CreationTime ?? DateTime.MinValue;
                }
            }
        }

        public string FileName
        {
            get
            {
                lock (_minorLock)
                {
                    return _file;
                }
            }
        }

        public File(string file, TailPbl bl, int fileIndex)
        {
            if (string.IsNullOrEmpty(file)) throw new ArgumentException(@"Param cannot be empty", nameof(file));
            _bl = bl ?? throw new ArgumentNullException(nameof(bl));
            _file = file;
            _fileIndex = fileIndex;
            _logicalLinesHistoryQueue = new LogicalLinesHistoryQueue(Math.Max(1, Configs.ContextBefore));
            _startFromNum = Configs.LinesStartNumber;

            if (_file == Constants.CONSOLE_FILENAME)
            {
                _fileType = FileTypes.Console;
            }
            else if (ArchiveSupport.TryGetArchivePath(file, out var archive, out var finalFile)
                && ArchiveSupport.IsValidArchive(archive))
            {
                _fileType = string.IsNullOrWhiteSpace(finalFile)
                            ? FileTypes.Archive
                            : FileTypes.ArchivedFile;
            }

            UpdateFileInfo();
            ResetCounters();
        }

        private void UpdateFileInfo()
        {
            lock (_minorLock)
            {
                try
                {
                    switch (_fileType)
                    {
                        case FileTypes.Console:
                            _fileInfo = new FileInfoCache(LastPos);
                            break;

                        case FileTypes.Regular:
                            _fileInfo = new FileInfoCache(new FileInfo(_file));
                            break;

                        case FileTypes.ArchivedFile:
                            _fileInfo ??= new FileInfoCache(_file);
                            break;

                        default:
                            throw new InvalidOperationException(
                                $"Unknown FileType {_fileType}");
                    }
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    _fileInfo = null;
                }
            }
        }

        private readonly object _processLock = new object();

        private void ProcessInternal(ref Stream stream)
        {
            UpdateFileInfo();
            CheckReplacingOfFile();
            if (IsFileUnchanged())
            {
                return;
            }
            _bl.SetLastFile(this);

            stream = GetStream();
            FindLastLinesInStream(stream);
            ProcessStreamFromLastPosToEnd(stream, null);
            CreateNoProcessTimer();
        }

        private void ProcessError(string error)
        {
            FlushLogicalLine();
            ShowError(error);

            if (Configs.Follow)
            {
                ResetCounters();
            }
        }

        public void Process()
        {
            lock (_processLock)
            {
                DisposeNoProcessTimer();
                Stream stream = null;
                _errorShown = false;
                try
                {
                    ProcessInternal(ref stream);
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    ProcessError(ex.Message);
                }
                finally
                {
                    stream?.Dispose();
                }
            }
        }

        private bool _lastLinesProcessed;

        private void FindLastLinesInStream(Stream stream)
        {
            if (_lastLinesProcessed
                || Configs.LinesStartFrom != NumLinesStart.End)
            {
                return;
            }

            if (_startFromNum != 0)
            {
                var logicalLinesHistory = new LogicalLinesHistoryQueue(
                    _startFromNum * (Configs.ContextLines + 1));
                if (stream.CanSeek)
                {
                    if (!ProcessStreamInPages(stream, logicalLinesHistory))
                    {
                        // optimization is not working, try without optimization
                        ResetCounters();
                        logicalLinesHistory.Clear();
                        ProcessStreamFromLastPosToEnd(stream, logicalLinesHistory);
                    }
                }
                else
                {
                    ProcessStreamFromLastPosToEnd(stream, logicalLinesHistory);
                }

                FlushLogicalLine(logicalLinesHistory);
                while (logicalLinesHistory.Any())
                {
                    var forPrinting = new LogicalLinesHistoryQueue();
                    PrepareLogicalLineForPrinting(logicalLinesHistory.Dequeue(), forPrinting);
                    PrintLogicalLines(forPrinting);
                }
            }

            LastPos = FileSize;
            _lastLinesProcessed = true;
        }

        private void ProcessStreamFromLastPosToEnd(Stream stream, LogicalLinesHistoryQueue logicalLines)
        {
            var encoding = Encoding.Default;

            if (FileType == FileTypes.Console)
            {
                if (!stream.CanRead)
                {
                    return;
                }

                encoding = Console.InputEncoding;
            }

            using var sr = new StreamReader(stream, encoding, true);
            SeekToLastPos(sr);
            var s = ReadLine(sr);
            while (s != null)
            {
                ProcessReadLine(s, logicalLines);
                s = ReadLine(sr);
            }

            // for archives, LastPos remains 0, so update-it to FileSize
            // NOTE: do not set LastPos = FileSize for files, cause FileSize is just
            //       a cached value, and do not reflect actual file size!
            if (!sr.BaseStream.CanSeek)
            {
                LastPos = FileSize;
            }
        }

        private static Encoding DetectEncoding(Stream stream)
        {
            using var sr = new StreamReader(stream, Encoding.Default, true,
                Constants.REVERS_SEARCH_PAGE_SIZE, true);
            sr.Peek();
            return sr.CurrentEncoding;
        }

        private bool CanProcessInPages() =>
                // process in pages only if makes sense
                FileSize > Constants.REVERS_SEARCH_PAGE_SIZE

                // NOTE: it seems quite complicate to store the before context,
                //       when file is reading in pages from end to begin.
                //       for the moment, disable the optimization when before context
                //       is needed
                && (!Configs.IsContextBeforeUsed);

        // Optimization used:
        //       read from end in pages by XXX bytes to a memory stream
        //       and stops to read if _startFromNum lines found
        //
        //       Will not works, if line length is greater than PAGE_SIZE
        private bool ProcessStreamInPages(Stream stream, LogicalLinesHistoryQueue logicalLines)
        {
            if (!CanProcessInPages())
            {
                return false;
            }

            var encoding = DetectEncoding(stream);
            var historyDeep = _startFromNum * (Configs.ContextLines + 1);
            var foundLines = new LogicalLinesHistoryQueue(historyDeep);
            var from = FileSize;

            while (from != 0 && foundLines.Count != historyDeep)
            {
                var buf = new byte[Constants.REVERS_SEARCH_PAGE_SIZE];
                var pageLines = new LogicalLinesHistoryQueue(historyDeep);

                from = Math.Max(0, from - Constants.REVERS_SEARCH_PAGE_SIZE);
                stream.Seek(from, SeekOrigin.Begin);
                var sz = stream.Read(buf, 0, Constants.REVERS_SEARCH_PAGE_SIZE);

                Stream ms = null;
                try
                {
                    ms = new MemoryStream(buf, 0, sz);
                    using var sr = new StreamReader(ms, encoding,
                        from == 0 // ignore BOM only at file beginning
                    );
                    ms = null; // prevent disposing several times

                    if (from != 0)
                    {
                        var nul = sr.ReadLine(); // ignore first line, may be incomplete
                        var szBytes = encoding.GetByteCount(nul ?? string.Empty);
                        if (szBytes >= Constants.REVERS_SEARCH_PAGE_SIZE) // extra long line
                        {
                            return false;
                        }
                        from += szBytes;
                    }

                    var s = sr.ReadLine();
                    while (s != null)
                    {
                        if (encoding.GetByteCount(s) >= Constants.REVERS_SEARCH_PAGE_SIZE)
                        {
                            return false;
                        }
                        ProcessReadLine(s, pageLines);
                        s = sr.ReadLine();
                    }
                }
                finally
                {
#pragma warning disable CA1508 // Avoid dead conditional code
                    ms?.Dispose();
#pragma warning restore CA1508 // Avoid dead conditional code
                }
                FlushLogicalLine(pageLines);
                pageLines.Enqueue(foundLines);
                foundLines.ReplaceBy(pageLines);
                LastPos = FileSize - from;
            }

            logicalLines.ReplaceBy(foundLines);
            // because lines was searched in pages, line numbers are irrelevant
            logicalLines.SetLinesNumberToUnknown();

            LastPos = FileSize;

            return true;
        }

        private Timer _noProcessTimer;

        private void CreateNoProcessTimer()
        {
            _noProcessTimer = new Timer((_) =>
            {
                lock (_processLock)
                {
                    DisposeNoProcessTimer();
                    // exiting by timeout, seems that logical line is anyway finished
                    FlushLogicalLine();
                }
            },
            null, Constants.LOGICAL_LINE_DELAY, TimeSpan.FromMilliseconds(-1));
        }

        private void DisposeNoProcessTimer()
        {
            lock (_processLock)
            {
                if (_noProcessTimer != null)
                {
                    _noProcessTimer.Dispose();
                    _noProcessTimer = null;
                }
            }
        }

        private string ReadLine(StreamReader sr)
        {
            var s = sr.ReadLine();
            if (s != null)
            {
                UpdateLastPos(sr, s.Length);
            }
            return s;
        }

        private Stream GetStream()
        {
            Stream stream;
            switch (_fileType)
            {
                case FileTypes.Console:
                    stream = Console.OpenStandardInput();
                    break;

                case FileTypes.Regular:
                    stream = new FileStream(_file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    break;

                case FileTypes.ArchivedFile:
                    stream = ArchiveSupport.GetFileStream(_file);
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Invalid _fileType {_fileType}");
            }

            return stream;
        }

        private bool IsFileUnchanged()
        {
            var res = FileSize > 0 && LastPos == FileSize;
            if (res)
            {
                FlushLogicalLine();
            }
            return res;
        }

        private void CheckReplacingOfFile()
        {
            if (_lastCreationTime != FileCreationTime)
            {
                FlushLogicalLine();
                ResetCounters();
                _lastCreationTime = FileCreationTime;
            }
        }

        private void UpdateLastPos(StreamReader sr, long bytesCount)
        {
            if (sr.BaseStream.CanSeek)
            {
                LastPos = sr.BaseStream.Position;
            }
            else
            {
                LastPos += bytesCount;
                FileSize = LastPos;
            }
        }

        private void SeekToLastPos(StreamReader sr)
        {
            if (sr.BaseStream.CanSeek)
            {
                sr.DiscardBufferedData();
                sr.BaseStream.Seek(LastPos, SeekOrigin.Begin);
            }
        }

        private void ResetCounters()
        {
            LastPos = GetLastLocation();
            _lineNumber = 0;
            _isFilenameNeeded = true;
        }

        private readonly object _errorShownLock = new object();

        private void ShowError(string error)
        {
            bool showError;
            lock (_errorShownLock)
            {
                showError = !_errorShown;
                if (!_errorShown)
                {
                    _errorShown = true;
                }
            }
            if (showError)
            {
                error += $". [{FileName}]";
                _bl.NewLineCallback(TailPbl.GetErrorLine(error), 0);
            }
        }

        private void ProcessReadLine(string readLine, LogicalLinesHistoryQueue logicalLinesHistoryQueue)
        {
            var isLogicalContinuation =
                readLine.Length < Configs.LogicalLineMarker.Length
                || readLine
                    .Substring(0, Configs.LogicalLineMarker.Length)
                    .Contains(Configs.LogicalLineMarker, Configs.ComparisonOptions);

            if (!isLogicalContinuation) // a new line begins, flush memory
            {
                ++_lineNumber;
                FlushLogicalLine(logicalLinesHistoryQueue);
            }

            var line = new Line(readLine, Configs.ComparisonOptions, Configs.Regex,
                isLogicalContinuation, _lineNumber);
            line.CheckFilters(Configs.FiltersShow, Configs.FiltersHide, Configs.FiltersHighlight);
            AddLineNumberIfApplicable(line);
            TruncateIfApplicable(line);
            _logicalLine.Add(line);
        }

        private long GetLastLocation()
        {
            switch (Configs.StartLocationType)
            {
                case StartLocationTypes.B:
                    return Configs.StartLocation;

                case StartLocationTypes.P:
                    return Configs.StartLocation * FileSize / 100;

                default:
                    throw new InvalidOperationException($"Invalid _startLocationType {Configs.StartLocationType}");
            }
        }

        private bool ShouldBeHided()
        {
#pragma warning disable RCS1080 // Use 'Count/Length' property instead of 'Any' method.
            if (!Configs.FiltersHide.Any())
#pragma warning restore RCS1080 // Use 'Count/Length' property instead of 'Any' method.
            {
                return false;
            }

            return Configs.AllFilters ?
                        _logicalLine.FoundHideFiltersCount == Configs.FiltersHide.Count :
                        _logicalLine.IsHidedFlagExists;
        }

        private bool ShouldBeShown()
        {
#pragma warning disable RCS1080 // Use 'Count/Length' property instead of 'Any' method.
            if (!Configs.FiltersShow.Any())
#pragma warning restore RCS1080 // Use 'Count/Length' property instead of 'Any' method.
            {
                return true;
            }

            return Configs.AllFilters ?
                        _logicalLine.FoundShowFiltersCount == Configs.FiltersShow.Count :
                        _logicalLine.IsShowedFlagExists;
        }

        private bool SkipFromNumLines()
        {
            var mustSkip =
                Configs.LinesStartFrom == NumLinesStart.Begin
                && _startFromNum > 0
                && _logicalLine.IsVisible;

            if (mustSkip)
            {
                --_startFromNum;
            }

            return mustSkip;
        }

        private void FlushLogicalLine(LogicalLinesHistoryQueue logicalLinesHistoryQueue = null)
        {
            if (_logicalLine.IsEmpty)
            {
                return;
            }

            _logicalLine.IsVisible = !ShouldBeHided() && ShouldBeShown();

            if (
                (_logicalLine.IsVisible || _afterCounter > 0)
                && !SkipFromNumLines())
            {
                var forPrinting = new LogicalLinesHistoryQueue();
                PrepareLogicalLineForPrinting(_logicalLine, forPrinting);

                if (logicalLinesHistoryQueue == null)
                {
                    PrintLogicalLines(forPrinting);
                }
                else
                {
                    logicalLinesHistoryQueue.Enqueue(forPrinting);
                }

                if (_logicalLine.IsVisible)
                {
                    _afterCounter = Configs.ContextAfter;
                }
            }

            _logicalLinesHistoryQueue.Enqueue(_logicalLine);
            _logicalLine = new LogicalLine();
        }

        private void PrintFileName()
        {
            _bl.PrintFileName(FileName, _isFilenameNeeded);
            _isFilenameNeeded = false;
        }

        private int _lastPrintedLine;

        private void PrintLogicalLines(LogicalLinesHistoryQueue logicalLines)
        {
            lock (_bl.PrintLock)
            {
                PrintFileName();

                while (logicalLines.Any())
                {
                    var logicalLine = logicalLines.Dequeue();

                    if (!logicalLine.IsPrinted)
                    {
                        if (Configs.IsContextUsed
                            && Math.Abs(logicalLine.LineNumber - _lastPrintedLine) > 1)
                        {
                            _bl.PrintLogicalLine(TailPbl.GetContextDelimiter(), _fileIndex);
                        }

                        _bl.PrintLogicalLine(logicalLine, _fileIndex);
                        _lastPrintedLine = logicalLine.LineNumber;
                    }
                }
            }
        }

        private void PrepareLogicalLineForPrinting(LogicalLine logicalLine, LogicalLinesHistoryQueue prepared)
        {
            if (Configs.IsContextBeforeUsed
                && _logicalLinesHistoryQueue.Any()
                && _logicalLine.IsVisible)
            {
                prepared.Enqueue(_logicalLinesHistoryQueue);
                _logicalLinesHistoryQueue.Clear();
            }

            prepared.Enqueue(logicalLine);

            if (_afterCounter > 0)
            {
                --_afterCounter;
            }
        }

        private static void TruncateIfApplicable(Line line)
        {
            if (Configs.Truncate)
            {
                line.Truncate(Constants.MAX_WIDTH - 1 /* -1 for newline */);
            }
        }

        private static void AddLineNumberIfApplicable(Line line)
        {
            if (Configs.ShowLineNumber)
            {
                line.AddLineNumber();
            }
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;

            if (obj is File f)
            {
                return _file.Equals(f._file, StringComparison.OrdinalIgnoreCase);
            }

            if (obj is string s)
            {
                return _file.Equals(s, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        public override int GetHashCode() => _file.GetHashCode(StringComparison.OrdinalIgnoreCase);

        public override string ToString() => FileName;

        public void Dispose() => DisposeNoProcessTimer();
    }
}