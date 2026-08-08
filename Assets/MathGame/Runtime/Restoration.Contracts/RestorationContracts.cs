using System;

namespace MathGame.Restoration.Contracts
{
    public readonly struct StageRunId : IEquatable<StageRunId>
    {
        public StageRunId(long value) { if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); Value = value; }
        public long Value { get; }
        public bool IsValid => Value > 0;
        public bool Equals(StageRunId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is StageRunId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
    }

    public readonly struct WorldCommitId : IEquatable<WorldCommitId>
    {
        public WorldCommitId(StageRunId runId) { if (!runId.IsValid) throw new ArgumentException("A valid run is required.", nameof(runId)); Value = runId.Value; }
        public long Value { get; }
        public bool IsValid => Value > 0;
        public bool Equals(WorldCommitId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is WorldCommitId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
    }

    public readonly struct WorldRestorationId : IEquatable<WorldRestorationId>
    {
        public WorldRestorationId(int value) { if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); Value = value; }
        public int Value { get; }
        public bool IsValid => Value > 0;
        public bool Equals(WorldRestorationId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is WorldRestorationId other && Equals(other);
        public override int GetHashCode() => Value;
    }

    public sealed class StageRestorationConfig
    {
        public StageRestorationConfig(WorldRestorationId worldId, long stageCapacity)
        {
            WorldId = worldId;
            StageCapacity = stageCapacity;
        }
        public WorldRestorationId WorldId { get; }
        public long StageCapacity { get; }
        public bool IsValid => WorldId.IsValid && StageCapacity > 0;
    }

    public enum RestorationAwardSource { Answer, LargeFeverEnd }
    public sealed class RestorationAwardEvidence
    {
        public RestorationAwardEvidence(StageRunId runId, RestorationAwardSource source, long sourceId, long grossAward, int submittedLength = 0, bool feverAnswer = false, int rulesVersion = 1)
        {
            RunId = runId; Source = source; SourceId = sourceId; GrossAward = grossAward; SubmittedLength = submittedLength; FeverAnswer = feverAnswer; RulesVersion = rulesVersion;
        }
        public StageRunId RunId { get; }
        public RestorationAwardSource Source { get; }
        public long SourceId { get; }
        public long GrossAward { get; }
        public int SubmittedLength { get; }
        public bool FeverAnswer { get; }
        public int RulesVersion { get; }
        public bool IsValid => RunId.IsValid && SourceId > 0 && GrossAward > 0 &&
            RulesVersion == 1 && (Source == RestorationAwardSource.LargeFeverEnd ? GrossAward == 50 && SubmittedLength == 0 && !FeverAnswer : SubmittedLength > 0);
    }

    public enum RestorationLifecycle { Provisional, FailedPendingDecision, CommittedSuccess, Discarded }
    public enum WorldRestorationMilestone { Quarter = 25, Half = 50, ThreeQuarters = 75, Complete = 100 }

    public interface IWorldCommitPlan
    {
        WorldCommitId CommitId { get; }
        WorldRestorationId WorldId { get; }
        long PreparedWorldVersion { get; }
    }

    public readonly struct ContinueGrantId : IEquatable<ContinueGrantId>
    {
        public ContinueGrantId(long value) { if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); Value = value; }
        public long Value { get; }
        public bool IsValid => Value > 0;
        public bool Equals(ContinueGrantId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ContinueGrantId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
    }
    public sealed class ContinueGrant
    {
        public ContinueGrant(ContinueGrantId id, StageRunId runId) { Id = id; RunId = runId; }
        public ContinueGrantId Id { get; } public StageRunId RunId { get; } public int AdditionalMoves => 5;
    }
    public interface IContinueGrantReservation { ContinueGrant Grant { get; } }
    public interface IContinueGrantAuthority
    {
        IContinueGrantReservation PrepareConsume(ContinueGrant grant);
        void CommitConsume(IContinueGrantReservation reservation);
        void CancelReservation(IContinueGrantReservation reservation);
    }
    public interface IStageRunIdSource { bool TryNext(out StageRunId runId); }
}
