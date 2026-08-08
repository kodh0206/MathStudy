using System;
using System.Collections.Generic;
using System.Linq;
using MathGame.Answer;
using MathGame.Core.Random;

namespace MathGame.Targets
{
    public sealed class TargetSelectionPolicy
    {
        public TargetSelectionPolicy(int maxConsecutiveIdenticalTargets) { MaxConsecutiveIdenticalTargets = maxConsecutiveIdenticalTargets; }
        public int MaxConsecutiveIdenticalTargets { get; }
        internal bool IsValid => MaxConsecutiveIdenticalTargets > 0;
    }

    public sealed class TargetHistory
    {
        public TargetHistory(TargetNumber? lastTarget, int consecutiveCount) { LastTarget = lastTarget; ConsecutiveCount = consecutiveCount; }
        public TargetNumber? LastTarget { get; } public int ConsecutiveCount { get; }
        internal bool IsValid => (!LastTarget.HasValue && ConsecutiveCount == 0) || (LastTarget.HasValue && LastTarget.Value.IsValid && ConsecutiveCount > 0);
    }

    public enum TargetSelectionStatus
    {
        Succeeded, MissingSearchResult, SearchNotSuccessful, NoCandidates, MissingPolicy,
        InvalidPolicy, InvalidHistory, HistoryOverflow
    }

    public sealed class TargetSelectionResult
    {
        internal TargetSelectionResult(TargetSelectionStatus status, TargetSolution solution, TargetHistory history, bool fallback)
        { Status = status; SelectedSolution = solution; UpdatedHistory = history; UsedRepetitionFallback = fallback; }
        public TargetSelectionStatus Status { get; } public bool Succeeded => Status == TargetSelectionStatus.Succeeded;
        public TargetSolution SelectedSolution { get; }
        public TargetHistory UpdatedHistory { get; } public bool UsedRepetitionFallback { get; }
    }

    public sealed class SafeTargetSelector
    {
        private readonly IRandomSource random;
        public SafeTargetSelector(IRandomSource randomSource) { random = randomSource ?? throw new ArgumentNullException(nameof(randomSource)); }
        public TargetSelectionResult Select(TargetSearchResult search, TargetSelectionPolicy policy, TargetHistory history)
        {
            if (search == null) return Failed(TargetSelectionStatus.MissingSearchResult);
            if (search.Status != TargetSearchStatus.Succeeded) return Failed(TargetSelectionStatus.SearchNotSuccessful);
            if (search.Solutions.Count == 0) return Failed(TargetSelectionStatus.NoCandidates);
            if (policy == null) return Failed(TargetSelectionStatus.MissingPolicy);
            if (!policy.IsValid) return Failed(TargetSelectionStatus.InvalidPolicy);
            if (history == null || !history.IsValid) return Failed(TargetSelectionStatus.InvalidHistory);
            var candidates = search.Solutions.OrderBy(solution => solution.Target.Value).ToList(); var fallback = false;
            if (history.LastTarget.HasValue && history.ConsecutiveCount >= policy.MaxConsecutiveIdenticalTargets)
            {
                var alternatives = candidates.Where(solution => solution.Target.Value != history.LastTarget.Value.Value).ToList();
                if (alternatives.Count > 0) candidates = alternatives; else fallback = true;
            }
            var index = random.NextInt(0, candidates.Count);
            if (index < 0 || index >= candidates.Count) throw new InvalidOperationException("Random selection index was out of range.");
            var selected = candidates[index];
            int consecutive;
            try
            {
                consecutive = history.LastTarget.HasValue && history.LastTarget.Value.Value == selected.Target.Value
                    ? checked(history.ConsecutiveCount + 1)
                    : 1;
            }
            catch (OverflowException) { return Failed(TargetSelectionStatus.HistoryOverflow); }
            return new TargetSelectionResult(TargetSelectionStatus.Succeeded, selected, new TargetHistory(selected.Target, consecutive), fallback);
        }
        private static TargetSelectionResult Failed(TargetSelectionStatus status) => new TargetSelectionResult(status, null, null, false);
    }
}
