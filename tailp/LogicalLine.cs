// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++ and C#: http://www.viva64.com
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TailP
{
    public class LogicalLine : List<Line>
    {
        public bool IsVisible { get; set; }
        public bool IsPrinted { get; set; }

        public bool IsEmpty
        {
            get
            {
                return !this.Any();
            }
        }

        public int LineNumber
        {
            get
            {
                if (IsEmpty)
                {
                    return 0;
                }
                else
                {
                    return this.First().LineNumber;
                }
            }
        }

        public bool IsFilterFound
        {
            get
            {
                return this.Any(x => x.FoundShowFilters.Any());
            }
        }

        public int FoundShowFiltersCount
        {
            get
            {
                var foundFilters = new HashSet<int>();
                foreach (var line in this)
                {
                    foundFilters.UnionWith(line.FoundShowFilters);
                }
                return foundFilters.Count;
            }
        }

        public int FoundHideFiltersCount
        {
            get
            {
                var foundFilters = new HashSet<int>();
                foreach (var line in this)
                {
                    foundFilters.UnionWith(line.FoundHideFilters);
                }
                return foundFilters.Count;
            }
        }

        public bool IsShowedFlagExists
        {
            get
            {
                return this.Any(x => x.IsShowed);
            }
        }

        public bool IsHidedFlagExists
        {
            get
            {
                return this.Any(x => x.IsHided);
            }
        }

        public void SetLinesNumberToUnknown()
        {
            ForEach(x => x.SetLineNumberToUnknown());
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            ForEach(x =>
            {
                sb.Append(x);
                sb.AppendLine();
            });

            sb.AppendFormat(@"visible={0}, printed={1}", IsVisible, IsPrinted);

            return sb.ToString();
        }
    }
}
