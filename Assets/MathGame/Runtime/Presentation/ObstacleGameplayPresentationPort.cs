using System;
using MathGame.Answer;
using MathGame.Connection;
using MathGame.Fever;
using MathGame.ObstacleFlow;
using MathGame.Stage;
using MathGame.StageSession;
using MathGame.BoardResolution;
using System.Collections.Generic;

namespace MathGame.Presentation.Unity
{
    public sealed class ObstacleGameplayPresentationPort : IGameplayCommandPort
    {
        readonly ObstacleResolutionCoordinator gameplay;
        readonly StageController stage;
        readonly FeverController fever;
        readonly MathGame.StageSession.StageSession session;
        readonly AnswerValidator answers;
        readonly RestorationLifecycleCoordinator failedDecisions;
        ConnectionPath path;
        long lastCommandId;
        long lastAcknowledgedSequence;
        PresentationAcknowledgementKind expectedAcknowledgement;
        long expectedSourceId;
        GameplayStateToken expectedToken;
        readonly List<RemovedNumberDelta> lastCommittedFootprint = new List<RemovedNumberDelta>();
        bool endTargetReady;
        bool pendingMiss;
        bool pendingMissWasFever;

        public ObstacleGameplayPresentationPort(ObstacleResolutionCoordinator gameplay, StageController stage, FeverController fever)
            : this(gameplay, stage, fever, null, new AnswerValidator(AnswerTimingThresholds.Prototype), null) { }

        public ObstacleGameplayPresentationPort(ObstacleResolutionCoordinator gameplay, StageController stage,
            FeverController fever, MathGame.StageSession.StageSession session, AnswerValidator answers,
            RestorationLifecycleCoordinator failedDecisions)
        {
            this.gameplay = gameplay ?? throw new ArgumentNullException(nameof(gameplay));
            this.stage = stage ?? throw new ArgumentNullException(nameof(stage));
            this.fever = fever ?? throw new ArgumentNullException(nameof(fever));
            this.session = session;
            this.answers = answers ?? throw new ArgumentNullException(nameof(answers));
            this.failedDecisions = failedDecisions;
        }

        public GameplayStateToken CurrentToken => gameplay.CurrentGameplayToken;
        public bool IsStageTerminated => stage.State is StageState.Success or StageState.Failure or StageState.Exited;

        public GameplayCommandResult BeginPath(PathCommandRequest request)
        {
            var invalid = Validate(request?.CommandId ?? default, request?.Token ?? default, true);
            if (invalid.HasValue) return Result(invalid.Value);
            path = new ConnectionPath(gameplay.CaptureGameplayState().Board);
            var result = Select(request.Position); lastCommandId = request.CommandId.Value; return result;
        }

        public GameplayCommandResult ExtendPath(PathCommandRequest request)
        {
            var invalid = Validate(request?.CommandId ?? default, request?.Token ?? default, true);
            if (invalid.HasValue) return Result(invalid.Value);
            if (path == null) return Result(PresentationCommandStatus.InvalidStageState);
            var result = Select(request.Position); lastCommandId = request.CommandId.Value; return result;
        }

        public GameplayCommandResult CancelPath(PresentationCommandId commandId, GameplayStateToken token)
        {
            var invalid = Validate(commandId, token, false);
            if (invalid.HasValue) return Result(invalid.Value);
            path?.Cancel(); path = null; lastCommandId = commandId.Value;
            return Result(PresentationCommandStatus.Accepted);
        }

        public GameplayCommandResult ReleasePath(ReleasePathRequest request)
        {
            var invalid = Validate(request?.CommandId ?? default, request?.Token ?? default, true);
            if (invalid.HasValue) return Result(invalid.Value);
            if (path == null || request.Refill == null || request.History == null || request.TargetConfig == null)
                return Result(PresentationCommandStatus.MissingRequest);
            var snapshot = path.CreateSnapshot(); path = null; lastCommandId = request.CommandId.Value;
            var answer = answers.Evaluate(snapshot, request.Target, request.ElapsedSeconds);
            var wasFever = stage.State == StageState.FeverInput;
            if (stage.BeginAnswerResolution() != TransitionResult.Succeeded) return Result(PresentationCommandStatus.InvalidStageState, answer: answer);

            if (!answer.IsCorrect)
            {
                if (session == null) return Result(PresentationCommandStatus.DomainRejected, answer: answer);
                StageAttemptResult applied;
                if (wasFever)
                {
                    var prepared = fever.PrepareFeverAttempt(request.AttemptId, answer, null, null);
                    var feverResult = prepared.Plan == null
                        ? null
                        : fever.CommitFeverAttempt(prepared.Plan, prepared.Plan.StagePlan);
                    applied = feverResult?.StageResult;
                    if (feverResult?.Status != FeverAttemptApplyStatus.AppliedMiss)
                        return Result(PresentationCommandStatus.DomainRejected, answer: answer);
                }
                else
                {
                    var prepared = session.PrepareAttempt(
                        new StageAttemptCommand(request.AttemptId, answer,
                            (MathGame.BoardResolution.ObstacleResolutionResult)null, StageAttemptRules.Normal), null);
                    var committed = prepared.Plan == null ? null : session.CommitAttempt(prepared.Plan);
                    applied = committed?.Result;
                    if (committed?.Status != StageAttemptCommitStatus.CommittedMiss)
                        return Result(PresentationCommandStatus.DomainRejected, answer: answer);
                }
                if (applied?.Status != StageAttemptApplyStatus.AppliedMiss)
                    return Result(PresentationCommandStatus.DomainRejected, answer: answer);
                pendingMiss = true;
                pendingMissWasFever = wasFever;
                Expect(PresentationAcknowledgementKind.Answer, request.AttemptId.Value, CurrentToken);
                return Result(PresentationCommandStatus.Accepted, answer: answer);
            }

            var flowRequest = new ObstacleAnswerFlowRequest(answer, request.AttemptId, request.Refill, request.History, request.TargetConfig);
            var flow = wasFever ? gameplay.ResolveFeverAnswer(flowRequest) : gameplay.ResolveNormalAnswer(flowRequest);
            if (!flow.AttemptCommitted) return Result(PresentationCommandStatus.DomainRejected, answer: answer, flow: flow);
            lastCommittedFootprint.Clear();if(flow.ResolutionResult!=null)lastCommittedFootprint.AddRange(flow.ResolutionResult.Removed);
            if(flow.Status==ObstacleAnswerFlowStatus.StageTerminal&&flow.StageResult?.After!=null)
            {
                var transition=flow.StageResult.After.Status==StageSessionStatus.Success?stage.Complete():stage.Fail();
                if(transition!=TransitionResult.Succeeded)return Result(PresentationCommandStatus.DomainRejected,answer:answer,flow:flow);
            }
            if (flow.IsInputReady)
                Expect(PresentationAcknowledgementKind.Answer, request.AttemptId.Value, flow.GameplayToken);
            else if (flow.Status == ObstacleAnswerFlowStatus.FailedPendingDecision)
                Expect(PresentationAcknowledgementKind.FailedDecision, request.AttemptId.Value, flow.GameplayToken);
            else if (flow.Status == ObstacleAnswerFlowStatus.StageTerminal)
                Expect(PresentationAcknowledgementKind.Terminal, request.AttemptId.Value, flow.GameplayToken);
            else
                ClearExpectation(); // committed Board is waiting for RetryTargetRecovery; it is not terminal.
            return Result(PresentationCommandStatus.Accepted, answer: answer, flow: flow, token: flow.GameplayToken);
        }

        public GameplayCommandResult RetryTargetRecovery(TargetRetryRequest request)
        {
            var invalid = Validate(request?.CommandId ?? default, request?.Token ?? default, false);
            if (invalid.HasValue) return Result(invalid.Value);
            if (request.History == null || request.Config == null) return Result(PresentationCommandStatus.MissingRequest);
            lastCommandId = request.CommandId.Value;
            var flow = gameplay.RetryTargetRecovery(request.History, request.Config);
            if (!flow.IsInputReady) return Result(PresentationCommandStatus.DomainRejected, flow: flow);
            if (stage.State == StageState.ResolvingAnswer && stage.BeginTargetPresentation() != TransitionResult.Succeeded)
                return Result(PresentationCommandStatus.DomainRejected, flow: flow);
            Expect(PresentationAcknowledgementKind.TargetReady, flow.GameplayToken.SourceId, flow.GameplayToken);
            return Result(PresentationCommandStatus.Accepted, flow: flow, token: flow.GameplayToken);
        }

        public GameplayCommandResult ResolveFeverEnd(FeverEndCommandRequest request)
        {
            var invalid=Validate(request?.CommandId??default,request?.Token??default,false);
            if(invalid.HasValue)return Result(invalid.Value);
            if(request.Refill==null||request.History==null||request.Config==null||fever.PendingEndResult==null)return Result(PresentationCommandStatus.MissingRequest);
            MathGame.Board.BoardPosition? center=null;var selected=default(MathGame.Board.BoardPosition);var tier=fever.PendingEndResult.EffectTier;
            var spatial=tier is FeverEndEffectTier.SmallAreaExplosion or FeverEndEffectTier.CenterAreaExplosion or FeverEndEffectTier.LargeExplosionAndRestoration;
            if(spatial&&!FeverAreaCenterSelector.TrySelect(gameplay.CurrentBoard.Topology,lastCommittedFootprint,out selected))return Result(PresentationCommandStatus.DomainRejected);
            else if(spatial)center=selected;
            lastCommandId=request.CommandId.Value;
            var flow=gameplay.ResolveAndCommitEnd(new ObstacleEndFlowRequest(fever.PendingEndResult,center,request.Refill,request.History,request.Config));
            if(!flow.EffectCommitted)return Result(PresentationCommandStatus.DomainRejected,end:flow);
            if(flow.Status==ObstacleEndFlowStatus.StageTerminal&&flow.SystemEffectResult?.After!=null)
            {if(stage.Complete()!=TransitionResult.Succeeded)return Result(PresentationCommandStatus.DomainRejected,end:flow);}
            endTargetReady=flow.IsInputReady;Expect(PresentationAcknowledgementKind.FeverEnd,flow.ResolutionResult.SystemEffectId.Value,flow.GameplayToken);
            return Result(PresentationCommandStatus.Accepted,end:flow,token:flow.GameplayToken);
        }

        public GameplayCommandResult ResolveFailedDecision(FailedDecisionRequest request)
        {
            var invalid = Validate(request?.CommandId ?? default, request?.Token ?? default, false);
            if (invalid.HasValue) return Result(invalid.Value);
            if (failedDecisions == null) return Result(PresentationCommandStatus.DomainRejected);
            lastCommandId = request.CommandId.Value;
            FailedDecisionResult decision = request.Choice switch
            {
                FailedDecisionChoice.Continue => failedDecisions.ContinueFailedStage(request.ContinueGrant),
                FailedDecisionChoice.Retry => failedDecisions.RetryFailedStage(request.RetryDefinition),
                FailedDecisionChoice.Abandon => failedDecisions.AbandonFailedStage(),
                _ => null
            };
            if (decision == null || decision.Status is not (FailedDecisionStatus.Continued or FailedDecisionStatus.Retried or FailedDecisionStatus.Abandoned))
                return Result(PresentationCommandStatus.DomainRejected, decision: decision);
            return Result(PresentationCommandStatus.Accepted, decision: decision);
        }

        public PresentationAcknowledgementStatus Acknowledge(PresentationAcknowledgement acknowledgement)
        {
            if (acknowledgement == null) return PresentationAcknowledgementStatus.MissingAcknowledgement;
            if (IsStageTerminated && acknowledgement.Kind != PresentationAcknowledgementKind.Terminal) return PresentationAcknowledgementStatus.StageTerminated;
            if (!gameplay.IsCurrent(acknowledgement.Token)) return PresentationAcknowledgementStatus.StaleGameplayToken;
            if (acknowledgement.Kind != expectedAcknowledgement || acknowledgement.SourceId != expectedSourceId || acknowledgement.Token != expectedToken)
                return PresentationAcknowledgementStatus.WrongSourceIdentity;
            if (acknowledgement.SequenceId.Value <= lastAcknowledgedSequence) return PresentationAcknowledgementStatus.DuplicateAcknowledgement;
            if (acknowledgement.SequenceId.Value != lastAcknowledgedSequence + 1) return PresentationAcknowledgementStatus.OutOfOrderAcknowledgement;

            var wasFeverEnd=acknowledgement.Kind==PresentationAcknowledgementKind.FeverEnd;
            var accepted = acknowledgement.Kind switch
            {
                PresentationAcknowledgementKind.TargetReady when stage.State == StageState.PresentingTarget =>
                    fever.State == FeverState.Active ? stage.EnableFeverInput() : stage.EnablePlayerInput(),
                PresentationAcknowledgementKind.Answer when stage.State == StageState.ResolvingAnswer && pendingMiss =>
                    pendingMissWasFever ? stage.FinishFeverMissResolution() : stage.FinishMissResolution(),
                PresentationAcknowledgementKind.Answer when stage.State == StageState.ResolvingAnswer => stage.BeginTargetPresentation(),
                PresentationAcknowledgementKind.FeverEntry when stage.State == StageState.EnteringFever =>
                    fever.CompleteEntry() == FeverControllerCommandResult.Succeeded ? TransitionResult.Succeeded : TransitionResult.InvalidFromCurrentState,
                PresentationAcknowledgementKind.FeverEnd when stage.State == StageState.EndingFever =>
                    fever.CompleteEnding(true) == FeverControllerCommandResult.Succeeded ? TransitionResult.Succeeded : TransitionResult.InvalidFromCurrentState,
                PresentationAcknowledgementKind.Terminal when IsStageTerminated => TransitionResult.Succeeded,
                PresentationAcknowledgementKind.FailedDecision when stage.State == StageState.FailedPendingDecision => TransitionResult.Succeeded,
                _ => TransitionResult.InvalidFromCurrentState
            };
            if (accepted != TransitionResult.Succeeded) return PresentationAcknowledgementStatus.WrongPhase;
            lastAcknowledgedSequence = acknowledgement.SequenceId.Value;
            if (acknowledgement.Kind == PresentationAcknowledgementKind.Answer && pendingMiss)
            {
                pendingMiss = false;
                pendingMissWasFever = false;
                ClearExpectation();
            }
            else if (acknowledgement.Kind == PresentationAcknowledgementKind.Answer)
                Expect(PresentationAcknowledgementKind.TargetReady, acknowledgement.SourceId, CurrentToken);
            else if(wasFeverEnd&&endTargetReady&&stage.State==StageState.ResolvingAnswer&&stage.BeginTargetPresentation()==TransitionResult.Succeeded)
                Expect(PresentationAcknowledgementKind.TargetReady,acknowledgement.SourceId,CurrentToken);
            else ClearExpectation();
            return PresentationAcknowledgementStatus.Accepted;
        }

        GameplayCommandResult Select(MathGame.Board.BoardPosition position)
        {
            var step = path.TrySelect(position);
            return new GameplayCommandResult(PresentationCommandStatus.Accepted, CurrentToken, step, path.CreateSnapshot());
        }
        PresentationCommandStatus? Validate(PresentationCommandId id, GameplayStateToken token, bool requiresInput)
        {
            if (!id.IsValid) return PresentationCommandStatus.MissingRequest;
            if (IsStageTerminated) return PresentationCommandStatus.StageTerminated;
            if (!gameplay.IsCurrent(token)) return PresentationCommandStatus.StaleGameplayToken;
            if (id.Value <= lastCommandId) return PresentationCommandStatus.DuplicateCommand;
            if (id.Value != lastCommandId + 1) return PresentationCommandStatus.OutOfOrderCommand;
            if (requiresInput && !stage.AcceptsPlayerInput) return PresentationCommandStatus.InvalidStageState;
            if (expectedAcknowledgement != PresentationAcknowledgementKind.None)
                return PresentationCommandStatus.PresentationStillRunning;
            return null;
        }
        void Expect(PresentationAcknowledgementKind kind,long source,GameplayStateToken token){expectedAcknowledgement=kind;expectedSourceId=source;expectedToken=token;}
        void ClearExpectation(){expectedAcknowledgement=PresentationAcknowledgementKind.None;expectedSourceId=0;expectedToken=default;}
        GameplayCommandResult Result(PresentationCommandStatus status, AnswerResult answer=null, ObstacleAnswerFlowResult flow=null, FailedDecisionResult decision=null, GameplayStateToken? token=null,ObstacleEndFlowResult end=null)
            => new GameplayCommandResult(status,token??CurrentToken,null,null,answer,flow,decision,end);
    }
}
