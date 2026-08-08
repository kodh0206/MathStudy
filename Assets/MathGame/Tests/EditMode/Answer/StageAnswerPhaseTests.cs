using System.Collections.Generic;
using MathGame.Stage;
using NUnit.Framework;

namespace MathGame.Tests.Answer
{
    public sealed class StageAnswerPhaseTests
    {
        [Test]
        public void AnswerPhasesFollowApprovedGraphAndCauses()
        {
            var stage = Ready();
            var transitions = new List<StageTransition>();
            stage.StateChanged += transitions.Add;
            Assert.That(stage.BeginTargetPresentation(), Is.EqualTo(TransitionResult.Succeeded));
            Assert.That(stage.EnablePlayerInput(), Is.EqualTo(TransitionResult.Succeeded));
            Assert.That(stage.BeginAnswerResolution(), Is.EqualTo(TransitionResult.Succeeded));
            Assert.That(stage.FinishMissResolution(), Is.EqualTo(TransitionResult.Succeeded));
            Assert.That(transitions[0].Cause, Is.EqualTo(StageTransitionCause.TargetPresentationBegan));
            Assert.That(transitions[1].Cause, Is.EqualTo(StageTransitionCause.PlayerInputEnabled));
            Assert.That(transitions[2].Cause, Is.EqualTo(StageTransitionCause.AnswerResolutionBegan));
            Assert.That(transitions[3].Cause, Is.EqualTo(StageTransitionCause.MissResolutionFinished));
            Assert.That(stage.AcceptsPlayerInput, Is.True);
        }

        [Test]
        public void WrongPhaseAndPausedCommandsPublishNoEvent()
        {
            var stage = Ready();
            var count = 0;
            stage.StateChanged += _ => count++;
            Assert.That(stage.EnablePlayerInput(), Is.EqualTo(TransitionResult.InvalidFromCurrentState));
            Assert.That(stage.BeginAnswerResolution(), Is.EqualTo(TransitionResult.InvalidFromCurrentState));
            Assert.That(stage.FinishMissResolution(), Is.EqualTo(TransitionResult.InvalidFromCurrentState));
            Assert.That(count, Is.Zero);
            stage.Pause(PauseReason.User);
            count = 0;
            Assert.That(stage.BeginTargetPresentation(), Is.EqualTo(TransitionResult.InvalidFromCurrentState));
            Assert.That(stage.EnablePlayerInput(), Is.EqualTo(TransitionResult.InvalidFromCurrentState));
            Assert.That(stage.BeginAnswerResolution(), Is.EqualTo(TransitionResult.InvalidFromCurrentState));
            Assert.That(stage.FinishMissResolution(), Is.EqualTo(TransitionResult.InvalidFromCurrentState));
            Assert.That(count, Is.Zero);
        }

        [Test]
        public void TerminalStageRejectsEveryAnswerPhaseCommandWithoutEvent()
        {
            var stage = Ready();
            stage.Exit(StageExitReason.UserRequested);
            var count = 0;
            stage.StateChanged += _ => count++;

            Assert.That(stage.BeginTargetPresentation(), Is.EqualTo(TransitionResult.StageAlreadyTerminated));
            Assert.That(stage.EnablePlayerInput(), Is.EqualTo(TransitionResult.StageAlreadyTerminated));
            Assert.That(stage.BeginAnswerResolution(), Is.EqualTo(TransitionResult.StageAlreadyTerminated));
            Assert.That(stage.FinishMissResolution(), Is.EqualTo(TransitionResult.StageAlreadyTerminated));
            Assert.That(count, Is.Zero);
        }

        private static StageController Ready()
        {
            var stage = new StageController(); stage.Start(); stage.FinishInitialization(); return stage;
        }
    }
}
