using System;
using System.Collections.Generic;
using System.Linq;
using MathGame.Answer;
using MathGame.Board;
using DomainBoard = MathGame.Board.Board;

namespace MathGame.Targets
{
    public sealed class TargetSearchConfig
    {
        public TargetSearchConfig(int minTarget, int maxTarget, int minPathLength, int maxPathLength, int maxNodeExpansions)
        { MinTarget = minTarget; MaxTarget = maxTarget; MinPathLength = minPathLength; MaxPathLength = maxPathLength; MaxNodeExpansions = maxNodeExpansions; }
        public int MinTarget { get; } public int MaxTarget { get; }
        public int MinPathLength { get; } public int MaxPathLength { get; } public int MaxNodeExpansions { get; }
        internal bool IsValid => MinTarget > 0 && MaxTarget >= MinTarget && MinPathLength >= 2 && MaxPathLength >= MinPathLength && MaxNodeExpansions > 0;
    }

    public readonly struct TargetSolutionStep
    {
        public TargetSolutionStep(BoardPosition position, NumberBlock block) { Position = position; Block = block; }
        public BoardPosition Position { get; } public NumberBlock Block { get; }
    }
    public enum TargetSolutionValidation
    {
        Valid, MissingBoard, DifferentBoard, InvalidCell, BlockMismatch, DuplicatePosition,
        DuplicateBlockId, NotOrthogonallyAdjacent, SumMismatch
    }

    public sealed class TargetSolution
    {
        private readonly IReadOnlyList<TargetSolutionStep> steps;
        internal TargetSolution(DomainBoard sourceBoard, int target, TargetSolutionStep[] steps, long sum)
        { SourceBoard = sourceBoard; Target = new TargetNumber(target); this.steps = Array.AsReadOnly(steps); Sum = sum; }
        public DomainBoard SourceBoard { get; } public TargetNumber Target { get; }
        public IReadOnlyList<TargetSolutionStep> Steps => steps; public int Count => steps.Count; public long Sum { get; }
        public TargetSolutionValidation Validate(DomainBoard board)
        {
            if (board == null) return TargetSolutionValidation.MissingBoard;
            if (!ReferenceEquals(board, SourceBoard)) return TargetSolutionValidation.DifferentBoard;
            var positions = new HashSet<BoardPosition>(); var ids = new HashSet<BlockId>(); long sum = 0;
            for (var i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                if (!positions.Add(step.Position)) return TargetSolutionValidation.DuplicatePosition;
                if (!ids.Add(step.Block.Id)) return TargetSolutionValidation.DuplicateBlockId;
                if (board.TryGetCell(step.Position, out var cell) != CellLookupResult.Succeeded || !cell.IsSelectable)
                    return TargetSolutionValidation.InvalidCell;
                if (cell.Block.Value != step.Block) return TargetSolutionValidation.BlockMismatch;
                if (i > 0)
                {
                    var previous = steps[i - 1].Position;
                    if (Math.Abs((long)previous.Column - step.Position.Column) + Math.Abs((long)previous.Row - step.Position.Row) != 1)
                        return TargetSolutionValidation.NotOrthogonallyAdjacent;
                }
                sum = checked(sum + step.Block.Value);
            }
            return sum == Target.Value && sum == Sum && Count >= 2 ? TargetSolutionValidation.Valid : TargetSolutionValidation.SumMismatch;
        }
    }

    public enum TargetSearchStatus { Succeeded, NoAvailableTarget, SearchLimitExceeded, InvalidConfiguration, MissingBoard, UnsupportedBoardState }
    public sealed class TargetSearchResult
    {
        internal TargetSearchResult(TargetSearchStatus status, IEnumerable<TargetSolution> solutions, int expansions)
        { Status = status; Solutions = Array.AsReadOnly(solutions.ToArray()); NodeExpansions = expansions; }
        public TargetSearchStatus Status { get; } public IReadOnlyList<TargetSolution> Solutions { get; } public int NodeExpansions { get; }
    }

    public sealed class TargetPathSearcher
    {
        private bool limitExceeded;
        private int expansions;
        private TargetSearchConfig config;
        private DomainBoard board;
        private readonly SortedDictionary<int, TargetSolution> found = new SortedDictionary<int, TargetSolution>();

        public TargetSearchResult Search(DomainBoard source, TargetSearchConfig searchConfig)
        {
            if (source == null) return new TargetSearchResult(TargetSearchStatus.MissingBoard, Array.Empty<TargetSolution>(), 0);
            if (searchConfig == null || !searchConfig.IsValid) return new TargetSearchResult(TargetSearchStatus.InvalidConfiguration, Array.Empty<TargetSolution>(), 0);
            foreach (var position in source.EnumerateActivePositions())
            {
                source.TryGetCell(position, out var cell);
                if (cell.Role == CellRole.NumberSlot && !cell.Block.HasValue)
                    return new TargetSearchResult(TargetSearchStatus.UnsupportedBoardState, Array.Empty<TargetSolution>(), 0);
            }
            board = source; config = searchConfig; expansions = 0; limitExceeded = false; found.Clear();
            foreach (var start in source.EnumerateActivePositions())
            {
                var path = new List<TargetSolutionStep>(); var positions = new HashSet<BoardPosition>(); var ids = new HashSet<BlockId>();
                Visit(start, 0, path, positions, ids);
                if (limitExceeded) return new TargetSearchResult(TargetSearchStatus.SearchLimitExceeded, Array.Empty<TargetSolution>(), expansions);
                if ((long)found.Count == (long)config.MaxTarget - config.MinTarget + 1) break;
            }
            return new TargetSearchResult(found.Count > 0 ? TargetSearchStatus.Succeeded : TargetSearchStatus.NoAvailableTarget, found.Values, expansions);
        }

        private void Visit(BoardPosition position, long sum, List<TargetSolutionStep> path, HashSet<BoardPosition> positions, HashSet<BlockId> ids)
        {
            if (limitExceeded) return;
            board.TryGetCell(position, out var cell); if (!cell.IsSelectable) return; var block = cell.Block.Value;
            if (positions.Contains(position) || ids.Contains(block.Id)) return;
            if (expansions == config.MaxNodeExpansions) { limitExceeded = true; return; }
            expansions++; long nextSum;
            try { nextSum = checked(sum + block.Value); } catch (OverflowException) { return; }
            path.Add(new TargetSolutionStep(position, block)); positions.Add(position); ids.Add(block.Id);
            if (path.Count >= config.MinPathLength && path.Count <= config.MaxPathLength && nextSum >= config.MinTarget && nextSum <= config.MaxTarget && !found.ContainsKey((int)nextSum))
                found.Add((int)nextSum, new TargetSolution(board, (int)nextSum, path.ToArray(), nextSum));
            if (path.Count < config.MaxPathLength && nextSum < config.MaxTarget)
                foreach (var neighbor in board.EnumerateOrthogonalNeighbors(position)) Visit(neighbor, nextSum, path, positions, ids);
            ids.Remove(block.Id); positions.Remove(position); path.RemoveAt(path.Count - 1);
        }
    }
}
