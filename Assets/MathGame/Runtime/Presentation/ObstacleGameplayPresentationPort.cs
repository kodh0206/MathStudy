using System;
using MathGame.Fever;
using MathGame.ObstacleFlow;
using MathGame.Stage;

namespace MathGame.Presentation.Unity
{
    public sealed class ObstacleGameplayPresentationPort : IGameplayCommandPort
    {
        readonly ObstacleResolutionCoordinator gameplay;
        readonly StageController stage;
        readonly FeverController fever;
        long lastAcknowledgedSequence;

        public ObstacleGameplayPresentationPort(ObstacleResolutionCoordinator gameplay, StageController stage, FeverController fever)
        { this.gameplay = gameplay ?? throw new ArgumentNullException(nameof(gameplay)); this.stage = stage ?? throw new ArgumentNullException(nameof(stage)); this.fever = fever ?? throw new ArgumentNullException(nameof(fever)); }

        public GameplayStateToken CurrentToken => gameplay.CurrentGameplayToken;
        public bool IsStageTerminated => stage.State is StageState.Success or StageState.Failure or StageState.Exited;

        public PresentationAcknowledgementStatus Acknowledge(PresentationAcknowledgement acknowledgement)
        {
            if (acknowledgement == null) return PresentationAcknowledgementStatus.MissingAcknowledgement;
            if (IsStageTerminated && acknowledgement.Kind != PresentationAcknowledgementKind.Terminal) return PresentationAcknowledgementStatus.StageTerminated;
            if (!gameplay.IsCurrent(acknowledgement.Token)) return PresentationAcknowledgementStatus.StaleGameplayToken;
            if (acknowledgement.SequenceId.Value <= lastAcknowledgedSequence) return PresentationAcknowledgementStatus.DuplicateAcknowledgement;

            var accepted = acknowledgement.Kind switch
            {
                PresentationAcknowledgementKind.TargetReady when stage.State == StageState.PresentingTarget =>
                    fever.State == FeverState.Active ? stage.EnableFeverInput() : stage.EnablePlayerInput(),
                PresentationAcknowledgementKind.FeverEntry when stage.State == StageState.EnteringFever =>
                    fever.CompleteEntry() == FeverControllerCommandResult.Succeeded ? TransitionResult.Succeeded : TransitionResult.InvalidFromCurrentState,
                PresentationAcknowledgementKind.FeverEnd when stage.State == StageState.EndingFever =>
                    fever.CompleteEnding(true) == FeverControllerCommandResult.Succeeded ? TransitionResult.Succeeded : TransitionResult.InvalidFromCurrentState,
                PresentationAcknowledgementKind.Terminal when IsStageTerminated => TransitionResult.Succeeded,
                PresentationAcknowledgementKind.Answer => TransitionResult.Succeeded,
                PresentationAcknowledgementKind.FailedDecision when stage.State == StageState.FailedPendingDecision => TransitionResult.Succeeded,
                _ => TransitionResult.InvalidFromCurrentState
            };
            if (accepted != TransitionResult.Succeeded) return PresentationAcknowledgementStatus.WrongPhase;
            lastAcknowledgedSequence = acknowledgement.SequenceId.Value;
            return PresentationAcknowledgementStatus.Accepted;
        }
    }
}
