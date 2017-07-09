﻿using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace TailP
{
    public static class RegexObjects
    {
        private static ConcurrentDictionary<string, Regex> _regexs = new ConcurrentDictionary<string, Regex>();
        public static Regex GetRegexObject(string filter, Func<Regex> createRegex)
        {
            Regex result;

            if (!_regexs.TryGetValue(filter, out result))
            {
                result = createRegex.Invoke();
                _regexs.TryAdd(filter, result);
            }

            return result;
        }

    }
}
