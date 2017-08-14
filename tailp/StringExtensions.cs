// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++ and C#: http://www.viva64.com
using System;

namespace TailP
{
    public static class StringExtensions
    {
        public static string AppendFromRight(this string s, string toBeApend, int finalWidth)
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
}