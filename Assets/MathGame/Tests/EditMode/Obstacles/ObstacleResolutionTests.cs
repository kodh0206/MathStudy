using System.Collections.Generic;
using System.Linq;
using MathGame.Board;
using MathGame.BoardResolution;
using MathGame.Core.Random;
using MathGame.Obstacles;
using MathGame.Targets;
using MathGame.StageSession;
using MathGame.Answer;
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
            var prepared = session.PrepareSystemEffect(resolution); Assert.That(prepared.Status, Is.EqualTo(StageSystemEffectPrepareStatus.PreparedSuccess));
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
            var first = session.PrepareSystemEffect(resolution); var second = session.PrepareSystemEffect(resolution);
            Assert.That(first.Status, Is.EqualTo(StageSystemEffectPrepareStatus.PreparedContinue));
            Assert.That(session.CommitSystemEffect(first.Plan).Status, Is.EqualTo(StageSystemEffectCommitStatus.CommittedContinue));
            Assert.That(session.CommitSystemEffect(second.Plan).Status, Is.EqualTo(StageSystemEffectCommitStatus.StalePlan));
        }

        private static MathGame.StageSession.StageSession CreateNumberSession()
        {
            var objective = new StageObjectiveDefinition(StageObjectiveKind.RemoveNumberBlocks, 20, default(TargetNumber), 0);
            MathGame.StageSession.StageSession.TryCreate(new StageDefinition(new StageDefinitionId(2), 2, new[] { objective }, new ScoreRewardConfig(0, 0, 0, 0, new ConnectionLengthScoreRule[0])), out var session); return session;
        }

        private static DomainBoard Full(int width, int height)
        {
            var board = new DomainBoard(BoardTopology.CreateRectangular(width, height)); var id = 1;
            foreach (var p in board.EnumerateActivePositions()) Assert.That(board.TryPlaceBlock(p, new NumberBlock(new BlockId(id++), 1)), Is.EqualTo(BoardMutationResult.Succeeded));
            return board;
        }
        private sealed class ConstantRandom : IRandomSource { private readonly int value; public ConstantRandom(int value) { this.value = value; } public int NextInt(int minInclusive, int maxExclusive) => value < minInclusive ? minInclusive : value >= maxExclusive ? maxExclusive - 1 : value; public float NextFloat() => 0; }
    }
}
