using System.Collections.Generic;
using System.Linq;

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
    }
}
