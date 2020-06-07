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

        public bool IsEmpty => !this.Any();

        public int LineNumber => IsEmpty ? 0 : this.First().LineNumber;

        public int FoundShowFiltersCount =>
            this.SelectMany(x => x.FoundShowFilters.Select(y => y))
                .Distinct()
                .Count();

        public int FoundHideFiltersCount =>
            this.SelectMany(x => x.FoundHideFilters.Select(y => y))
                .Distinct()
                .Count();

        public bool IsShowedFlagExists => this.Any(x => x.IsShowed);
        public bool IsHidedFlagExists => this.Any(x => x.IsHided);

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