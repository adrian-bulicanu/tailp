// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++ and C#: http://www.viva64.com

using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace tailp
{
    public static class Utils
    {
        public static bool IsMatchMask(string path, string mask)
        {
            if (string.IsNullOrEmpty(mask))
            {
                return true;
            }

            if (path is null) throw new ArgumentNullException(nameof(path));

            if (path.Length < mask.Length)
            {
                return false;
            }

            var normalizedMask = mask.Replace('\\', '/').ToUpper(CultureInfo.InvariantCulture);
            var normalizedPath = path.Replace('\\', '/').ToUpper(CultureInfo.InvariantCulture);

            // regex is slow, tries string comparison first
            if (normalizedMask.IndexOfAny(new[] { '*', '?' }) == -1)
            {
                return normalizedPath.IndexOf(normalizedMask, StringComparison.InvariantCulture)
                    == normalizedPath.Length - normalizedMask.Length;
            }

            var rg = RegexObjects.GetRegexObject(normalizedMask,
                () => new Regex(normalizedMask
                    .Replace(@".", @"[.]", StringComparison.OrdinalIgnoreCase)
                    .Replace(@"*", @".*", StringComparison.OrdinalIgnoreCase)
                    .Replace(@"?", @".", StringComparison.OrdinalIgnoreCase)
                    , RegexOptions.Compiled));

            var matches = rg.Matches(normalizedPath);

            // check that no chars excepting wildcards remains after last math
            if (matches.Count <= 0)
            {
                return false;
            }

            var lastMatch = matches[^1];
            var lastCharPos = lastMatch.Index + lastMatch.Length;
            for (var i = lastCharPos; i != normalizedPath.Length; ++i)
            {
                if (normalizedPath[i] != '*' && normalizedPath[i] != '?')
                {
                    return false;
                }
            }

            return true;
        }
    }
}