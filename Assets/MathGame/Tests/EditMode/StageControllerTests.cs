using System.Collections.Generic;
using MathGame.Stage;
using NUnit.Framework;

namespace MathGame.Tests.EditMode
{
    public sealed class StageControllerTests
    {
        private StageController _controller;

        [SetUp]
        public void SetUp()
        {
            _controller = new StageController();
        }

        [Test]
        public void StartAndFinishInitialization_EntersReadyInOrder()
        {
            var transitions = new List<StageTransition>();
            _controller.StateChanged += transitions.Add;

            Assert.That(_controller.Start(), Is.EqualTo(TransitionResult.Succeeded));
            Assert.That(_controller.FinishInitialization(), Is.EqualTo(TransitionResult.Succeeded));

            Assert.That(_controller.State, Is.EqualTo(StageState.Ready));
            Assert.That(_controller.AcceptsPlayerInput, Is.False);
            Assert.That(transitions, Has.Count.EqualTo(2));
            Assert.That(transitions[0].Previous, Is.EqualTo(StageState.None));
            Assert.That(transitions[0].Current, Is.EqualTo(StageState.Initializing));
            Assert.That(transitions[0].Cause, Is.EqualTo(StageTransitionCause.StartRequested));
            Assert.That(transitions[1].Previous, Is.EqualTo(StageState.Initializing));
            Assert.That(transitions[1].Current, Is.EqualTo(StageState.Ready));
            Assert.That(transitions[1].Cause, Is.EqualTo(StageTransitionCause.InitializationCompleted));
        }

        [Test]
        public void PauseAndResume_RestoresPreviousState()
        {
            EnterReady();

            Assert.That(_controller.Pause(PauseReason.User), Is.EqualTo(TransitionResult.Succeeded));
            Assert.That(_controller.State, Is.EqualTo(StageState.Paused));
            Assert.That(_controller.StateBeforePause, Is.EqualTo(StageState.Ready));
            Assert.That(_controller.AcceptsPlayerInput, Is.False);

            Assert.That(_controller.Resume(PauseReason.User), Is.EqualTo(TransitionResult.Succeeded));
            Assert.That(_controller.State, Is.EqualTo(StageState.Ready));
            Assert.That(_controller.StateBeforePause, Is.Null);
        }

        [Test]
        public void NestedPause_DoesNotResumeUntilEveryReasonIsCleared()
        {
            EnterReady();
            _controller.Pause(PauseReason.User);
            _controller.Pause(PauseReason.ApplicationBackground);

            Assert.That(_controller.ActivePauseReasonCount, Is.EqualTo(2));
            Assert.That(
                _controller.Resume(PauseReason.ApplicationBackground),
                Is.EqualTo(TransitionResult.BlockedByPauseReason));
            Assert.That(_controller.State, Is.EqualTo(StageState.Paused));

            Assert.That(_controller.Resume(PauseReason.User), Is.EqualTo(TransitionResult.Succeeded));
            Assert.That(_controller.State, Is.EqualTo(StageState.Ready));
        }

        [Test]
        public void DuplicatePauseReason_DoesNotCreateAnotherTransition()
        {
            EnterReady();
            int transitionCount = 0;
            _controller.StateChanged += _ => transitionCount++;

            Assert.That(_controller.Pause(PauseReason.User), Is.EqualTo(TransitionResult.Succeeded));
            Assert.That(
                _controller.Pause(PauseReason.User),
                Is.EqualTo(TransitionResult.AlreadyInRequestedState));
            Assert.That(transitionCount, Is.EqualTo(1));
        }

        [TestCase(PauseReason.User)]
        [TestCase(PauseReason.ApplicationBackground)]
        [TestCase(PauseReason.ApplicationFocusLost)]
        [TestCase(PauseReason.Advertisement)]
        [TestCase(PauseReason.SystemInterruption)]
        public void EveryPauseReason_IsIndependentAndIdempotent(PauseReason reason)
        {
            EnterReady();

            Assert.That(_controller.Pause(reason), Is.EqualTo(TransitionResult.Succeeded));
            Assert.That(_controller.Pause(reason), Is.EqualTo(TransitionResult.AlreadyInRequestedState));
            Assert.That(_controller.HasPauseReason(reason), Is.True);
            Assert.That(_controller.ActivePauseReasonCount, Is.EqualTo(1));
            Assert.That(_controller.Resume(reason), Is.EqualTo(TransitionResult.Succeeded));
            Assert.That(_controller.State, Is.EqualTo(StageState.Ready));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Finish_FromReady_IsRejectedWithoutPublishingTransition(bool succeeds)
        {
            EnterReady();
            int transitionCount = 0;
            _controller.StateChanged += _ => transitionCount++;

            TransitionResult finishResult = succeeds ? _controller.Complete() : _controller.Fail();

            Assert.That(finishResult, Is.EqualTo(TransitionResult.InvalidFromCurrentState));
            Assert.That(_controller.State, Is.EqualTo(StageState.Ready));
            Assert.That(_controller.IsTerminal, Is.False);
            Assert.That(transitionCount, Is.Zero);
        }

        [Test]
        public void Exit_ClearsPauseStateAndRejectsFurtherCommands()
        {
            EnterReady();
            _controller.Pause(PauseReason.User);

            Assert.That(
                _controller.Exit(StageExitReason.UserRequested),
                Is.EqualTo(TransitionResult.Succeeded));
            Assert.That(_controller.State, Is.EqualTo(StageState.Exited));
            Assert.That(_controller.ActivePauseReasonCount, Is.Zero);
            Assert.That(_controller.StateBeforePause, Is.Null);
            Assert.That(_controller.Start(), Is.EqualTo(TransitionResult.StageAlreadyTerminated));
            Assert.That(_controller.Complete(), Is.EqualTo(TransitionResult.StageAlreadyTerminated));
        }

        [Test]
        public void InvalidTransition_DoesNotChangeStateOrRaiseEvent()
        {
            int transitionCount = 0;
            _controller.StateChanged += _ => transitionCount++;

            Assert.That(_controller.Complete(), Is.EqualTo(TransitionResult.InvalidFromCurrentState));
            Assert.That(_controller.State, Is.EqualTo(StageState.None));
            Assert.That(transitionCount, Is.Zero);
        }

        [Test]
        public void ResumeUnknownReason_DoesNotChangePausedState()
        {
            EnterReady();
            _controller.Pause(PauseReason.User);

            Assert.That(
                _controller.Resume(PauseReason.ApplicationBackground),
                Is.EqualTo(TransitionResult.BlockedByPauseReason));
            Assert.That(_controller.State, Is.EqualTo(StageState.Paused));
            Assert.That(_controller.HasPauseReason(PauseReason.User), Is.True);
        }

        private void EnterReady()
        {
            _controller.Start();
            _controller.FinishInitialization();
        }
    }
}
