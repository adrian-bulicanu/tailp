// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++ and C#: http://www.viva64.com

using System;

namespace tailp
{
    public static class StringExtensions
    {
        public static string AppendFromRight(this string s, string toBeAppend, int finalWidth)
        {
            if (s is null) throw new ArgumentNullException(nameof(s));
            if (toBeAppend is null) throw new ArgumentNullException(nameof(toBeAppend));

            var remains = finalWidth - s.Length;
            if (remains <= 0) return s;
            
            var index = Math.Max(toBeAppend.Length - remains, 0);

            if (index > 0)
            {
                s += "...";
            }

            s += toBeAppend[index..];
            return s;
        }
    }
}