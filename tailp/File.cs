using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace TailP
{
    public sealed class File: IDisposable
    {
        private class FileInfoCache // FileInfo accesses file on each method call
        {
            public long Length { get; private set; }
            public DateTime CreationTime { get; private set; }

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
        }

        private readonly TimeSpan LOGICAL_LINE_DELAY = TimeSpan.FromMilliseconds(250);
        private readonly TimeSpan DELAY_AFTER_SECOND_ERROR = TimeSpan.FromSeconds(1);
        private readonly int TRIES_BEFORE_ERROR = 1;
        private readonly int PAGE_SIZE = 1024 * 1024; // 1MiB

        private LogicalLine _logicalLine = new LogicalLine();
        // TODO: set LogicalLinesHistory limit to -C from command prompt
        private LogicalLinesHistory _logicalLinesHistory = new LogicalLinesHistory(1);
        private int _lineNumber = 0;
        private int _lastPrintedLineNumer = -1;
        private string _file = string.Empty;
        private object _minorLock = new object();
        private long _lastPos = 0;
        private DateTime _lastCreationTime = DateTime.MinValue;
        private TailPBL _bl;
        private int _fileIndex;
        private FileInfoCache _fileInfo = null;
        private bool _errorShown = false;
        private FileTypes _fileType = FileTypes.Regular;
        private NumLinesStart _startFromType = NumLinesStart.begin;
        private int _startFromNum = 0;

        public FileTypes FileType
        {
            get
            {
                lock(_minorLock)
                {
                    return _fileType;
                }
            }
        }
        public long LastPos
        {
            get
            {
                lock(_minorLock)
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
                    return _fileInfo == null ? 0 : _fileInfo.Length;
                }
            }
        }
        public DateTime FileCreationTime
        {
            get
            {
                lock(_minorLock)
                {
                    return _fileInfo == null ? DateTime.MinValue : _fileInfo.CreationTime;
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
        public int FileIndex
        {
            get
            {
                lock (_minorLock)
                {
                    return _fileIndex;
                }
            }
        }

        public File(string file, TailPBL bl, int fileIndex,
                    NumLinesStart startFromType, int startFromNum)
        {
            if (string.IsNullOrEmpty(file)) throw new ArgumentException("file");
            if (bl == null) throw new ArgumentNullException("bl");

            _file = file;
            _bl = bl;
            _fileIndex = fileIndex;
            _startFromType = startFromType;
            _startFromNum = startFromNum;

            string archive;
            string finalFile;
            if (ArchiveSupport.TryGetArchivePath(file, out archive, out finalFile))
            {
                if (ArchiveSupport.IsValidArchive(archive))
                {
                    _fileType = finalFile == string.Empty
                                ? FileTypes.Archive
                                : FileTypes.ArchivedFile;
                }
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
                    switch(_fileType)
                    {
                        case FileTypes.Regular:
                            _fileInfo = new FileInfoCache(new FileInfo(_file));
                            break;
                        case FileTypes.ArchivedFile:
                            if (_fileInfo == null)
                            {
                                _fileInfo = new FileInfoCache(_file);
                            }
                            break;
                        default:
                            throw new InvalidOperationException(
                                string.Format("Unknown FileType {0}", _fileType));
                    }
                }
                catch
                {
                    _fileInfo = null;
                }
            }
        }

        private object _processLock = new object();
        public void ProcessFile()
        {
            lock (_processLock)
            {
                DisposeNoProcessTimer();
                var tries = 0;
                while (true)
                {
                    Stream stream = null;
                    _errorShown = false;
                    try
                    {
                        UpdateFileInfo();
                        CheckReplacingOfFile();
                        if (IsFileUnchanged())
                        {
                            return;
                        }
                        _bl.LastFile = this;

                        stream = GetStream();
                        FindLastLinesInStream(stream);
                        ProcessStreamFromLastPostToEnd(stream, null);
                        CreateNoProcessTimer();

                        break; // no errors, exiting from while
                    }
                    catch (Exception ex)
                    {
                        if (tries++ < TRIES_BEFORE_ERROR)
                        {
                            if (tries > 1)
                            {
                                Thread.Sleep(DELAY_AFTER_SECOND_ERROR);
                            }
                        }
                        else
                        {
                            FlushLogicalLine();
                            ShowError(ex.Message);

                            if (_bl.Follow)
                            {
                                ResetCounters();
                            }

                            break;
                        }
                    }
                    finally
                    {
                        if (stream != null)
                        {
                            stream.Dispose();
                        }
                    }
                }
            }
        }

        private bool _lastLinesProcessed = false;
        private void FindLastLinesInStream(Stream stream)
        {
            if (_lastLinesProcessed ||
                _startFromType != NumLinesStart.end)
            {
                return;
            }

            if (_startFromNum != 0)
            {
                var logicalLinesHistory = new LogicalLinesHistory(_startFromNum);
                if (stream.CanSeek)
                {
                    if (!ProcessStreamInPages(stream, logicalLinesHistory))
                    {
                        // optimization is not working, try without optimization
                        ResetCounters();
                        logicalLinesHistory.Clear();
                        ProcessStreamFromLastPostToEnd(stream, logicalLinesHistory);
                    }
                }
                else
                {
                    ProcessStreamFromLastPostToEnd(stream, logicalLinesHistory);
                }

                FlushLogicalLine(logicalLinesHistory);
                while (logicalLinesHistory.Any())
                {
                    PrintLogicalLine(logicalLinesHistory.Dequeue());
                }
            }

            LastPos = FileSize;
            _lastLinesProcessed = true;
        }

        private void ProcessStreamFromLastPostToEnd(Stream stream, LogicalLinesHistory logicalLines)
        {
            using (var sr = new StreamReader(stream, Encoding.Default, true))
            {
                SeekToLastPos(sr);
                var s = ReadLine(sr);
                while (s != null)
                {
                    ProcessReadLine(s, logicalLines);
                    s = ReadLine(sr);
                }

                LastPos = FileSize;
            }
        }

        private Encoding DetectEncoding(Stream stream)
        {
            using (var sr = new StreamReader(stream, Encoding.Default, true, PAGE_SIZE, true))
            {
                sr.Peek();
                return sr.CurrentEncoding;
            }
        }

        private bool ProcessStreamInPages(Stream stream, LogicalLinesHistory logicalLines)
        {
            if (FileSize <= PAGE_SIZE)
            {
                return false;
            }

            // Optimization:
            //       read from end in pages by XXX bytes to a memory stream
            //       and stops to read if _startFromNum lines found
            //
            //       Will not works, if line length is greater than PAGE_SIZE
            var encoding = DetectEncoding(stream);
            var foundLines = new LogicalLinesHistory(_startFromNum);
            var from = FileSize;

            while (from != 0 &&
                   foundLines.Count != _startFromNum)
            {
                var buf = new byte[PAGE_SIZE];
                var pageLines = new LogicalLinesHistory(foundLines.Limit);

                from = Math.Max(0, from - PAGE_SIZE);
                stream.Seek(from, SeekOrigin.Begin);
                var sz = stream.Read(buf, 0, PAGE_SIZE);

                Stream ms = null;
                try
                {
                    ms = new MemoryStream(buf, 0, sz);
                    using (var sr = new StreamReader(ms, encoding,
                            from == 0 // ignore BOM only at file beginning
                            ))
                    {
                        ms = null; // prevent disposing several times

                        if (from != 0)
                        {
                            var nul = sr.ReadLine(); // ignore first line, may be incomplete
                            var szBytes = encoding.GetByteCount(nul);
                            if (szBytes >= PAGE_SIZE)
                            {
                                return false;
                            }
                            from += szBytes;
                        }

                        var s = sr.ReadLine();
                        while (s != null)
                        {
                            if (encoding.GetByteCount(s) >= PAGE_SIZE)
                            {
                                return false;
                            }
                            ProcessReadLine(s, pageLines);
                            s = sr.ReadLine();
                        }
                    }
                }
                finally
                {
                    if (ms != null)
                    {
                        ms.Dispose();
                        ms = null;
                    }
                }
                FlushLogicalLine(pageLines);
                pageLines.Enqueue(foundLines);
                foundLines.ReplaceBy(pageLines);
                LastPos = FileSize - from;
            }

            logicalLines.ReplaceBy(foundLines);
            LastPos = FileSize;

            return true;
        }

        private Timer _noProcessTimer = null;
        private void CreateNoProcessTimer()
        {
            _noProcessTimer = new Timer((s) =>
            {
                lock (_processLock)
                {
                    DisposeNoProcessTimer();
                    // exiting by timeout, seems that logical line is anyway finished
                    FlushLogicalLine();
                }
            },
            null, LOGICAL_LINE_DELAY, TimeSpan.FromMilliseconds(-1));
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
            UpdateLastPos(sr);
            return s;
        }

        private Stream GetStream()
        {
            Stream stream = null;

            switch (_fileType)
            {
                case FileTypes.Regular:
                    stream = new FileStream(_file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    break;
                case FileTypes.ArchivedFile:
                    stream = ArchiveSupport.GetFileStream(_file);
                    break;
                default:
                    throw new InvalidOperationException(
                        string.Format("Invalid _fileType {0}", _fileType));
            }

            return stream;
        }

        private bool IsFileUnchanged()
        {
            bool res = FileSize > 0 && LastPos == FileSize;
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

        private void UpdateLastPos(StreamReader sr)
        {
            if (sr.BaseStream.CanSeek)
            {
                LastPos = sr.BaseStream.Position;
            }
        }

        private void SeekToLastPos(StreamReader sr)
        {
            if (sr.BaseStream.CanSeek)
            {
                sr.BaseStream.Seek(LastPos, SeekOrigin.Begin);
            }
        }

        private void ResetCounters()
        {
            LastPos = GetLastLocation();
            _lineNumber = 0;
            _lastPrintedLineNumer = -1;
        }

        private object _errorShownLock = new object();
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
                error += string.Format(". [{0}]", FileName);
                _bl.NewLineCallback(TailPBL.GetErrorLine(error), 0);
            }
        }

        private void ProcessReadLine(string readLine, LogicalLinesHistory logicalLinesHistory)
        {
            var isLogicalContinuation =
                readLine.Length < _bl.LogicalLineMarker.Length ||
                readLine.Substring(0, _bl.LogicalLineMarker.Length).IndexOf(
                    _bl.LogicalLineMarker, _bl.ComparisonOptions) == -1;

            if (!isLogicalContinuation) // a new line begins, flush memory
            {
                ++_lineNumber;
                FlushLogicalLine(logicalLinesHistory);
            }

            var line = new Line(readLine, _bl.ComparisonOptions, _bl.Regex,
                isLogicalContinuation, _lineNumber);
            line.CheckFilters(_bl.FiltersShow, _bl.FiltersHide, _bl.FiltersHighlight);
            AddLineNumberIfApplicable(line);
            TruncateIfApplicable(line);
            _logicalLine.Add(line);
        }

        private long GetLastLocation()
        {
            switch (_bl.StartLocationType)
            {
                case StartLocationTypes.b:
                    return _bl.StartLocation;
                case StartLocationTypes.p:
                    return _bl.StartLocation * FileSize / 100;
                default:
                    throw new InvalidOperationException(string.Format(
                        "Invalid _startLocationType {0}", _bl.StartLocationType));
            }
        }

        private bool ShouldBeHided()
        {
            if (!_bl.FiltersHide.Any())
            {
                return false;
            }

            return _bl.AllFilters ?
                        _logicalLine.FoundHideFiltersCount == _bl.FiltersHide.Count :
                        _logicalLine.IsHidedFlagExists;
        }

        private bool ShouldBeShown()
        {
            if (!_bl.FiltersShow.Any())
            {
                return true;
            }

            return _bl.AllFilters ?
                        _logicalLine.FoundShowFiltersCount == _bl.FiltersShow.Count :
                        _logicalLine.IsShowedFlagExists;
        }

        private void FlushLogicalLine(LogicalLinesHistory logicalLinesHistory = null)
        {
            if (_logicalLine.IsEmpty)
            {
                return;
            }

            _logicalLine.IsVisible = !ShouldBeHided() && ShouldBeShown();

            if (_logicalLine.IsVisible)
            {
                // skip first _startFromNum lines
                if (_startFromType == NumLinesStart.begin &&
                    _startFromNum > 0)
                {
                    --_startFromNum;
                }
                else
                {
                    if (logicalLinesHistory == null)
                    {
                        PrintLogicalLine(_logicalLine);
                    }
                    else
                    {
                        logicalLinesHistory.Enqueue(_logicalLine);
                    }
                }
            }

            _logicalLinesHistory.Enqueue(_logicalLine);
            _logicalLine = new LogicalLine();
        }

        private void PrintLogicalLine(LogicalLine logicalLine)
        {
            lock (_bl.PrintLock)
            {
                _bl.PrintFileName(FileName, _lastPrintedLineNumer == -1);
                _bl.PrintLogicalLine(logicalLine, _fileIndex);
            }
            _lastPrintedLineNumer = logicalLine.Max(x => x.LineNumber);
        }

        private void TruncateIfApplicable(Line line)
        {
            if (_bl.Truncate)
            {
                line.Truncate(_bl.MAX_WIDTH - 1 /* -1 for newline */);
            }
        }

        private void AddLineNumberIfApplicable(Line line)
        {
            if (_bl.ShowLineNumber)
            {
                line.AddLineNumber();
            }
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;

            var f = obj as File;
            if (f != null)
            {
                return _file.Equals(f._file, StringComparison.InvariantCultureIgnoreCase);
            }

            var s = obj as string;
            if (s != null)
            {
                return _file.Equals(s, StringComparison.InvariantCultureIgnoreCase);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return _file.GetHashCode();
        }

        public override string ToString()
        {
            return FileName.ToString();
        }

        public void Dispose()
        {
            DisposeNoProcessTimer();
        }
    }
}
