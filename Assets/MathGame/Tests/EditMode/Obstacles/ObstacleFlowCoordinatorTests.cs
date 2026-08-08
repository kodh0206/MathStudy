using MathGame.Answer;
using MathGame.Board;
using MathGame.BoardResolution;
using MathGame.Connection;
using MathGame.Core.Random;
using MathGame.Fever;
using MathGame.ObstacleFlow;
using MathGame.Stage;
using MathGame.StageSession;
using MathGame.Targets;
using NUnit.Framework;
using DomainBoard = MathGame.Board.Board;
using Session = MathGame.StageSession.StageSession;

namespace MathGame.Tests.Obstacles
{
    public sealed class ObstacleFlowCoordinatorTests
    {
        [Test]
        public void TerminalSessionPrecedesResolutionAndConsumesNoRandomness()
        {
            var session = CreateSession(1);
            var terminalAttempt = CreateAttempt(Full(2), 1);
            Assert.That(session.ApplyAttempt(terminalAttempt).Status, Is.EqualTo(StageAttemptApplyStatus.AppliedSuccess));

            var stage = ResolvingStage();
            var random = new CountingRandom();
            FeverController.TryCreate(FeverConfig.Prototype, stage, session, new FakeTime(), out var fever);
            using (fever)
            {
                var board = Full(2);
                var coordinator = CreateCoordinator(board, stage, session, fever, random);
                var request = Request(CreateAnswer(board), session.CreateSnapshot().NextExpectedAttemptId, ValidTargets());
                var before = coordinator.CurrentBoard;

                var result = coordinator.ResolveNormalAnswer(request);

                Assert.That(result.Status, Is.EqualTo(ObstacleAnswerFlowStatus.InvalidStageState));
                Assert.That(result.AttemptCommitted, Is.False);
                Assert.That(random.Calls, Is.Zero);
                AssertBoardsEqual(before, coordinator.CurrentBoard);
            }
        }

        [Test]
        public void TargetLimitFailureAdoptsCommittedAnswerThenRetryOnlyProvesTarget()
        {
            var board = Full(2);
            var session = CreateSession(99);
            var stage = ResolvingStage();
            var random = new CountingRandom();
            FeverController.TryCreate(FeverConfig.Prototype, stage, session, new FakeTime(), out var fever);
            using (fever)
            {
                var coordinator = CreateCoordinator(board, stage, session, fever, random);
                var failed = coordinator.ResolveNormalAnswer(Request(
                    CreateAnswer(board), new StageAttemptId(1),
                    new TargetRecoveryConfig(new TargetSearchConfig(2, 2, 2, 2, 1), new TargetSelectionPolicy(1), 1)));

                Assert.That(failed.Status, Is.EqualTo(ObstacleAnswerFlowStatus.TargetSearchLimitExceeded));
                Assert.That(failed.AttemptCommitted, Is.True);
                Assert.That(failed.IsInputReady, Is.False);
                Assert.That(session.CreateSnapshot().NextExpectedAttemptId.Value, Is.EqualTo(2));
                var adopted = coordinator.CurrentBoard;
                failed.ResolutionResult.Board.TryRemoveBlock(new BoardPosition(0, 0), out _);
                AssertBoardsEqual(adopted, coordinator.CurrentBoard);

                var retried = coordinator.RetryTargetRecovery(new TargetHistory(null, 0), ValidTargets());

                Assert.That(retried.Status, Is.EqualTo(ObstacleAnswerFlowStatus.Succeeded));
                Assert.That(retried.AttemptCommitted, Is.False);
                Assert.That(retried.IsInputReady, Is.True);
                Assert.That(session.CreateSnapshot().NextExpectedAttemptId.Value, Is.EqualTo(2));
                AssertBoardsEqual(adopted, coordinator.CurrentBoard);
            }
        }

        [Test]
        public void ResolverFailureRollsBackCoordinatorAndSession()
        {
            var board = Full(2);
            var session = CreateSession(99);
            var stage = ResolvingStage();
            var random = new CountingRandom();
            FeverController.TryCreate(FeverConfig.Prototype, stage, session, new FakeTime(), out var fever);
            using (fever)
            {
                var coordinator = CreateCoordinator(board, stage, session, fever, random);
                var before = coordinator.CurrentBoard;
                var snapshot = session.CreateSnapshot();
                var staleBoard = Full(2);
                staleBoard.TryRemoveBlock(new BoardPosition(0, 0), out _);
                staleBoard.TryPlaceBlock(new BoardPosition(0, 0), new NumberBlock(new BlockId(99), 1));

                var result = coordinator.ResolveNormalAnswer(Request(CreateAnswer(staleBoard), new StageAttemptId(1), ValidTargets()));

                Assert.That(result.Status, Is.EqualTo(ObstacleAnswerFlowStatus.ResolutionFailed));
                Assert.That(result.ResolutionResult.Failure, Is.EqualTo(ObstacleResolutionFailure.SelectedBlockMismatch));
                Assert.That(session.CreateSnapshot().NextExpectedAttemptId, Is.EqualTo(snapshot.NextExpectedAttemptId));
                AssertBoardsEqual(before, coordinator.CurrentBoard);
            }
        }

        [Test]
        public void ConstructorRejectsCollidingNextBlockId()
        {
            var board = Full(2);
            var session = CreateSession(99);
            var stage = ResolvingStage();
            FeverController.TryCreate(FeverConfig.Prototype, stage, session, new FakeTime(), out var fever);
            using (fever)
            {
                Assert.Throws<System.ArgumentException>(() => new ObstacleResolutionCoordinator(
                    new ObstacleBoardResolver(new CountingRandom()), stage, session, fever,
                    new TargetRecoveryCoordinator(new CountingRandom()), board, 2));
            }
        }

        private static ObstacleResolutionCoordinator CreateCoordinator(
            DomainBoard board, StageController stage, Session session, FeverController fever, IRandomSource random)
            => new ObstacleResolutionCoordinator(
                new ObstacleBoardResolver(random), stage, session, fever,
                new TargetRecoveryCoordinator(random), board, 3);

        private static ObstacleAnswerFlowRequest Request(AnswerResult answer, StageAttemptId id, TargetRecoveryConfig targets)
            => new ObstacleAnswerFlowRequest(answer, id, new RefillValueRange(1, 1), new TargetHistory(null, 0), targets);

        private static TargetRecoveryConfig ValidTargets()
            => new TargetRecoveryConfig(new TargetSearchConfig(2, 2, 2, 2, 100), new TargetSelectionPolicy(1), 1);

        private static StageController ResolvingStage()
        {
            var stage = new StageController();
            stage.Start();
            stage.FinishInitialization();
            stage.BeginTargetPresentation();
            stage.EnablePlayerInput();
            stage.BeginAnswerResolution();
            return stage;
        }

        private static Session CreateSession(int required)
        {
            var definition = new StageDefinition(
                new StageDefinitionId(10), 5,
                new[] { new StageObjectiveDefinition(StageObjectiveKind.RemoveNumberBlocks, required, default, 0) },
                new ScoreRewardConfig(0, 0, 0, 0, new ConnectionLengthScoreRule[0]));
            Assert.That(Session.TryCreate(definition, out var session), Is.EqualTo(StageSessionCreateStatus.Succeeded));
            return session;
        }

        private static StageAttemptCommand CreateAttempt(DomainBoard board, long id)
        {
            var answer = CreateAnswer(board);
            var resolution = new BoardResolver(new CountingRandom()).Resolve(
                new BoardResolutionRequest(board, answer, new RefillValueRange(1, 1), 3));
            return new StageAttemptCommand(new StageAttemptId(id), answer, resolution);
        }

        private static AnswerResult CreateAnswer(DomainBoard board)
        {
            var path = new ConnectionPath(board);
            path.TrySelect(new BoardPosition(0, 0));
            path.TrySelect(new BoardPosition(1, 0));
            return new AnswerValidator(AnswerTimingThresholds.Prototype)
                .Evaluate(path.CreateSnapshot(), new TargetNumber(2), 1);
        }

        private static DomainBoard Full(int width)
        {
            var board = new DomainBoard(BoardTopology.CreateRectangular(width, 1));
            for (var column = 0; column < width; column++)
                board.TryPlaceBlock(new BoardPosition(column, 0), new NumberBlock(new BlockId(column + 1), 1));
            return board;
        }

        private static void AssertBoardsEqual(DomainBoard expected, DomainBoard actual)
        {
            foreach (var position in expected.EnumerateActivePositions())
            {
                expected.TryGetCell(position, out var left);
                actual.TryGetCell(position, out var right);
                Assert.That(right.Role, Is.EqualTo(left.Role));
                Assert.That(right.Block, Is.EqualTo(left.Block));
                Assert.That(right.Dust, Is.EqualTo(left.Dust));
                Assert.That(right.Box, Is.EqualTo(left.Box));
            }
        }

        private sealed class CountingRandom : IRandomSource
        {
            public int Calls { get; private set; }
            public int NextInt(int minInclusive, int maxExclusive) { Calls++; return minInclusive; }
            public float NextFloat() => 0;
        }

        private sealed class FakeTime : MathGame.Core.Time.ITimeProvider
        {
            public double RealtimeSeconds => 0;
        }
    }
}
