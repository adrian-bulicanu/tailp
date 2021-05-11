// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++ and C#: http://www.viva64.com

using System;
using System.Collections.Generic;

namespace tailp
{
    public static class Configs
    {
        public static bool Truncate { get; set; } // default to false
        public static bool ShowLineNumber { get; set; } // default to false
        public static string LogicalLineMarker { get; set; } = string.Empty;
        public static long StartLocation { get; set; } // default to 0
        public static StartLocationTypes StartLocationType { get; set; } = StartLocationTypes.B;

        // NOTE: temporary change the comparison options to OrdinalIgnoreCase,
        //       because the CurrentCultureIgnoreCase are extremely slow in .NET 5
        //       See coresponding issue: https://github.com/dotnet/runtime/issues/37951
        public static StringComparison ComparisonOptions { get; set; } = StringComparison.OrdinalIgnoreCase; // StringComparison.CurrentCultureIgnoreCase;

        public static bool AllFilters { get; set; } // default to false
        public static bool Regex { get; set; } // default to false
        public static bool Follow { get; set; } // default to false
        public static NumLinesStart LinesStartFrom { get; set; } = NumLinesStart.Begin;
        public static int LinesStartNumber { get; set; } // default to 0
        public static bool Recursive { get; set; } = true;

        public static int ContextAfter { get; set; } = -1;
        public static int ContextBefore { get; set; } = -1;
        public static int ContextLines => Math.Max(0, ContextBefore) + Math.Max(0, ContextAfter);
        public static bool IsContextBeforeUsed => ContextBefore > 0;
        private static bool IsContextAfterUsed => ContextAfter > 0;
        public static bool IsContextUsed => IsContextBeforeUsed || IsContextAfterUsed;

        public static ICollection<string> FiltersShow { get; } = new List<string>();
        public static ICollection<string> FiltersHide { get; } = new List<string>();
        public static ICollection<string> FiltersHighlight { get; } = new List<string>();

        private static readonly object ShowFileLocker = new object();
        private static bool? _showFile;

        public static bool ShowFile
        {
            get
            {
                lock (ShowFileLocker)
                {
                    return _showFile == true;
                }
            }
            set
            {
                lock (ShowFileLocker)
                {
                    _showFile = value;
                }
            }
        }

        public static bool IsShowFileDefined
        {
            get
            {
                lock (ShowFileLocker)
                {
                    return _showFile.HasValue;
                }
            }
        }
    }
}