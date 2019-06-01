// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++ and C#: http://www.viva64.com
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace TailP
{
    internal static class Program
    {
        // used hardcoded colors instead of default, because on Ctrl-C color changes to last used
        private const ConsoleColor DEFAULT_BACKGROUND = ConsoleColor.Black;

        private const ConsoleColor DEFAULT_FOREGROUND = ConsoleColor.Gray;

        private const int STATUS_TIMER_PERIOD_MS = 1000;

        static private Timer _statusTimer;
        static private long _lastPos = 0;
        static private string _lastFileName;
        static private DateTime _lastChanged = DateTime.Now;

        static private string ProcessedBytesToString(long processed, long total)
        {
            if (total < 1024)
            {
                return string.Format("{0} of {1} bytes", processed, total);
            }
            if (total < 1024 * 1024)
            {
                return string.Format("{0} of {1} KiB",
                    Math.Round(100.0 * processed / 1024 / 100, 2),
                    Math.Round(100.0 * total / 1024 / 100, 2));
            }
            if (total < 1024 * 1024 * 1024)
            {
                return string.Format("{0} of {1} MiB",
                    Math.Round(100.0 * processed / 1024 / 1024 / 100, 2),
                    Math.Round(100.0 * total / 1024 / 1024 / 100, 2));
            }

            return string.Format("{0} of {1} GiB",
                Math.Round(100.0 * processed / 1024 / 1024 / 1024 / 100, 2),
                Math.Round(100.0 * total / 1024 / 1024 / 1024 / 100, 2));
        }

        static private void StartUpdatingStatus()
        {
            _statusTimer = new Timer(
                (_) => UpdateStatus(), null, 0, STATUS_TIMER_PERIOD_MS);
        }

        static private void UpdateProgressBar(byte percents)
        {
            if (percents < 100)
            {
                TaskbarProgress.SetState(TaskbarStates.Normal);
                TaskbarProgress.SetValue(percents, 100);
            }
            else
            {
                TaskbarProgress.SetState(TaskbarStates.NoProgress);
            }
        }

        static private void UpdateStatus()
        {
            var lastPos = _bl.TotalProcessed;
            var totalSize = _bl.TotalFilesSize;
            var lastFileName = _bl.LastFileName;

            var percents = (byte)(totalSize == 0 ? 0 : 100 * lastPos / totalSize);

            if (lastPos != _lastPos || lastFileName != _lastFileName)
            {
                _lastPos = lastPos;
                _lastFileName = lastFileName;
                _lastChanged = DateTime.Now;
            }

            if (!string.IsNullOrEmpty(lastFileName))
            {
                var totalFiles = _bl.FilesCount;
                var pendingFiles = _bl.Pending;
                var files = Configuration.Follow
                    ? string.Format("{0} files", totalFiles)
                    : string.Format("{0} of {1}", totalFiles - pendingFiles, totalFiles);

                var title = string.Format(
                    "{0} | {1} last processed: {2} {3} ago ({4}) | ",
                    ProcessedBytesToString(lastPos, totalSize),
                    GetETAText(percents),
                    Path.GetFileName(_bl.LastFileName),
                    DateTime.Now.Subtract(_lastChanged).ToHumanReadableString(),
                    files);

                Console.Title = title.AppendFromRight(
                    Path.GetFullPath(_bl.LastFileName), Console.WindowWidth);

                UpdateProgressBar(percents);
            }
        }

        static private DateTime _lastCalculate = DateTime.UtcNow;
        static private byte _lastPercents = 100;
        static private readonly Queue<double> _secondsPerPercent = new Queue<double>();
        private const int SAMPLES_COUNT = 5;

        private static string GetETAText(byte percents)
        {
            if (_lastPercents != percents)
            {
                if (_lastPercents < percents)
                {
                    double speed =
                    1.0 * (DateTime.UtcNow - _lastCalculate).TotalSeconds
                    / (percents - _lastPercents); //-V3064

                    _secondsPerPercent.Enqueue(speed);
                }

                _lastPercents = percents;
                _lastCalculate = DateTime.UtcNow;
            }

            while (_secondsPerPercent.Count > SAMPLES_COUNT)
            {
                _secondsPerPercent.Dequeue();
            }

            var estimated = _secondsPerPercent.Count == SAMPLES_COUNT
                ? TimeSpan.FromSeconds(
                    Math.Round(_secondsPerPercent.Average() * (100 - percents))
                    )
                : TimeSpan.FromSeconds(0);

            if (estimated > TimeSpan.FromSeconds(5))
            {
                return string.Format("ETA: {0} |", estimated.ToHumanReadableString());
            }

            if (estimated > TimeSpan.FromSeconds(0))
            {
                return "ETA: almost done |";
            }

            return string.Empty;
        }

        static private void ResetConsoleColors()
        {
            Console.BackgroundColor = DEFAULT_BACKGROUND;
            Console.ForegroundColor = DEFAULT_FOREGROUND;
        }

        static private TailPBL _bl;

        private static void Main(string[] args)
        {
            // reset colors on Ctrl+C
            Console.CancelKeyPress += (s, a) => ResetConsoleColors();

            NewLine();

            _bl = new TailPBL((l, i) => WriteLine(l, i));
            try
            {
                _bl.ParseArgs(args);

                StartUpdatingStatus();

                _bl.StartProcess();

                if (Configuration.Follow)
                {
                    Console.ReadLine();
                }

                _statusTimer.Dispose();
                UpdateStatus();
            }
            catch (TailPHelpException)
            {
                WriteMessage(_bl.GetHelp(), Types.None);
            }
            catch (TailPArgsException ex)
            {
                WriteMessage(ex.ToString(), Types.Error);
                WriteMessage(_bl.GetHelp(), Types.None);
            }
            catch (Exception ex)
            {
                WriteMessage(ex.ToString(), Types.Error);
            }
            finally
            {
                ResetConsoleColors();
            }
        }

        static private readonly ConsoleColor[] _availableFilterColors = new ConsoleColor[]
        {
            ConsoleColor.Yellow,
            ConsoleColor.Green,
            ConsoleColor.Cyan,
            ConsoleColor.Magenta,
            ConsoleColor.DarkYellow,
            ConsoleColor.DarkGreen,
            ConsoleColor.DarkCyan,
            ConsoleColor.DarkMagenta
        };

        static private readonly Dictionary<int, ConsoleColor> _filterColors = new Dictionary<int, ConsoleColor>();
        static private int _lastFilterColor = _availableFilterColors.Length;

        static private ConsoleColor GetFilterBackgroundColor(int colorIndex)
        {
            if (!_filterColors.TryGetValue(colorIndex, out ConsoleColor color))
            {
                ++_lastFilterColor;
                if (_lastFilterColor >= _availableFilterColors.Length)
                {
                    _lastFilterColor = 0;
                }
                color = _availableFilterColors[_lastFilterColor];
                _filterColors.Add(colorIndex, color);
            }
            return color;
        }

        static private readonly ConsoleColor[] _availableFilesColors = new ConsoleColor[]
        {
            ConsoleColor.Gray,
            ConsoleColor.Yellow,
            ConsoleColor.Green,
            ConsoleColor.Cyan,
            ConsoleColor.Magenta,
        };

        static private readonly Dictionary<int, ConsoleColor> _fileColors = new Dictionary<int, ConsoleColor>();
        static private int _lastFileColor = _availableFilesColors.Length;

        static private ConsoleColor GetFileForegroundColor(int fileIndex)
        {
            if (!_fileColors.TryGetValue(fileIndex, out ConsoleColor color))
            {
                ++_lastFileColor;
                if (_lastFileColor >= _availableFilesColors.Length)
                {
                    _lastFileColor = 0;
                }
                color = _availableFilesColors[_lastFileColor];
                _fileColors.Add(fileIndex, color);
            }
            return color;
        }

        private static ConsoleColor TypeToForegroundColor(Types type, int fileIndex)
        {
            switch (type)
            {
                case Types.Highlight:
                case Types.Show:
                    return ConsoleColor.Black;

                case Types.Truncated:
                    return ConsoleColor.Red;

                case Types.Error:
                    TaskbarProgress.SetState(TaskbarStates.Error);
                    return ConsoleColor.Red;

                case Types.LineNumber:
                case Types.FileName:
                    return ConsoleColor.DarkGray;

                default:
                    return Configuration.Follow
                                ? GetFileForegroundColor(fileIndex)
                                : DEFAULT_FOREGROUND;
            }
        }

        private static ConsoleColor TypeToBackgroundColor(Types type, int colorIndex)
        {
            switch (type)
            {
                case Types.Show:
                case Types.Highlight:
                    return GetFilterBackgroundColor(colorIndex);

                case Types.Truncated:
                    return ConsoleColor.DarkRed;

                default:
                    return DEFAULT_BACKGROUND;
            }
        }

        private static readonly object _writeLineLock = new object();

        private static void NewLine()
        {
            lock (_writeLineLock)
            {
                ResetConsoleColors();
                Console.WriteLine();
            }
        }

#pragma warning disable S3242 // Method parameters should be declared with base types

        private static void WriteLine(Line line, int fileIndex)
#pragma warning restore S3242 // Method parameters should be declared with base types
        {
            lock (_writeLineLock)
            {
                line.ForEach(x =>
                {
                    if (x.Type == Types.NewLine)
                    {
                        NewLine();
                    }
                    else
                    {
                        Console.BackgroundColor = TypeToBackgroundColor(x.Type, x.ColorIndex);
                        Console.ForegroundColor = TypeToForegroundColor(x.Type, fileIndex);
                        Console.Write(x.Text);
                    }
                });

                NewLine();
            }
        }

        private static void WriteMessage(string mess, Types type)
        {
            lock (_writeLineLock)
            {
                Console.ForegroundColor = TypeToForegroundColor(type, 0);
                Console.BackgroundColor = TypeToBackgroundColor(type, 0);
                Console.WriteLine(mess);
            }
        }
    }
}