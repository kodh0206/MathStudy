using System;
using System.Collections.Generic;
using MathGame.Restoration.Contracts;
using MathGame.StageSession;

namespace MathGame.Restoration
{
    public static class RestorationAwardCalculator
    {
        public static long ForAnswer(int submittedConnectionLength, StageAttemptMode mode)
        {
            if (submittedConnectionLength < 1) throw new ArgumentOutOfRangeException(nameof(submittedConnectionLength));
            var lengthTenths = submittedConnectionLength <= 2 ? 10 : submittedConnectionLength == 3 ? 12 : submittedConnectionLength == 4 ? 15 : 20;
            var feverTenths = mode == StageAttemptMode.Normal ? 10 : mode == StageAttemptMode.Fever ? 20 : throw new ArgumentOutOfRangeException(nameof(mode));
            return checked(10L * lengthTenths * feverTenths / 100L);
        }

        public static long ForLargeFeverEnd() => 50;
        public static RestorationAwardEvidence CreateAnswerEvidence(StageRunId runId, long attemptId, int length, StageAttemptMode mode)
            => new RestorationAwardEvidence(runId, RestorationAwardSource.Answer, attemptId, ForAnswer(length, mode), length, mode == StageAttemptMode.Fever);
        public static RestorationAwardEvidence CreateLargeEndEvidence(StageRunId runId, long effectId)
            => new RestorationAwardEvidence(runId, RestorationAwardSource.LargeFeverEnd, effectId, 50);
    }

    public enum WorldRestorationCommitStatus { MissingInput, InvalidWorldIdentity, WorldIdentityMismatch, InvalidStageSuccess, InvalidCommitId, AlreadyCommitted, ArithmeticOverflow, Committed, StalePlan }

    public sealed class WorldRestorationSnapshot
    {
        internal WorldRestorationSnapshot(WorldRestorationId id, long current, long capacity, WorldRestorationMilestone[] reached, int appliedCount, long version)
        { WorldId = id; Current = current; Capacity = capacity; ReachedMilestones = Array.AsReadOnly(reached); AppliedCommitCount = appliedCount; Version = version; }
        public WorldRestorationId WorldId { get; }
        public long Current { get; }
        public long Capacity { get; }
        public IReadOnlyList<WorldRestorationMilestone> ReachedMilestones { get; }
        public int AppliedCommitCount { get; }
        public long Version { get; }
    }

    public sealed class WorldCommitPlan : IWorldCommitPlan
    {
        internal WorldCommitPlan(WorldRestorationProgress owner, long version, WorldCommitId id, WorldRestorationSnapshot before, WorldRestorationSnapshot after, long stageAmount, long applied, long discarded, WorldRestorationMilestone[] crossed)
        { Owner = owner; Version = version; CommitId = id; Before = before; After = after; StageAmount = stageAmount; AppliedAmount = applied; DiscardedExcess = discarded; CrossedMilestones = Array.AsReadOnly(crossed); }
        internal WorldRestorationProgress Owner { get; }
        internal long Version { get; }
        public WorldCommitId CommitId { get; }
        public WorldRestorationId WorldId => Before.WorldId;
        public long PreparedWorldVersion => Version;
        public WorldRestorationSnapshot Before { get; }
        public WorldRestorationSnapshot After { get; }
        public long StageAmount { get; }
        public long AppliedAmount { get; }
        public long DiscardedExcess { get; }
        public IReadOnlyList<WorldRestorationMilestone> CrossedMilestones { get; }
    }

    public sealed class WorldRestorationCommitResult
    {
        internal WorldRestorationCommitResult(WorldRestorationCommitStatus status, WorldCommitPlan plan, WorldRestorationSnapshot before, WorldRestorationSnapshot after)
        { Status = status; Plan = plan; Before = before; After = after; }
        public WorldRestorationCommitStatus Status { get; }
        public WorldCommitPlan Plan { get; }
        public WorldRestorationSnapshot Before { get; }
        public WorldRestorationSnapshot After { get; }
    }

    public sealed class WorldRestorationProgress
    {
        private static readonly WorldRestorationMilestone[] Ordered = { WorldRestorationMilestone.Quarter, WorldRestorationMilestone.Half, WorldRestorationMilestone.ThreeQuarters, WorldRestorationMilestone.Complete };
        private readonly HashSet<long> applied = new HashSet<long>();
        private long current;
        private long version;
        public WorldRestorationProgress(WorldRestorationId id, long capacity, long initialCurrent = 0)
        { if (!id.IsValid) throw new ArgumentException("Invalid world.", nameof(id)); if (capacity <= 0 || initialCurrent < 0 || initialCurrent > capacity) throw new ArgumentOutOfRangeException(nameof(capacity)); WorldId = id; Capacity = capacity; current = initialCurrent; }
        public WorldRestorationId WorldId { get; }
        public long Capacity { get; }
        public WorldRestorationSnapshot Snapshot => CreateSnapshot(current, version, applied.Count);
        public bool HasCommitted(WorldCommitId id) => id.IsValid && applied.Contains(id.Value);

        internal WorldRestorationCommitResult Prepare(WorldRestorationId worldId, WorldCommitId commitId, long stageAmount)
        {
            var before = Snapshot;
            if (!worldId.IsValid) return new WorldRestorationCommitResult(WorldRestorationCommitStatus.InvalidWorldIdentity, null, before, before);
            if (!worldId.Equals(WorldId)) return new WorldRestorationCommitResult(WorldRestorationCommitStatus.WorldIdentityMismatch, null, before, before);
            if (!commitId.IsValid) return new WorldRestorationCommitResult(WorldRestorationCommitStatus.InvalidCommitId, null, before, before);
            if (applied.Contains(commitId.Value)) return new WorldRestorationCommitResult(WorldRestorationCommitStatus.AlreadyCommitted, null, before, before);
            if (stageAmount < 0) return new WorldRestorationCommitResult(WorldRestorationCommitStatus.InvalidStageSuccess, null, before, before);
            try
            {
                var unclamped = checked(current + stageAmount);
                var afterValue = Math.Min(Capacity, unclamped);
                var appliedAmount = afterValue - current;
                var discarded = stageAmount - appliedAmount;
                var crossed = Crossed(current, afterValue);
                var after = CreateSnapshot(afterValue, checked(version + 1), checked(applied.Count + 1));
                var plan = new WorldCommitPlan(this, version, commitId, before, after, stageAmount, appliedAmount, discarded, crossed);
                return new WorldRestorationCommitResult(WorldRestorationCommitStatus.Committed, plan, before, after);
            }
            catch (OverflowException) { return new WorldRestorationCommitResult(WorldRestorationCommitStatus.ArithmeticOverflow, null, before, before); }
        }

        internal WorldRestorationCommitResult Commit(WorldCommitPlan plan)
        {
            var before = Snapshot;
            if (plan == null) return new WorldRestorationCommitResult(WorldRestorationCommitStatus.MissingInput, null, before, before);
            if (!ReferenceEquals(plan.Owner, this) || plan.Version != version || applied.Contains(plan.CommitId.Value)) return new WorldRestorationCommitResult(WorldRestorationCommitStatus.StalePlan, null, before, before);
            current = plan.After.Current; version = plan.After.Version; applied.Add(plan.CommitId.Value);
            return new WorldRestorationCommitResult(WorldRestorationCommitStatus.Committed, plan, before, Snapshot);
        }

        private WorldRestorationSnapshot CreateSnapshot(long value, long snapshotVersion, int count)
        { var reached = new List<WorldRestorationMilestone>(); foreach (var milestone in Ordered) if (Reached(value, (int)milestone)) reached.Add(milestone); return new WorldRestorationSnapshot(WorldId, value, Capacity, reached.ToArray(), count, snapshotVersion); }
        private WorldRestorationMilestone[] Crossed(long before, long after)
        { var values = new List<WorldRestorationMilestone>(); foreach (var milestone in Ordered) if (!Reached(before, (int)milestone) && Reached(after, (int)milestone)) values.Add(milestone); return values.ToArray(); }
        private bool Reached(long value, int percent)
        {
            var quotient = Capacity / 100;
            var remainder = Capacity % 100;
            var threshold = checked(quotient * percent + (remainder * percent + 99) / 100);
            return value > 0 && value >= threshold;
        }
    }
}
