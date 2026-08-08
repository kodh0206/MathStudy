using MathGame.Answer;
using MathGame.Core.Time;
using MathGame.Stage;
using NUnit.Framework;

namespace MathGame.Tests.PlayMode
{
    public sealed class InteractiveAnswerClockTests
    {
        [Test]
        public void ArmWaitsForInputAndMissResolutionContinuesSameElapsedTime()
        {
            var stage = Ready();
            var time = new ManualTime();
            using var clock = new InteractiveAnswerClock(stage, time);
            Assert.That(clock.Arm(), Is.EqualTo(AnswerClockCommandResult.Succeeded));
            time.Now = 10;
            Assert.That(clock.ElapsedSeconds, Is.Zero);
            stage.BeginTargetPresentation(); stage.EnablePlayerInput();
            time.Now = 12;
            Assert.That(clock.ElapsedSeconds, Is.EqualTo(2));
            stage.BeginAnswerResolution();
            time.Now = 20;
            Assert.That(clock.ElapsedSeconds, Is.EqualTo(2));
            stage.FinishMissResolution();
            time.Now = 23;
            Assert.That(clock.ElapsedSeconds, Is.EqualTo(5));
        }

        [Test]
        public void NestedPauseReasonsDoNotResumeUntilFinalReasonClears()
        {
            var stage = InputStage(); var time = new ManualTime();
            using var clock = new InteractiveAnswerClock(stage, time);
            clock.Arm(); time.Now = 2;
            stage.Pause(PauseReason.User); stage.Pause(PauseReason.ApplicationBackground);
            time.Now = 10; stage.Resume(PauseReason.User);
            Assert.That(clock.ElapsedSeconds, Is.EqualTo(2));
            stage.Resume(PauseReason.ApplicationBackground); time.Now = 13;
            Assert.That(clock.ElapsedSeconds, Is.EqualTo(5));
        }

        [TestCase(PauseReason.ApplicationBackground)]
        [TestCase(PauseReason.ApplicationFocusLost)]
        [TestCase(PauseReason.Advertisement)]
        [TestCase(PauseReason.SystemInterruption)]
        public void EveryExternalPauseExcludesTime(PauseReason reason)
        {
            var stage = InputStage(); var time = new ManualTime();
            using var clock = new InteractiveAnswerClock(stage, time);
            clock.Arm(); time.Now = 1; stage.Pause(reason); time.Now = 100;
            stage.Resume(reason); time.Now = 102;
            Assert.That(clock.ElapsedSeconds, Is.EqualTo(3));
        }

        [Test]
        public void StopFreezesAndResetCommandTableIsDeterministic()
        {
            var stage = InputStage(); var time = new ManualTime();
            using var clock = new InteractiveAnswerClock(stage, time);
            Assert.That(clock.Stop(), Is.EqualTo(AnswerClockCommandResult.InvalidFromCurrentState));
            clock.Arm(); Assert.That(clock.Arm(), Is.EqualTo(AnswerClockCommandResult.AlreadyArmed));
            Assert.That(clock.Reset(), Is.EqualTo(AnswerClockCommandResult.InvalidFromCurrentState));
            time.Now = 3; Assert.That(clock.Stop(), Is.EqualTo(AnswerClockCommandResult.Succeeded));
            time.Now = 30; Assert.That(clock.ElapsedSeconds, Is.EqualTo(3));
            Assert.That(clock.Stop(), Is.EqualTo(AnswerClockCommandResult.AlreadyStopped));
            Assert.That(clock.Reset(), Is.EqualTo(AnswerClockCommandResult.Succeeded));
            Assert.That(clock.ElapsedSeconds, Is.Zero);
        }

        [Test]
        public void InvalidTimeSamplesFaultAtomicallyAndResetClearsFault()
        {
            var stage = InputStage(); var time = new ManualTime();
            using var clock = new InteractiveAnswerClock(stage, time);
            clock.Arm(); time.Now = 2; Assert.That(clock.ElapsedSeconds, Is.EqualTo(2));
            time.Now = 1; Assert.That(clock.ElapsedSeconds, Is.EqualTo(2));
            Assert.That(clock.State, Is.EqualTo(AnswerClockState.Faulted));
            Assert.That(clock.Fault, Is.EqualTo(AnswerClockFault.TimeMovedBackward));
            Assert.That(clock.Stop(), Is.EqualTo(AnswerClockCommandResult.Faulted));
            Assert.That(clock.Reset(), Is.EqualTo(AnswerClockCommandResult.Succeeded));
            Assert.That(clock.Fault, Is.EqualTo(AnswerClockFault.None));
            time.Now = double.NaN; Assert.That(clock.Arm(), Is.EqualTo(AnswerClockCommandResult.Faulted));
            Assert.That(clock.Fault, Is.EqualTo(AnswerClockFault.NonFiniteTime));
        }

        [Test]
        public void ArmedStopAndDisposeFollowContract()
        {
            var stage = Ready(); var time = new ManualTime();
            var clock = new InteractiveAnswerClock(stage, time);
            clock.Arm();
            Assert.That(clock.Stop(), Is.EqualTo(AnswerClockCommandResult.NotStarted));
            clock.Dispose(); clock.Dispose();
            Assert.That(clock.State, Is.EqualTo(AnswerClockState.Disposed));
            Assert.That(clock.Arm(), Is.EqualTo(AnswerClockCommandResult.Disposed));
            Assert.That(clock.Reset(), Is.EqualTo(AnswerClockCommandResult.Disposed));
        }

        private static StageController Ready()
        {
            var stage = new StageController(); stage.Start(); stage.FinishInitialization(); return stage;
        }
        private static StageController InputStage()
        {
            var stage = Ready(); stage.BeginTargetPresentation(); stage.EnablePlayerInput(); return stage;
        }
        private sealed class ManualTime : ITimeProvider
        {
            public double Now { get; set; }
            public double RealtimeSeconds => Now;
        }
    }
}
