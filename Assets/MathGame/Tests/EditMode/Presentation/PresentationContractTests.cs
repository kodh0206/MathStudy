using System.Collections.Generic;
using MathGame.Board;
using MathGame.BoardResolution;
using MathGame.ObstacleFlow;
using MathGame.Presentation;
using MathGame.Restoration;
using MathGame.Restoration.Contracts;
using NUnit.Framework;

namespace MathGame.Tests.Presentation
{
    public sealed class PresentationContractTests
    {
        [Test]
        public void FeverCenter_UsesArithmeticCenterAndRowMajorTieBreakWithoutRandomness()
        {
            var topology = BoardTopology.CreateRectangular(3, 3);
            var footprint = new[]
            {
                Removed(new BoardPosition(0, 0), 1),
                Removed(new BoardPosition(2, 0), 2)
            };
            Assert.That(FeverAreaCenterSelector.TrySelect(topology, footprint, out var center), Is.True);
            Assert.That(center, Is.EqualTo(new BoardPosition(1, 0)));
        }

        [Test]
        public void LogicalTouch_UsesApprovedRadiusAndInterpolatesInTraversalOrder()
        {
            Assert.That(LogicalBoardTouch.TryHit(.44, 0, 1, 5, 5, out var hit), Is.True);
            Assert.That(hit, Is.EqualTo(new BoardPosition(0, 0)));
            Assert.That(LogicalBoardTouch.TryHit(.46, 0, 1, 5, 5, out _), Is.False);
            Assert.That(LogicalBoardTouch.Interpolate(new BoardPosition(0, 0), new BoardPosition(3, 0)),
                Is.EqualTo(new[] { new BoardPosition(1, 0), new BoardPosition(2, 0), new BoardPosition(3, 0) }));
        }

        [Test]
        public void Coordinator_RejectsStaleAndDuplicate_AndAcknowledgesExactlyOnceAfterReconcile()
        {
            var board = StableBoard(1, 1);
            var token = new GameplayStateToken(new StageRunId(9), 1, GameplayStateSource.Initial, 0);
            var port = new FakeGameplayPort(token);
            var view = new FakeView();
            var coordinator = new GameplayPresentationCoordinator(port, view);
            var stale = Envelope(board, new GameplayStateToken(new StageRunId(9), 2, GameplayStateSource.Answer, 1), 1);
            Assert.That(coordinator.Prepare(new PresentationPlan(stale, new PresentationSettings(false))), Is.EqualTo(PresentationCommandStatus.StaleGameplayToken));

            var plan = new PresentationPlan(Envelope(board, token, 2), new PresentationSettings(false));
            Assert.That(coordinator.Prepare(plan), Is.EqualTo(PresentationCommandStatus.Accepted));
            Assert.That(coordinator.Prepare(plan), Is.EqualTo(PresentationCommandStatus.DuplicateCommand));
            Assert.That(port.Acknowledgements, Is.Zero);
            Assert.That(coordinator.CompletePlayback(), Is.EqualTo(PresentationAcknowledgementStatus.Accepted));
            Assert.That(view.Reconciles, Is.EqualTo(1));
            Assert.That(port.Acknowledgements, Is.EqualTo(1));
            Assert.That(coordinator.CompletePlayback(), Is.EqualTo(PresentationAcknowledgementStatus.MissingAcknowledgement));
        }

        [Test]
        public void CancellationBeforeReconcile_DoesNotAcknowledgeOrMutateGameplay()
        {
            var board = StableBoard(1, 1);
            var token = new GameplayStateToken(new StageRunId(4), 1, GameplayStateSource.Initial, 0);
            var port = new FakeGameplayPort(token);
            var coordinator = new GameplayPresentationCoordinator(port, new FakeView());
            Assert.That(coordinator.Prepare(new PresentationPlan(Envelope(board, token, 1), new PresentationSettings(false))), Is.EqualTo(PresentationCommandStatus.Accepted));
            Assert.That(coordinator.CancelBeforeReconcile(), Is.EqualTo(PresentationCommandStatus.Accepted));
            Assert.That(port.Acknowledgements, Is.Zero);
            Assert.That(port.CurrentToken, Is.EqualTo(token));
        }

        [Test]
        public void PauseResume_ReconcilesStalePlanWithoutReplayingDomainCommand()
        {
            var board = StableBoard(1, 1);
            var token = new GameplayStateToken(new StageRunId(6), 1, GameplayStateSource.Initial, 0);
            var port = new FakeGameplayPort(token);
            var view = new FakeView();
            var coordinator = new GameplayPresentationCoordinator(port, view);
            coordinator.Prepare(new PresentationPlan(Envelope(board, token, 1), new PresentationSettings(true)));
            Assert.That(coordinator.Pause(), Is.EqualTo(PresentationCommandStatus.Accepted));
            Assert.That(coordinator.ResumeOrReconcile(), Is.EqualTo(PresentationCommandStatus.Accepted));
            Assert.That(coordinator.Phase, Is.EqualTo(PresentationPhase.Playing));
            Assert.That(view.Reconciles, Is.Zero);
        }

        [Test]
        public void Milestones_AreMarkedOncePerWorldCommitAndIdentity()
        {
            var world = new WorldRestorationProgress(new WorldRestorationId(1), 100, 20);
            var prepared = world.Prepare(world.WorldId, new WorldCommitId(new StageRunId(20)), 80);
            var committed = world.Commit(prepared.Plan);
            var tracker = new ExactlyOnceMilestoneTracker();
            foreach (var milestone in committed.Plan.CrossedMilestones)
            {
                Assert.That(tracker.TryMark(committed, milestone), Is.True);
                Assert.That(tracker.TryMark(committed, milestone), Is.False);
            }
            Assert.That(committed.Plan.CrossedMilestones, Is.EqualTo(new[] { WorldRestorationMilestone.Quarter, WorldRestorationMilestone.Half, WorldRestorationMilestone.ThreeQuarters, WorldRestorationMilestone.Complete }));
        }

        static RemovedNumberDelta Removed(BoardPosition p, int id) => new RemovedNumberDelta(p, new NumberBlock(new BlockId(id), 1), RemovedNumberCause.Selected, RemovalOrigin.Fever);
        static MathGame.Board.Board StableBoard(int width, int height)
        {
            var board = new MathGame.Board.Board(BoardTopology.CreateRectangular(width, height));
            var id = 1;
            foreach (var p in board.EnumerateActivePositions()) board.TryPlaceBlock(p, new NumberBlock(new BlockId(id++), 1));
            return board;
        }
        static PresentationEnvelope Envelope(MathGame.Board.Board board, GameplayStateToken token, long sequence) =>
            new PresentationEnvelope(new PresentationSequenceId(sequence), new GameplayStateSnapshot(token, board, 10), null, null, PresentationAcknowledgementKind.TargetReady, 0);

        sealed class FakeGameplayPort : IGameplayCommandPort
        {
            public FakeGameplayPort(GameplayStateToken token) { CurrentToken = token; }
            public GameplayStateToken CurrentToken { get; set; }
            public bool IsStageTerminated { get; set; }
            public int Acknowledgements { get; private set; }
            public PresentationAcknowledgementStatus Acknowledge(PresentationAcknowledgement acknowledgement) { Acknowledgements++; return PresentationAcknowledgementStatus.Accepted; }
        }
        sealed class FakeView : IPresentationViewPort
        {
            public int Reconciles { get; private set; }
            public void Prepare(IPresentationPlan plan) { }
            public void ApplyFinalState(IPresentationPlan plan) => Reconciles++;
            public void TearDown() { }
        }
    }
}
