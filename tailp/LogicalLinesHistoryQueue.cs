// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++ and C#: http://www.viva64.com

using System;
using System.Collections.Generic;

namespace tailp
{
#pragma warning disable CA1010 // Generic interface should also be implemented
    public class LogicalLinesHistoryQueue : Queue<LogicalLine>
#pragma warning restore CA1010 // Generic interface should also be implemented
    {
        private int Limit { get; }

        public LogicalLinesHistoryQueue(int limit)
        {
            if (limit < 1)
            {
                throw new ArgumentException(@"must be >= 1", nameof(limit));
            }

            Limit = limit;
        }

        public LogicalLinesHistoryQueue()
        {
            Limit = int.MaxValue;
        }

        public new void Enqueue(LogicalLine item)
        {
            while (Count >= Limit)
            {
                Dequeue();
            }
            base.Enqueue(item);
        }

        public void Enqueue(LogicalLinesHistoryQueue historyQueue)
        {
            if (historyQueue is null) throw new ArgumentNullException(nameof(historyQueue));

            foreach (var line in historyQueue)
            {
                Enqueue(line);
            }
        }

        public void ReplaceBy(LogicalLinesHistoryQueue historyQueue)
        {
            Clear();
            Enqueue(historyQueue);
        }

        public void SetLinesNumberToUnknown()
        {
            foreach (var line in this)
            {
                line.SetLinesNumberToUnknown();
            }
        }
    }
}