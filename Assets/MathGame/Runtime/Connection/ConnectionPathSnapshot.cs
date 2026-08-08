using System;
using System.Collections.Generic;

namespace MathGame.Connection
{
    public sealed class ConnectionPathSnapshot
    {
        private readonly IReadOnlyList<ConnectionEntry> entries;

        internal ConnectionPathSnapshot(ConnectionEntry[] entries, long sum)
        {
            this.entries = Array.AsReadOnly(entries);
            Sum = sum;
        }

        public IReadOnlyList<ConnectionEntry> Entries => entries;
        public int Count => entries.Count;
        public long Sum { get; }
        public bool IsEmpty => Count == 0;
    }
}
