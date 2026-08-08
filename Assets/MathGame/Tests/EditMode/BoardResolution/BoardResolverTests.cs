using System;
using System.Collections.Generic;
using MathGame.Answer;
using MathGame.Board;
using MathGame.BoardResolution;
using MathGame.Connection;
using MathGame.Core.Random;
using NUnit.Framework;
using DomainBoard = MathGame.Board.Board;

namespace MathGame.Tests.BoardResolution
{
    public sealed class BoardResolverTests
    {
        [Test]
        public void BottomRemovalCompactsAndRefillsWithExactDeltas()
        {
            var board = Filled(BoardTopology.CreateRectangular(1, 4));
            var answer = Correct(board, new BoardPosition(0, 0), new BoardPosition(0, 1));
            var random = new RecordingRandom(8, 9);
            var result = new BoardResolver(random).Resolve(new BoardResolutionRequest(board, answer, new RefillValueRange(1, 9), 5));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Board, Is.Not.SameAs(board));
            Assert.That(result.Board.Topology, Is.SameAs(board.Topology));
            Assert.That(result.Removed, Has.Count.EqualTo(2));
            Assert.That(result.Removed[0].Position, Is.EqualTo(new BoardPosition(0, 0)));
            Assert.That(result.Moved, Has.Count.EqualTo(2));
            Assert.That(result.Moved[0].From, Is.EqualTo(new BoardPosition(0, 2)));
            Assert.That(result.Moved[0].To, Is.EqualTo(new BoardPosition(0, 0)));
            Assert.That(result.Spawned[0].Destination, Is.EqualTo(new BoardPosition(0, 2)));
            Assert.That(result.Spawned[0].Block.Id.Value, Is.EqualTo(5));
            Assert.That(result.NextBlockIdValue, Is.EqualTo(7));
            Assert.That(random.Calls, Is.EqualTo(new[] { new Call(1, 10), new Call(1, 10) }));
            AssertCell(board, new BoardPosition(0, 0), 1);
            AssertCell(result.Board, new BoardPosition(0, 0), 3);
        }

        [Test]
        public void HoleSeparatesGravitySegments()
        {
            var topology = BoardTopology.CreateMasked(1, 4, new[]
            {
                new BoardPosition(0, 0), new BoardPosition(0, 2), new BoardPosition(0, 3)
            });
            var board = Filled(topology);
            var result = Resolve(board, Correct(board, new BoardPosition(0, 2), new BoardPosition(0, 3)), 4, 7, 8);

            Assert.That(result.Moved, Is.Empty);
            Assert.That(result.Spawned[0].Destination, Is.EqualTo(new BoardPosition(0, 2)));
            Assert.That(result.Spawned[1].Destination, Is.EqualTo(new BoardPosition(0, 3)));
            AssertCell(result.Board, new BoardPosition(0, 0), 1);
            Assert.That(result.Board.TryGetCell(new BoardPosition(0, 1), out _), Is.EqualTo(CellLookupResult.InactivePosition));
        }

        [Test]
        public void EntireSegmentRemovalSpawnsInColumnThenRowOrder()
        {
            var board = Filled(BoardTopology.CreateRectangular(2, 2));
            var answer = Correct(board, new BoardPosition(0, 0), new BoardPosition(0, 1));
            var random = new RecordingRandom(1, 9);
            var result = new BoardResolver(random).Resolve(new BoardResolutionRequest(board, answer, new RefillValueRange(1, 9), 5));

            Assert.That(result.Moved, Is.Empty);
            Assert.That(result.Spawned[0].Destination, Is.EqualTo(new BoardPosition(0, 0)));
            Assert.That(result.Spawned[1].Destination, Is.EqualTo(new BoardPosition(0, 1)));
            Assert.That(result.Spawned[0].Block.Id.Value, Is.EqualTo(5));
            Assert.That(result.Spawned[1].Block.Id.Value, Is.EqualTo(6));
            Assert.That(result.Spawned[0].Block.Value, Is.EqualTo(1));
            Assert.That(result.Spawned[1].Block.Value, Is.EqualTo(9));
        }

        [Test]
        public void ExpectedPreflightFailuresConsumeNoRandomnessAndExposeNothing()
        {
            var random = new RecordingRandom(); var resolver = new BoardResolver(random);
            AssertFailure(resolver.Resolve(null), BoardResolutionFailure.MissingRequest, random);
            AssertFailure(resolver.Resolve(new BoardResolutionRequest(null, null, null, 0)), BoardResolutionFailure.MissingBoard, random);
            var board = Filled(BoardTopology.CreateRectangular(1, 2));
            AssertFailure(resolver.Resolve(new BoardResolutionRequest(board, null, new RefillValueRange(1, 9), 3)), BoardResolutionFailure.MissingAnswer, random);
            var answer = Correct(board, new BoardPosition(0, 0), new BoardPosition(0, 1));
            AssertFailure(resolver.Resolve(new BoardResolutionRequest(board, answer, null, 3)), BoardResolutionFailure.MissingRefillRange, random);
            AssertFailure(resolver.Resolve(new BoardResolutionRequest(board, answer, new RefillValueRange(0, 9), 3)), BoardResolutionFailure.InvalidRefillRange, random);
            AssertFailure(resolver.Resolve(new BoardResolutionRequest(board, answer, new RefillValueRange(1, 9), 0)), BoardResolutionFailure.InvalidNextBlockId, random);
            AssertFailure(resolver.Resolve(new BoardResolutionRequest(board, answer, new RefillValueRange(1, 9), 2)), BoardResolutionFailure.NextBlockIdCollision, random);
        }

        [Test]
        public void BlockedOrEmptySourceIsUnsupportedWithoutDraws()
        {
            var board = Filled(BoardTopology.CreateRectangular(1, 2));
            var answer = Correct(board, new BoardPosition(0, 0), new BoardPosition(0, 1));
            board.TryRemoveBlock(new BoardPosition(0, 1), out _);
            var random = new RecordingRandom();
            AssertFailure(new BoardResolver(random).Resolve(new BoardResolutionRequest(board, answer, new RefillValueRange(1, 9), 3)), BoardResolutionFailure.UnsupportedBoardState, random);
        }

        [Test]
        public void StaleCapturedIdentityAndRepeatedResolutionFailAtomically()
        {
            var board = Filled(BoardTopology.CreateRectangular(1, 2));
            var answer = Correct(board, new BoardPosition(0, 0), new BoardPosition(0, 1));
            var first = Resolve(board, answer, 3, 5, 6);
            var random = new RecordingRandom();
            AssertFailure(new BoardResolver(random).Resolve(new BoardResolutionRequest(first.Board, answer, new RefillValueRange(1, 9), 4)), BoardResolutionFailure.SelectedBlockMismatch, random);
        }

        [Test]
        public void OutOfContractRandomThrowsAndSourceRemainsUnchanged()
        {
            var board = Filled(BoardTopology.CreateRectangular(1, 2));
            var answer = Correct(board, new BoardPosition(0, 0), new BoardPosition(0, 1));
            Assert.Throws<InvalidOperationException>(() => new BoardResolver(new RecordingRandom(10)).Resolve(
                new BoardResolutionRequest(board, answer, new RefillValueRange(1, 9), 3)));
            AssertCell(board, new BoardPosition(0, 0), 1);
            AssertCell(board, new BoardPosition(0, 1), 2);
        }

        [Test]
        public void ResultDeltaCollectionsAreReadOnly()
        {
            var board = Filled(BoardTopology.CreateRectangular(1, 2));
            var result = Resolve(board, Correct(board, new BoardPosition(0, 0), new BoardPosition(0, 1)), 3, 1, 2);
            Assert.Throws<NotSupportedException>(() => ((IList<RemovedBlockDelta>)result.Removed)[0] = default);
            Assert.Throws<NotSupportedException>(() => ((IList<SpawnedBlockDelta>)result.Spawned)[0] = default);
        }

        [Test]
        public void ConstructorRejectsNullRandomSource()
        {
            Assert.Throws<ArgumentNullException>(() => new BoardResolver(null));
        }

        private static BoardResolutionResult Resolve(DomainBoard board, AnswerResult answer, int nextId, params int[] values)
            => new BoardResolver(new RecordingRandom(values)).Resolve(new BoardResolutionRequest(board, answer, new RefillValueRange(1, 9), nextId));

        private static DomainBoard Filled(BoardTopology topology)
        {
            var board = new DomainBoard(topology); var id = 1;
            foreach (var position in board.EnumerateActivePositions()) board.TryPlaceBlock(position, new NumberBlock(new BlockId(id), id++));
            return board;
        }

        private static AnswerResult Correct(DomainBoard board, params BoardPosition[] positions)
        {
            var path = new ConnectionPath(board); long sum = 0;
            foreach (var position in positions) { path.TrySelect(position); board.TryGetCell(position, out var cell); sum += cell.Block.Value.Value; }
            return new AnswerValidator(new AnswerTimingThresholds(2, 4)).Evaluate(path.CreateSnapshot(), new TargetNumber((int)sum), 1);
        }

        private static void AssertCell(DomainBoard board, BoardPosition position, int id)
        {
            board.TryGetCell(position, out var cell); Assert.That(cell.Block.Value.Id.Value, Is.EqualTo(id));
        }

        private static void AssertFailure(BoardResolutionResult result, BoardResolutionFailure failure, RecordingRandom random)
        {
            Assert.That(result.Failure, Is.EqualTo(failure)); Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Board, Is.Null); Assert.That(result.NextBlockIdValue, Is.Zero);
            Assert.That(result.Removed, Is.Empty); Assert.That(result.Moved, Is.Empty); Assert.That(result.Spawned, Is.Empty);
            Assert.That(random.Calls, Is.Empty);
        }

        private readonly struct Call
        {
            public Call(int min, int max) { Min = min; Max = max; }
            public int Min { get; } public int Max { get; }
            public override bool Equals(object obj) => obj is Call other && Min == other.Min && Max == other.Max;
            public override int GetHashCode() => (Min * 397) ^ Max;
        }

        private sealed class RecordingRandom : IRandomSource
        {
            private readonly Queue<int> values;
            public RecordingRandom(params int[] values) { this.values = new Queue<int>(values); }
            public List<Call> Calls { get; } = new List<Call>();
            public int NextInt(int minInclusive, int maxExclusive)
            { Calls.Add(new Call(minInclusive, maxExclusive)); return values.Dequeue(); }
            public float NextFloat() => throw new NotSupportedException();
        }
    }
}
