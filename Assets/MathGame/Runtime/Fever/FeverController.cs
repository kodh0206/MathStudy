using System;
using MathGame.Answer;
using MathGame.BoardResolution;
using MathGame.Core.Time;
using MathGame.Stage;
using MathGame.StageSession;
using MathGame.Restoration.Contracts;

namespace MathGame.Fever
{
    public sealed class FeverController : IDisposable
    {
        private readonly StageController stage; private readonly MathGame.StageSession.StageSession stageSession;
        private readonly int maximumGauge; private long presentationRevision = 1;
        private readonly FeverChargeTracker charge; private readonly InteractiveFeverClock clock; private FeverSession session;
        private FeverController(FeverConfig config, StageController stage, MathGame.StageSession.StageSession stageSession, ITimeProvider time)
        { this.stage = stage; this.stageSession = stageSession; maximumGauge = config.MaximumGauge; charge = new FeverChargeTracker(config.MaximumGauge); clock = new InteractiveFeverClock(stage, time, config.DurationSeconds); clock.Faulted += ClockFault; stage.StateChanged += StageChanged; }
        public FeverState State { get; private set; } = FeverState.Charging; public int Gauge => charge.Gauge;
        public FeverClockState ClockState => clock.State; public FeverSessionSnapshot SessionSnapshot => session?.Snapshot;
        public FeverEndResult PendingEndResult { get; private set; }
        public FeverPresentationSnapshot CapturePresentationSnapshot() => new FeverPresentationSnapshot(State, Gauge, maximumGauge,
            clock.State, clock.DurationSeconds, clock.ElapsedSeconds, clock.RemainingSeconds, session?.Snapshot,
            PendingEndResult?.EffectTier, presentationRevision);
        public bool IsBoundTo(StageController stageController, MathGame.StageSession.StageSession sessionController)
            => ReferenceEquals(stage, stageController) && ReferenceEquals(stageSession, sessionController);
        public static FeverControllerCreateResult TryCreate(FeverConfig config, StageController stage, MathGame.StageSession.StageSession stageSession, ITimeProvider time, out FeverController controller)
        { controller = null; if (config == null) return FeverControllerCreateResult.MissingConfig; if (config.MaximumGauge <= 0) return FeverControllerCreateResult.InvalidMaximumGauge; if (double.IsNaN(config.DurationSeconds) || double.IsInfinity(config.DurationSeconds) || config.DurationSeconds <= 0) return FeverControllerCreateResult.InvalidDuration; if (stage == null) return FeverControllerCreateResult.MissingStage; if (stageSession == null) return FeverControllerCreateResult.MissingStageSession; if (time == null) return FeverControllerCreateResult.MissingTimeProvider; controller = new FeverController(config, stage, stageSession, time); return FeverControllerCreateResult.Succeeded; }
        public FeverChargeApplyResult ApplyNormalAttempt(StageAttemptResult result)
        { if (State != FeverState.Charging) return FeverChargeApplyResult.NotCharging; var applied = charge.ApplyNormalAttempt(result); if (applied == FeverChargeApplyResult.ReachedMaximum) { charge.Charging = false; State = FeverState.PendingEntry; } if (result != null && result.After != null && result.After.Status != StageSessionStatus.Active) Abort(result.After.Status == StageSessionStatus.Success ? FeverTerminationReason.StageSucceeded : FeverTerminationReason.StageFailed); return applied; }
        public FeverControllerCommandResult BeginEntry(bool safeTargetReady, bool stageSessionActive)
        { if (State == FeverState.Disposed) return FeverControllerCommandResult.Disposed; if (State != FeverState.PendingEntry) return FeverControllerCommandResult.InvalidFromCurrentState; if (!safeTargetReady || !stageSessionActive) return FeverControllerCommandResult.UnsafeEntry; if (stage.State != StageState.PresentingTarget) return FeverControllerCommandResult.InvalidFromCurrentState; if (stage.BeginFeverEntry() != TransitionResult.Succeeded) return FeverControllerCommandResult.StageRejected; State = FeverState.Entering; return FeverControllerCommandResult.Succeeded; }
        public FeverControllerCommandResult CompleteEntry()
        { if (State == FeverState.Disposed) return FeverControllerCommandResult.Disposed; if (State != FeverState.Entering || stage.State != StageState.EnteringFever) return FeverControllerCommandResult.InvalidFromCurrentState; if (clock.Arm() != FeverClockResult.Succeeded) return FeverControllerCommandResult.ClockFaulted; if (stage.CompleteFeverEntry() != TransitionResult.Succeeded) { clock.Stop(); clock.Reset(); return FeverControllerCommandResult.StageRejected; } if (State == FeverState.Faulted) return FeverControllerCommandResult.ClockFaulted; session = new FeverSession(stageSession.CreateSnapshot().NextExpectedAttemptId); State = FeverState.Active; return FeverControllerCommandResult.Succeeded; }
        public FeverAttemptResult ApplyFeverAttempt(StageAttemptId id, AnswerResult answer, BoardResolutionResult resolution)
        { if (State == FeverState.Disposed) return new FeverAttemptResult(FeverAttemptApplyStatus.Disposed, null, null, null, FeverGameplayModifiers.None); if (State != FeverState.Active || stage.State != StageState.ResolvingAnswer) return Reject(FeverAttemptApplyStatus.InvalidState); var before = session.Snapshot; if (!stageSession.CreateSnapshot().NextExpectedAttemptId.Equals(id) || !session.Preview(id, answer, out var next, out var rules, out var modifiers)) return new FeverAttemptResult(FeverAttemptApplyStatus.InvalidAttempt, null, before, before, FeverGameplayModifiers.None); var stageResult = stageSession.ApplyAttempt(new StageAttemptCommand(id, answer, resolution, rules)); if (stageResult.Status is not (StageAttemptApplyStatus.AppliedContinue or StageAttemptApplyStatus.AppliedMiss or StageAttemptApplyStatus.AppliedSuccess or StageAttemptApplyStatus.AppliedFailure)) return new FeverAttemptResult(FeverAttemptApplyStatus.StageSessionRejected, stageResult, before, before, FeverGameplayModifiers.None); session.Commit(next); if (stageResult.After.Status != StageSessionStatus.Active) { Abort(stageResult.After.Status == StageSessionStatus.Success ? FeverTerminationReason.StageSucceeded : FeverTerminationReason.StageFailed); return new FeverAttemptResult(FeverAttemptApplyStatus.AppliedTerminal, stageResult, before, next, answer.IsCorrect ? modifiers : FeverGameplayModifiers.None); } return new FeverAttemptResult(answer.IsCorrect ? FeverAttemptApplyStatus.AppliedContinue : FeverAttemptApplyStatus.AppliedMiss, stageResult, before, next, answer.IsCorrect ? modifiers : FeverGameplayModifiers.None); }
        public FeverAttemptResult ApplyFeverAttempt(StageAttemptId id, AnswerResult answer, ObstacleResolutionResult resolution)
        { if (State == FeverState.Disposed) return new FeverAttemptResult(FeverAttemptApplyStatus.Disposed, null, null, null, FeverGameplayModifiers.None); if (State != FeverState.Active || stage.State != StageState.ResolvingAnswer) return Reject(FeverAttemptApplyStatus.InvalidState); var before = session.Snapshot; if (!stageSession.CreateSnapshot().NextExpectedAttemptId.Equals(id) || !session.Preview(id, answer, out var next, out var rules, out var modifiers)) return new FeverAttemptResult(FeverAttemptApplyStatus.InvalidAttempt, null, before, before, FeverGameplayModifiers.None); var stageResult = stageSession.ApplyAttempt(new StageAttemptCommand(id, answer, resolution, rules)); if (stageResult.Status is not (StageAttemptApplyStatus.AppliedContinue or StageAttemptApplyStatus.AppliedSuccess or StageAttemptApplyStatus.AppliedFailure)) return new FeverAttemptResult(FeverAttemptApplyStatus.StageSessionRejected, stageResult, before, before, FeverGameplayModifiers.None); session.Commit(next); if (stageResult.After.Status != StageSessionStatus.Active) { Abort(stageResult.After.Status == StageSessionStatus.Success ? FeverTerminationReason.StageSucceeded : FeverTerminationReason.StageFailed); return new FeverAttemptResult(FeverAttemptApplyStatus.AppliedTerminal, stageResult, before, next, modifiers); } return new FeverAttemptResult(FeverAttemptApplyStatus.AppliedContinue, stageResult, before, next, modifiers); }

        public FeverAttemptPrepareResult PrepareFeverAttempt(StageAttemptId id, AnswerResult answer, ObstacleResolutionResult resolution, RestorationAwardEvidence restoration)
        {
            if (State == FeverState.Disposed) return new FeverAttemptPrepareResult(FeverAttemptPrepareStatus.Disposed, null);
            if (State != FeverState.Active || stage.State != StageState.ResolvingAnswer) return new FeverAttemptPrepareResult(FeverAttemptPrepareStatus.InvalidState, null);
            var before = session.Snapshot;
            if (!stageSession.CreateSnapshot().NextExpectedAttemptId.Equals(id) || !session.Preview(id, answer, out var next, out var rules, out var modifiers))
                return new FeverAttemptPrepareResult(FeverAttemptPrepareStatus.InvalidAttempt, null);
            var prepared = stageSession.PrepareAttempt(new StageAttemptCommand(id, answer, resolution, rules, restoration), restoration);
            if (prepared.Plan == null) return new FeverAttemptPrepareResult(FeverAttemptPrepareStatus.StageSessionRejected, null);
            return new FeverAttemptPrepareResult(FeverAttemptPrepareStatus.Prepared, new FeverAttemptPlan(this, before, next, modifiers, prepared.Plan));
        }

        public FeverAttemptResult CommitFeverAttempt(FeverAttemptPlan feverPlan, StageAttemptPlan stagePlan)
        {
            if (feverPlan == null || !ReferenceEquals(feverPlan.Owner, this) || !ReferenceEquals(feverPlan.StagePlan, stagePlan))
                return Reject(FeverAttemptApplyStatus.InvalidAttempt);
            var committed = stageSession.CommitAttempt(stagePlan);
            if (committed.Result == null) return Reject(FeverAttemptApplyStatus.StageSessionRejected);
            session.Commit(feverPlan.Prospective);
            var after = feverPlan.After;
            if (committed.Result.After.Status != StageSessionStatus.Active)
            {
                Abort(committed.Result.After.Status == StageSessionStatus.Success ? FeverTerminationReason.StageSucceeded : FeverTerminationReason.StageFailed);
                return new FeverAttemptResult(FeverAttemptApplyStatus.AppliedTerminal, committed.Result, feverPlan.Before, after, feverPlan.Modifiers);
            }
            return new FeverAttemptResult(FeverAttemptApplyStatus.AppliedContinue, committed.Result, feverPlan.Before, after, feverPlan.Modifiers);
        }
        public FeverControllerTickResult Tick()
        { if (State == FeverState.Disposed) return FeverControllerTickResult.Disposed; if (State == FeverState.Ending) return FeverControllerTickResult.AlreadyEnding; if (State != FeverState.Active) return FeverControllerTickResult.InvalidFromCurrentState; var result = clock.Tick(); if (result == FeverClockResult.InvalidTimeSource) return FeverControllerTickResult.ClockFaulted; if (result != FeverClockResult.JustExpired) return FeverControllerTickResult.NoChange; if (stage.State != StageState.FeverInput || stage.BeginFeverEnding() != TransitionResult.Succeeded) { State = FeverState.Faulted; return FeverControllerTickResult.InvalidFromCurrentState; } PendingEndResult = new FeverEndResult(FeverTerminationReason.NaturalExpiry, Tier(session.Snapshot.TotalCorrectAnswers), session.Snapshot, clock.ElapsedSeconds); State = FeverState.Ending; return FeverControllerTickResult.EndingBegan; }
        public FeverControllerCommandResult CompleteEnding(bool effectsAcknowledged)
        { if (State == FeverState.Disposed) return FeverControllerCommandResult.Disposed; if (State != FeverState.Ending) return FeverControllerCommandResult.InvalidFromCurrentState; if (!effectsAcknowledged) return FeverControllerCommandResult.InvalidFromCurrentState; if (stage.FinishFeverEnding() != TransitionResult.Succeeded) return FeverControllerCommandResult.StageRejected; clock.Reset(); session = null; PendingEndResult = null; charge.ResetGauge(); State = FeverState.Charging; return FeverControllerCommandResult.Succeeded; }
        public FeverControllerCommandResult Abort(FeverTerminationReason reason)
        { if (State == FeverState.Disposed) return FeverControllerCommandResult.Disposed; if (State == FeverState.Aborted) return FeverControllerCommandResult.AlreadyInRequestedState; if (clock.State is FeverClockState.Armed or FeverClockState.Running or FeverClockState.Suspended) clock.Stop(); session = null; PendingEndResult = null; charge.ResetGauge(); charge.Charging = false; State = FeverState.Aborted; return FeverControllerCommandResult.Succeeded; }
        public void Dispose() { if (State == FeverState.Disposed) return; stage.StateChanged -= StageChanged; clock.Faulted -= ClockFault; clock.Dispose(); session = null; PendingEndResult = null; State = FeverState.Disposed; }
        private FeverAttemptResult Reject(FeverAttemptApplyStatus status) { var value = session?.Snapshot; return new FeverAttemptResult(status, null, value, value, FeverGameplayModifiers.None); }
        private void ClockFault() { if (State == FeverState.Disposed) return; State = FeverState.Faulted; session = null; PendingEndResult = null; if (stage.State == StageState.FeverInput) stage.BeginFeverEnding(); }
        private void StageChanged(StageTransition transition) { if (transition.Current == StageState.FeverInput && State == FeverState.Faulted) stage.BeginFeverEnding(); if (transition.Current == StageState.Success) Abort(FeverTerminationReason.StageSucceeded); else if (transition.Current == StageState.Failure) Abort(FeverTerminationReason.StageFailed); else if (transition.Current == StageState.Exited) Abort(FeverTerminationReason.StageExited); }
        private static FeverEndEffectTier Tier(long count) => count <= 0 ? FeverEndEffectTier.None : count == 1 ? FeverEndEffectTier.RandomThreeBlocks : count == 2 ? FeverEndEffectTier.SmallAreaExplosion : count == 3 ? FeverEndEffectTier.CenterAreaExplosion : FeverEndEffectTier.LargeExplosionAndRestoration;
    }
}
