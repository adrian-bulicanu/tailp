// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++ and C#: http://www.viva64.com
using System;
using System.Collections.Generic;

namespace TailP
{
    public static class Configuration
    {
        public static bool Truncate { get; set; } = false;
        public static bool ShowLineNumber { get; set; } = false;
        public static string LogicalLineMarker { get; set; } = string.Empty;
        public static long StartLocation { get; set; } = 0;
        public static StartLocationTypes StartLocationType { get; set; } = StartLocationTypes.b;
        public static StringComparison ComparisonOptions { get; set; } = StringComparison.InvariantCultureIgnoreCase;
        public static bool AllFilters { get; set; } = false;
        public static bool Regex { get; set; } = false;
        public static bool Follow { get; set; } = false;
        public static NumLinesStart LinesStartFrom { get; set; } = NumLinesStart.begin;
        public static int LinesStartNumber { get; set; } = 0;
        public static bool Recursive { get; set; } = true;

        public static int ContextAfter { get; set; } = -1;
        public static int ContextBefore { get; set; } = -1;
        public static int ContextLines => Math.Max(0, ContextBefore) + Math.Max(0, ContextAfter);
        public static bool IsContextBeforeUsed => ContextBefore > 0;
        public static bool IsContextAfterUsed => ContextAfter > 0;
        public static bool IsContextUsed => IsContextBeforeUsed || IsContextAfterUsed;

        public static List<string> FiltersShow { get; } = new List<string>();
        public static List<string> FiltersHide { get; } = new List<string>();
        public static List<string> FiltersHighlight { get; } = new List<string>();

        private static readonly object _showFileLocker = new object();
        private static bool? _showFile;

        public static bool ShowFile
        {
            get
            {
                lock (_showFileLocker)
                {
                    return _showFile == true;
                }
            }
            set
            {
                lock (_showFileLocker)
                {
                    _showFile = value;
                }
            }
        }

        public static bool IsShowFileDefined
        {
            get
            {
                lock (_showFileLocker)
                {
                    return _showFile.HasValue;
                }
            }
        }

        static Configuration()
        {
            Recursive = true;
        }
    }
}