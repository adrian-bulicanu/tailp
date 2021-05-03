using System;
using System.Text.RegularExpressions;

namespace tailp
{
    public static class Matcher
    {
        /// <summary>
        /// Perform a string comparison or a regex match and returns first found token index and length
        /// </summary>
        /// <param name="text"></param>
        /// <param name="filterText"></param>
        /// <returns>index and length of found token in text. (-1, N) if token is not found</returns>
        public static Tuple<int, int> GetMatchTextIndex(string text, string filterText)
        {
            if (text is null) throw new ArgumentNullException(nameof(text));
            if (string.IsNullOrEmpty(filterText)) throw new ArgumentException(@"must be non empty", nameof(filterText));

            if (Configs.Regex)
            {
                var regex = RegexObjects.GetRegexObject(filterText,
                    () => new Regex(filterText, RegexOptions.Compiled));
                var matches = regex.Matches(text);

                return matches.Count > 0 ?
                    new Tuple<int, int>(matches[0].Index, matches[0].Length) :
                    new Tuple<int, int>(-1, 0);
            }

            return new Tuple<int, int>(
                text.IndexOf(filterText, 0, Configs.ComparisonOptions),
                filterText.Length);
        }
    }
}