using System;
using System.Collections.Generic;
using MathGame.Board;
using MathGame.ObstacleFlow;
using MathGame.Restoration;
using MathGame.Restoration.Contracts;
using MathGame.StageSession;

namespace MathGame.Presentation
{
    public readonly struct PresentationCommandId : IEquatable<PresentationCommandId>
    {
        public PresentationCommandId(long value) { if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); Value = value; }
        public long Value { get; }
        public bool IsValid => Value > 0;
        public bool Equals(PresentationCommandId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PresentationCommandId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
    }

    public readonly struct PresentationSequenceId : IEquatable<PresentationSequenceId>
    {
        public PresentationSequenceId(long value) { if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); Value = value; }
        public long Value { get; }
        public bool IsValid => Value > 0;
        public bool Equals(PresentationSequenceId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PresentationSequenceId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
    }

    public enum PresentationCommandStatus
    {
        Disposed, MissingRequest, StageTerminated, InvalidStageState, StaleGameplayToken,
        DuplicateCommand, OutOfOrderCommand, PresentationStillRunning, DomainRejected, Accepted
    }

    public enum PresentationAcknowledgementStatus
    {
        Disposed, MissingAcknowledgement, StageTerminated, StaleGameplayToken, WrongSourceIdentity,
        DuplicateAcknowledgement, OutOfOrderAcknowledgement, WrongPhase, Accepted
    }

    public enum PresentationAcknowledgementKind
    {
        None, Answer, TargetReady, FeverEntry, FeverEnd, FailedDecision, Terminal
    }

    public enum PresentationPhase
    {
        Idle, Preparing, Playing, Paused, Reconciling, AwaitingAcknowledgement, Disposed, Faulted
    }

    public sealed class PresentationTiming
    {
        public PresentationTiming(int selectionMilliseconds = 80, int removalMilliseconds = 120,
            int gravityMilliseconds = 160, int refillMilliseconds = 160, int restorationMilestoneMilliseconds = 300)
        {
            if (selectionMilliseconds < 0 || removalMilliseconds < 0 || gravityMilliseconds < 0 ||
                refillMilliseconds < 0 || restorationMilestoneMilliseconds < 0) throw new ArgumentOutOfRangeException();
            SelectionMilliseconds = selectionMilliseconds;
            RemovalMilliseconds = removalMilliseconds;
            GravityMilliseconds = gravityMilliseconds;
            RefillMilliseconds = refillMilliseconds;
            RestorationMilestoneMilliseconds = restorationMilestoneMilliseconds;
        }
        public int SelectionMilliseconds { get; }
        public int RemovalMilliseconds { get; }
        public int GravityMilliseconds { get; }
        public int RefillMilliseconds { get; }
        public int RestorationMilestoneMilliseconds { get; }
        public int ForReducedMotion(int normal) => Math.Min(50, normal);
        public static PresentationTiming Approved { get; } = new PresentationTiming();
    }

    public sealed class PresentationSettings
    {
        public PresentationSettings(bool reducedMotion, bool audioEnabled = true, bool hapticsEnabled = true)
        { ReducedMotion = reducedMotion; AudioEnabled = audioEnabled; HapticsEnabled = hapticsEnabled; }
        public bool ReducedMotion { get; }
        public bool AudioEnabled { get; }
        public bool HapticsEnabled { get; }
        public bool PortraitOnly => true;
    }

    public sealed class PresentationEnvelope
    {
        public PresentationEnvelope(PresentationSequenceId sequenceId, GameplayStateSnapshot gameplay,
            StageSessionSnapshot session, MathGame.Fever.FeverPresentationSnapshot fever,
            PresentationAcknowledgementKind acknowledgementKind, long sourceId,
            FailurePresentationSnapshot failure = null, SuccessPresentationSnapshot success = null)
        {
            if (!sequenceId.IsValid) throw new ArgumentException("Invalid sequence.", nameof(sequenceId));
            Gameplay = gameplay ?? throw new ArgumentNullException(nameof(gameplay));
            SequenceId = sequenceId;
            Session = session;
            Fever = fever;
            AcknowledgementKind = acknowledgementKind;
            SourceId = sourceId;
            Failure = failure;
            Success = success;
        }
        public PresentationSequenceId SequenceId { get; }
        public GameplayStateSnapshot Gameplay { get; }
        public StageSessionSnapshot Session { get; }
        public MathGame.Fever.FeverPresentationSnapshot Fever { get; }
        public PresentationAcknowledgementKind AcknowledgementKind { get; }
        public long SourceId { get; }
        public FailurePresentationSnapshot Failure { get; }
        public SuccessPresentationSnapshot Success { get; }
    }

    public sealed class FailurePresentationSnapshot
    {
        public FailurePresentationSnapshot(StageSessionSnapshot session, bool continueEligible)
        { Session = session ?? throw new ArgumentNullException(nameof(session)); ContinueEligible = continueEligible; }
        public StageSessionSnapshot Session { get; }
        public long StageLocalRestoration => Session.ProvisionalRestoration;
        public IReadOnlyList<ObjectiveProgressSnapshot> Objectives => Session.Objectives;
        public bool ContinueEligible { get; }
        public bool RetryAvailable => true;
        public bool AbandonAvailable => true;
    }

    public sealed class SuccessPresentationSnapshot
    {
        public SuccessPresentationSnapshot(long restorationEarned, WorldRestorationCommitResult worldResult)
        { RestorationEarned = restorationEarned; WorldResult = worldResult ?? throw new ArgumentNullException(nameof(worldResult)); }
        public long RestorationEarned { get; }
        public WorldRestorationCommitResult WorldResult { get; }
        public WorldRestorationSnapshot ResultingWorld => WorldResult.After;
        public IReadOnlyList<WorldRestorationMilestone> NewlyCrossedMilestones => WorldResult.Plan?.CrossedMilestones ?? Array.Empty<WorldRestorationMilestone>();
        public bool ProceedAvailable => true;
    }

    public enum PresentationStateIndicator { Selected, Blocked, Damaged, Completed, Unavailable }
    public enum PresentationFeedbackCue
    {
        Selection, Correct, Miss, Perfect, Fast, TimeRecovery, Combo,
        ObstacleDamaged, ObstacleDestroyed, FeverEntry, FeverEnd, Milestone,
        RunEnd, PlayAgain, ReconfigurationStart, ReconfigurationScan, ReconfigurationComplete, Success, Failure
    }
    public interface IPresentationFeedbackPort { void Play(PresentationFeedbackCue cue, bool audioEnabled, bool hapticsEnabled); }

    public sealed class GameplayHudSnapshot
    {
        public GameplayHudSnapshot(int target, StageSessionSnapshot session, MathGame.Fever.FeverPresentationSnapshot fever)
        { Target=target;Session=session??throw new ArgumentNullException(nameof(session));Fever=fever; }
        public int Target{get;} public StageSessionSnapshot Session{get;} public MathGame.Fever.FeverPresentationSnapshot Fever{get;}
        public int RemainingMoves=>Session.RemainingMoves;public long Score=>Session.Score;
        public IReadOnlyList<ObjectiveProgressSnapshot> Objectives=>Session.Objectives;
        public long StageLocalRestoration=>Session.ProvisionalRestoration;
    }
}
