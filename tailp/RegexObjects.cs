// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++ and C#: http://www.viva64.com

using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace tailp
{
    public static class RegexObjects
    {
        private static readonly ConcurrentDictionary<string, Regex> Regexs =
            new ConcurrentDictionary<string, Regex>();

        public static Regex GetRegexObject(string filter, Func<Regex> createRegex)
        {
            if (createRegex is null) throw new ArgumentNullException(nameof(createRegex));

            if (Regexs.TryGetValue(filter, out var result))
            {
                return result;
            }

            result = createRegex.Invoke();
            Regexs.TryAdd(filter, result);

            return result;
        }
    }
}