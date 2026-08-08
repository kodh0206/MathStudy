using NUnit.Framework;
using MathGame.Board;
using DomainBoard = MathGame.Board.Board;

namespace MathGame.Tests.Board
{
    public sealed class BoardTests
    {
        private static NumberBlock Block(int id, int value = 5) => new NumberBlock(new BlockId(id), value);
        [Test] public void CellsBeginOpenEmptyAndHolesAreExplicit()
        {
            var board = new DomainBoard(BoardTopology.CreateMasked(2, 1, new[] { new BoardPosition(0, 0) }));
            Assert.That(board.TryGetCell(new BoardPosition(0, 0), out var cell), Is.EqualTo(CellLookupResult.Succeeded));
            Assert.That(cell.Access, Is.EqualTo(CellAccess.Open));
            Assert.That(cell.HasBlock, Is.False);
            Assert.That(board.TryGetCell(new BoardPosition(1, 0), out _), Is.EqualTo(CellLookupResult.InactivePosition));
            Assert.That(board.TryGetCell(new BoardPosition(2, 0), out _), Is.EqualTo(CellLookupResult.OutOfBounds));
        }
        [Test] public void PlaceAndRemoveMaintainCountAndIndex()
        {
            var board = new DomainBoard(BoardTopology.CreateRectangular(2, 1));
            var block = Block(1, 11);
            var position = new BoardPosition(0, 0);
            Assert.That(board.TryPlaceBlock(position, block), Is.EqualTo(BoardMutationResult.Succeeded));
            Assert.That(board.BlockCount, Is.EqualTo(1));
            Assert.That(board.TryFindBlock(block.Id, out var found), Is.True);
            Assert.That(found, Is.EqualTo(position));
            Assert.That(board.TryRemoveBlock(position, out var removed), Is.EqualTo(BoardMutationResult.Succeeded));
            Assert.That(removed, Is.EqualTo(block));
            Assert.That(board.BlockCount, Is.Zero);
            Assert.That(board.TryFindBlock(block.Id, out _), Is.False);
        }
        [Test] public void PlacementFailuresAreAtomicAndDeterministic()
        {
            var board = new DomainBoard(BoardTopology.CreateRectangular(2, 1));
            var first = Block(1);
            Assert.That(board.TryPlaceBlock(new BoardPosition(0, 0), default), Is.EqualTo(BoardMutationResult.InvalidBlock));
            Assert.That(board.TryPlaceBlock(new BoardPosition(0, 0), first), Is.EqualTo(BoardMutationResult.Succeeded));
            Assert.That(board.TryPlaceBlock(new BoardPosition(0, 0), Block(2)), Is.EqualTo(BoardMutationResult.Occupied));
            Assert.That(board.TryPlaceBlock(new BoardPosition(1, 0), first), Is.EqualTo(BoardMutationResult.DuplicateBlockId));
            Assert.That(board.BlockCount, Is.EqualTo(1));
        }
        [Test] public void RelocationPreservesIdentityValueCountAndIndex()
        {
            var board = new DomainBoard(BoardTopology.CreateRectangular(2, 1));
            var source = new BoardPosition(0, 0);
            var destination = new BoardPosition(1, 0);
            var block = Block(3, 9);
            board.TryPlaceBlock(source, block);
            Assert.That(board.TryRelocateBlock(source, destination), Is.EqualTo(BoardMutationResult.Succeeded));
            Assert.That(board.BlockCount, Is.EqualTo(1));
            Assert.That(board.TryFindBlock(block.Id, out var found), Is.True);
            Assert.That(found, Is.EqualTo(destination));
            board.TryGetCell(source, out var oldCell);
            board.TryGetCell(destination, out var newCell);
            Assert.That(oldCell.HasBlock, Is.False);
            Assert.That(newCell.Block.Value, Is.EqualTo(block));
        }
        [Test] public void RelocationFailuresPreserveEndpoints()
        {
            var board = new DomainBoard(BoardTopology.CreateRectangular(2, 1));
            var source = new BoardPosition(0, 0);
            var destination = new BoardPosition(1, 0);
            var block = Block(1);
            board.TryPlaceBlock(source, block);
            Assert.That(board.TryRelocateBlock(source, source), Is.EqualTo(BoardMutationResult.SourceEqualsDestination));
            board.TryRemoveBlock(destination, out _);
            Assert.That(board.TryRelocateBlock(source, destination), Is.EqualTo(BoardMutationResult.Blocked));
            Assert.That(board.TryFindBlock(block.Id, out var found), Is.True);
            Assert.That(found, Is.EqualTo(source));
            Assert.That(board.BlockCount, Is.EqualTo(1));
        }
        [Test] public void AccessIsDerivedFromLayerRole()
        {
            var board = new DomainBoard(BoardTopology.CreateRectangular(1, 1));
            var position = default(BoardPosition);
            var block = Block(1);
            board.TryPlaceBlock(position, block);
            board.TryGetCell(position, out var cell);
            Assert.That(cell.Block.Value, Is.EqualTo(block));
            Assert.That(cell.IsNormallyAccessible, Is.True);
            Assert.That(cell.Access, Is.EqualTo(CellAccess.Open));
        }
    }
}
