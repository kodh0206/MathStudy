using System;
using MathGame.Core.Time;
using MathGame.Fever;
using MathGame.Stage;
using MathGame.StageSession;
using NUnit.Framework;

namespace MathGame.Tests
{
    public sealed class FeverCoreTests
    {
        [Test]
        public void PrototypeAndCreateValidationAreExact()
        {
            Assert.That(FeverConfig.Prototype.MaximumGauge, Is.EqualTo(100));
            Assert.That(FeverConfig.Prototype.DurationSeconds, Is.EqualTo(8d));
            Assert.That(FeverController.TryCreate(null, null, null, null, out var controller), Is.EqualTo(FeverControllerCreateResult.MissingConfig));
            Assert.That(controller, Is.Null);
            Assert.Throws<ArgumentOutOfRangeException>(() => new FeverChargeTracker(0));
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(5)]
        public void FeverRulesExposeClosedMultipliersAndZeroMoveCost(int multiplier)
        {
            var rules = StageAttemptRules.CreateFever(multiplier);
            Assert.That(rules.Mode, Is.EqualTo(StageAttemptMode.Fever));
            Assert.That(rules.CorrectMoveCost, Is.Zero);
            Assert.That(rules.ScoreMultiplier, Is.EqualTo(multiplier));
        }

        [TestCase(0)] [TestCase(4)] [TestCase(6)] [TestCase(-1)]
        public void FeverRulesRejectOtherMultipliers(int multiplier)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => StageAttemptRules.CreateFever(multiplier));
        }

        [Test]
        public void StageTracksResolutionOriginAndRequiresMatchingMissReturn()
        {
            var stage = PresentedStage();
            Assert.That(stage.EnableFeverInput(), Is.EqualTo(TransitionResult.Succeeded));
            Assert.That(stage.BeginAnswerResolution(), Is.EqualTo(TransitionResult.Succeeded));
            Assert.That(stage.ResolutionOrigin, Is.EqualTo(AnswerResolutionOrigin.Fever));
            Assert.That(stage.FinishMissResolution(), Is.EqualTo(TransitionResult.InvalidFromCurrentState));
            Assert.That(stage.FinishFeverMissResolution(), Is.EqualTo(TransitionResult.Succeeded));
            Assert.That(stage.ResolutionOrigin, Is.EqualTo(AnswerResolutionOrigin.None));
        }

        [Test]
        public void FeverClockCountsOnlyExactFeverInputAndExpiresOnce()
        {
            var stage = PresentedStage();
            var time = new FakeTime();
            using var clock = new InteractiveFeverClock(stage, time, 8);
            Assert.That(clock.Arm(), Is.EqualTo(FeverClockResult.Succeeded));
            stage.EnableFeverInput();
            time.Value = 3;
            Assert.That(clock.Tick(), Is.EqualTo(FeverClockResult.Succeeded));
            stage.BeginAnswerResolution();
            time.Value = 100;
            Assert.That(clock.ElapsedSeconds, Is.EqualTo(3));
            stage.FinishFeverMissResolution();
            time.Value = 105;
            Assert.That(clock.Tick(), Is.EqualTo(FeverClockResult.JustExpired));
            Assert.That(clock.Tick(), Is.EqualTo(FeverClockResult.AlreadyExpired));
        }

        [Test]
        public void FeverClockFaultsOnBackwardAndNonfiniteSamples()
        {
            var stage = PresentedStage(); stage.EnableFeverInput();
            var time = new FakeTime { Value = 4 };
            using var clock = new InteractiveFeverClock(stage, time, 8);
            Assert.That(clock.Arm(), Is.EqualTo(FeverClockResult.Succeeded));
            time.Value = 3;
            Assert.That(clock.Tick(), Is.EqualTo(FeverClockResult.InvalidTimeSource));
            Assert.That(clock.Fault, Is.EqualTo(FeverClockFault.TimeRegressed));
        }

        [Test]
        public void StageFeverGraphHonorsPauseAndTerminalPrecedence()
        {
            var stage = PresentedStage();
            Assert.That(stage.BeginFeverEntry(), Is.EqualTo(TransitionResult.Succeeded));
            Assert.That(stage.Pause(PauseReason.User), Is.EqualTo(TransitionResult.Succeeded));
            Assert.That(stage.CompleteFeverEntry(), Is.EqualTo(TransitionResult.InvalidFromCurrentState));
            Assert.That(stage.Resume(PauseReason.User), Is.EqualTo(TransitionResult.Succeeded));
            Assert.That(stage.CompleteFeverEntry(), Is.EqualTo(TransitionResult.Succeeded));
            Assert.That(stage.BeginFeverEnding(), Is.EqualTo(TransitionResult.Succeeded));
            Assert.That(stage.FinishFeverEnding(), Is.EqualTo(TransitionResult.Succeeded));
            Assert.That(stage.Complete(), Is.EqualTo(TransitionResult.Succeeded));
            Assert.That(stage.BeginFeverEntry(), Is.EqualTo(TransitionResult.StageAlreadyTerminated));
        }

        private static StageController PresentedStage()
        {
            var stage = new StageController();
            stage.Start(); stage.FinishInitialization(); stage.BeginTargetPresentation();
            return stage;
        }
        private sealed class FakeTime : ITimeProvider { public double Value; public double RealtimeSeconds => Value; }
    }
}
