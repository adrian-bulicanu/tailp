using System;
using System.Collections.Generic;

namespace TailP
{
    public class LogicalLinesHistory : Queue<LogicalLine>
    {
        public int Limit { get; private set; }

        public LogicalLinesHistory(int limit) : base()
        {
            if (limit < 1)
            {
                throw new ArgumentException("must be >= 1", "limit");
            }

            Limit = limit;
        }

        public LogicalLinesHistory() : base()
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

        public void Enqueue(LogicalLinesHistory history)
        {
            foreach(var line in history)
            {
                Enqueue(line);
            }
        }

        public void ReplaceBy(LogicalLinesHistory history)
        {
            Clear();
            Enqueue(history);
        }
    }
}
