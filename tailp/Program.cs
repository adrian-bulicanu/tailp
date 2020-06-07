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
        private const ConsoleColor DefaultBackground = ConsoleColor.Black;

        private const ConsoleColor DefaultForeground = ConsoleColor.Gray;

        private const int StatusTimerPeriodMs = 1000;

        private static Timer _statusTimer;
        private static long _lastPos;
        private static string _lastFileName;
        private static DateTime _lastChanged = DateTime.Now;

        private static string ProcessedBytesToString(long processed, long total)
        {
            if (total < 1024)
            {
                return $"{processed} of {total} bytes";
            }
            if (total < 1024 * 1024)
            {
                return
                    $"{Math.Round(100.0 * processed / 1024 / 100, 2)} of {Math.Round(100.0 * total / 1024 / 100, 2)} KiB";
            }
            if (total < 1024 * 1024 * 1024)
            {
                return
                    $"{Math.Round(100.0 * processed / 1024 / 1024 / 100, 2)} of {Math.Round(100.0 * total / 1024 / 1024 / 100, 2)} MiB";
            }

            return
                $"{Math.Round(100.0 * processed / 1024 / 1024 / 1024 / 100, 2)} of {Math.Round(100.0 * total / 1024 / 1024 / 1024 / 100, 2)} GiB";
        }

        private static void StartUpdatingStatus()
        {
            _statusTimer = new Timer(
                (_) => UpdateStatus(), null, 0, StatusTimerPeriodMs);
        }

        private static void UpdateProgressBar(byte percents)
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

        private static void UpdateStatus()
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
                    ? $"{totalFiles} files"
                    : $"{totalFiles - pendingFiles} of {totalFiles}";

                var title =
                    $"{ProcessedBytesToString(lastPos, totalSize)} | {GetEtaText(percents)} last processed: {Path.GetFileName(_bl.LastFileName)} {DateTime.Now.Subtract(_lastChanged).ToHumanReadableString()} ago ({files}) | ";

                Console.Title = title.AppendFromRight(
                    Path.GetFullPath(_bl.LastFileName!), Console.WindowWidth);

                UpdateProgressBar(percents);
            }
        }

        private static DateTime _lastCalculate = DateTime.UtcNow;
        private static byte _lastPercents = 100;
        private static readonly Queue<double> SecondsPerPercent = new Queue<double>();
        private const int SamplesCount = 5;

        private static string GetEtaText(byte percents)
        {
            if (_lastPercents != percents)
            {
                if (_lastPercents < percents)
                {
                    var speed =
                    1.0 * (DateTime.UtcNow - _lastCalculate).TotalSeconds
                    / (percents - _lastPercents); //-V3064

                    SecondsPerPercent.Enqueue(speed);
                }

                _lastPercents = percents;
                _lastCalculate = DateTime.UtcNow;
            }

            while (SecondsPerPercent.Count > SamplesCount)
            {
                SecondsPerPercent.Dequeue();
            }

            var estimated = SecondsPerPercent.Count == SamplesCount
                ? TimeSpan.FromSeconds(
                    Math.Round(SecondsPerPercent.Average() * (100 - percents))
                    )
                : TimeSpan.FromSeconds(0);

            if (estimated > TimeSpan.FromSeconds(5))
            {
                return $"ETA: {estimated.ToHumanReadableString()} |";
            }

            if (estimated > TimeSpan.FromSeconds(0))
            {
                return "ETA: almost done |";
            }

            return string.Empty;
        }

        private static void ResetConsoleColors()
        {
            Console.BackgroundColor = DefaultBackground;
            Console.ForegroundColor = DefaultForeground;
        }

        private static TailPbl _bl;

        private static void Main(string[] args)
        {
            // reset colors on Ctrl+C
            Console.CancelKeyPress += (s, a) => ResetConsoleColors();

            NewLine();

            _bl = new TailPbl(WriteLine);
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

        private static readonly ConsoleColor[] AvailableFilterColors = new[]
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

        private static readonly Dictionary<int, ConsoleColor> FilterColors = new Dictionary<int, ConsoleColor>();
        private static int _lastFilterColor = AvailableFilterColors.Length;

        private static ConsoleColor GetFilterBackgroundColor(int colorIndex)
        {
            if (!FilterColors.TryGetValue(colorIndex, out var color))
            {
                ++_lastFilterColor;
                if (_lastFilterColor >= AvailableFilterColors.Length)
                {
                    _lastFilterColor = 0;
                }
                color = AvailableFilterColors[_lastFilterColor];
                FilterColors.Add(colorIndex, color);
            }
            return color;
        }

        private static readonly ConsoleColor[] AvailableFilesColors = new[]
        {
            ConsoleColor.Gray,
            ConsoleColor.Yellow,
            ConsoleColor.Green,
            ConsoleColor.Cyan,
            ConsoleColor.Magenta,
        };

        private static readonly Dictionary<int, ConsoleColor> FileColors = new Dictionary<int, ConsoleColor>();
        private static int _lastFileColor = AvailableFilesColors.Length;

        private static ConsoleColor GetFileForegroundColor(int fileIndex)
        {
            if (!FileColors.TryGetValue(fileIndex, out var color))
            {
                ++_lastFileColor;
                if (_lastFileColor >= AvailableFilesColors.Length)
                {
                    _lastFileColor = 0;
                }
                color = AvailableFilesColors[_lastFileColor];
                FileColors.Add(fileIndex, color);
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
                                : DefaultForeground;
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
                    return DefaultBackground;
            }
        }

        private static readonly object WriteLineLock = new object();

        private static void NewLine()
        {
            lock (WriteLineLock)
            {
                ResetConsoleColors();
                Console.WriteLine();
            }
        }

#pragma warning disable S3242 // Method parameters should be declared with base types

        private static void WriteLine(Line line, int fileIndex)
#pragma warning restore S3242 // Method parameters should be declared with base types
        {
            lock (WriteLineLock)
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
            lock (WriteLineLock)
            {
                Console.ForegroundColor = TypeToForegroundColor(type, 0);
                Console.BackgroundColor = TypeToBackgroundColor(type, 0);
                Console.WriteLine(mess);
            }
        }
    }
}