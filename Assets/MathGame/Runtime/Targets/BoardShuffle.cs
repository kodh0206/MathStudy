using System;
using System.Collections.Generic;
using System.Linq;
using MathGame.Board;
using MathGame.Core.Random;
using DomainBoard = MathGame.Board.Board;

namespace MathGame.Targets
{
    public readonly struct ShuffledBlockDelta
    {
        public ShuffledBlockDelta(NumberBlock block, BoardPosition from, BoardPosition to) { Block = block; From = from; To = to; }
        public NumberBlock Block { get; } public BoardPosition From { get; } public BoardPosition To { get; }
    }
    public enum BoardShuffleStatus { Succeeded, MissingBoard, UnsupportedBoardState, InsufficientMovableBlocks, FinalBoardMutationRejected }
    public sealed class BoardShuffleResult
    {
        internal BoardShuffleResult(BoardShuffleStatus status, DomainBoard board, IEnumerable<ShuffledBlockDelta> deltas)
        { Status = status; Board = board; Deltas = Array.AsReadOnly(deltas.ToArray()); }
        public bool Succeeded => Status == BoardShuffleStatus.Succeeded; public BoardShuffleStatus Status { get; }
        public DomainBoard Board { get; } public IReadOnlyList<ShuffledBlockDelta> Deltas { get; }
    }
    public sealed class BoardShuffler
    {
        private readonly IRandomSource random;
        public BoardShuffler(IRandomSource randomSource) { random = randomSource ?? throw new ArgumentNullException(nameof(randomSource)); }
        public BoardShuffleResult Shuffle(DomainBoard source)
        {
            if (source == null) return Failed(BoardShuffleStatus.MissingBoard);
            var positions = source.EnumerateActivePositions().ToArray();
            var blocks = new NumberBlock[positions.Length]; var original = new Dictionary<BlockId, BoardPosition>();
            for (var i = 0; i < positions.Length; i++)
            {
                source.TryGetCell(positions[i], out var cell);
                if (cell.Access != CellAccess.Open || !cell.Block.HasValue) return Failed(BoardShuffleStatus.UnsupportedBoardState);
                blocks[i] = cell.Block.Value; original.Add(blocks[i].Id, positions[i]);
            }
            if (positions.Length < 2) return Failed(BoardShuffleStatus.InsufficientMovableBlocks);
            for (var i = blocks.Length - 1; i >= 1; i--)
            {
                var selected = random.NextInt(0, i + 1);
                if (selected < 0 || selected > i) throw new InvalidOperationException("Random shuffle index was out of range.");
                var temporary = blocks[i]; blocks[i] = blocks[selected]; blocks[selected] = temporary;
            }
            var replacement = new DomainBoard(source.Topology); var deltas = new List<ShuffledBlockDelta>();
            for (var i = 0; i < positions.Length; i++)
            {
                if (replacement.TryPlaceBlock(positions[i], blocks[i]) != BoardMutationResult.Succeeded) return Failed(BoardShuffleStatus.FinalBoardMutationRejected);
                var from = original[blocks[i].Id]; if (from != positions[i]) deltas.Add(new ShuffledBlockDelta(blocks[i], from, positions[i]));
            }
            return new BoardShuffleResult(BoardShuffleStatus.Succeeded, replacement, deltas);
        }
        private static BoardShuffleResult Failed(BoardShuffleStatus status) => new BoardShuffleResult(status, null, Array.Empty<ShuffledBlockDelta>());
    }
}
