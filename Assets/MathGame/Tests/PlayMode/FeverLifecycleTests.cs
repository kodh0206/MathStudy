using MathGame.Core.Time;
using MathGame.Fever;
using MathGame.Stage;
using NUnit.Framework;

namespace MathGame.Tests.PlayMode
{
    public sealed class FeverLifecycleTests
    {
        [Test]
        public void FeverClockExcludesResolutionAndNestedFocusPauseWithoutSleeping()
        {
            var stage = Presented();
            var time = new ManualTime();
            using var clock = new InteractiveFeverClock(stage, time, 8);
            clock.Arm();
            stage.EnableFeverInput();
            time.Now = 2;
            stage.BeginAnswerResolution();
            time.Now = 20;
            stage.BeginTargetPresentation();
            stage.EnableFeverInput();
            time.Now = 21;
            stage.Pause(PauseReason.ApplicationFocusLost);
            stage.Pause(PauseReason.ApplicationBackground);
            time.Now = 100;
            stage.Resume(PauseReason.ApplicationFocusLost);
            Assert.That(clock.ElapsedSeconds, Is.EqualTo(3));
            stage.Resume(PauseReason.ApplicationBackground);
            time.Now = 105;
            Assert.That(clock.Tick(), Is.EqualTo(FeverClockResult.JustExpired));
            Assert.That(clock.ElapsedSeconds, Is.EqualTo(8));
        }

        [Test]
        public void DisposedClockNoLongerObservesStageTransitions()
        {
            var stage = Presented();
            var time = new ManualTime();
            var clock = new InteractiveFeverClock(stage, time, 8);
            clock.Arm();
            clock.Dispose();
            stage.EnableFeverInput();
            time.Now = 20;
            Assert.That(clock.State, Is.EqualTo(FeverClockState.Disposed));
            Assert.That(clock.Tick(), Is.EqualTo(FeverClockResult.Disposed));
        }

        private static StageController Presented()
        {
            var stage = new StageController();
            stage.Start();
            stage.FinishInitialization();
            stage.BeginTargetPresentation();
            return stage;
        }

        private sealed class ManualTime : ITimeProvider
        {
            public double Now { get; set; }
            public double RealtimeSeconds => Now;
        }
    }
}
