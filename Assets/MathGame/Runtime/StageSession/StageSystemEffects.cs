using System;
using System.Collections.Generic;
using MathGame.BoardResolution;
using MathGame.Restoration.Contracts;

namespace MathGame.StageSession
{
    public enum StageSystemEffectPrepareStatus { MissingResult, SessionAlreadyTerminal, ResolutionNotSucceeded, NotSystemEffect, InvalidEffectId, DuplicateEffect, OutOfOrderEffect, InvalidEvidence, ArithmeticOverflow, PreparedContinue, PreparedSuccess }
    public enum StageSystemEffectCommitStatus { CommittedContinue, CommittedSuccess, MissingPlan, SessionAlreadyTerminal, StalePlan }

    public sealed class StageSystemEffectPlan
    {
        internal StageSystemEffectPlan(StageSession owner, long version, BoardSystemEffectId id, StageSessionSnapshot before, StageSessionSnapshot after, long[] progress, long totalRemoved, long totalDust, long totalBoxes, long grossRestoration, long provisionalRestoration, long discardedRestoration, StageSessionStatus status, StageSessionEvent[] events, IWorldCommitPlan worldPlan = null)
        { Owner = owner; PreparedSessionVersion = version; EffectId = id; Before = before; ProspectiveAfter = after; ProspectiveProgress = progress; ProspectiveTotalRemoved = totalRemoved; ProspectiveDestroyedDust = totalDust; ProspectiveDestroyedBoxes = totalBoxes; ProspectiveGrossRestoration = grossRestoration; ProspectiveRestoration = provisionalRestoration; ProspectiveDiscardedRestoration = discardedRestoration; ProspectiveStatus = status; Events = Array.AsReadOnly(events); WorldPlan = worldPlan; }
        internal StageSession Owner { get; }
        internal long[] ProspectiveProgress { get; }
        internal long ProspectiveTotalRemoved { get; }
        internal long ProspectiveDestroyedDust { get; }
        internal long ProspectiveDestroyedBoxes { get; }
        internal long ProspectiveGrossRestoration { get; }
        internal long ProspectiveRestoration { get; }
        internal long ProspectiveDiscardedRestoration { get; }
        internal IWorldCommitPlan WorldPlan { get; }
        internal StageSessionStatus ProspectiveStatus { get; }
        public BoardSystemEffectId EffectId { get; }
        public long PreparedSessionVersion { get; }
        public StageSessionSnapshot Before { get; }
        public StageSessionSnapshot ProspectiveAfter { get; }
        public IReadOnlyList<StageSessionEvent> Events { get; }
        public bool WouldSucceed => ProspectiveStatus == StageSessionStatus.Success;
        public bool IsWorldBound => WorldPlan != null;
    }

    public sealed class StageSystemEffectPrepareResult
    {
        internal StageSystemEffectPrepareResult(StageSystemEffectPrepareStatus status, StageSystemEffectPlan plan, StageSessionSnapshot before)
        { Status = status; Plan = plan; Before = before; Events = plan == null ? Array.AsReadOnly(Array.Empty<StageSessionEvent>()) : plan.Events; }
        public StageSystemEffectPrepareStatus Status { get; }
        public StageSystemEffectPlan Plan { get; }
        public StageSessionSnapshot Before { get; }
        public StageSessionSnapshot ProspectiveAfter => Plan?.ProspectiveAfter ?? Before;
        public IReadOnlyList<StageSessionEvent> Events { get; }
    }

    public sealed class StageSystemEffectCommitResult
    {
        internal StageSystemEffectCommitResult(StageSystemEffectCommitStatus status, BoardSystemEffectId id, StageSessionSnapshot before, StageSessionSnapshot after, IReadOnlyList<StageSessionEvent> events)
        { Status = status; EffectId = id; Before = before; After = after; Events = events ?? Array.AsReadOnly(Array.Empty<StageSessionEvent>()); }
        public StageSystemEffectCommitStatus Status { get; }
        public BoardSystemEffectId EffectId { get; }
        public StageSessionSnapshot Before { get; }
        public StageSessionSnapshot After { get; }
        public IReadOnlyList<StageSessionEvent> Events { get; }
    }
}
