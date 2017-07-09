using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Linq;

namespace TailP
{
    public static class TimespanExtensions
    {
        // for idea - thanks to http://stackoverflow.com/questions/16689468/how-to-produce-human-readable-strings-to-represent-a-timespan/21649465
        public static string ToHumanReadableString(this TimeSpan t)
        {
            if (t.TotalMinutes < 1)
            {
                return string.Format("{0} second(s)", (int)t.TotalSeconds);
            }
            if (t.TotalHours < 1)
            {
                return string.Format("{0}:{1:00} minute(s)", (int)t.TotalMinutes, t.Seconds);
            }
            if (t.TotalDays < 1)
            {
                return string.Format("{0}:{1:00} hour(s)", (int)t.TotalHours, t.Minutes);
            }
            if (t.TotalDays < 2)
            {
                return string.Format("over {0} hour(s)", (int)t.TotalHours);
            }

            return string.Format("over {0} day(s)", (int)t.TotalDays);
        }
    }

    public static class StringExtensions
    {
        public static string AppendFromRight(this string s,
            string toBeApend, int finalWidth)
        {
            var remains = finalWidth - s.Length;
            if (remains > 0)
            {
                var index = Math.Max(toBeApend.Length - remains, 0);

                if (index > 0 )
                {
                    s += "...";
                }

                s += toBeApend.Substring(index);
            }
            return s;
        }
    }

    class Program
    {
        // used hardcoded colors instead of default, because on Ctrl-C color changes to last used
        static private readonly ConsoleColor DEFAULT_BACKGROUND = ConsoleColor.Black;// Console.BackgroundColor;
        static private readonly ConsoleColor DEFAULT_FOREGROUND = ConsoleColor.Gray;// Console.ForegroundColor;

        static private readonly int STATUS_TIMER_PERIOD_MS = 1000;

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
                (s) =>
                {
                    UpdateStatus();
                }, null, 0, STATUS_TIMER_PERIOD_MS);
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
                var files = _bl.Follow
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
        static private Queue<double> _secondsPerPercent = new Queue<double>();
        static readonly int SAMPLES_COUNT = 5;
        private static string GetETAText(byte percents)
        {
            if (_lastPercents != percents)
            {
                if (_lastPercents < percents)
                {
                    double speed =
                    1.0 * (DateTime.UtcNow - _lastCalculate).TotalSeconds /
                    (percents - _lastPercents);

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

        static void Main(string[] args)
        {
            // reset colors on Ctrl+C
            Console.CancelKeyPress += (s, a) => { ResetConsoleColors(); };

            NewLine();

            _bl = new TailPBL((l, i) => WriteLine(l, i));
            try
            {
                _bl.ParseArgs(args);

                StartUpdatingStatus();

                _bl.StartProcess();

                if (_bl.Follow)
                {
                    Console.ReadLine();
                }

                _statusTimer.Dispose();
                UpdateStatus();
            }
            catch (TailPExceptionHelp)
            {
                WriteMessage(_bl.GetHelp(), Types.None);
            }
            catch (TailPExceptionArgs ex)
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

        static private ConsoleColor[] _availableFilterColors = new ConsoleColor[]
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
        static private Dictionary<int, ConsoleColor> _filterColors = new Dictionary<int, ConsoleColor>();
        static private int _lastFilterColor = _availableFilterColors.Length;
        static private ConsoleColor GetFilterBackgroundColor(int colorIndex)
        {
            ConsoleColor color;
            if (!_filterColors.TryGetValue(colorIndex, out color))
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

        static private ConsoleColor[] _availableFilesColors = new ConsoleColor[]
        {
            ConsoleColor.Gray,
            ConsoleColor.Yellow,
            ConsoleColor.Green,
            ConsoleColor.Cyan,
            ConsoleColor.Magenta,
        };
        static private Dictionary<int, ConsoleColor> _fileColors = new Dictionary<int, ConsoleColor>();
        static private int _lastFileColor = _availableFilesColors.Length;
        static private ConsoleColor GetFileForegroundColor(int fileIndex)
        {
            ConsoleColor color;
            if (!_fileColors.TryGetValue(fileIndex, out color))
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

        static ConsoleColor TypeToForegroundColor(Types type, int colorIndex, int fileIndex)
        {
            switch(type)
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
                    return _bl.Follow
                                ? GetFileForegroundColor(fileIndex)
                                : DEFAULT_FOREGROUND;
            }
        }

        static ConsoleColor TypeToBackgroundColor(Types type, int colorIndex, int fileIndex)
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

        static object _writeLineLock = new object();
        static void NewLine()
        {
            lock (_writeLineLock)
            {
                ResetConsoleColors();
                Console.WriteLine();
            }
        }

        static void WriteLine(Line line, int fileIndex)
        {
            lock (_writeLineLock)
            {
                foreach (var item in line)
                {
                    if (item.Type == Types.NewLine)
                    {
                        NewLine();
                    }
                    else
                    {
                        Console.BackgroundColor = TypeToBackgroundColor(item.Type, item.ColorIndex, fileIndex);
                        Console.ForegroundColor = TypeToForegroundColor(item.Type, item.ColorIndex, fileIndex);
                        Console.Write(item.Text);
                    }
                }

                NewLine();
            }
        }

        static void WriteMessage(string mess, Types type)
        {
            lock (_writeLineLock)
            {

                Console.ForegroundColor = TypeToForegroundColor(type, 0, 0);
                Console.BackgroundColor = TypeToBackgroundColor(type, 0, 0);
                Console.WriteLine(mess);
            }
        }
    }
}