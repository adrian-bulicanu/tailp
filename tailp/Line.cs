using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace TailP
{
    public class Line : List<Token>
    {
        private readonly string TAB_REPLACER = "    ";
        private readonly string LINE_NUMBER_FORMAT = "{0:D8} ";
        private readonly string LINE_NUMBER_PADDING = "         ";
        private readonly string LINE_NUMBER_UNKNOWN = " unknown ";

        // NOTE: unit tests to be edited if markers length is changed!
        public readonly string TRUNCATED_MARKER_END = ">";
        public readonly string TRUNCATED_MARKER_MIDDLE = "<...>";

        private HashSet<int> _foundShowFilters = new HashSet<int>();
        public HashSet<int> FoundShowFilters
        {
            get
            {
                return _foundShowFilters;
            }
        }

        private HashSet<int> _foundHideFilters = new HashSet<int>();
        public HashSet<int> FoundHideFilters
        {
            get
            {
                return _foundHideFilters;
            }
        }

        public StringComparison Comparison { get; set; }
        public bool UseRegex { get; set; }
        public bool IsLogicalContinuation { get; private set; }
        public int LineNumber { get; private set; }

        public Line() : base()
        {
        }

        public Line(Line other, bool copyTokens = true): base()
        {
            Comparison = other.Comparison;
            UseRegex = other.UseRegex;
            IsLogicalContinuation = other.IsLogicalContinuation;
            LineNumber = other.LineNumber;
            if (copyTokens)
            {
                other.AddRange(this);
            }
        }

        public Line(string s, StringComparison comparison, bool useRegex,
            bool isLogicalContinuation, int lineNumber)
        {
            Add(new Token(Types.None, s.Replace("\t", TAB_REPLACER)));
            Comparison = comparison;
            UseRegex = useRegex;
            IsLogicalContinuation = isLogicalContinuation;
            LineNumber = lineNumber;
        }

        /// <summary>
        /// Returns total length of text
        /// </summary>
        public int Length
        {
            get
            {
                return this.Sum(x => x.Text.Length);
            }
        }

        public bool IsShowed
        {
            get
            {
                return _foundShowFilters.Any();
            }
        }

        public bool IsHided
        {
            get
            {
                return _foundHideFilters.Any();
            }
        }

        /// <summary>
        /// Analog to string.Substring
        /// </summary>
        /// <param name="start">index, from 0</param>
        /// <param name="length">max(length, available) chars will be returned</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException">if start is an invalid value</exception>
        public Line Substring(int start, int length = int.MaxValue)
        {
            if (start < 0 || start >= this.Length)
            {
                throw new ArgumentOutOfRangeException("start");
            }

            Line result = new Line(this, false);

            foreach (var item in this)
            {
                var s = item.Text;

                if (start < s.Length)
                {
                    s = s.Substring(start, Math.Min(length, s.Length - start));
                    result.Add(new Token(item.Type, s, item.ColorIndex));

                    length -= s.Length;
                    if (length <= 0)
                    {
                        break;
                    }
                    start = 0;
                }
                else
                {
                    start = Math.Max(start - s.Length, 0);
                }
            }

            return result;
        }

        /// <summary>
        /// Perform a string comparison or a regex match and returns first found token index and length
        /// </summary>
        /// <param name="text"></param>
        /// <param name="filterText"></param>
        /// <returns>index and length of found token in text. (-1, 0) if token is not found</returns>
        private Tuple<int, int> GetMatchTextIndex(string text, string filterText)
        {
            if (UseRegex)
            {
                var regex = RegexObjects.GetRegexObject(filterText,
                    () => new Regex(filterText, RegexOptions.Compiled));
                var matches = regex.Matches(text);
                if (matches.Count > 0)
                {
                    return new Tuple<int, int>(matches[0].Index, matches[0].Length);
                }
                else
                {
                    return new Tuple<int, int>(-1, 0);
                }
            }
            else
            {
                return new Tuple<int, int>(
                    text.IndexOf(filterText, 0, Comparison),
                    filterText.Length);
            }
        }
        /// <summary>
        /// Check filters in Types.None items and modify list if filtered item found
        /// </summary>
        private void CheckFilter(string filterText, int filterIndex, Types type, int colorIndex, HashSet<int> foundFilters)
        {
            var currentList = new List<Token>(this);
            Clear();

            foreach (var item in currentList)
            {
                // search only in Types.None
                if (item.Type != Types.None)
                {
                    Add(item);
                    continue;
                }

                var s = item.Text;
                var match = GetMatchTextIndex(s, filterText);
                // while token is found in the string
                while (match.Item1 != -1)
                {
                    // skip text till found index
                    if (match.Item1 > 0)
                    {
                        var head = s.Substring(0, match.Item1);
                        Add(new Token(Types.None, head));
                    }

                    // change Type for found token
                    var h = s.Substring(match.Item1, match.Item2);
                    Add(new Token(type, h, colorIndex));
                    if (foundFilters != null)
                    {
                        foundFilters.Add(filterIndex);
                    }

                    // keep searching in remained text
                    s = s.Substring(match.Item1 + match.Item2,
                            s.Length - match.Item1 - match.Item2);
                    match = GetMatchTextIndex(s, filterText);
                }
                // do not forget to save not found tail
                if (!string.IsNullOrEmpty(s))
                {
                    Add(new Token(Types.None, s));
                }
            }
        }

        public void CheckFilters(List<string> showFilters, List<string> hideFilters, List<string> highlightFilters)
        {
            for (int i = 0; i != hideFilters.Count; ++i)
            {
                CheckFilter(hideFilters[i], i, Types.Hide, 0, _foundHideFilters);
            }

            var colorIndex = 0;
            for (int i = 0; i != showFilters.Count; ++i)
            {
                CheckFilter(showFilters[i], i, Types.Show, colorIndex++, _foundShowFilters);
            }

            foreach (var filter in highlightFilters)
            {
                CheckFilter(filter, -1, Types.Highlight, colorIndex++, null);
            }
        }

        private bool TruncateFromEnd(int resultStringLength, bool force)
        {
            if (resultStringLength >= Length)
            {
                return true;
            }

            var remainder = Substring(resultStringLength - TRUNCATED_MARKER_END.Length);
            var canBeTruncated = !remainder.Any(x => x.Type != Types.None);
            if (force || canBeTruncated)
            {
                var result = Substring(0, resultStringLength - TRUNCATED_MARKER_END.Length);
                result.Add(new Token(Types.Truncated, TRUNCATED_MARKER_END));
                Clear();
                AddRange(result);
                return true;
            }
            else
            {
                return false;
            }
        }

        private void TruncateFromMiddle(int resultStringLength)
        {
            while (Length > resultStringLength)
            {
                // truncate the longest element
                int longestIndex = -1;
                int longestLength = -1;
                for (int i = 0; i != Count; ++i)
                {
                    if (this[i].Type == Types.None &&
                        this[i].Text.Length > longestLength)
                    {
                        longestIndex = i;
                        longestLength = this[i].Text.Length;
                    }
                }

                if (longestIndex < 0 ||
                    longestLength < TRUNCATED_MARKER_MIDDLE.Length + 1)
                {
                    return;
                }

                TruncateItem(Length - resultStringLength, longestIndex);
            }
        }

        private void TruncateItem(int toBeTruncated, int index)
        {
            var item = this[index];

            var finalLength = Math.Max(
                item.Text.Length - toBeTruncated, TRUNCATED_MARKER_MIDDLE.Length);
            var firstItemLength = Math.Max(
                (finalLength - TRUNCATED_MARKER_MIDDLE.Length) / 2, 0);
            var secondItemLength = finalLength - firstItemLength -
                                   TRUNCATED_MARKER_MIDDLE.Length;

            RemoveAt(index);

            if (firstItemLength > 0)
            {
                Insert(index++, new Token(
                    Types.None, item.Text.Substring(0, firstItemLength)));
            }

            Insert(index++, new Token(Types.Truncated, TRUNCATED_MARKER_MIDDLE));

            if (secondItemLength > 0)
            {
                Insert(index, new Token(
                    Types.None, item.Text.Substring(item.Text.Length - secondItemLength)));
            }
        }

        public void SetLineNumberToUnknown()
        {
            ForEach(x =>
            {
                if (x.Type == Types.LineNumber && x.Text != LINE_NUMBER_PADDING)
                {
                    x.Text = LINE_NUMBER_UNKNOWN;
                }
            });
        }

        public void AddLineNumber()
        {
            Insert(0, new Token(Types.LineNumber,
                IsLogicalContinuation ? LINE_NUMBER_PADDING :
                    string.Format(LINE_NUMBER_FORMAT, LineNumber)));
        }

        public void Truncate(int resultStringLength)
        {
            if (TruncateFromEnd(resultStringLength, false))
            {
                return;
            }

            TruncateFromMiddle(resultStringLength);

            // after middle truncate, length may still remains too large
            TruncateFromEnd(resultStringLength, true);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            ForEach(x =>
            {
                sb.Append(x);
            });

            return sb.ToString();
        }
    }
}
