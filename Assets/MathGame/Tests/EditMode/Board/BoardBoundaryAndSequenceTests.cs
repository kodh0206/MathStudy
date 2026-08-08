using MathGame.Board;
using NUnit.Framework;
using DomainBoard = MathGame.Board.Board;

namespace MathGame.Tests.Board
{
    public sealed class BoardBoundaryAndSequenceTests
    {
        private static NumberBlock Block(int id, int value = 5)
        {
            return new NumberBlock(new BlockId(id), value);
        }

        private static DomainBoard CreateMaskedBoard()
        {
            return new DomainBoard(BoardTopology.CreateMasked(
                3,
                1,
                new[] { new BoardPosition(0, 0), new BoardPosition(2, 0) }));
        }

        [Test]
        public void PlacementBoundaryFailuresPreserveState()
        {
            var board = CreateMaskedBoard();
            var valid = new BoardPosition(0, 0);
            Assert.That(board.TrySetAccess(valid, CellAccess.Blocked), Is.EqualTo(BoardMutationResult.Succeeded));
            Assert.That(board.TryPlaceBlock(valid, Block(1)), Is.EqualTo(BoardMutationResult.Blocked));
            Assert.That(board.TryPlaceBlock(new BoardPosition(1, 0), Block(1)), Is.EqualTo(BoardMutationResult.InactivePosition));
            Assert.That(board.TryPlaceBlock(new BoardPosition(3, 0), Block(1)), Is.EqualTo(BoardMutationResult.OutOfBounds));
            Assert.That(board.BlockCount, Is.Zero);
            Assert.That(board.TryFindBlock(new BlockId(1), out _), Is.False);
        }

        [Test]
        public void RemovalBoundaryFailuresPreserveState()
        {
            var board = CreateMaskedBoard();
            Assert.That(board.TryRemoveBlock(new BoardPosition(0, 0), out _), Is.EqualTo(BoardMutationResult.Empty));
            Assert.That(board.TryRemoveBlock(new BoardPosition(1, 0), out _), Is.EqualTo(BoardMutationResult.InactivePosition));
            Assert.That(board.TryRemoveBlock(new BoardPosition(-1, 0), out _), Is.EqualTo(BoardMutationResult.OutOfBounds));
            Assert.That(board.BlockCount, Is.Zero);
        }

        [Test]
        public void RelocationFailuresReturnExactResultsAndPreserveBothEndpoints()
        {
            var board = new DomainBoard(BoardTopology.CreateMasked(
                4,
                1,
                new[] { new BoardPosition(0, 0), new BoardPosition(1, 0), new BoardPosition(3, 0) }));
            var source = new BoardPosition(0, 0);
            var destination = new BoardPosition(1, 0);
            var first = Block(1);
            var second = Block(2);

            Assert.That(board.TryRelocateBlock(source, destination), Is.EqualTo(BoardMutationResult.Empty));
            board.TryPlaceBlock(source, first);
            board.TryPlaceBlock(destination, second);
            Assert.That(board.TryRelocateBlock(source, destination), Is.EqualTo(BoardMutationResult.Occupied));
            AssertPreserved(board, source, first, destination, second);

            board.TrySetAccess(source, CellAccess.Blocked);
            Assert.That(board.TryRelocateBlock(source, new BoardPosition(3, 0)), Is.EqualTo(BoardMutationResult.Blocked));
            board.TrySetAccess(source, CellAccess.Open);
            board.TrySetAccess(new BoardPosition(3, 0), CellAccess.Blocked);
            Assert.That(board.TryRelocateBlock(source, new BoardPosition(3, 0)), Is.EqualTo(BoardMutationResult.Blocked));
            Assert.That(board.TryRelocateBlock(new BoardPosition(2, 0), destination), Is.EqualTo(BoardMutationResult.InactivePosition));
            Assert.That(board.TryRelocateBlock(source, new BoardPosition(2, 0)), Is.EqualTo(BoardMutationResult.InactivePosition));
            Assert.That(board.TryRelocateBlock(new BoardPosition(-1, 0), destination), Is.EqualTo(BoardMutationResult.OutOfBounds));
            Assert.That(board.TryRelocateBlock(source, new BoardPosition(4, 0)), Is.EqualTo(BoardMutationResult.OutOfBounds));
            AssertPreserved(board, source, first, destination, second);
        }

        [Test]
        public void MixedSequenceMaintainsUniqueIndexAndAllowsRemovedIdReuse()
        {
            var board = new DomainBoard(BoardTopology.CreateRectangular(3, 1));
            var first = Block(1, 3);
            var second = Block(2, 3);
            board.TryPlaceBlock(new BoardPosition(0, 0), first);
            board.TryPlaceBlock(new BoardPosition(1, 0), second);
            Assert.That(board.TryRemoveBlock(new BoardPosition(0, 0), out var removed), Is.EqualTo(BoardMutationResult.Succeeded));
            Assert.That(removed, Is.EqualTo(first));
            Assert.That(board.TryPlaceBlock(new BoardPosition(2, 0), first), Is.EqualTo(BoardMutationResult.Succeeded));
            Assert.That(board.TryRelocateBlock(new BoardPosition(1, 0), new BoardPosition(0, 0)), Is.EqualTo(BoardMutationResult.Succeeded));
            Assert.That(board.BlockCount, Is.EqualTo(2));
            Assert.That(board.TryFindBlock(first.Id, out var firstPosition), Is.True);
            Assert.That(firstPosition, Is.EqualTo(new BoardPosition(2, 0)));
            Assert.That(board.TryFindBlock(second.Id, out var secondPosition), Is.True);
            Assert.That(secondPosition, Is.EqualTo(new BoardPosition(0, 0)));
        }

        [Test]
        public void AccessTransitionsForEmptyAndOccupiedCellsAndReopenRestoresMutation()
        {
            var board = new DomainBoard(BoardTopology.CreateRectangular(2, 1));
            var empty = new BoardPosition(0, 0);
            var occupied = new BoardPosition(1, 0);
            var block = Block(1);
            Assert.That(board.TrySetAccess(empty, CellAccess.Blocked), Is.EqualTo(BoardMutationResult.Succeeded));
            Assert.That(board.TrySetAccess(empty, CellAccess.Open), Is.EqualTo(BoardMutationResult.Succeeded));
            Assert.That(board.TryPlaceBlock(occupied, block), Is.EqualTo(BoardMutationResult.Succeeded));
            Assert.That(board.TrySetAccess(occupied, CellAccess.Blocked), Is.EqualTo(BoardMutationResult.Succeeded));
            Assert.That(board.TrySetAccess(occupied, (CellAccess)99), Is.EqualTo(BoardMutationResult.InvalidAccess));
            board.TryGetCell(occupied, out var unchanged);
            Assert.That(unchanged.Access, Is.EqualTo(CellAccess.Blocked));
            Assert.That(unchanged.Block.Value, Is.EqualTo(block));
            Assert.That(board.TrySetAccess(occupied, CellAccess.Open), Is.EqualTo(BoardMutationResult.Succeeded));
            Assert.That(board.TryRemoveBlock(occupied, out var removed), Is.EqualTo(BoardMutationResult.Succeeded));
            Assert.That(removed, Is.EqualTo(block));
        }

        private static void AssertPreserved(
            DomainBoard board,
            BoardPosition firstPosition,
            NumberBlock first,
            BoardPosition secondPosition,
            NumberBlock second)
        {
            Assert.That(board.BlockCount, Is.EqualTo(2));
            board.TryGetCell(firstPosition, out var firstCell);
            board.TryGetCell(secondPosition, out var secondCell);
            Assert.That(firstCell.Block.Value, Is.EqualTo(first));
            Assert.That(secondCell.Block.Value, Is.EqualTo(second));
            Assert.That(board.TryFindBlock(first.Id, out var indexedFirst), Is.True);
            Assert.That(board.TryFindBlock(second.Id, out var indexedSecond), Is.True);
            Assert.That(indexedFirst, Is.EqualTo(firstPosition));
            Assert.That(indexedSecond, Is.EqualTo(secondPosition));
        }
    }
}
