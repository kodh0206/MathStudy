using System;
using MathGame.Fever;
using MathGame.Restoration;
using MathGame.Restoration.Contracts;
using MathGame.Stage;
using MathGame.StageSession;
using System.Collections.Generic;

namespace MathGame.ObstacleFlow
{
    public sealed class RestorationTransactionCoordinator
    {
        private readonly MathGame.StageSession.StageSession session;
        private readonly WorldRestorationProgress world;
        public RestorationTransactionCoordinator(MathGame.StageSession.StageSession session, WorldRestorationProgress world)
        { this.session = session ?? throw new ArgumentNullException(nameof(session)); this.world = world ?? throw new ArgumentNullException(nameof(world)); }
        public RestorationTransactionResult CommitPreparedAttempt(StageAttemptPlan plan)
        {
            if (plan == null) return new RestorationTransactionResult(null, null);
            if (!plan.WouldSucceed) return new RestorationTransactionResult(session.CommitAttempt(plan), null);
            var prepared = PrepareWorld(plan.ProspectiveAfter); if (prepared.Plan == null) return new RestorationTransactionResult(null, prepared);
            var bound = session.BindWorldCommit(plan, prepared.Plan); if (bound.Plan == null) return new RestorationTransactionResult(null, prepared);
            var stageResult = session.CommitAttempt(bound.Plan); if (stageResult.Status != StageAttemptCommitStatus.CommittedSuccess) return new RestorationTransactionResult(stageResult, prepared);
            return new RestorationTransactionResult(stageResult, CommitGuaranteed(prepared.Plan));
        }
        public FeverAttemptResult CommitPreparedFever(FeverController controller, FeverAttemptPlan feverPlan)
        {
            if (controller == null || feverPlan == null) return null; var stagePlan = feverPlan.StagePlan; WorldCommitPlan worldPlan = null;
            if (stagePlan.WouldSucceed) { var prepared = PrepareWorld(stagePlan.ProspectiveAfter); if (prepared.Plan == null) return null; var bound = session.BindWorldCommit(stagePlan, prepared.Plan); if (bound.Plan == null) return null; stagePlan = bound.Plan; worldPlan = prepared.Plan; }
            var result = controller.CommitFeverAttempt(feverPlan, stagePlan); if (result?.StageResult != null && worldPlan != null) CommitGuaranteed(worldPlan); return result;
        }
        public StageSystemEffectCommitResult CommitPreparedSystemEffect(StageSystemEffectPlan plan)
        {
            if (plan == null) return null; WorldCommitPlan worldPlan = null;
            if (plan.WouldSucceed) { var prepared = PrepareWorld(plan.ProspectiveAfter); if (prepared.Plan == null) return null; var bound = session.BindWorldCommit(plan, prepared.Plan); if (bound.Plan == null) return null; plan = bound.Plan; worldPlan = prepared.Plan; }
            var result = session.CommitSystemEffect(plan); if (result?.Status == StageSystemEffectCommitStatus.CommittedSuccess && worldPlan != null) CommitGuaranteed(worldPlan); return result;
        }
        private WorldRestorationCommitResult PrepareWorld(StageSessionSnapshot after) => world.Prepare(after.RestorationWorldId, new WorldCommitId(after.StageRunId), after.ProvisionalRestoration);
        private WorldRestorationCommitResult CommitGuaranteed(WorldCommitPlan plan) { var result = world.Commit(plan); if (result.Status != WorldRestorationCommitStatus.Committed) throw new InvalidOperationException("Validated world plan became stale inside the sole-owner transaction."); return result; }
    }
    public sealed class RestorationTransactionResult { internal RestorationTransactionResult(StageAttemptCommitResult stage, WorldRestorationCommitResult world) { StageResult=stage;WorldResult=world; } public StageAttemptCommitResult StageResult{get;} public WorldRestorationCommitResult WorldResult{get;} }

    public sealed class StageRunRegistry
    {
        readonly HashSet<long> ids=new HashSet<long>(); readonly WorldRestorationProgress world;
        public StageRunRegistry(WorldRestorationProgress world){this.world=world??throw new ArgumentNullException(nameof(world));}
        public bool TryReserve(StageRunId id){if(!id.IsValid||ids.Contains(id.Value)||world.HasCommitted(new WorldCommitId(id)))return false;return ids.Add(id.Value);}
        public bool Contains(StageRunId id)=>id.IsValid&&ids.Contains(id.Value);
        internal void ReleaseReservation(StageRunId id){if(id.IsValid&&!world.HasCommitted(new WorldCommitId(id)))ids.Remove(id.Value);}
    }
    public sealed class StageRunHandle { public StageRunHandle(StageRunId id, StageController stage, MathGame.StageSession.StageSession session, StageRunRegistry registry) { RunId=id;Stage=stage;Session=session;Registry=registry??throw new ArgumentNullException(nameof(registry)); } public StageRunId RunId{get;} public StageController Stage{get;} public MathGame.StageSession.StageSession Session{get;} public StageRunRegistry Registry{get;} }
    public interface IStageRunFactory { bool TryCreate(StageDefinition definition, StageRunId runId, out StageRunHandle handle); }
    public enum FailedDecisionStatus { Continued, Retried, Abandoned, InvalidStageState, SessionNotFailedPendingDecision, MissingOrInvalidGrant, GrantRunMismatch, GrantRejected, ContinueAlreadyUsed, RunIdAllocationFailed, ArithmeticOverflow }
    public sealed class FailedDecisionResult { internal FailedDecisionResult(FailedDecisionStatus status, StageSessionSnapshot before, StageSessionSnapshot after, StageRunHandle retry=null) {Status=status;Before=before;After=after;RetryHandle=retry;} public FailedDecisionStatus Status{get;} public StageSessionSnapshot Before{get;} public StageSessionSnapshot After{get;} public StageRunHandle RetryHandle{get;} }
    public sealed class RestorationLifecycleCoordinator
    {
        readonly StageController stage; readonly MathGame.StageSession.StageSession session; readonly IContinueGrantAuthority grants; readonly IStageRunIdSource ids; readonly IStageRunFactory factory; readonly StageRunRegistry registry;
        readonly HashSet<long> grantIds=new HashSet<long>();
        public RestorationLifecycleCoordinator(StageController stage, MathGame.StageSession.StageSession session, IContinueGrantAuthority grants, IStageRunIdSource ids, IStageRunFactory factory, StageRunRegistry registry) {this.stage=stage??throw new ArgumentNullException(nameof(stage));this.session=session??throw new ArgumentNullException(nameof(session));this.grants=grants??throw new ArgumentNullException(nameof(grants));this.ids=ids??throw new ArgumentNullException(nameof(ids));this.factory=factory??throw new ArgumentNullException(nameof(factory));this.registry=registry??throw new ArgumentNullException(nameof(registry));var run=session.CreateSnapshot().StageRunId;if(!run.IsValid)throw new ArgumentException("Session has no run identity.");if(!registry.Contains(run)&&!registry.TryReserve(run))throw new ArgumentException("Run identity collision.");}
        public FailedDecisionResult ContinueFailedStage(ContinueGrant grant) {var before=session.CreateSnapshot();if(stage.State!=StageState.FailedPendingDecision)return R(FailedDecisionStatus.InvalidStageState,before);if(session.Status!=StageSessionStatus.FailedPendingDecision)return R(FailedDecisionStatus.SessionNotFailedPendingDecision,before);if(grant==null||!grant.Id.IsValid)return R(FailedDecisionStatus.MissingOrInvalidGrant,before);if(!grant.RunId.Equals(before.StageRunId))return R(FailedDecisionStatus.GrantRunMismatch,before);if(before.ContinueUsed||grantIds.Contains(grant.Id.Value))return R(FailedDecisionStatus.ContinueAlreadyUsed,before);if(before.RemainingMoves>int.MaxValue-5)return R(FailedDecisionStatus.ArithmeticOverflow,before);var reservation=grants.PrepareConsume(grant);if(reservation==null)return R(FailedDecisionStatus.GrantRejected,before);if(reservation.Grant==null||reservation.Grant.Id.Value!=grant.Id.Value||!reservation.Grant.RunId.Equals(grant.RunId)){grants.CancelReservation(reservation);return R(FailedDecisionStatus.GrantRejected,before);}if(!session.TryContinueFailedAttempt(5)){grants.CancelReservation(reservation);return R(FailedDecisionStatus.ArithmeticOverflow,before);}if(stage.ResumeFromContinue()!=TransitionResult.Succeeded)throw new InvalidOperationException("Validated continue transition failed.");grants.CommitConsume(reservation);grantIds.Add(grant.Id.Value);return new FailedDecisionResult(FailedDecisionStatus.Continued,before,session.CreateSnapshot());}
        public FailedDecisionResult RetryFailedStage(StageDefinition definition) {var before=session.CreateSnapshot();if(stage.State!=StageState.FailedPendingDecision)return R(FailedDecisionStatus.InvalidStageState,before);if(session.Status!=StageSessionStatus.FailedPendingDecision)return R(FailedDecisionStatus.SessionNotFailedPendingDecision,before);if(!ids.TryNext(out var id)||!registry.TryReserve(id))return R(FailedDecisionStatus.RunIdAllocationFailed,before);if(!factory.TryCreate(definition,id,out var handle)||!ValidHandle(handle,id,definition,registry)){registry.ReleaseReservation(id);return R(FailedDecisionStatus.RunIdAllocationFailed,before);}if(!session.TryDiscardFailedAttempt())throw new InvalidOperationException("Validated retry discard failed.");if(stage.Fail()!=TransitionResult.Succeeded)throw new InvalidOperationException("Validated retry terminal transition failed.");return new FailedDecisionResult(FailedDecisionStatus.Retried,before,session.CreateSnapshot(),handle);}
        public FailedDecisionResult AbandonFailedStage(){var before=session.CreateSnapshot();if(stage.State!=StageState.FailedPendingDecision)return R(FailedDecisionStatus.InvalidStageState,before);if(session.Status!=StageSessionStatus.FailedPendingDecision)return R(FailedDecisionStatus.SessionNotFailedPendingDecision,before);if(!session.TryDiscardFailedAttempt())throw new InvalidOperationException("Validated abandon discard failed.");if(stage.Fail()!=TransitionResult.Succeeded)throw new InvalidOperationException("Validated abandon transition failed.");return new FailedDecisionResult(FailedDecisionStatus.Abandoned,before,session.CreateSnapshot());}
        FailedDecisionResult R(FailedDecisionStatus s,StageSessionSnapshot b)=>new FailedDecisionResult(s,b,b);
        static bool ValidHandle(StageRunHandle handle,StageRunId id,StageDefinition definition,StageRunRegistry registry){if(handle==null||!handle.RunId.Equals(id)||!ReferenceEquals(handle.Registry,registry)||handle.Stage==null||handle.Session==null||handle.Stage.State!=StageState.Ready)return false;var snapshot=handle.Session.CreateSnapshot();var config=definition.RestorationConfig;return snapshot.StageRunId.Equals(id)&&snapshot.DefinitionId.Equals(definition.Id)&&snapshot.ProvisionalRestoration==0&&snapshot.GrossRestorationEarned==0&&snapshot.Status==StageSessionStatus.Active&&snapshot.StageRestorationCapacity==(config?.StageCapacity??0)&&snapshot.RestorationWorldId.Equals(config?.WorldId??default);}
    }
}
