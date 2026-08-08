using System;
using MathGame.StageSession;

namespace MathGame.Fever
{
    public sealed class FeverConfig
    {
        public FeverConfig(int maximumGauge, double durationSeconds) { MaximumGauge = maximumGauge; DurationSeconds = durationSeconds; }
        public int MaximumGauge { get; }
        public double DurationSeconds { get; }
        public static FeverConfig Prototype { get; } = new FeverConfig(100, 8d);
    }
    public enum FeverState { Charging, PendingEntry, Entering, Active, Ending, Faulted, Aborted, Disposed }
    public enum FeverEndEffectTier { None, RandomThreeBlocks, SmallAreaExplosion, CenterAreaExplosion, LargeExplosionAndRestoration }
    public enum FeverControllerCreateResult { MissingConfig, InvalidMaximumGauge, InvalidDuration, MissingStage, MissingStageSession, MissingTimeProvider, Succeeded }
    public enum FeverChargeApplyResult { Applied, ReachedMaximum, AppliedMiss, MissingResult, NotApplied, WrongMode, StaleOrDuplicateAttempt, NotCharging }
    public enum FeverAttemptApplyStatus { AppliedContinue, AppliedMiss, AppliedTerminal, InvalidState, InvalidAttempt, StageSessionRejected, Disposed }
    public enum FeverControllerCommandResult { Succeeded, AlreadyInRequestedState, InvalidFromCurrentState, UnsafeEntry, StageRejected, ClockFaulted, MissingAttempt, AttemptRejected, Disposed }
    public enum FeverControllerTickResult { NoChange, EndingBegan, AlreadyEnding, ClockFaulted, InvalidFromCurrentState, Disposed }
    public enum FeverTerminationReason { NaturalExpiry, StageSucceeded, StageFailed, StageExited, ClockFault, Cancelled }
    public enum FeverClockState { Idle, Armed, Running, Suspended, Expired, Stopped, Faulted, Disposed }
    public enum FeverClockResult { Succeeded, JustExpired, AlreadyExpired, AlreadyInRequestedState, InvalidFromCurrentState, InvalidTimeSource, Disposed }
    public enum FeverClockFault { None, NonFiniteSample, TimeRegressed }

    public readonly struct FeverGameplayModifiers
    {
        internal FeverGameplayModifiers(int scoreMultiplier)
        { MoveCost = 0; ScoreMultiplier = scoreMultiplier; ObstacleDamageMultiplier = 2; RestorationMultiplier = 2; ExpandedRemovalRequested = true; }
        public int MoveCost { get; } public int ScoreMultiplier { get; } public int ObstacleDamageMultiplier { get; }
        public int RestorationMultiplier { get; } public bool ExpandedRemovalRequested { get; }
        public static FeverGameplayModifiers None => default;
    }
    public sealed class FeverSessionSnapshot
    {
        internal FeverSessionSnapshot(long total, int current, int maximum, StageAttemptId last)
        { TotalCorrectAnswers = total; CurrentCombo = current; MaximumCombo = maximum; LastCommittedAttemptId = last; }
        public long TotalCorrectAnswers { get; } public int CurrentCombo { get; } public int MaximumCombo { get; }
        public int CurrentMultiplier => FeverSession.Multiplier(CurrentCombo); public StageAttemptId LastCommittedAttemptId { get; }
    }
    public sealed class FeverAttemptResult
    {
        internal FeverAttemptResult(FeverAttemptApplyStatus status, StageAttemptResult stage, FeverSessionSnapshot before, FeverSessionSnapshot after, FeverGameplayModifiers modifiers)
        { Status = status; StageResult = stage; Before = before; After = after; Modifiers = modifiers; }
        public FeverAttemptApplyStatus Status { get; } public StageAttemptResult StageResult { get; }
        public FeverSessionSnapshot Before { get; } public FeverSessionSnapshot After { get; } public FeverGameplayModifiers Modifiers { get; }
    }
    public sealed class FeverEndResult
    {
        internal FeverEndResult(FeverTerminationReason reason, FeverEndEffectTier tier, FeverSessionSnapshot session, double elapsed)
        { TerminationReason = reason; EffectTier = tier; TotalCorrectAnswers = session.TotalCorrectAnswers; CurrentCombo = session.CurrentCombo; MaximumCombo = session.MaximumCombo; FinalMultiplier = session.CurrentMultiplier; InteractiveElapsedSeconds = elapsed; ObstacleDamageMultiplier = 2; RestorationMultiplier = 2; }
        public FeverTerminationReason TerminationReason { get; } public FeverEndEffectTier EffectTier { get; }
        public long TotalCorrectAnswers { get; } public int CurrentCombo { get; } public int MaximumCombo { get; } public int FinalMultiplier { get; }
        public double InteractiveElapsedSeconds { get; } public int ObstacleDamageMultiplier { get; } public int RestorationMultiplier { get; }
    }
}
