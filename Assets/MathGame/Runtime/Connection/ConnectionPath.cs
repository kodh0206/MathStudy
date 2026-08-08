using System;
using System.Collections.Generic;
using MathGame.Board;
using DomainBoard = MathGame.Board.Board;

namespace MathGame.Connection
{
    public sealed class ConnectionPath
    {
        private readonly DomainBoard board;
        private readonly List<ConnectionEntry> entries = new List<ConnectionEntry>();
        private readonly HashSet<BoardPosition> selectedPositions = new HashSet<BoardPosition>();
        private long sum;

        public ConnectionPath(DomainBoard board)
        {
            this.board = board ?? throw new ArgumentNullException(nameof(board));
        }

        public int Count => entries.Count;
        public long Sum => sum;
        public bool IsEmpty => entries.Count == 0;

        public bool Contains(BoardPosition position)
        {
            return selectedPositions.Contains(position);
        }

        public ConnectionStepResult TrySelect(BoardPosition position)
        {
            if (entries.Count >= 2 && entries[entries.Count - 2].Position == position)
            {
                var removed = entries[entries.Count - 1];
                entries.RemoveAt(entries.Count - 1);
                selectedPositions.Remove(removed.Position);
                sum -= removed.Block.Value;
                return ConnectionStepResult.Backtracked;
            }

            if (selectedPositions.Contains(position))
            {
                return ConnectionStepResult.AlreadySelected;
            }

            var lookup = board.TryGetCell(position, out var cell);
            if (lookup == CellLookupResult.OutOfBounds)
            {
                return ConnectionStepResult.OutOfBounds;
            }

            if (lookup == CellLookupResult.InactivePosition)
            {
                return ConnectionStepResult.InactivePosition;
            }

            if (cell.Access == CellAccess.Blocked)
            {
                return ConnectionStepResult.Blocked;
            }

            if (!cell.Block.HasValue)
            {
                return ConnectionStepResult.Empty;
            }

            if (entries.Count > 0 && !AreOrthogonallyAdjacent(entries[entries.Count - 1].Position, position))
            {
                return ConnectionStepResult.NotOrthogonallyAdjacent;
            }

            long nextSum;
            try
            {
                nextSum = checked(sum + cell.Block.Value.Value);
            }
            catch (OverflowException)
            {
                return ConnectionStepResult.SumOverflow;
            }

            entries.Add(new ConnectionEntry(position, cell.Block.Value));
            selectedPositions.Add(position);
            sum = nextSum;
            return ConnectionStepResult.Added;
        }

        public ConnectionCancelResult Cancel()
        {
            if (entries.Count == 0)
            {
                return ConnectionCancelResult.AlreadyEmpty;
            }

            entries.Clear();
            selectedPositions.Clear();
            sum = 0;
            return ConnectionCancelResult.Cleared;
        }

        public ConnectionPathSnapshot CreateSnapshot()
        {
            return new ConnectionPathSnapshot(entries.ToArray(), sum);
        }

        private static bool AreOrthogonallyAdjacent(BoardPosition first, BoardPosition second)
        {
            var columnDistance = Math.Abs((long)first.Column - second.Column);
            var rowDistance = Math.Abs((long)first.Row - second.Row);
            return columnDistance + rowDistance == 1;
        }
    }
}
