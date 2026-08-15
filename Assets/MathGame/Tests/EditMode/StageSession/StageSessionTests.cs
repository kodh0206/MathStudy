using System;
using System.Linq;
using MathGame.Answer;
using MathGame.Board;
using MathGame.BoardResolution;
using MathGame.Connection;
using MathGame.Core.Random;
using MathGame.StageSession;
using NUnit.Framework;
using DomainBoard = MathGame.Board.Board;
using Session = MathGame.StageSession.StageSession;

namespace MathGame.Tests.StageSession
{
    public sealed class StageSessionTests
    {
        [Test]
        public void CreationValidationPrecedenceAndUnsupportedObjectivesAreExplicit()
        {
            Assert.That(Session.TryCreate(null, out _), Is.EqualTo(StageSessionCreateStatus.MissingDefinition));
            Assert.That(Session.TryCreate(new StageDefinition(default, 1, null, null), out _), Is.EqualTo(StageSessionCreateStatus.InvalidDefinitionId));
            var unsupported = Definition(2, new StageObjectiveDefinition(StageObjectiveKind.RemoveObstacle, 1, default, 0));
            Assert.That(Session.TryCreate(unsupported, out _), Is.EqualTo(StageSessionCreateStatus.UnsupportedObjective));
            var duplicate = Definition(2, Remove(2), Remove(3));
            Assert.That(Session.TryCreate(duplicate, out _), Is.EqualTo(StageSessionCreateStatus.DuplicateObjective));
        }

        [Test]
        public void CorrectAttemptCorrelatesResolutionScoresRewardsAndSucceedsOnFinalMove()
        {
            var session = Create(Definition(1, Remove(2)));
            var attempt = CorrectAttempt(1, SpeedGrade.Perfect, 2);
            var result = session.ApplyAttempt(attempt);

            Assert.That(result.Status, Is.EqualTo(StageAttemptApplyStatus.AppliedSuccess));
            Assert.That(result.MoveCost, Is.EqualTo(1));
            Assert.That(result.After.RemainingMoves, Is.Zero);
            Assert.That(result.After.Status, Is.EqualTo(StageSessionStatus.Success));
            Assert.That(result.After.Score, Is.EqualTo(135));
            Assert.That(result.Reward.GradeFeverContribution, Is.EqualTo(25));
            Assert.That(result.Reward.LengthRewardTier, Is.EqualTo(ConnectionLengthRewardTier.StandardRemoval));
            Assert.That(result.After.TotalRemovedNumberBlocks, Is.EqualTo(2));
            Assert.That(result.Events.Select(value => value.Kind), Is.EqualTo(new[]
            {
                StageSessionEventKind.AnswerAccepted, StageSessionEventKind.ScoreAwarded,
                StageSessionEventKind.ObjectiveProgressed, StageSessionEventKind.MoveConsumed,
                StageSessionEventKind.StageSucceeded
            }));
            Assert.That(session.ApplyAttempt(CorrectAttempt(2, SpeedGrade.Fast, 2)).Status,
                Is.EqualTo(StageAttemptApplyStatus.SessionAlreadyTerminal));
        }

        [Test]
        public void TwoObjectivesUseAndSemanticsAndFinalMoveIncompleteFails()
        {
            var session = Create(Definition(1, Remove(2), new StageObjectiveDefinition(
                StageObjectiveKind.CompleteTarget, 2, new TargetNumber(2), 0)));
            var result = session.ApplyAttempt(CorrectAttempt(1, SpeedGrade.Normal, 2));

            Assert.That(result.Status, Is.EqualTo(StageAttemptApplyStatus.AppliedFailure));
            Assert.That(result.After.Objectives[0].IsComplete, Is.True);
            Assert.That(result.After.Objectives[1].Current, Is.EqualTo(1));
            Assert.That(result.After.Status, Is.EqualTo(StageSessionStatus.Failure));
        }

        [Test]
        public void MissCostsNoMoveResetsFastStreakAndAdvancesSequence()
        {
            var session = Create(Definition(3, Remove(99)));
            session.ApplyAttempt(CorrectAttempt(1, SpeedGrade.Fast, 2));
            var miss = MissAttempt(2);
            var result = session.ApplyAttempt(miss);

            Assert.That(result.Status, Is.EqualTo(StageAttemptApplyStatus.AppliedMiss));
            Assert.That(result.MoveCost, Is.Zero);
            Assert.That(result.After.RemainingMoves, Is.EqualTo(2));
            Assert.That(result.After.MissCount, Is.EqualTo(1));
            Assert.That(result.After.CurrentFastStreak, Is.Zero);
            Assert.That(result.Events.Single().Kind, Is.EqualTo(StageSessionEventKind.MissRecorded));
            Assert.That(result.After.NextExpectedAttemptId.Value, Is.EqualTo(3));
        }

        [Test]
        public void FastStreakAndLengthRewardsAreSemanticAndHighestScoreRuleWins()
        {
            var session = Create(Definition(4, Remove(99)));
            session.ApplyAttempt(CorrectAttempt(1, SpeedGrade.Fast, 3));
            var second = session.ApplyAttempt(CorrectAttempt(2, SpeedGrade.Fast, 3));

            Assert.That(second.Reward.FastStreakFeverContribution, Is.EqualTo(5));
            Assert.That(second.Reward.LengthFeverContribution, Is.EqualTo(3));
            Assert.That(second.Reward.ScoreAwarded, Is.EqualTo(130));
            Assert.That(second.After.CurrentFastStreak, Is.EqualTo(2));
            Assert.That(second.After.MaximumFastStreak, Is.EqualTo(2));
            Assert.That(second.After.TotalLongConnections, Is.EqualTo(2));
        }

        [Test]
        public void ContinuousRunTracksProgressButNeverConsumesMovesOrTerminalizesFromObjectives()
        {
            var legacy = Definition(1, Remove(2));
            var runDefinition = new StageDefinition(legacy.Id, legacy.InitialMoves, legacy.Objectives,
                legacy.ScoreConfig, null, StageSessionMode.ContinuousRun);
            var session = Create(runDefinition);

            var result = session.ApplyAttempt(CorrectAttempt(1, SpeedGrade.Perfect, 2));

            Assert.That(result.Status, Is.EqualTo(StageAttemptApplyStatus.AppliedContinue));
            Assert.That(result.MoveCost, Is.Zero);
            Assert.That(result.After.Mode, Is.EqualTo(StageSessionMode.ContinuousRun));
            Assert.That(result.After.RemainingMoves, Is.EqualTo(1));
            Assert.That(result.After.SpentMoves, Is.Zero);
            Assert.That(result.After.Objectives[0].IsComplete, Is.True);
            Assert.That(result.After.Status, Is.EqualTo(StageSessionStatus.Active));
            Assert.That(result.Events.Any(e => e.Kind is StageSessionEventKind.MoveConsumed or StageSessionEventKind.StageSucceeded), Is.False);
        }

        [Test]
        public void ContinuousRunPermitsNoObjectivesWhileLegacyStageStillRequiresOne()
        {
            var score = Definition(1, Remove(2)).ScoreConfig;
            var run = new StageDefinition(new StageDefinitionId(1), 1,
                Array.Empty<StageObjectiveDefinition>(), score, null, StageSessionMode.ContinuousRun);
            var legacy = new StageDefinition(new StageDefinitionId(1), 1,
                Array.Empty<StageObjectiveDefinition>(), score);

            Assert.That(Session.TryCreate(run, out var session), Is.EqualTo(StageSessionCreateStatus.Succeeded));
            Assert.That(session.CreateSnapshot().Objectives, Is.Empty);
            Assert.That(Session.TryCreate(legacy, out _), Is.EqualTo(StageSessionCreateStatus.InvalidObjectiveCount));
        }

        [Test]
        public void AttemptOrderingAndUnexpectedResolutionRejectAtomically()
        {
            var session = Create(Definition(3, Remove(99)));
            var initial = session.CreateSnapshot();
            AssertRejected(session.ApplyAttempt(null), StageAttemptApplyStatus.MissingCommand, initial);
            AssertRejected(session.ApplyAttempt(CorrectAttempt(2, SpeedGrade.Fast, 2)), StageAttemptApplyStatus.OutOfOrderAttempt, initial);
            var miss = MissAttempt(1);
            var unexpected = new StageAttemptCommand(miss.Id, miss.Answer, CorrectAttempt(1, SpeedGrade.Fast, 2).Resolution);
            AssertRejected(session.ApplyAttempt(unexpected), StageAttemptApplyStatus.UnexpectedResolution, initial);
            Assert.That(session.ApplyAttempt(miss).Status, Is.EqualTo(StageAttemptApplyStatus.AppliedMiss));
            Assert.That(session.ApplyAttempt(miss).Status, Is.EqualTo(StageAttemptApplyStatus.DuplicateAttempt));
        }

        [TestCase(1, ConnectionLengthRewardTier.None)]
        [TestCase(2, ConnectionLengthRewardTier.StandardRemoval)]
        [TestCase(3, ConnectionLengthRewardTier.ExtraFeverRequested)]
        [TestCase(4, ConnectionLengthRewardTier.BasicSpecialRequested)]
        [TestCase(5, ConnectionLengthRewardTier.EnhancedAreaSpecialRequested)]
        public void LengthClassifierHasExactBoundaries(int length, ConnectionLengthRewardTier tier)
        {
            Assert.That(ConnectionLengthRewardClassifier.Classify(length), Is.EqualTo(tier));
        }

        private static StageDefinition Definition(int moves, params StageObjectiveDefinition[] objectives)
        {
            return new StageDefinition(new StageDefinitionId(1), moves, objectives,
                new ScoreRewardConfig(100, 25, 15, 5, new[]
                {
                    new ConnectionLengthScoreRule(2, 10),
                    new ConnectionLengthScoreRule(3, 15)
                }));
        }

        private static StageObjectiveDefinition Remove(int count)
            => new StageObjectiveDefinition(StageObjectiveKind.RemoveNumberBlocks, count, default, 0);

        private static Session Create(StageDefinition definition)
        {
            Assert.That(Session.TryCreate(definition, out var session), Is.EqualTo(StageSessionCreateStatus.Succeeded));
            return session;
        }

        private static StageAttemptCommand CorrectAttempt(long attemptId, SpeedGrade grade, int count)
        {
            var board = new DomainBoard(BoardTopology.CreateRectangular(count, 1));
            var path = new ConnectionPath(board);
            for (var index = 0; index < count; index++)
            {
                var position = new BoardPosition(index, 0);
                board.TryPlaceBlock(position, new NumberBlock(new BlockId(index + 1), 1));
                path.TrySelect(position);
            }
            var elapsed = grade == SpeedGrade.Perfect ? 1 : grade == SpeedGrade.Fast ? 3 : 5;
            var answer = new AnswerValidator(AnswerTimingThresholds.Prototype).Evaluate(path.CreateSnapshot(), new TargetNumber(count), elapsed);
            var resolution = new BoardResolver(new ConstantRandom(1)).Resolve(
                new BoardResolutionRequest(board, answer, new RefillValueRange(1, 9), count + 1));
            return new StageAttemptCommand(new StageAttemptId(attemptId), answer, resolution);
        }

        private static StageAttemptCommand MissAttempt(long attemptId)
        {
            var board = new DomainBoard(BoardTopology.CreateRectangular(1, 1));
            board.TryPlaceBlock(default, new NumberBlock(new BlockId(1), 1));
            var path = new ConnectionPath(board); path.TrySelect(default);
            var answer = new AnswerValidator(AnswerTimingThresholds.Prototype).Evaluate(path.CreateSnapshot(), new TargetNumber(2), 1);
            return new StageAttemptCommand(new StageAttemptId(attemptId), answer, null);
        }

        private static void AssertRejected(StageAttemptResult result, StageAttemptApplyStatus status, StageSessionSnapshot before)
        {
            Assert.That(result.Status, Is.EqualTo(status));
            Assert.That(result.Before.Score, Is.EqualTo(before.Score));
            Assert.That(result.After.Score, Is.EqualTo(before.Score));
            Assert.That(result.Events, Is.Empty);
            Assert.That(result.MoveCost, Is.Zero);
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
