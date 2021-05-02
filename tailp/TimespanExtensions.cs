// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++ and C#: http://www.viva64.com

using System;

namespace tailp
{
    public static class TimespanExtensions
    {
        // for idea - thanks to http://stackoverflow.com/questions/16689468/how-to-produce-human-readable-strings-to-represent-a-timespan/21649465
        public static string ToHumanReadableString(this TimeSpan t)
        {
            if (t.TotalMinutes < 1)
            {
                return $"{(int) t.TotalSeconds} second(s)";
            }
            if (t.TotalHours < 1)
            {
                return $"{(int) t.TotalMinutes}:{t.Seconds:00} minute(s)";
            }
            if (t.TotalDays < 1)
            {
                return $"{(int) t.TotalHours}:{t.Minutes:00} hour(s)";
            }
            if (t.TotalDays < 2)
            {
                return $"over {(int) t.TotalHours} hour(s)";
            }

            return $"over {(int) t.TotalDays} day(s)";
        }
    }
}