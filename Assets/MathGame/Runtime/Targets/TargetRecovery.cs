using System;
using System.Collections.Generic;
using System.Linq;
using MathGame.Answer;
using MathGame.Board;
using MathGame.Core.Random;
using DomainBoard = MathGame.Board.Board;

namespace MathGame.Targets
{
    public sealed class TargetRecoveryConfig
    {
        public TargetRecoveryConfig(TargetSearchConfig searchConfig, TargetSelectionPolicy selectionPolicy, int maxShuffleAttempts)
        { SearchConfig = searchConfig; SelectionPolicy = selectionPolicy; MaxShuffleAttempts = maxShuffleAttempts; }
        public TargetSearchConfig SearchConfig { get; } public TargetSelectionPolicy SelectionPolicy { get; }
        public int MaxShuffleAttempts { get; }
        internal bool IsValid => SearchConfig != null && SearchConfig.IsValid && SelectionPolicy != null && SelectionPolicy.IsValid && MaxShuffleAttempts > 0;
    }

    public enum TargetRecoveryStatus
    {
        CurrentTargetStillValid, SelectedOnCurrentBoard, RecoveredByShuffle, MissingInput,
        InvalidConfiguration, InvalidBoardState, SearchLimitExceeded, ShuffleFailed, UnrecoverableDeadlock
    }

    public sealed class TargetRecoveryResult
    {
        internal TargetRecoveryResult(TargetRecoveryStatus status, DomainBoard board, TargetSolution solution,
            TargetHistory history, IEnumerable<ShuffledBlockDelta> deltas, int attempts)
        {
            Status = status; Board = board; Solution = solution; UpdatedHistory = history;
            Deltas = Array.AsReadOnly(deltas.ToArray()); ShuffleAttemptCount = attempts;
        }
        public TargetRecoveryStatus Status { get; }
        public bool Succeeded => Status is TargetRecoveryStatus.CurrentTargetStillValid or TargetRecoveryStatus.SelectedOnCurrentBoard or TargetRecoveryStatus.RecoveredByShuffle;
        public DomainBoard Board { get; } public TargetSolution Solution { get; } public TargetHistory UpdatedHistory { get; }
        public IReadOnlyList<ShuffledBlockDelta> Deltas { get; } public int ShuffleAttemptCount { get; }
        public bool BoardChanged => Status == TargetRecoveryStatus.RecoveredByShuffle; public int MoveCost => 0;
    }

    public sealed class TargetRecoveryCoordinator
    {
        private readonly TargetPathSearcher searcher = new TargetPathSearcher();
        private readonly SafeTargetSelector selector;
        private readonly BoardShuffler shuffler;

        public TargetRecoveryCoordinator(IRandomSource randomSource)
        {
            if (randomSource == null) throw new ArgumentNullException(nameof(randomSource));
            selector = new SafeTargetSelector(randomSource); shuffler = new BoardShuffler(randomSource);
        }

        public TargetRecoveryResult SelectNextTarget(DomainBoard board, TargetHistory history, TargetRecoveryConfig config)
            => Recover(board, null, history, config);

        public TargetRecoveryResult RecoverCurrentTarget(DomainBoard board, TargetNumber current, TargetHistory history, TargetRecoveryConfig config)
        {
            if (!current.IsValid) return Failed(TargetRecoveryStatus.MissingInput, 0);
            return Recover(board, current, history, config);
        }

        private TargetRecoveryResult Recover(DomainBoard original, TargetNumber? current, TargetHistory history, TargetRecoveryConfig config)
        {
            if (original == null || history == null || config == null) return Failed(TargetRecoveryStatus.MissingInput, 0);
            if (!history.IsValid || !config.IsValid) return Failed(TargetRecoveryStatus.InvalidConfiguration, 0);
            var candidate = original;
            for (var attempts = 0; ; attempts++)
            {
                var search = searcher.Search(candidate, config.SearchConfig);
                if (search.Status == TargetSearchStatus.SearchLimitExceeded) return Failed(TargetRecoveryStatus.SearchLimitExceeded, attempts);
                if (search.Status is TargetSearchStatus.MissingBoard or TargetSearchStatus.UnsupportedBoardState)
                    return Failed(TargetRecoveryStatus.InvalidBoardState, attempts);
                if (search.Status == TargetSearchStatus.InvalidConfiguration) return Failed(TargetRecoveryStatus.InvalidConfiguration, attempts);
                if (search.Status == TargetSearchStatus.Succeeded)
                {
                    if (attempts == 0 && current.HasValue)
                    {
                        var witness = search.Solutions.FirstOrDefault(solution => solution.Target.Value == current.Value.Value);
                        if (witness != null)
                            return new TargetRecoveryResult(TargetRecoveryStatus.CurrentTargetStillValid, original, witness, history, Array.Empty<ShuffledBlockDelta>(), 0);
                    }
                    var selection = selector.Select(search, config.SelectionPolicy, history);
                    if (!selection.Succeeded) return Failed(TargetRecoveryStatus.InvalidConfiguration, attempts);
                    var status = attempts == 0 ? TargetRecoveryStatus.SelectedOnCurrentBoard : TargetRecoveryStatus.RecoveredByShuffle;
                    var deltas = attempts == 0 ? Array.Empty<ShuffledBlockDelta>() : BuildOriginalDeltas(original, candidate);
                    return new TargetRecoveryResult(status, candidate, selection.SelectedSolution, selection.UpdatedHistory, deltas, attempts);
                }
                if (attempts == config.MaxShuffleAttempts) return Failed(TargetRecoveryStatus.UnrecoverableDeadlock, attempts);
                var shuffle = shuffler.Shuffle(candidate);
                if (shuffle.Status == BoardShuffleStatus.UnsupportedBoardState) return Failed(TargetRecoveryStatus.InvalidBoardState, attempts);
                if (shuffle.Status == BoardShuffleStatus.InsufficientMovableBlocks) return Failed(TargetRecoveryStatus.UnrecoverableDeadlock, attempts);
                if (!shuffle.Succeeded) return Failed(TargetRecoveryStatus.ShuffleFailed, attempts);
                candidate = shuffle.Board;
            }
        }

        private static ShuffledBlockDelta[] BuildOriginalDeltas(DomainBoard original, DomainBoard final)
        {
            var origins = new Dictionary<BlockId, BoardPosition>();
            foreach (var position in original.EnumerateActivePositions())
            { original.TryGetCell(position, out var cell); origins.Add(cell.Block.Value.Id, position); }
            var deltas = new List<ShuffledBlockDelta>();
            foreach (var destination in final.EnumerateActivePositions())
            {
                final.TryGetCell(destination, out var cell); var from = origins[cell.Block.Value.Id];
                if (from != destination) deltas.Add(new ShuffledBlockDelta(cell.Block.Value, from, destination));
            }
            return deltas.ToArray();
        }

        private static TargetRecoveryResult Failed(TargetRecoveryStatus status, int attempts)
            => new TargetRecoveryResult(status, null, null, null, Array.Empty<ShuffledBlockDelta>(), attempts);
    }
}
