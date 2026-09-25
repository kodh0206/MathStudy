using System.Collections.Generic;
using System.Linq;
using MathGame.Board;
using MathGame.BoardResolution;
using MathGame.Core.Random;
using MathGame.Obstacles;
using MathGame.Targets;
using MathGame.StageSession;
using MathGame.Answer;
using MathGame.Fever;
using MathGame.Presentation;
using MathGame.Connection;
using NUnit.Framework;
using DomainBoard = MathGame.Board.Board;

namespace MathGame.Tests.Obstacles
{
    public sealed class ObstacleResolutionTests
    {
        [Test]
        public void Builder_CreatesDustOverlayAndBoxOccupant()
        {
            var source = Full(3, 2);
            var layout = new ObstacleLayout(new[] { ObstacleLayoutEntry.Dust(new BoardPosition(0, 0), new ObstacleId(1)), ObstacleLayoutEntry.Box(new BoardPosition(1, 0), new ObstacleId(2)) });
            var result = new ObstacleBoardBuilder().Build(source, layout);
            Assert.That(result.Succeeded, Is.True); Assert.That(result.DiscardedSetupBlocks.Count, Is.EqualTo(1));
            result.Board.TryGetCell(new BoardPosition(0, 0), out var dust); result.Board.TryGetCell(new BoardPosition(1, 0), out var box);
            Assert.That(dust.IsSelectable && dust.HasDust, Is.True); Assert.That(box.HasBox && !box.HasBlock && box.IsGravityBarrier, Is.True); Assert.That(result.Board.ValidateStable().IsStable, Is.True);
        }

        [TestCase(FeverEndPattern.Small, 5)]
        [TestCase(FeverEndPattern.Center, 9)]
        [TestCase(FeverEndPattern.Large, 9)]
        public void AreaEndEffects_UseManhattanRadius(FeverEndPattern pattern, int expected)
        {
            var board = Full(3, 3); var random = new ConstantRandom(1); var resolver = new ObstacleBoardResolver(random);
            var result = resolver.Resolve(ObstacleResolutionRequest.FeverEnd(board, pattern, new BoardSystemEffectId(1), new BoardPosition(1, 1), new RefillValueRange(1, 1), 10));
            Assert.That(result.Succeeded, Is.True); Assert.That(result.Removed.Count, Is.EqualTo(expected)); Assert.That(result.Board.ValidateStable().IsStable, Is.True);
        }

        [Test]
        public void RandomThree_IsWithoutReplacementAndDustDamageComesFromRemoval()
        {
            var built = new ObstacleBoardBuilder().Build(Full(2, 2), new ObstacleLayout(new[] { ObstacleLayoutEntry.Dust(new BoardPosition(0, 0), new ObstacleId(8)) })).Board;
            var result = new ObstacleBoardResolver(new ConstantRandom(0)).Resolve(ObstacleResolutionRequest.FeverEnd(built, FeverEndPattern.RandomThree, new BoardSystemEffectId(1), null, new RefillValueRange(1, 1), 5));
            Assert.That(result.Removed.Select(x => x.Block.Id).Distinct().Count(), Is.EqualTo(3));
            Assert.That(result.DestroyedObstacles.Any(x => x.Kind == ObstacleKind.Dust), Is.True);
        }

        [Test]
        public void FeverEndPresentation_TelegraphsCommittedCellsBeforeAuthoritativeRemoval()
        {
            var result = new ObstacleBoardResolver(new ConstantRandom(0)).Resolve(
                ObstacleResolutionRequest.FeverEnd(Full(2, 2), FeverEndPattern.RandomThree,
                    new BoardSystemEffectId(1), null, new RefillValueRange(1, 1), 5));
            var events = ObstaclePresentationPlanBuilder.FeverEndEvents(result, FeverEndEffectTier.RandomThreeBlocks);
            Assert.That(events[0].Kind, Is.EqualTo(PresentationEventKind.FeverEndAnnouncement));
            var telegraphs = events.Where(x => x.Kind == PresentationEventKind.FeverEndTelegraph).ToArray();
            Assert.That(telegraphs.Select(x => x.Position), Is.EqualTo(result.Removed.Select(x => x.Position)));
            Assert.That(events.FindIndex(x => x.Kind == PresentationEventKind.FeverEndWave),
                Is.LessThan(events.FindIndex(x => x.Kind is PresentationEventKind.RemoveSelected or PresentationEventKind.RemoveCollateral)));
        }

        [Test]
        public void FeverEndPresentation_NoneHasAnnouncementAndFadeButNoBoardEffectEvents()
        {
            var result = new ObstacleBoardResolver(new ConstantRandom(0)).Resolve(
                ObstacleResolutionRequest.FeverEnd(Full(2, 2), FeverEndPattern.None,
                    new BoardSystemEffectId(1), null, new RefillValueRange(1, 1), 5));
            var events = ObstaclePresentationPlanBuilder.FeverEndEvents(result, FeverEndEffectTier.None);
            Assert.That(events.Select(x => x.Kind), Is.EqualTo(new[]
            {
                PresentationEventKind.FeverEndAnnouncement, PresentationEventKind.FeverEndFade
            }));
        }

        [Test]
        public void FeverEndPresentation_EmitsPositiveScoreLabelOnly()
        {
            var result = new ObstacleBoardResolver(new ConstantRandom(0)).Resolve(
                ObstacleResolutionRequest.FeverEnd(Full(2, 2), FeverEndPattern.RandomThree,
                    new BoardSystemEffectId(1), null, new RefillValueRange(1, 1), 5));
            var withScore = ObstaclePresentationPlanBuilder.FeverEndEvents(
                result, FeverEndEffectTier.RandomThreeBlocks, 30);
            var withoutScore = ObstaclePresentationPlanBuilder.FeverEndEvents(
                result, FeverEndEffectTier.RandomThreeBlocks, 0);

            Assert.That(withScore.Single(x => x.Kind == PresentationEventKind.FeverClearScore).Identity, Is.EqualTo(30));
            Assert.That(withoutScore.Any(x => x.Kind == PresentationEventKind.FeverClearScore), Is.False);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(5)]
        public void FeverAnswer_ScoresOnlyExpandedRemovals_WithCurrentMultiplier(int multiplier)
        {
            var board = Full(3, 2);
            var answer = Answer(board, new BoardPosition(0, 0), new BoardPosition(1, 0));
            var resolution = new ObstacleBoardResolver(new ConstantRandom(1)).Resolve(
                ObstacleResolutionRequest.FeverAnswer(board, answer, new RefillValueRange(1, 1), 7));
            var session = CreateNumberSession(100);

            var result = session.ApplyAttempt(new StageAttemptCommand(new StageAttemptId(1), answer, resolution,
                StageAttemptRules.CreateFever(multiplier)));

            var feverCount = resolution.CollateralRemoved.Count(x => x.Cause == RemovedNumberCause.FeverExpanded);
            Assert.That(result.Status, Is.EqualTo(StageAttemptApplyStatus.AppliedContinue));
            Assert.That(result.Reward.FeverRemovalScoreAwarded, Is.EqualTo(feverCount * 10L * multiplier));
            Assert.That(result.Reward.ScoreAwarded, Is.EqualTo(result.Reward.FeverRemovalScoreAwarded));
            Assert.That(result.After.Score, Is.EqualTo(result.Reward.FeverRemovalScoreAwarded));
            Assert.That(result.Events.Count(x => x.Kind == StageSessionEventKind.FeverRemovalScoreAwarded), Is.EqualTo(1));
            Assert.That(result.Events.Single(x => x.Kind == StageSessionEventKind.ScoreAwarded).Amount,
                Is.EqualTo(result.Reward.ScoreAwarded));
        }

        [Test]
        public void FeverAnswer_WithNoExpandedRemoval_AwardsNoBonus()
        {
            var board = Full(2, 1);
            var answer = Answer(board, new BoardPosition(0, 0), new BoardPosition(1, 0));
            var resolution = new ObstacleBoardResolver(new ConstantRandom(1)).Resolve(
                ObstacleResolutionRequest.FeverAnswer(board, answer, new RefillValueRange(1, 1), 3));

            var result = CreateNumberSession().ApplyAttempt(new StageAttemptCommand(
                new StageAttemptId(1), answer, resolution, StageAttemptRules.CreateFever(5)));

            Assert.That(resolution.CollateralRemoved, Is.Empty);
            Assert.That(result.Reward.FeverRemovalScoreAwarded, Is.Zero);
            Assert.That(result.Events.Any(x => x.Kind == StageSessionEventKind.FeverRemovalScoreAwarded), Is.False);
        }

        [Test]
        public void NormalAnswer_DoesNotAwardFeverRemovalScore()
        {
            var board = Full(3, 2);
            var answer = Answer(board, new BoardPosition(0, 0), new BoardPosition(1, 0));
            var resolution = new ObstacleBoardResolver(new ConstantRandom(1)).Resolve(
                ObstacleResolutionRequest.NormalAnswer(board, answer, new RefillValueRange(1, 1), 7));
            var result = CreateNumberSession().ApplyAttempt(new StageAttemptCommand(
                new StageAttemptId(1), answer, resolution, StageAttemptRules.Normal));

            Assert.That(result.Reward.FeverRemovalScoreAwarded, Is.Zero);
            Assert.That(result.Events.Any(x => x.Kind == StageSessionEventKind.FeverRemovalScoreAwarded), Is.False);
        }

        [TestCase(FeverEndPattern.RandomThree, 1)]
        [TestCase(FeverEndPattern.Small, 2)]
        [TestCase(FeverEndPattern.Center, 3)]
        [TestCase(FeverEndPattern.Large, 5)]
        public void FeverEnd_PrepareAndCommit_AwardsEveryRemovedBlockAtomically(FeverEndPattern pattern, int multiplier)
        {
            var board = Full(5, 5);
            var center = pattern == FeverEndPattern.RandomThree ? (BoardPosition?)null : new BoardPosition(2, 2);
            var resolution = new ObstacleBoardResolver(new ConstantRandom(0)).Resolve(
                ObstacleResolutionRequest.FeverEnd(board, pattern, new BoardSystemEffectId(1), center,
                    new RefillValueRange(1, 1), 26));
            var session = CreateNumberSession(100);

            var prepared = session.PrepareSystemEffect(resolution, null,
                new FeverEndScoreEvidence(resolution.SystemEffectId, multiplier));

            var expected = resolution.Removed.Count * 10L * multiplier;
            Assert.That(prepared.FeverRemovalScoreAwarded, Is.EqualTo(expected));
            Assert.That(session.CreateSnapshot().Score, Is.Zero);
            var committed = session.CommitSystemEffect(prepared.Plan);
            Assert.That(committed.FeverRemovalScoreAwarded, Is.EqualTo(expected));
            Assert.That(session.CreateSnapshot().Score, Is.EqualTo(expected));
            Assert.That(session.CommitSystemEffect(prepared.Plan).Status, Is.EqualTo(StageSystemEffectCommitStatus.StalePlan));
            Assert.That(session.CreateSnapshot().Score, Is.EqualTo(expected));
        }

        [Test]
        public void FeverEnd_NoneAndDiscardedPlan_AwardNothing()
        {
            var none = new ObstacleBoardResolver(new ConstantRandom(0)).Resolve(
                ObstacleResolutionRequest.FeverEnd(Full(2, 2), FeverEndPattern.None,
                    new BoardSystemEffectId(1), null, new RefillValueRange(1, 1), 5));
            var session = CreateNumberSession();
            var prepared = session.PrepareSystemEffect(none, null, new FeverEndScoreEvidence(none.SystemEffectId, 5));

            Assert.That(prepared.Status, Is.EqualTo(StageSystemEffectPrepareStatus.PreparedContinue));
            Assert.That(prepared.FeverRemovalScoreAwarded, Is.Zero);
            Assert.That(prepared.Events.Any(x => x.Kind == StageSessionEventKind.ScoreAwarded), Is.False);
            Assert.That(session.CreateSnapshot().Score, Is.Zero);
            Assert.That(session.CreateSnapshot().NextExpectedSystemEffectId.Value, Is.EqualTo(1));
        }

        [Test]
        public void FeverEnd_ObstacleDamageDoesNotAddScore()
        {
            var layout = new ObstacleLayout(new[]
            {
                ObstacleLayoutEntry.Dust(new BoardPosition(0, 0), new ObstacleId(1)),
                ObstacleLayoutEntry.Box(new BoardPosition(2, 0), new ObstacleId(2))
            });
            var board = new ObstacleBoardBuilder().Build(Full(3, 2), layout).Board;
            var resolution = new ObstacleBoardResolver(new ConstantRandom(0)).Resolve(
                ObstacleResolutionRequest.FeverEnd(board, FeverEndPattern.Small, new BoardSystemEffectId(1),
                    new BoardPosition(1, 0), new RefillValueRange(1, 1), 7));
            var session = CreateNumberSession(100);
            var prepared = session.PrepareSystemEffect(resolution, null,
                new FeverEndScoreEvidence(resolution.SystemEffectId, 2));

            Assert.That(resolution.DestroyedObstacles.Count, Is.GreaterThan(0));
            Assert.That(prepared.FeverRemovalScoreAwarded, Is.EqualTo(resolution.Removed.Count * 20L));
        }

        [Test]
        public void FeverEnd_RequiresExplicitCorrelatedMultiplierEvidence()
        {
            var resolution = new ObstacleBoardResolver(new ConstantRandom(0)).Resolve(
                ObstacleResolutionRequest.FeverEnd(Full(2, 2), FeverEndPattern.RandomThree,
                    new BoardSystemEffectId(1), null, new RefillValueRange(1, 1), 5));
            var session = CreateNumberSession();

            Assert.That(session.PrepareSystemEffect(resolution).Status,
                Is.EqualTo(StageSystemEffectPrepareStatus.InvalidEvidence));
            Assert.That(session.CreateSnapshot().Score, Is.Zero);
        }

        [Test]
        public void FeverEndScoreOverflow_RejectsWithoutPartialMutation()
        {
            var board = Full(2, 1);
            var answer = Answer(board, new BoardPosition(0, 0), new BoardPosition(1, 0));
            var normal = new BoardResolver(new ConstantRandom(1)).Resolve(
                new BoardResolutionRequest(board, answer, new RefillValueRange(1, 1), 3));
            var definition = new StageDefinition(new StageDefinitionId(9), 2,
                new[] { new StageObjectiveDefinition(StageObjectiveKind.RemoveNumberBlocks, 100, default, 0) },
                new ScoreRewardConfig(long.MaxValue - 5, 0, 0, 0, new ConnectionLengthScoreRule[0]));
            MathGame.StageSession.StageSession.TryCreate(definition, out var session);
            Assert.That(session.ApplyAttempt(new StageAttemptCommand(new StageAttemptId(1), answer, normal)).Status,
                Is.EqualTo(StageAttemptApplyStatus.AppliedContinue));
            var before = session.CreateSnapshot();
            var end = new ObstacleBoardResolver(new ConstantRandom(0)).Resolve(
                ObstacleResolutionRequest.FeverEnd(normal.Board, FeverEndPattern.RandomThree,
                    new BoardSystemEffectId(1), null, new RefillValueRange(1, 1), normal.NextBlockIdValue));

            var prepared = session.PrepareSystemEffect(end, null, new FeverEndScoreEvidence(end.SystemEffectId, 1));

            Assert.That(prepared.Status, Is.EqualTo(StageSystemEffectPrepareStatus.ArithmeticOverflow));
            Assert.That(session.CreateSnapshot().Score, Is.EqualTo(before.Score));
            Assert.That(session.CreateSnapshot().NextExpectedSystemEffectId, Is.EqualTo(before.NextExpectedSystemEffectId));
        }

        [Test]
        public void SearchAndShuffle_SkipBoxAndPreserveObstacleLayers()
        {
            var built = new ObstacleBoardBuilder().Build(Full(3, 2), new ObstacleLayout(new[] { ObstacleLayoutEntry.Dust(new BoardPosition(0, 0), new ObstacleId(1)), ObstacleLayoutEntry.Box(new BoardPosition(1, 0), new ObstacleId(2)) })).Board;
            var search = new TargetPathSearcher().Search(built, new TargetSearchConfig(2, 10, 2, 3, 1000)); Assert.That(search.Status, Is.EqualTo(TargetSearchStatus.Succeeded));
            var shuffled = new BoardShuffler(new ConstantRandom(0)).Shuffle(built); Assert.That(shuffled.Succeeded, Is.True);
            shuffled.Board.TryGetCell(new BoardPosition(0, 0), out var dust); shuffled.Board.TryGetCell(new BoardPosition(1, 0), out var box); Assert.That(dust.HasDust, Is.True); Assert.That(box.HasBox, Is.True);
        }

        [Test]
        public void FeverEnd_DestroyedBoxRefillsAndObjectiveCommitsAtomically()
        {
            var built = new ObstacleBoardBuilder().Build(Full(2, 1), new ObstacleLayout(new[] { ObstacleLayoutEntry.Box(new BoardPosition(1, 0), new ObstacleId(2)) })).Board;
            var resolver = new ObstacleBoardResolver(new ConstantRandom(1));
            var resolution = resolver.Resolve(ObstacleResolutionRequest.FeverEnd(built, FeverEndPattern.Small, new BoardSystemEffectId(1), new BoardPosition(0, 0), new RefillValueRange(1, 1), 3));
            Assert.That(resolution.Succeeded, Is.True); Assert.That(resolution.DestroyedObstacles.Count, Is.EqualTo(1));
            resolution.Board.TryGetCell(new BoardPosition(1, 0), out var formerBox); Assert.That(formerBox.HasBox, Is.False); Assert.That(formerBox.HasBlock, Is.True);
            var objective = new StageObjectiveDefinition(StageObjectiveKind.RemoveObstacle, 1, default(TargetNumber), 0, ObstacleKind.Box);
            var definition = new StageDefinition(new StageDefinitionId(1), 2, new[] { objective }, new ScoreRewardConfig(0, 0, 0, 0, new ConnectionLengthScoreRule[0]));
            Assert.That(MathGame.StageSession.StageSession.TryCreate(definition, out var session), Is.EqualTo(StageSessionCreateStatus.Succeeded));
            var prepared = session.PrepareSystemEffect(resolution, null, new FeverEndScoreEvidence(resolution.SystemEffectId, 1)); Assert.That(prepared.Status, Is.EqualTo(StageSystemEffectPrepareStatus.PreparedSuccess));
            Assert.That(session.CreateSnapshot().Objectives[0].Current, Is.Zero);
            Assert.That(prepared.ProspectiveAfter.TotalDestroyedBoxes, Is.EqualTo(1));
            var committed = session.CommitSystemEffect(prepared.Plan); Assert.That(committed.Status, Is.EqualTo(StageSystemEffectCommitStatus.CommittedSuccess));
            Assert.That(session.CreateSnapshot().Objectives[0].Current, Is.EqualTo(1));
            Assert.That(session.CreateSnapshot().TotalDestroyedBoxes, Is.EqualTo(1));
            Assert.That(session.CreateSnapshot().TotalDestroyedDust, Is.Zero);
            Assert.That(session.CommitSystemEffect(prepared.Plan).Status, Is.EqualTo(StageSystemEffectCommitStatus.SessionAlreadyTerminal));
        }

        [Test]
        public void FailedEndResolution_DoesNotPrepareOrAdvanceEffectId()
        {
            var session = CreateNumberSession(); var before = session.CreateSnapshot();
            var failed = new ObstacleBoardResolver(new ConstantRandom(1)).Resolve(ObstacleResolutionRequest.FeverEnd(Full(2, 1), FeverEndPattern.Small, new BoardSystemEffectId(1), null, new RefillValueRange(1, 1), 3));
            Assert.That(failed.Failure, Is.EqualTo(ObstacleResolutionFailure.MissingCenter));
            Assert.That(session.PrepareSystemEffect(failed).Status, Is.EqualTo(StageSystemEffectPrepareStatus.ResolutionNotSucceeded));
            Assert.That(session.CreateSnapshot().NextExpectedSystemEffectId.Value, Is.EqualTo(before.NextExpectedSystemEffectId.Value));
        }

        [Test]
        public void SecondPreparedPlanBecomesStaleAfterFirstCommit()
        {
            var session = CreateNumberSession();
            var resolution = new ObstacleBoardResolver(new ConstantRandom(1)).Resolve(ObstacleResolutionRequest.FeverEnd(Full(2, 1), FeverEndPattern.RandomThree, new BoardSystemEffectId(1), null, new RefillValueRange(1, 1), 3));
            var evidence = new FeverEndScoreEvidence(resolution.SystemEffectId, 1);
            var first = session.PrepareSystemEffect(resolution, null, evidence); var second = session.PrepareSystemEffect(resolution, null, evidence);
            Assert.That(first.Status, Is.EqualTo(StageSystemEffectPrepareStatus.PreparedContinue));
            Assert.That(session.CommitSystemEffect(first.Plan).Status, Is.EqualTo(StageSystemEffectCommitStatus.CommittedContinue));
            Assert.That(session.CommitSystemEffect(second.Plan).Status, Is.EqualTo(StageSystemEffectCommitStatus.StalePlan));
        }

        private static MathGame.StageSession.StageSession CreateNumberSession(int required = 20)
        {
            var objective = new StageObjectiveDefinition(StageObjectiveKind.RemoveNumberBlocks, required, default(TargetNumber), 0);
            MathGame.StageSession.StageSession.TryCreate(new StageDefinition(new StageDefinitionId(2), 2, new[] { objective }, new ScoreRewardConfig(0, 0, 0, 0, new ConnectionLengthScoreRule[0])), out var session); return session;
        }

        private static DomainBoard Full(int width, int height)
        {
            var board = new DomainBoard(BoardTopology.CreateRectangular(width, height)); var id = 1;
            foreach (var p in board.EnumerateActivePositions()) Assert.That(board.TryPlaceBlock(p, new NumberBlock(new BlockId(id++), 1)), Is.EqualTo(BoardMutationResult.Succeeded));
            return board;
        }
        private static AnswerResult Answer(DomainBoard board, params BoardPosition[] positions)
        {
            var path = new ConnectionPath(board);
            foreach (var position in positions) Assert.That(path.TrySelect(position), Is.EqualTo(ConnectionStepResult.Added));
            return new AnswerValidator(AnswerTimingThresholds.Prototype)
                .Evaluate(path.CreateSnapshot(), new TargetNumber(positions.Length), 1);
        }
        private sealed class ConstantRandom : IRandomSource { private readonly int value; public ConstantRandom(int value) { this.value = value; } public int NextInt(int minInclusive, int maxExclusive) => value < minInclusive ? minInclusive : value >= maxExclusive ? maxExclusive - 1 : value; public float NextFloat() => 0; }
    }
}
