using MathGame.Answer;
using MathGame.Core.Time;
using MathGame.Stage;
using NUnit.Framework;

namespace MathGame.Tests.PlayMode
{
    public sealed class StageRecoveryTests
    {
        [Test]
        public void RecoveryPhaseRejectsInputPausesAndSuspendsClock()
        {
            var stage = new StageController(); stage.Start(); stage.FinishInitialization();
            stage.BeginTargetPresentation(); stage.EnablePlayerInput();
            var time = new ManualTime();
            using var clock = new InteractiveAnswerClock(stage, time);
            clock.Arm(); time.Now = 2;
            Assert.That(stage.BeginDeadlockRecovery(), Is.EqualTo(TransitionResult.Succeeded));
            Assert.That(stage.State, Is.EqualTo(StageState.RecoveringBoard));
            Assert.That(stage.AcceptsPlayerInput, Is.False);
            time.Now = 10; Assert.That(clock.ElapsedSeconds, Is.EqualTo(2));
            stage.Pause(PauseReason.ApplicationFocusLost);
            Assert.That(stage.StateBeforePause, Is.EqualTo(StageState.RecoveringBoard));
            stage.Resume(PauseReason.ApplicationFocusLost);
            Assert.That(stage.State, Is.EqualTo(StageState.RecoveringBoard));
            Assert.That(stage.BeginTargetPresentation(), Is.EqualTo(TransitionResult.Succeeded));
        }

        private sealed class ManualTime : ITimeProvider
        {
            public double Now { get; set; }
            public double RealtimeSeconds => Now;
        }
    }
}
