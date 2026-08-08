using System;
using System.Collections.Generic;
using System.Linq;
using MathGame.Answer;
using MathGame.Board;
using MathGame.BoardResolution;
using MathGame.Connection;
using MathGame.Core.Random;
using MathGame.Stage;
using MathGame.StageSession;
using NUnit.Framework;
using DomainBoard = MathGame.Board.Board;
using Session = MathGame.StageSession.StageSession;

namespace MathGame.Tests.StageSession
{
    public sealed class StageSessionReviewTests
    {
        [Test]
        public void DifferentRequiredCountsWithSameConditionAreDuplicates()
        {
            var definition = new StageDefinition(
                new StageDefinitionId(1),
                2,
                new[] { Remove(2), Remove(3) },
                ValidScore());

            Assert.That(Session.TryCreate(definition, out var session),
                Is.EqualTo(StageSessionCreateStatus.DuplicateObjective));
            Assert.That(session, Is.Null);
        }

        [Test]
        public void TryCreateCoversEveryRemainingValidationStatusInPrecedenceOrder()
        {
            AssertCreate(new StageDefinition(new StageDefinitionId(1), 0, null, null), StageSessionCreateStatus.InvalidMoves);
            AssertCreate(new StageDefinition(new StageDefinitionId(1), 1, null, null), StageSessionCreateStatus.MissingObjectives);
            AssertCreate(new StageDefinition(new StageDefinitionId(1), 1, Array.Empty<StageObjectiveDefinition>(), ValidScore()), StageSessionCreateStatus.InvalidObjectiveCount);
            AssertCreate(new StageDefinition(new StageDefinitionId(1), 1, new StageObjectiveDefinition[] { null }, ValidScore()), StageSessionCreateStatus.MissingObjective);
            AssertCreate(new StageDefinition(new StageDefinitionId(1), 1, new[]
            {
                new StageObjectiveDefinition(StageObjectiveKind.UseSpecial, 0, default, -1)
            }, ValidScore()), StageSessionCreateStatus.UnsupportedObjective);
            AssertCreate(new StageDefinition(new StageDefinitionId(1), 1, new[]
            {
                new StageObjectiveDefinition(StageObjectiveKind.RemoveNumberBlocks, 0, default, 0)
            }, ValidScore()), StageSessionCreateStatus.InvalidObjective);
            AssertCreate(new StageDefinition(new StageDefinitionId(1), 1, new[] { Remove(1) }, null), StageSessionCreateStatus.MissingScoreConfig);
            AssertCreate(new StageDefinition(new StageDefinitionId(1), 1, new[] { Remove(1) },
                new ScoreRewardConfig(-1, 0, 0, 0, Array.Empty<ConnectionLengthScoreRule>())),
                StageSessionCreateStatus.InvalidScoreConfig);
        }

        [Test]
        public void DefinitionAndScoreInputsAreCopiedAndInvalidRulesAreRejected()
        {
            var objectives = new List<StageObjectiveDefinition> { Remove(1) };
            var rules = new List<ConnectionLengthScoreRule> { new ConnectionLengthScoreRule(2, 1) };
            var definition = new StageDefinition(new StageDefinitionId(1), 2, objectives,
                new ScoreRewardConfig(0, 0, 0, 0, rules));
            objectives.Clear(); rules.Clear();
            Assert.That(definition.Objectives, Has.Count.EqualTo(1));
            Assert.That(definition.ScoreConfig.LengthRules, Has.Count.EqualTo(1));
            Assert.That(Session.TryCreate(definition, out _), Is.EqualTo(StageSessionCreateStatus.Succeeded));

            Assert.That(Session.TryCreate(new StageDefinition(new StageDefinitionId(1), 1, new[] { Remove(1) },
                new ScoreRewardConfig(0, 0, 0, 0, null)), out _), Is.EqualTo(StageSessionCreateStatus.InvalidScoreConfig));
            Assert.That(Session.TryCreate(new StageDefinition(new StageDefinitionId(1), 1, new[] { Remove(1) },
                new ScoreRewardConfig(0, 0, 0, 0, new[] { new ConnectionLengthScoreRule(1, 0) })), out _),
                Is.EqualTo(StageSessionCreateStatus.InvalidScoreConfig));
        }

        [Test]
        public void TargetAndLongConnectionObjectivesIgnoreUnrelatedFactsAndClampInDefinitionOrder()
        {
            var session = Create(3,
                new StageObjectiveDefinition(StageObjectiveKind.CompleteTarget, 1, new TargetNumber(9), 0),
                new StageObjectiveDefinition(StageObjectiveKind.CompleteLongConnection, 1, default, 3));
            var first = session.ApplyAttempt(Correct(1, SpeedGrade.Normal, 2));
            Assert.That(first.After.Objectives[0].Current, Is.Zero);
            Assert.That(first.After.Objectives[1].Current, Is.Zero);

            var second = session.ApplyAttempt(Correct(2, SpeedGrade.Normal, 3));
            Assert.That(second.After.Objectives[0].Current, Is.Zero);
            Assert.That(second.After.Objectives[1].Current, Is.EqualTo(1));
            Assert.That(second.Events.Where(value => value.Kind == StageSessionEventKind.ObjectiveProgressed)
                .Select(value => value.ObjectiveIndex), Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void CorrelationRejectsCountOrderAndValueMismatchAtomically()
        {
            var session = Create(4, Remove(99));
            var before = session.CreateSnapshot();
            var two = Correct(1, SpeedGrade.Fast, 2);
            var three = Correct(1, SpeedGrade.Fast, 3);
            AssertRejected(session.ApplyAttempt(new StageAttemptCommand(two.Id, two.Answer, three.Resolution)), before);

            var forward = BuildEvidence(2, false, 1);
            var reverse = BuildEvidence(2, true, 1);
            AssertRejected(session.ApplyAttempt(new StageAttemptCommand(new StageAttemptId(1), reverse.answer, forward.resolution)), before);

            var differentValues = BuildEvidence(2, false, 2);
            AssertRejected(session.ApplyAttempt(new StageAttemptCommand(new StageAttemptId(1), two.Answer, differentValues.resolution)), before);
        }

        [Test]
        public void NoSelectionAndAllThreeValidMissFormsBehaveExactly()
        {
            var session = Create(5, Remove(99));
            var validator = new AnswerValidator(AnswerTimingThresholds.Prototype);
            var emptyBoard = Board(1);
            var empty = validator.Evaluate(new ConnectionPath(emptyBoard).CreateSnapshot(), new TargetNumber(1), 0);
            Assert.That(session.ApplyAttempt(new StageAttemptCommand(new StageAttemptId(1), empty, null)).Status,
                Is.EqualTo(StageAttemptApplyStatus.InvalidAnswer));

            var under = MissAnswer(1, 2);
            Assert.That(session.ApplyAttempt(new StageAttemptCommand(new StageAttemptId(1), under, null)).Status,
                Is.EqualTo(StageAttemptApplyStatus.AppliedMiss));
            var over = MissAnswer(3, 2);
            Assert.That(session.ApplyAttempt(new StageAttemptCommand(new StageAttemptId(2), over, null)).Status,
                Is.EqualTo(StageAttemptApplyStatus.AppliedMiss));
            var insufficient = MissAnswer(2, 2);
            Assert.That(session.ApplyAttempt(new StageAttemptCommand(new StageAttemptId(3), insufficient, null)).Status,
                Is.EqualTo(StageAttemptApplyStatus.AppliedMiss));
            Assert.That(session.CreateSnapshot().MissCount, Is.EqualTo(3));
            Assert.That(session.CreateSnapshot().RemainingMoves, Is.EqualTo(5));
        }

        [Test]
        public void ZeroScoreOmitsScoreEventAndEarlySuccessPreservesMove()
        {
            var definition = new StageDefinition(new StageDefinitionId(1), 2, new[] { Remove(2) },
                new ScoreRewardConfig(0, 0, 0, 0, Array.Empty<ConnectionLengthScoreRule>()));
            Assert.That(Session.TryCreate(definition, out var session), Is.EqualTo(StageSessionCreateStatus.Succeeded));
            var result = session.ApplyAttempt(Correct(1, SpeedGrade.Normal, 2));
            Assert.That(result.Status, Is.EqualTo(StageAttemptApplyStatus.AppliedSuccess));
            Assert.That(result.After.RemainingMoves, Is.EqualTo(1));
            Assert.That(result.Reward.ScoreAwarded, Is.Zero);
            Assert.That(result.Events.Any(value => value.Kind == StageSessionEventKind.ScoreAwarded), Is.False);
        }

        [Test]
        public void PerfectNormalAndMissEachResetFastStreakWhileMaximumRemainsHistorical()
        {
            var session = Create(8, Remove(99));
            session.ApplyAttempt(Correct(1, SpeedGrade.Fast, 2));
            session.ApplyAttempt(Correct(2, SpeedGrade.Fast, 2));
            Assert.That(session.CreateSnapshot().CurrentFastStreak, Is.EqualTo(2));
            session.ApplyAttempt(Correct(3, SpeedGrade.Perfect, 2));
            Assert.That(session.CreateSnapshot().CurrentFastStreak, Is.Zero);
            session.ApplyAttempt(Correct(4, SpeedGrade.Fast, 2));
            session.ApplyAttempt(Correct(5, SpeedGrade.Normal, 2));
            Assert.That(session.CreateSnapshot().CurrentFastStreak, Is.Zero);
            session.ApplyAttempt(Correct(6, SpeedGrade.Fast, 2));
            session.ApplyAttempt(new StageAttemptCommand(new StageAttemptId(7), MissAnswer(1, 2), null));
            Assert.That(session.CreateSnapshot().CurrentFastStreak, Is.Zero);
            Assert.That(session.CreateSnapshot().MaximumFastStreak, Is.EqualTo(2));
        }

        [Test]
        public void SnapshotsResultsAndEventsAreHistoricalAndReadOnly()
        {
            var session = Create(3, Remove(99));
            var snapshot = session.CreateSnapshot();
            var result = session.ApplyAttempt(Correct(1, SpeedGrade.Fast, 2));
            Assert.That(snapshot.Score, Is.Zero);
            Assert.That(result.Before.Score, Is.Zero);
            Assert.Throws<NotSupportedException>(() => ((IList<ObjectiveProgressSnapshot>)result.After.Objectives).Add(null));
            Assert.Throws<NotSupportedException>(() => ((IList<StageSessionEvent>)result.Events).Add(default));
        }

        [Test]
        public void ResolvingAnswerIntegrationMapsContinueSuccessAndFailureWithoutEnablingInput()
        {
            AssertStageResult(Create(2, Remove(99)), StageAttemptApplyStatus.AppliedContinue, StageState.ResolvingAnswer);
            AssertStageResult(Create(2, Remove(2)), StageAttemptApplyStatus.AppliedSuccess, StageState.Success);
            AssertStageResult(Create(1, Remove(99)), StageAttemptApplyStatus.AppliedFailure, StageState.Failure);
        }

        [Test]
        public void ConfiguredScoreOverflowRejectsAtomicallyWithoutAdvancingAnything()
        {
            var definition = new StageDefinition(
                new StageDefinitionId(1),
                2,
                new[] { Remove(99) },
                new ScoreRewardConfig(long.MaxValue, 1, 0, 0, Array.Empty<ConnectionLengthScoreRule>()));
            Assert.That(Session.TryCreate(definition, out var session), Is.EqualTo(StageSessionCreateStatus.Succeeded));
            var before = session.CreateSnapshot();

            var result = session.ApplyAttempt(Correct(1, SpeedGrade.Perfect, 2));

            Assert.That(result.Status, Is.EqualTo(StageAttemptApplyStatus.ArithmeticOverflow));
            Assert.That(result.Before, Is.SameAs(result.After));
            var after = session.CreateSnapshot();
            Assert.That(after.NextExpectedAttemptId, Is.EqualTo(before.NextExpectedAttemptId));
            Assert.That(after.Score, Is.EqualTo(before.Score));
            Assert.That(after.RemainingMoves, Is.EqualTo(before.RemainingMoves));
            Assert.That(after.Objectives[0].Current, Is.EqualTo(before.Objectives[0].Current));
            Assert.That(after.CorrectCount, Is.EqualTo(before.CorrectCount));
            Assert.That(result.Events, Is.Empty);
        }

        private static void AssertStageResult(Session session, StageAttemptApplyStatus expected, StageState expectedState)
        {
            var stage = new StageController(); stage.Start(); stage.FinishInitialization();
            stage.BeginTargetPresentation(); stage.EnablePlayerInput(); stage.BeginAnswerResolution();
            var result = session.ApplyAttempt(Correct(1, SpeedGrade.Normal, 2));
            if (result.Status == StageAttemptApplyStatus.AppliedSuccess) stage.Complete();
            else if (result.Status == StageAttemptApplyStatus.AppliedFailure) stage.Fail();
            Assert.That(result.Status, Is.EqualTo(expected));
            Assert.That(stage.State, Is.EqualTo(expectedState));
            Assert.That(stage.AcceptsPlayerInput, Is.False);
        }

        private static Session Create(int moves, params StageObjectiveDefinition[] objectives)
        {
            var definition = new StageDefinition(new StageDefinitionId(1), moves, objectives,
                new ScoreRewardConfig(1, 1, 1, 1, Array.Empty<ConnectionLengthScoreRule>()));
            Assert.That(Session.TryCreate(definition, out var session), Is.EqualTo(StageSessionCreateStatus.Succeeded));
            return session;
        }

        private static StageObjectiveDefinition Remove(int count)
            => new StageObjectiveDefinition(StageObjectiveKind.RemoveNumberBlocks, count, default, 0);

        private static ScoreRewardConfig ValidScore()
            => new ScoreRewardConfig(0, 0, 0, 0, Array.Empty<ConnectionLengthScoreRule>());

        private static void AssertCreate(StageDefinition definition, StageSessionCreateStatus expected)
        {
            Assert.That(Session.TryCreate(definition, out var session), Is.EqualTo(expected));
            Assert.That(session, Is.Null);
        }

        private static StageAttemptCommand Correct(long id, SpeedGrade grade, int count)
        {
            var evidence = BuildEvidence(count, false, 1, grade);
            return new StageAttemptCommand(new StageAttemptId(id), evidence.answer, evidence.resolution);
        }

        private static (AnswerResult answer, BoardResolutionResult resolution) BuildEvidence(
            int count, bool reverse, int value, SpeedGrade grade = SpeedGrade.Fast)
        {
            var board = Board(Enumerable.Repeat(value, count).ToArray());
            var path = new ConnectionPath(board);
            var positions = board.EnumerateActivePositions().ToArray();
            if (reverse) Array.Reverse(positions);
            foreach (var position in positions) path.TrySelect(position);
            var elapsed = grade == SpeedGrade.Perfect ? 1 : grade == SpeedGrade.Fast ? 3 : 5;
            var answer = new AnswerValidator(AnswerTimingThresholds.Prototype).Evaluate(
                path.CreateSnapshot(), new TargetNumber(count * value), elapsed);
            var resolution = new BoardResolver(new ConstantRandom(1)).Resolve(
                new BoardResolutionRequest(board, answer, new RefillValueRange(1, 9), count + 1));
            return (answer, resolution);
        }

        private static AnswerResult MissAnswer(int value, int target)
        {
            var board = Board(value); var path = new ConnectionPath(board); path.TrySelect(default);
            return new AnswerValidator(AnswerTimingThresholds.Prototype).Evaluate(
                path.CreateSnapshot(), new TargetNumber(target), 1);
        }

        private static DomainBoard Board(params int[] values)
        {
            var board = new DomainBoard(BoardTopology.CreateRectangular(values.Length, 1));
            for (var index = 0; index < values.Length; index++)
                board.TryPlaceBlock(new BoardPosition(index, 0), new NumberBlock(new BlockId(index + 1), values[index]));
            return board;
        }

        private static void AssertRejected(StageAttemptResult result, StageSessionSnapshot before)
        {
            Assert.That(result.Status, Is.EqualTo(StageAttemptApplyStatus.AnswerResolutionMismatch));
            Assert.That(result.After.Score, Is.EqualTo(before.Score));
            Assert.That(result.After.NextExpectedAttemptId, Is.EqualTo(before.NextExpectedAttemptId));
            Assert.That(result.Events, Is.Empty);
        }

        private sealed class ConstantRandom : IRandomSource
        {
            private readonly int value;
            public ConstantRandom(int value) { this.value = value; }
            public int NextInt(int minInclusive, int maxExclusive) => value;
            public float NextFloat() => 0;
        }
    }
}
