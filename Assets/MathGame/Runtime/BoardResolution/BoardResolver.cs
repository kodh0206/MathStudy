using System;
using System.Collections.Generic;
using MathGame.Board;
using MathGame.Core.Random;
using DomainBoard = MathGame.Board.Board;

namespace MathGame.BoardResolution
{
    public sealed class BoardResolver
    {
        private readonly IRandomSource random;
        public BoardResolver(IRandomSource randomSource)
        {
            random = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
        }

        public BoardResolutionResult Resolve(BoardResolutionRequest request)
        {
            var failure = Validate(request, out var source, out var removedPositions, out var removedDeltas, out var spawnCount);
            if (failure != BoardResolutionFailure.None)
                return BoardResolutionResult.Failed(failure);

            var placements = new Dictionary<BoardPosition, NumberBlock>();
            var moved = new List<MovedBlockDelta>();
            var spawnDestinations = new List<BoardPosition>();
            for (var column = 0; column < source.Topology.Width; column++)
            {
                var row = 0;
                while (row < source.Topology.Height)
                {
                    while (row < source.Topology.Height && !source.IsActive(new BoardPosition(column, row)))
                        row++;
                    if (row >= source.Topology.Height)
                        break;
                    var segment = new List<BoardPosition>();
                    while (row < source.Topology.Height && source.IsActive(new BoardPosition(column, row)))
                        segment.Add(new BoardPosition(column, row++));
                    var survivors = new List<KeyValuePair<BoardPosition, NumberBlock>>();
                    foreach (var position in segment)
                    {
                        source.TryGetCell(position, out var cell);
                        if (!removedPositions.Contains(position))
                            survivors.Add(new KeyValuePair<BoardPosition, NumberBlock>(position, cell.Block.Value));
                    }
                    for (var i = 0; i < survivors.Count; i++)
                    {
                        var destination = segment[i];
                        var survivor = survivors[i];
                        placements.Add(destination, survivor.Value);
                        if (survivor.Key != destination)
                            moved.Add(new MovedBlockDelta(survivor.Key, destination, survivor.Value));
                    }
                    for (var i = survivors.Count; i < segment.Count; i++)
                        spawnDestinations.Add(segment[i]);
                }
            }

            var spawned = new List<SpawnedBlockDelta>();
            var nextId = request.NextBlockIdValue;
            foreach (var destination in spawnDestinations)
            {
                var value = random.NextInt(request.RefillValues.MinimumValue, request.RefillValues.MaximumValue + 1);
                if (value < request.RefillValues.MinimumValue || value > request.RefillValues.MaximumValue)
                    throw new InvalidOperationException("The random source returned a value outside the refill range.");
                var block = new NumberBlock(new BlockId(nextId++), value);
                placements.Add(destination, block);
                spawned.Add(new SpawnedBlockDelta(destination, block));
            }

            var replacement = new DomainBoard(source.Topology);
            foreach (var position in source.EnumerateActivePositions())
                if (!placements.TryGetValue(position, out var block) || replacement.TryPlaceBlock(position, block) != BoardMutationResult.Succeeded)
                    return BoardResolutionResult.Failed(BoardResolutionFailure.FinalBoardMutationRejected);
            return BoardResolutionResult.Success(replacement, nextId, removedDeltas, moved, spawned);
        }

        private static BoardResolutionFailure Validate(BoardResolutionRequest request, out DomainBoard source,
            out HashSet<BoardPosition> removedPositions, out List<RemovedBlockDelta> removed, out int spawnCount)
        {
            source = null;
            removedPositions = new HashSet<BoardPosition>();
            removed = new List<RemovedBlockDelta>();
            spawnCount = 0;
            if (request == null) return BoardResolutionFailure.MissingRequest;
            if (request.Board == null) return BoardResolutionFailure.MissingBoard;
            if (request.Answer == null) return BoardResolutionFailure.MissingAnswer;
            if (request.RefillValues == null) return BoardResolutionFailure.MissingRefillRange;
            if (!request.Answer.IsCorrect) return BoardResolutionFailure.AnswerNotCorrect;
            // A public AnswerResult cannot be both Correct and empty. Retain this defensive guard
            // so the resolver remains safe if the Answer contract changes in a later STEP.
            if (request.Answer.Snapshot == null || request.Answer.Snapshot.IsEmpty)
                return BoardResolutionFailure.EmptySelection;
            if (!request.RefillValues.IsValid) return BoardResolutionFailure.InvalidRefillRange;
            if (request.NextBlockIdValue <= 0) return BoardResolutionFailure.InvalidNextBlockId;
            source = request.Board;
            var liveIds = new HashSet<BlockId>(); var maxId = 0;
            foreach (var position in source.EnumerateActivePositions())
            {
                source.TryGetCell(position, out var cell);
                if (cell.Access != CellAccess.Open || !cell.Block.HasValue) return BoardResolutionFailure.UnsupportedBoardState;
                liveIds.Add(cell.Block.Value.Id); if (cell.Block.Value.Id.Value > maxId) maxId = cell.Block.Value.Id.Value;
            }
            spawnCount = request.Answer.Snapshot.Count;
            var selectedIds = new HashSet<BlockId>();
            foreach (var entry in request.Answer.Snapshot.Entries)
            {
                if (!removedPositions.Add(entry.Position)) return BoardResolutionFailure.DuplicateSelectionPosition;
                if (!selectedIds.Add(entry.Block.Id)) return BoardResolutionFailure.DuplicateSelectionBlockId;
                if (source.TryGetCell(entry.Position, out var cell) != CellLookupResult.Succeeded)
                    return BoardResolutionFailure.SelectedPositionMissing;
                if (cell.Access != CellAccess.Open || !cell.Block.HasValue || cell.Block.Value != entry.Block)
                    return BoardResolutionFailure.SelectedBlockMismatch;
                removed.Add(new RemovedBlockDelta(entry.Position, entry.Block));
            }
            if (request.NextBlockIdValue <= maxId || liveIds.Contains(new BlockId(request.NextBlockIdValue)))
                return BoardResolutionFailure.NextBlockIdCollision;
            if (request.NextBlockIdValue > int.MaxValue - spawnCount)
                return BoardResolutionFailure.BlockIdRangeExhausted;
            return BoardResolutionFailure.None;
        }
    }
}
