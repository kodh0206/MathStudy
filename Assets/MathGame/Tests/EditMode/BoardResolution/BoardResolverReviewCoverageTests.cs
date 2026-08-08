using System;
using System.Collections.Generic;
using System.Linq;
using MathGame.Answer;
using MathGame.Board;
using MathGame.BoardResolution;
using MathGame.Connection;
using MathGame.Core.Random;
using NUnit.Framework;
using DomainBoard = MathGame.Board.Board;

namespace MathGame.Tests.BoardResolution
{
    public sealed class BoardResolverReviewCoverageTests
    {
        [Test]
        public void MissAndNoSelectionReturnAnswerNotCorrectWithoutRandomDraws()
        {
            var board = Filled(BoardTopology.CreateRectangular(1, 2));
            var validator = new AnswerValidator(AnswerTimingThresholds.Prototype);
            var empty = new ConnectionPath(board).CreateSnapshot();
            var noSelection = validator.Evaluate(empty, new TargetNumber(1), 0);
            var miss = validator.Evaluate(Path(board, new BoardPosition(0, 0)), new TargetNumber(99), 0);

            AssertFailure(board, noSelection, 3, BoardResolutionFailure.AnswerNotCorrect);
            AssertFailure(board, miss, 3, BoardResolutionFailure.AnswerNotCorrect);
        }

        [Test]
        public void EmptySelectionGuardIsDefensiveAndUnreachableThroughPublicAnswerApi()
        {
            var board = Filled(BoardTopology.CreateRectangular(1, 1));
            var answer = new AnswerValidator(AnswerTimingThresholds.Prototype).Evaluate(
                new ConnectionPath(board).CreateSnapshot(),
                new TargetNumber(1),
                0);

            Assert.That(answer.Outcome, Is.EqualTo(AnswerOutcome.NoSelection));
            Assert.That(answer.IsCorrect, Is.False);
        }

        [Test]
        public void NearIntegerIdCapacityHasExactValidAndExhaustedBoundaries()
        {
            var board = Filled(BoardTopology.CreateRectangular(1, 2));
            var answer = Correct(board, new BoardPosition(0, 0), new BoardPosition(0, 1));
            var valid = Resolve(board, answer, int.MaxValue - 2, new SequenceRandom(1, 2));

            Assert.That(valid.Succeeded, Is.True);
            Assert.That(valid.NextBlockIdValue, Is.EqualTo(int.MaxValue));
            Assert.That(valid.Spawned.Select(delta => delta.Block.Id.Value),
                Is.EqualTo(new[] { int.MaxValue - 2, int.MaxValue - 1 }));
            AssertFailure(board, answer, int.MaxValue - 1, BoardResolutionFailure.BlockIdRangeExhausted);
        }

        [Test]
        public void ActiveEmptyCellIsUnsupportedBeforeRandomness()
        {
            var board = Filled(BoardTopology.CreateRectangular(1, 2));
            var answer = Correct(board, new BoardPosition(0, 0), new BoardPosition(0, 1));
            board.TryRemoveBlock(new BoardPosition(0, 1), out _);

            AssertFailure(board, answer, 3, BoardResolutionFailure.UnsupportedBoardState);
        }

        [Test]
        public void MovedAndReplacedSelectedEvidenceReturnsExactMismatch()
        {
            var movedBoard = Filled(BoardTopology.CreateRectangular(1, 3));
            var movedAnswer = Correct(movedBoard, new BoardPosition(0, 0), new BoardPosition(0, 1));
            movedBoard.TryRemoveBlock(new BoardPosition(0, 2), out _);
            movedBoard.TryRelocateBlock(new BoardPosition(0, 1), new BoardPosition(0, 2));
            movedBoard.TryPlaceBlock(new BoardPosition(0, 1), new NumberBlock(new BlockId(4), 4));
            AssertFailure(movedBoard, movedAnswer, 5, BoardResolutionFailure.SelectedBlockMismatch);

            var replacedBoard = Filled(BoardTopology.CreateRectangular(1, 2));
            var replacedAnswer = Correct(replacedBoard, new BoardPosition(0, 0), new BoardPosition(0, 1));
            replacedBoard.TryRemoveBlock(new BoardPosition(0, 0), out _);
            replacedBoard.TryPlaceBlock(new BoardPosition(0, 0), new NumberBlock(new BlockId(1), 99));
            AssertFailure(replacedBoard, replacedAnswer, 3, BoardResolutionFailure.SelectedBlockMismatch);
        }

        [Test]
        public void MultiColumnMiddleRemovalMovesInColumnThenDestinationOrder()
        {
            var board = Filled(BoardTopology.CreateRectangular(2, 3));
            var answer = Correct(board, new BoardPosition(0, 1), new BoardPosition(1, 1));
            var result = Resolve(board, answer, 7, new SequenceRandom(8, 9));

            Assert.That(result.Moved.Select(delta => (delta.From, delta.To)), Is.EqualTo(new[]
            {
                (new BoardPosition(0, 2), new BoardPosition(0, 1)),
                (new BoardPosition(1, 2), new BoardPosition(1, 1))
            }));
            Assert.That(result.Spawned.Select(delta => delta.Destination), Is.EqualTo(new[]
            {
                new BoardPosition(0, 2), new BoardPosition(1, 2)
            }));
        }

        [Test]
        public void MultipleMaskedSegmentsAndColumnsUseExactRefillDrawAndIdOrder()
        {
            var topology = BoardTopology.CreateMasked(3, 4, new[]
            {
                new BoardPosition(0, 0), new BoardPosition(0, 2), new BoardPosition(0, 3),
                new BoardPosition(1, 2),
                new BoardPosition(2, 0), new BoardPosition(2, 1), new BoardPosition(2, 2)
            });
            var board = Filled(topology);
            var answer = Correct(board,
                new BoardPosition(0, 3), new BoardPosition(0, 2), new BoardPosition(1, 2),
                new BoardPosition(2, 2), new BoardPosition(2, 1), new BoardPosition(2, 0));
            var random = new SequenceRandom(1, 2, 3, 4, 5, 6);
            var result = Resolve(board, answer, 8, random);

            Assert.That(result.Spawned.Select(delta => delta.Destination), Is.EqualTo(new[]
            {
                new BoardPosition(0, 2), new BoardPosition(0, 3),
                new BoardPosition(1, 2),
                new BoardPosition(2, 0), new BoardPosition(2, 1), new BoardPosition(2, 2)
            }));
            Assert.That(result.Spawned.Select(delta => delta.Block.Id.Value), Is.EqualTo(new[] { 8, 9, 10, 11, 12, 13 }));
            Assert.That(random.Calls, Is.EqualTo(Enumerable.Repeat((1, 10), 6).ToArray()));
        }

        [Test]
        public void RandomExceptionPropagatesWithoutMutatingSource()
        {
            var board = Filled(BoardTopology.CreateRectangular(1, 2));
            var answer = Correct(board, new BoardPosition(0, 0), new BoardPosition(0, 1));
            Assert.Throws<TestRandomException>(() => Resolve(board, answer, 3, new ThrowingRandom()));
            AssertIds(board, 1, 2);
        }

        [Test]
        public void SuccessfulBoardsAreIndependentAndFinalStateMatchesEveryDeltaAndIndex()
        {
            var source = Filled(BoardTopology.CreateRectangular(1, 3));
            var answer = Correct(source, new BoardPosition(0, 0), new BoardPosition(0, 1));
            var result = Resolve(source, answer, 4, new SequenceRandom(7, 8));

            Assert.That(result.Board.BlockCount, Is.EqualTo(source.BlockCount));
            foreach (var removed in result.Removed)
                Assert.That(result.Board.TryFindBlock(removed.Block.Id, out _), Is.False);
            foreach (var moved in result.Moved)
            {
                Assert.That(result.Board.TryFindBlock(moved.Block.Id, out var indexed), Is.True);
                Assert.That(indexed, Is.EqualTo(moved.To));
            }
            foreach (var spawned in result.Spawned)
            {
                Assert.That(result.Board.TryFindBlock(spawned.Block.Id, out var indexed), Is.True);
                Assert.That(indexed, Is.EqualTo(spawned.Destination));
            }
            Assert.Throws<NotSupportedException>(() => ((IList<MovedBlockDelta>)result.Moved).Add(default));

            source.TrySetAccess(new BoardPosition(0, 2), CellAccess.Blocked);
            result.Board.TryRemoveBlock(new BoardPosition(0, 0), out _);
            Assert.That(source.BlockCount, Is.EqualTo(3));
            Assert.That(result.Board.BlockCount, Is.EqualTo(2));
            source.TryGetCell(new BoardPosition(0, 2), out var sourceCell);
            result.Board.TryGetCell(new BoardPosition(0, 2), out var replacementCell);
            Assert.That(sourceCell.Access, Is.EqualTo(CellAccess.Blocked));
            Assert.That(replacementCell.Access, Is.EqualTo(CellAccess.Open));
        }

        private static void AssertFailure(DomainBoard board, AnswerResult answer, int nextId, BoardResolutionFailure expected)
        {
            var random = new SequenceRandom();
            var result = Resolve(board, answer, nextId, random);
            Assert.That(result.Failure, Is.EqualTo(expected));
            Assert.That(result.Board, Is.Null);
            Assert.That(random.Calls, Is.Empty);
        }

        private static BoardResolutionResult Resolve(DomainBoard board, AnswerResult answer, int nextId, IRandomSource random)
        {
            return new BoardResolver(random).Resolve(
                new BoardResolutionRequest(board, answer, new RefillValueRange(1, 9), nextId));
        }

        private static DomainBoard Filled(BoardTopology topology)
        {
            var board = new DomainBoard(topology);
            var id = 1;
            foreach (var position in board.EnumerateActivePositions())
                board.TryPlaceBlock(position, new NumberBlock(new BlockId(id), id++));
            return board;
        }

        private static ConnectionPathSnapshot Path(DomainBoard board, params BoardPosition[] positions)
        {
            var path = new ConnectionPath(board);
            foreach (var position in positions) path.TrySelect(position);
            return path.CreateSnapshot();
        }

        private static AnswerResult Correct(DomainBoard board, params BoardPosition[] positions)
        {
            var snapshot = Path(board, positions);
            return new AnswerValidator(AnswerTimingThresholds.Prototype).Evaluate(
                snapshot, new TargetNumber((int)snapshot.Sum), 0);
        }

        private static void AssertIds(DomainBoard board, params int[] ids)
        {
            var index = 0;
            foreach (var position in board.EnumerateActivePositions())
            {
                board.TryGetCell(position, out var cell);
                Assert.That(cell.Block.Value.Id.Value, Is.EqualTo(ids[index++]));
            }
        }

        private sealed class SequenceRandom : IRandomSource
        {
            private readonly Queue<int> values;
            public SequenceRandom(params int[] values) { this.values = new Queue<int>(values); }
            public List<(int, int)> Calls { get; } = new List<(int, int)>();
            public int NextInt(int minInclusive, int maxExclusive)
            { Calls.Add((minInclusive, maxExclusive)); return values.Dequeue(); }
            public float NextFloat() => throw new NotSupportedException();
        }

        private sealed class ThrowingRandom : IRandomSource
        {
            public int NextInt(int minInclusive, int maxExclusive) => throw new TestRandomException();
            public float NextFloat() => throw new NotSupportedException();
        }

        private sealed class TestRandomException : Exception { }
    }
}
