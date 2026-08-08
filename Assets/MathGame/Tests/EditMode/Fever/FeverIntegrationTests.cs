using System;
using System.Collections.Generic;
using System.Linq;
using MathGame.Answer;
using MathGame.Board;
using MathGame.BoardResolution;
using MathGame.Connection;
using MathGame.Core.Random;
using MathGame.Core.Time;
using MathGame.Fever;
using MathGame.Stage;
using MathGame.StageSession;
using NUnit.Framework;
using DomainBoard = MathGame.Board.Board;
using Session = MathGame.StageSession.StageSession;

namespace MathGame.Tests
{
    public sealed class FeverIntegrationTests
    {
        [Test]
        public void ChargeCapsWithoutCarryAndRejectsDuplicatesWrongModeAndNotCharging()
        {
            var session = CreateSession(10);
            var tracker = new FeverChargeTracker(30);
            var first = session.ApplyAttempt(Attempt(1, SpeedGrade.Perfect, 2));
            Assert.That(tracker.ApplyNormalAttempt(first), Is.EqualTo(FeverChargeApplyResult.Applied));
            Assert.That(tracker.Gauge, Is.EqualTo(25));
            Assert.That(tracker.ApplyNormalAttempt(first), Is.EqualTo(FeverChargeApplyResult.StaleOrDuplicateAttempt));
            var second = session.ApplyAttempt(Attempt(2, SpeedGrade.Normal, 2));
            Assert.That(tracker.ApplyNormalAttempt(second), Is.EqualTo(FeverChargeApplyResult.ReachedMaximum));
            Assert.That(tracker.Gauge, Is.EqualTo(30));
            Assert.That(tracker.ApplyNormalAttempt(second), Is.EqualTo(FeverChargeApplyResult.StaleOrDuplicateAttempt));

            var fever = session.ApplyAttempt(WithRules(Attempt(3, SpeedGrade.Normal, 2), StageAttemptRules.CreateFever(1)));
            var other = new FeverChargeTracker(100);
            Assert.That(other.ApplyNormalAttempt(fever), Is.EqualTo(FeverChargeApplyResult.WrongMode));
            Assert.That(other.ApplyNormalAttempt(null), Is.EqualTo(FeverChargeApplyResult.MissingResult));
        }

        [Test]
        public void ChargeAcceptsGlobalAttemptGapsAndMissPreservesGauge()
        {
            var session = CreateSession(10);
            var tracker = new FeverChargeTracker(100);
            Assert.That(tracker.ApplyNormalAttempt(session.ApplyAttempt(Attempt(1, SpeedGrade.Perfect, 2))), Is.EqualTo(FeverChargeApplyResult.Applied));
            session.ApplyAttempt(WithRules(Attempt(2, SpeedGrade.Normal, 2), StageAttemptRules.CreateFever(1)));
            var miss = session.ApplyAttempt(Miss(3));
            Assert.That(tracker.ApplyNormalAttempt(miss), Is.EqualTo(FeverChargeApplyResult.AppliedMiss));
            Assert.That(tracker.Gauge, Is.EqualTo(25));
            Assert.That(tracker.LastConsumedNormalAttemptId.Value, Is.EqualTo(3));
        }

        [TestCase(1, 1)] [TestCase(2, 2)] [TestCase(3, 3)] [TestCase(4, 5)] [TestCase(5, 5)]
        public void ControllerAppliesExactComboAndScoreMultiplier(int correctCount, int expectedMultiplier)
        {
            using var fixture = ActiveController();
            FeverAttemptResult result = null;
            for (var index = 0; index < correctCount; index++)
            {
                fixture.Stage.BeginAnswerResolution();
                var command = Attempt(index + 2, SpeedGrade.Normal, 2);
                result = fixture.Controller.ApplyFeverAttempt(command.Id, command.Answer, command.Resolution);
                if (index + 1 < correctCount) ReturnToFever(fixture.Stage);
            }
            Assert.That(result.Status, Is.EqualTo(FeverAttemptApplyStatus.AppliedContinue));
            Assert.That(result.Modifiers.ScoreMultiplier, Is.EqualTo(expectedMultiplier));
            Assert.That(result.StageResult.ScoreMultiplier, Is.EqualTo(expectedMultiplier));
            Assert.That(result.StageResult.MoveCost, Is.Zero);
            Assert.That(result.After.TotalCorrectAnswers, Is.EqualTo(correctCount));
            Assert.That(result.After.CurrentMultiplier, Is.EqualTo(expectedMultiplier));
        }

        [Test]
        public void CorrectCorrectMissCorrectResetsComboButKeepsTotal()
        {
            using var fixture = ActiveController();
            ApplyCorrect(fixture, 2); ReturnToFever(fixture.Stage);
            ApplyCorrect(fixture, 3); ReturnToFever(fixture.Stage);
            fixture.Stage.BeginAnswerResolution();
            var miss = Miss(4);
            var missed = fixture.Controller.ApplyFeverAttempt(miss.Id, miss.Answer, (BoardResolutionResult)null);
            Assert.That(missed.Status, Is.EqualTo(FeverAttemptApplyStatus.AppliedMiss));
            Assert.That(missed.After.CurrentCombo, Is.Zero);
            Assert.That(missed.After.TotalCorrectAnswers, Is.EqualTo(2));
            fixture.Stage.FinishFeverMissResolution();
            var final = ApplyCorrect(fixture, 5);
            Assert.That(final.After.TotalCorrectAnswers, Is.EqualTo(3));
            Assert.That(final.After.CurrentCombo, Is.EqualTo(1));
            Assert.That(final.Modifiers.ScoreMultiplier, Is.EqualTo(1));
        }

        [Test]
        public void RejectedFeverAttemptIsAtomicAndPreservesSession()
        {
            using var fixture = ActiveController();
            fixture.Stage.BeginAnswerResolution();
            var command = Attempt(3, SpeedGrade.Normal, 2);
            var before = fixture.Controller.SessionSnapshot;
            var result = fixture.Controller.ApplyFeverAttempt(command.Id, command.Answer, command.Resolution);
            Assert.That(result.Status, Is.EqualTo(FeverAttemptApplyStatus.InvalidAttempt));
            Assert.That(result.Before, Is.SameAs(before));
            Assert.That(result.After, Is.SameAs(before));
            Assert.That(fixture.Session.CreateSnapshot().NextExpectedAttemptId.Value, Is.EqualTo(2));
        }

        [Test]
        public void StageSessionFeverCostsZeroMultipliesScoreAndPreservesNormalStreak()
        {
            var session = CreateSession(3);
            var normal = session.ApplyAttempt(Attempt(1, SpeedGrade.Fast, 2));
            var fever = session.ApplyAttempt(WithRules(Attempt(2, SpeedGrade.Fast, 2), StageAttemptRules.CreateFever(3)));
            Assert.That(normal.MoveCost, Is.EqualTo(1));
            Assert.That(fever.MoveCost, Is.Zero);
            Assert.That(fever.After.RemainingMoves, Is.EqualTo(2));
            Assert.That(fever.Reward.ScoreAwarded, Is.EqualTo(375));
            Assert.That(fever.After.CurrentFastStreak, Is.EqualTo(1));
            Assert.That(fever.Reward.TotalFeverContribution, Is.Zero);
        }

        [Test]
        public void FeverScoreOverflowRejectsAtomically()
        {
            var definition = new StageDefinition(new StageDefinitionId(2), 3,
                new[] { new StageObjectiveDefinition(StageObjectiveKind.RemoveNumberBlocks, 99, default, 0) },
                new ScoreRewardConfig(long.MaxValue, 0, 0, 0, Array.Empty<ConnectionLengthScoreRule>()));
            Assert.That(Session.TryCreate(definition, out var session), Is.EqualTo(StageSessionCreateStatus.Succeeded));
            var before = session.CreateSnapshot();
            var result = session.ApplyAttempt(WithRules(Attempt(1, SpeedGrade.Normal, 2), StageAttemptRules.CreateFever(2)));
            Assert.That(result.Status, Is.EqualTo(StageAttemptApplyStatus.ArithmeticOverflow));
            Assert.That(result.After.Score, Is.EqualTo(before.Score));
            Assert.That(result.After.RemainingMoves, Is.EqualTo(before.RemainingMoves));
            Assert.That(result.After.NextExpectedAttemptId, Is.EqualTo(before.NextExpectedAttemptId));
            Assert.That(result.Events, Is.Empty);
        }

        [TestCase(0, FeverEndEffectTier.None)] [TestCase(1, FeverEndEffectTier.RandomThreeBlocks)]
        [TestCase(2, FeverEndEffectTier.SmallAreaExplosion)] [TestCase(3, FeverEndEffectTier.CenterAreaExplosion)]
        [TestCase(4, FeverEndEffectTier.LargeExplosionAndRestoration)] [TestCase(6, FeverEndEffectTier.LargeExplosionAndRestoration)]
        public void NaturalExpiryClassifiesTierAndAcknowledgementResets(int count, FeverEndEffectTier tier)
        {
            using var fixture = ActiveController();
            for (var i = 0; i < count; i++) { ApplyCorrect(fixture, i + 2); ReturnToFever(fixture.Stage); }
            fixture.Time.Value = 8;
            Assert.That(fixture.Controller.Tick(), Is.EqualTo(FeverControllerTickResult.EndingBegan));
            Assert.That(fixture.Controller.PendingEndResult.EffectTier, Is.EqualTo(tier));
            Assert.That(fixture.Controller.PendingEndResult.TotalCorrectAnswers, Is.EqualTo(count));
            Assert.That(fixture.Controller.Tick(), Is.EqualTo(FeverControllerTickResult.AlreadyEnding));
            Assert.That(fixture.Controller.CompleteEnding(false), Is.EqualTo(FeverControllerCommandResult.InvalidFromCurrentState));
            Assert.That(fixture.Controller.PendingEndResult, Is.Not.Null);
            Assert.That(fixture.Controller.CompleteEnding(true), Is.EqualTo(FeverControllerCommandResult.Succeeded));
            Assert.That(fixture.Controller.State, Is.EqualTo(FeverState.Charging));
            Assert.That(fixture.Controller.Gauge, Is.Zero);
            Assert.That(fixture.Controller.PendingEndResult, Is.Null);
        }

        [Test]
        public void TerminalStageAbortsAndSuppressesEndIntent()
        {
            using var fixture = ActiveController();
            fixture.Stage.Complete();
            Assert.That(fixture.Controller.State, Is.EqualTo(FeverState.Aborted));
            Assert.That(fixture.Controller.Gauge, Is.Zero);
            Assert.That(fixture.Controller.PendingEndResult, Is.Null);
        }

        [Test]
        public void CreateEntryAndDisposalFailuresAreGuarded()
        {
            Assert.That(FeverController.TryCreate(new FeverConfig(0, 8), null, null, null, out _), Is.EqualTo(FeverControllerCreateResult.InvalidMaximumGauge));
            Assert.That(FeverController.TryCreate(new FeverConfig(1, double.NaN), null, null, null, out _), Is.EqualTo(FeverControllerCreateResult.InvalidDuration));
            using var fixture = ActiveController();
            fixture.Controller.Dispose();
            Assert.That(fixture.Controller.Tick(), Is.EqualTo(FeverControllerTickResult.Disposed));
            Assert.That(fixture.Controller.CompleteEntry(), Is.EqualTo(FeverControllerCommandResult.Disposed));
        }

        [Test]
        public void PendingControllerRejectsFurtherChargeAndUnsafeEntryIsAtomic()
        {
            var session = CreateSession(10); var stage = PresentedStage(); var time = new FakeTime();
            FeverController.TryCreate(new FeverConfig(25, 8), stage, session, time, out var controller);
            using (controller)
            {
                var first = session.ApplyAttempt(Attempt(1, SpeedGrade.Perfect, 2));
                Assert.That(controller.ApplyNormalAttempt(first), Is.EqualTo(FeverChargeApplyResult.ReachedMaximum));
                Assert.That(controller.ApplyNormalAttempt(first), Is.EqualTo(FeverChargeApplyResult.NotCharging));
                Assert.That(controller.BeginEntry(false, true), Is.EqualTo(FeverControllerCommandResult.UnsafeEntry));
                Assert.That(controller.State, Is.EqualTo(FeverState.PendingEntry));
                Assert.That(stage.State, Is.EqualTo(StageState.PresentingTarget));
            }
        }

        [Test]
        public void NonfiniteTransitionSampleFaultsBeforeInputCanRemainEnabled()
        {
            using var fixture = ActiveController();
            fixture.Stage.BeginAnswerResolution();
            fixture.Stage.BeginTargetPresentation();
            fixture.Time.Value = double.PositiveInfinity;
            fixture.Stage.EnableFeverInput();
            Assert.That(fixture.Controller.State, Is.EqualTo(FeverState.Faulted));
            Assert.That(fixture.Stage.State, Is.EqualTo(StageState.EndingFever));
        }

        [Test]
        public void WrongPhasePausedAndTerminalFeverCommandsEmitNoEvents()
        {
            var stage = PresentedStage(); var transitions = new List<StageTransition>(); stage.StateChanged += transitions.Add;
            var baseline = transitions.Count;
            Assert.That(stage.CompleteFeverEntry(), Is.EqualTo(TransitionResult.InvalidFromCurrentState));
            Assert.That(stage.BeginFeverEnding(), Is.EqualTo(TransitionResult.InvalidFromCurrentState));
            Assert.That(transitions.Count, Is.EqualTo(baseline));
            stage.BeginFeverEntry(); stage.Pause(PauseReason.User); baseline = transitions.Count;
            Assert.That(stage.CompleteFeverEntry(), Is.EqualTo(TransitionResult.InvalidFromCurrentState));
            Assert.That(transitions.Count, Is.EqualTo(baseline));
            stage.Resume(PauseReason.User); stage.Complete(); baseline = transitions.Count;
            Assert.That(stage.FinishFeverEnding(), Is.EqualTo(TransitionResult.StageAlreadyTerminated));
            Assert.That(transitions.Count, Is.EqualTo(baseline));
        }

        [Test]
        public void PausedBackwardFaultEndsImmediatelyOnResume()
        {
            using var fixture = ActiveController();
            fixture.Time.Value = 4;
            fixture.Stage.Pause(PauseReason.ApplicationFocusLost);
            fixture.Time.Value = 3;
            fixture.Stage.Resume(PauseReason.ApplicationFocusLost);
            Assert.That(fixture.Controller.State, Is.EqualTo(FeverState.Faulted));
            Assert.That(fixture.Stage.State, Is.EqualTo(StageState.EndingFever));
        }

        private static FeverAttemptResult ApplyCorrect(Fixture fixture, long id)
        { fixture.Stage.BeginAnswerResolution(); var command = Attempt(id, SpeedGrade.Normal, 2); return fixture.Controller.ApplyFeverAttempt(command.Id, command.Answer, command.Resolution); }
        private static void ReturnToFever(StageController stage) { stage.BeginTargetPresentation(); stage.EnableFeverInput(); }
        private static Fixture ActiveController()
        {
            var session = CreateSession(20); var stage = PresentedStage(); var time = new FakeTime();
            FeverController.TryCreate(new FeverConfig(25, 8), stage, session, time, out var controller);
            controller.ApplyNormalAttempt(session.ApplyAttempt(Attempt(1, SpeedGrade.Perfect, 2)));
            Assert.That(controller.BeginEntry(true, true), Is.EqualTo(FeverControllerCommandResult.Succeeded));
            Assert.That(controller.CompleteEntry(), Is.EqualTo(FeverControllerCommandResult.Succeeded));
            return new Fixture(stage, session, time, controller);
        }
        private static StageController PresentedStage() { var stage = new StageController(); stage.Start(); stage.FinishInitialization(); stage.BeginTargetPresentation(); return stage; }
        private static Session CreateSession(int moves) { var definition = new StageDefinition(new StageDefinitionId(1), moves, new[] { new StageObjectiveDefinition(StageObjectiveKind.RemoveNumberBlocks, 999, default, 0) }, new ScoreRewardConfig(100, 25, 15, 5, new[] { new ConnectionLengthScoreRule(2, 10) })); Assert.That(Session.TryCreate(definition, out var session), Is.EqualTo(StageSessionCreateStatus.Succeeded)); return session; }
        private static StageAttemptCommand WithRules(StageAttemptCommand command, StageAttemptRules rules) => new StageAttemptCommand(command.Id, command.Answer, command.Resolution, rules);
        private static StageAttemptCommand Attempt(long id, SpeedGrade grade, int count)
        { var board = new DomainBoard(BoardTopology.CreateRectangular(count, 1)); var path = new ConnectionPath(board); for (var i = 0; i < count; i++) { var p = new BoardPosition(i, 0); board.TryPlaceBlock(p, new NumberBlock(new BlockId(i + 1), 1)); path.TrySelect(p); } var elapsed = grade == SpeedGrade.Perfect ? 1 : grade == SpeedGrade.Fast ? 3 : 5; var answer = new AnswerValidator(AnswerTimingThresholds.Prototype).Evaluate(path.CreateSnapshot(), new TargetNumber(count), elapsed); var resolution = new BoardResolver(new ConstantRandom()).Resolve(new BoardResolutionRequest(board, answer, new RefillValueRange(1, 9), count + 1)); return new StageAttemptCommand(new StageAttemptId(id), answer, resolution); }
        private static StageAttemptCommand Miss(long id) { var board = new DomainBoard(BoardTopology.CreateRectangular(1, 1)); board.TryPlaceBlock(default, new NumberBlock(new BlockId(1), 1)); var path = new ConnectionPath(board); path.TrySelect(default); var answer = new AnswerValidator(AnswerTimingThresholds.Prototype).Evaluate(path.CreateSnapshot(), new TargetNumber(2), 1); return new StageAttemptCommand(new StageAttemptId(id), answer, null); }
        private sealed class ConstantRandom : IRandomSource { public int NextInt(int minInclusive, int maxExclusive) => minInclusive; public float NextFloat() => 0; }
        private sealed class FakeTime : ITimeProvider { public double Value; public double RealtimeSeconds => Value; }
        private sealed class Fixture : IDisposable { public Fixture(StageController stage, Session session, FakeTime time, FeverController controller) { Stage = stage; Session = session; Time = time; Controller = controller; } public StageController Stage { get; } public Session Session { get; } public FakeTime Time { get; } public FeverController Controller { get; } public void Dispose() => Controller.Dispose(); }
    }
}
