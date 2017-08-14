// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++ and C#: http://www.viva64.com
using System;
using System.Text.RegularExpressions;

namespace TailP
{
    public static class Utils
    {
        public static bool IsMatchMask(string path, string mask)
        {
            if (string.IsNullOrEmpty(mask))
            {
                return true;
            }

            if (path.Length < mask.Length)
            {
                return false;
            }

            var normalizedMask = mask.Replace('\\', '/').ToUpper();
            var normalizedPath = path.Replace('\\', '/').ToUpper();

            // regex is slow, tries string comparison first
            if (normalizedMask.IndexOfAny(new char[] { '*', '?' }) == -1)
            {
                return normalizedPath.IndexOf(normalizedMask, StringComparison.CurrentCulture)
                    == normalizedPath.Length - normalizedMask.Length;
            }

            var rg = RegexObjects.GetRegexObject(normalizedMask,
                () => new Regex(normalizedMask
                    .Replace(@".", @"[.]")
                    .Replace(@"*", @".*")
                    .Replace(@"?", @".")
                    , RegexOptions.Compiled));

            var matches = rg.Matches(normalizedPath);

            // check that no chars excepting wildcards remains after last math
            if (matches.Count > 0)
            {
                var lastMatch = matches[matches.Count - 1];
                var lastCharPos = lastMatch.Index + lastMatch.Length;
                for(int i = lastCharPos; i != normalizedPath.Length; ++i)
                {
                    if (normalizedPath[i] != '*' && normalizedPath[i] != '?')
                    {
                        return false;
                    }
                }
            }

            return matches.Count > 0;
        }
    }
}
