// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++ and C#: http://www.viva64.com
using System;

namespace TailP
{
    public static class Constants
    {
        private static int _maxWidth = 0;
        public static int MAX_WIDTH
        {
            get
            {
                if (_maxWidth == 0)
                {
                    _maxWidth = Math.Max(1, Console.BufferWidth);
                }

                return _maxWidth;
            }
        }

        // recheck file period if no file watch events received
        public static readonly TimeSpan FORCE_DETECT_PERIOD = TimeSpan.FromMilliseconds(500);

        public static readonly int MAX_PUSH_PROCESS_COUNT = 10;

        // delay before processing line if no new lines found
        public static readonly TimeSpan LOGICAL_LINE_DELAY = TimeSpan.FromMilliseconds(250);

        // waiting on file monitoring errors
        public static readonly TimeSpan WAIT_ON_ERROR = TimeSpan.FromSeconds(1);

        // using for reverse processing of file to find last lines
        public static readonly int REVERS_SEARCH_PAGE_SIZE = 1024 * 1024; // 1MiB

        public static readonly string FILENAME_PRINT_FORMAT = @"==> {0} <==";
        public static readonly string HELP_VERSION_HEADER = @"VERSION";
        public static readonly string HELP_VERSION_FORMAT = @"    {0} {1} / {2}";
        public static readonly string CONTEXT_LINE_DELIMITER = @"--";

        public static readonly string TAB_REPLACER = "    ";
        public static readonly string LINE_NUMBER_FORMAT = "{0:D8} ";
        public static readonly string LINE_NUMBER_PADDING = "         ";
        public static readonly string LINE_NUMBER_UNKNOWN = " unknown ";

        // NOTE: unit tests to be edited if markers length is changed!
        public static readonly string TRUNCATED_MARKER_END = ">";
        public static readonly string TRUNCATED_MARKER_MIDDLE = "<...>";
    }
}
