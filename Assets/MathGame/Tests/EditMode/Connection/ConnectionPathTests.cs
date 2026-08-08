using System;
using System.Collections.Generic;
using MathGame.Board;
using MathGame.Connection;
using NUnit.Framework;
using DomainBoard = MathGame.Board.Board;

namespace MathGame.Tests.Connection
{
    public sealed class ConnectionPathTests
    {
        [Test]
        public void FirstSelectionCapturesPositionBlockAndSum()
        {
            var board = CreateFilledBoard(1, 1, 7);
            var path = new ConnectionPath(board);

            Assert.That(path.TrySelect(default), Is.EqualTo(ConnectionStepResult.Added));
            var snapshot = path.CreateSnapshot();
            Assert.That(snapshot.Count, Is.EqualTo(1));
            Assert.That(snapshot.Sum, Is.EqualTo(7));
            Assert.That(snapshot.Entries[0].Position, Is.EqualTo(default(BoardPosition)));
            Assert.That(snapshot.Entries[0].Block.Id, Is.EqualTo(new BlockId(1)));
            Assert.That(snapshot.Entries[0].Block.Value, Is.EqualTo(7));
            Assert.That(path.Contains(default), Is.True);
        }

        [Test]
        public void InvalidFirstSelectionsReturnStructuralReasonsAndRemainEmpty()
        {
            var topology = BoardTopology.CreateMasked(4, 1, new[]
            {
                new BoardPosition(0, 0), new BoardPosition(2, 0), new BoardPosition(3, 0)
            });
            var board = new DomainBoard(topology);
            board.TryPlaceBlock(new BoardPosition(3, 0), Block(1, 4));
            board.TrySetAccess(new BoardPosition(3, 0), CellAccess.Blocked);
            var path = new ConnectionPath(board);

            Assert.That(path.TrySelect(new BoardPosition(-1, 0)), Is.EqualTo(ConnectionStepResult.OutOfBounds));
            Assert.That(path.TrySelect(new BoardPosition(1, 0)), Is.EqualTo(ConnectionStepResult.InactivePosition));
            Assert.That(path.TrySelect(new BoardPosition(2, 0)), Is.EqualTo(ConnectionStepResult.Empty));
            Assert.That(path.TrySelect(new BoardPosition(3, 0)), Is.EqualTo(ConnectionStepResult.Blocked));
            Assert.That(path.IsEmpty, Is.True);
            Assert.That(path.Sum, Is.Zero);
        }

        [Test]
        public void AllFourOrthogonalDirectionsCanAppend()
        {
            var board = CreateFilledBoard(3, 3, 1);
            AssertDirection(board, new BoardPosition(1, 1), new BoardPosition(1, 2));
            AssertDirection(board, new BoardPosition(1, 1), new BoardPosition(2, 1));
            AssertDirection(board, new BoardPosition(1, 1), new BoardPosition(1, 0));
            AssertDirection(board, new BoardPosition(1, 1), new BoardPosition(0, 1));
        }

        [TestCase(1, 2)]
        [TestCase(2, 2)]
        [TestCase(2, 1)]
        public void DiagonalGapAndNonAdjacentSelectionsAreRejected(int column, int row)
        {
            var board = CreateFilledBoard(3, 3, 1);
            var path = new ConnectionPath(board);
            path.TrySelect(new BoardPosition(0, 1));

            Assert.That(
                path.TrySelect(new BoardPosition(column, row)),
                Is.EqualTo(ConnectionStepResult.NotOrthogonallyAdjacent));
            Assert.That(path.Count, Is.EqualTo(1));
            Assert.That(path.Sum, Is.EqualTo(1));
        }

        [Test]
        public void HoleReasonPrecedesAdjacency()
        {
            var topology = BoardTopology.CreateMasked(3, 1, new[]
            {
                new BoardPosition(0, 0), new BoardPosition(2, 0)
            });
            var board = new DomainBoard(topology);
            board.TryPlaceBlock(new BoardPosition(0, 0), Block(1, 2));
            board.TryPlaceBlock(new BoardPosition(2, 0), Block(2, 2));
            var path = new ConnectionPath(board);
            path.TrySelect(new BoardPosition(0, 0));

            Assert.That(path.TrySelect(new BoardPosition(1, 0)), Is.EqualTo(ConnectionStepResult.InactivePosition));
            Assert.That(path.TrySelect(new BoardPosition(2, 0)), Is.EqualTo(ConnectionStepResult.NotOrthogonallyAdjacent));
        }

        [Test]
        public void DuplicateValuesWithDistinctIdsAreAllowedAndSummed()
        {
            var board = CreateFilledBoard(2, 1, 9);
            var path = new ConnectionPath(board);

            Assert.That(path.TrySelect(new BoardPosition(0, 0)), Is.EqualTo(ConnectionStepResult.Added));
            Assert.That(path.TrySelect(new BoardPosition(1, 0)), Is.EqualTo(ConnectionStepResult.Added));
            Assert.That(path.Sum, Is.EqualTo(18));
            Assert.That(path.CreateSnapshot().Entries[0].Block.Id, Is.Not.EqualTo(path.CreateSnapshot().Entries[1].Block.Id));
        }

        [Test]
        public void ImmediatePredecessorBacktracksOneEntryAtATimeAndTailCanBeReselected()
        {
            var board = CreateFilledBoard(3, 1, 2);
            var path = new ConnectionPath(board);
            path.TrySelect(new BoardPosition(0, 0));
            path.TrySelect(new BoardPosition(1, 0));
            path.TrySelect(new BoardPosition(2, 0));

            Assert.That(path.TrySelect(new BoardPosition(1, 0)), Is.EqualTo(ConnectionStepResult.Backtracked));
            Assert.That(path.Count, Is.EqualTo(2));
            Assert.That(path.Contains(new BoardPosition(2, 0)), Is.False);
            Assert.That(path.TrySelect(new BoardPosition(2, 0)), Is.EqualTo(ConnectionStepResult.Added));
            Assert.That(path.TrySelect(new BoardPosition(1, 0)), Is.EqualTo(ConnectionStepResult.Backtracked));
            Assert.That(path.TrySelect(new BoardPosition(0, 0)), Is.EqualTo(ConnectionStepResult.Backtracked));
            Assert.That(path.Count, Is.EqualTo(1));
            Assert.That(path.Sum, Is.EqualTo(2));
        }

        [Test]
        public void TailAndNonPredecessorDuplicatesAreRejectedWithoutMutation()
        {
            var board = CreateFilledBoard(2, 2, 3);
            var path = new ConnectionPath(board);
            path.TrySelect(new BoardPosition(0, 0));
            path.TrySelect(new BoardPosition(1, 0));
            path.TrySelect(new BoardPosition(1, 1));
            var before = path.CreateSnapshot();

            Assert.That(path.TrySelect(new BoardPosition(1, 1)), Is.EqualTo(ConnectionStepResult.AlreadySelected));
            Assert.That(path.TrySelect(new BoardPosition(0, 0)), Is.EqualTo(ConnectionStepResult.AlreadySelected));
            Assert.That(path.Count, Is.EqualTo(before.Count));
            Assert.That(path.Sum, Is.EqualTo(before.Sum));
        }

        [Test]
        public void BacktrackingUsesCapturedEntryEvenIfBoardChanged()
        {
            var board = CreateFilledBoard(2, 1, 4);
            var path = new ConnectionPath(board);
            path.TrySelect(new BoardPosition(0, 0));
            path.TrySelect(new BoardPosition(1, 0));
            board.TryRemoveBlock(new BoardPosition(1, 0), out _);

            Assert.That(path.TrySelect(new BoardPosition(0, 0)), Is.EqualTo(ConnectionStepResult.Backtracked));
            Assert.That(path.Count, Is.EqualTo(1));
            Assert.That(path.Sum, Is.EqualTo(4));
        }

        [Test]
        public void EntryRetainsCapturedIdentityAfterBoardCellIsReplaced()
        {
            var board = CreateFilledBoard(1, 1, 6);
            var path = new ConnectionPath(board);
            path.TrySelect(default);
            var captured = path.CreateSnapshot();

            board.TryRemoveBlock(default, out _);
            board.TryPlaceBlock(default, Block(99, 8));

            Assert.That(captured.Entries[0].Block.Id, Is.EqualTo(new BlockId(1)));
            Assert.That(captured.Entries[0].Block.Value, Is.EqualTo(6));
            Assert.That(path.CreateSnapshot().Entries[0].Block.Id, Is.EqualTo(new BlockId(1)));
            Assert.That(board.TryGetCell(default, out var current), Is.EqualTo(CellLookupResult.Succeeded));
            Assert.That(current.Block.Value.Id, Is.EqualTo(new BlockId(99)));
        }

        [Test]
        public void CancelIsIdempotentClearsMembershipAndAllowsReuse()
        {
            var board = CreateFilledBoard(2, 1, 5);
            var path = new ConnectionPath(board);
            path.TrySelect(default);

            Assert.That(path.Cancel(), Is.EqualTo(ConnectionCancelResult.Cleared));
            Assert.That(path.Cancel(), Is.EqualTo(ConnectionCancelResult.AlreadyEmpty));
            Assert.That(path.IsEmpty, Is.True);
            Assert.That(path.Sum, Is.Zero);
            Assert.That(path.Contains(default), Is.False);
            Assert.That(path.TrySelect(default), Is.EqualTo(ConnectionStepResult.Added));
        }

        [Test]
        public void SnapshotsAreCopiedReadOnlyHistoricalValues()
        {
            var board = CreateFilledBoard(3, 1, 2);
            var path = new ConnectionPath(board);
            path.TrySelect(new BoardPosition(0, 0));
            path.TrySelect(new BoardPosition(1, 0));
            var snapshot = path.CreateSnapshot();
            path.TrySelect(new BoardPosition(2, 0));
            path.Cancel();

            Assert.That(snapshot.Count, Is.EqualTo(2));
            Assert.That(snapshot.Sum, Is.EqualTo(4));
            Assert.That(snapshot.IsEmpty, Is.False);
            var mutableView = (IList<ConnectionEntry>)snapshot.Entries;
            Assert.Throws<NotSupportedException>(() => mutableView[0] = default);
        }

        [Test]
        public void SumUsesLongBeyondIntegerRangeAndBoardIsNotMutated()
        {
            var board = CreateFilledBoard(2, 1, int.MaxValue);
            var path = new ConnectionPath(board);

            path.TrySelect(new BoardPosition(0, 0));
            path.TrySelect(new BoardPosition(1, 0));

            Assert.That(path.Sum, Is.EqualTo(2L * int.MaxValue));
            Assert.That(board.BlockCount, Is.EqualTo(2));
            Assert.That(board.TryGetCell(new BoardPosition(0, 0), out var first), Is.EqualTo(CellLookupResult.Succeeded));
            Assert.That(first.HasBlock, Is.True);
        }

        [Test]
        public void ConstructorRejectsMissingBoard()
        {
            Assert.Throws<ArgumentNullException>(() => new ConnectionPath(null));
        }

        private static void AssertDirection(DomainBoard board, BoardPosition start, BoardPosition neighbor)
        {
            var path = new ConnectionPath(board);
            Assert.That(path.TrySelect(start), Is.EqualTo(ConnectionStepResult.Added));
            Assert.That(path.TrySelect(neighbor), Is.EqualTo(ConnectionStepResult.Added));
        }

        private static DomainBoard CreateFilledBoard(int width, int height, int value)
        {
            var board = new DomainBoard(BoardTopology.CreateRectangular(width, height));
            var id = 1;
            foreach (var position in board.EnumerateActivePositions())
            {
                board.TryPlaceBlock(position, Block(id++, value));
            }

            return board;
        }

        private static NumberBlock Block(int id, int value)
        {
            return new NumberBlock(new BlockId(id), value);
        }
    }
}
