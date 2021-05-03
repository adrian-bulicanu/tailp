// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++ and C#: http://www.viva64.com

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace tailp
{
    public class Line : List<Token>
    {
        public HashSet<int> FoundShowFilters { get; } = new HashSet<int>();
        public HashSet<int> FoundHideFilters { get; } = new HashSet<int>();
        public int LineNumber { get; }

        private readonly bool _isLogicalContinuation;

        public Line()
        {
        }

        private Line(Line other, bool copyTokens = true)
        {
            _isLogicalContinuation = other._isLogicalContinuation;
            LineNumber = other.LineNumber;
            if (copyTokens)
            {
                other.AddRange(this);
            }
        }

        public Line(string s, bool isLogicalContinuation, int lineNumber)
        {
            if (s is null) throw new ArgumentNullException(nameof(s));

            Add(new Token(Types.None, s.Replace("\t", Constants.TAB_REPLACER, StringComparison.Ordinal)));
            _isLogicalContinuation = isLogicalContinuation;
            LineNumber = lineNumber;
        }

        /// <summary>
        /// Returns total length of text
        /// </summary>
        private int Length => this.Sum(x => x.Text.Length);

#pragma warning disable RCS1080 // Use 'Count/Length' property instead of 'Any' method.
        public bool IsShowed => FoundShowFilters.Any();
        public bool IsHided => FoundHideFilters.Any();
#pragma warning restore RCS1080 // Use 'Count/Length' property instead of 'Any' method.

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
                throw new ArgumentOutOfRangeException(nameof(start));
            }

            var result = new Line(this, false);

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
        /// Check filters in Types.None items and modify list if filtered item found
        /// </summary>
        private void CheckFilter(string filterText, int filterIndex, Types type, int colorIndex, ISet<int> foundFilters)
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
                var match = Matcher.GetMatchTextIndex(s, filterText);
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
                    foundFilters?.Add(filterIndex);

                    // keep searching in remained text
                    s = s.Substring(match.Item1 + match.Item2,
                            s.Length - match.Item1 - match.Item2);
                    match = Matcher.GetMatchTextIndex(s, filterText);
                }
                // do not forget to save not found tail
                if (!string.IsNullOrEmpty(s))
                {
                    Add(new Token(Types.None, s));
                }
            }
        }

        public void CheckFilters(IEnumerable<string> showFilters,
            IEnumerable<string> hideFilters, IEnumerable<string> highlightFilters)
        {
            if (hideFilters is null) throw new ArgumentNullException(nameof(hideFilters));
            if (showFilters is null) throw new ArgumentNullException(nameof(showFilters));
            if (highlightFilters is null) throw new ArgumentNullException(nameof(highlightFilters));

            var index = 0;
            foreach (var filter in hideFilters)
            {
                CheckFilter(filter, index++, Types.Hide, 0, FoundHideFilters);
            }

            var colorIndex = 0;
            index = 0;
            foreach (var filter in showFilters)
            {
                CheckFilter(filter, index++, Types.Show, colorIndex++, FoundShowFilters);
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

            var remainder = Substring(resultStringLength - Constants.TRUNCATED_MARKER_END.Length);
            var canBeTruncated = remainder.All(x => x.Type == Types.None);

            if (force || canBeTruncated)
            {
                var result = Substring(0, resultStringLength - Constants.TRUNCATED_MARKER_END.Length);
                result.Add(new Token(Types.Truncated, Constants.TRUNCATED_MARKER_END));
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
                var longestIndex = -1;
                var longestLength = -1;
                for (var i = 0; i != Count; ++i)
                {
                    if (this[i].Type == Types.None
                        && this[i].Text.Length > longestLength)
                    {
                        longestIndex = i;
                        longestLength = this[i].Text.Length;
                    }
                }

                if (longestIndex < 0
                    || longestLength < Constants.TRUNCATED_MARKER_MIDDLE.Length + 1)
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
                item.Text.Length - toBeTruncated, Constants.TRUNCATED_MARKER_MIDDLE.Length);
            var firstItemLength = Math.Max(
                (finalLength - Constants.TRUNCATED_MARKER_MIDDLE.Length) / 2, 0);
            var secondItemLength = finalLength - firstItemLength
                                   - Constants.TRUNCATED_MARKER_MIDDLE.Length;

            RemoveAt(index);

            if (firstItemLength > 0)
            {
                Insert(index++, new Token(
                    Types.None, item.Text.Substring(0, firstItemLength)));
            }

            Insert(index++, new Token(Types.Truncated, Constants.TRUNCATED_MARKER_MIDDLE));

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
                if (x.Type == Types.LineNumber && x.Text != Constants.LINE_NUMBER_PADDING)
                {
                    x.Text = Constants.LINE_NUMBER_UNKNOWN;
                }
            });
        }

        public void AddLineNumber()
        {
            Insert(0, new Token(Types.LineNumber,
                _isLogicalContinuation ? Constants.LINE_NUMBER_PADDING :
                    string.Format(CultureInfo.InvariantCulture, Constants.LINE_NUMBER_FORMAT, LineNumber)));
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

            ForEach(x => sb.Append(x));

            return sb.ToString();
        }
    }
}