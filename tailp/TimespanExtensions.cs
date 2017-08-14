// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++ and C#: http://www.viva64.com
using System;

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
}