using System;
using System.Collections.Generic;
using System.Linq;
using MathGame.Answer;
using MathGame.Board;
using MathGame.Core.Random;
using MathGame.Targets;
using NUnit.Framework;
using DomainBoard = MathGame.Board.Board;

namespace MathGame.Tests.Targets
{
    public sealed class TargetsContractClosureTests
    {
        [TestCase(1, 2, 3)]
        [TestCase(2, 1, 4)]
        [TestCase(1, 0, 5)]
        [TestCase(0, 1, 6)]
        public void SearchFindsEachOrthogonalDirection(int neighborColumn, int neighborRow, int target)
        {
            var board = Filled(BoardTopology.CreateRectangular(3, 3), 100);
            Replace(board, new BoardPosition(1, 1), 50, 1);
            Replace(board, new BoardPosition(neighborColumn, neighborRow), 51, target - 1);

            var result = Search(board, target, target, 2, 2, 200);
            Assert.That(result.Status, Is.EqualTo(TargetSearchStatus.Succeeded));
            var positions = result.Solutions.Single().Steps.Select(step => step.Position).ToArray();
            Assert.That(positions, Does.Contain(new BoardPosition(1, 1)));
            Assert.That(positions, Does.Contain(new BoardPosition(neighborColumn, neighborRow)));
        }

        [Test]
        public void DiagonalHoleAndDisconnectedCellsNeverFormWitness()
        {
            var topology = BoardTopology.CreateMasked(2, 2, new[]
            {
                new BoardPosition(0, 0), new BoardPosition(1, 1)
            });
            var board = Filled(topology, 0);
            Replace(board, new BoardPosition(0, 0), 1, 2);
            Replace(board, new BoardPosition(1, 1), 2, 3);

            Assert.That(Search(board, 5, 5, 2, 2, 20).Status, Is.EqualTo(TargetSearchStatus.NoAvailableTarget));
        }

        [Test]
        public void ReversePathsAndLongerPathsProduceOneDistinctTargetWitness()
        {
            var board = Line(1, 2, 3);
            var result = Search(board, 3, 6, 2, 3, 100);
            Assert.That(result.Solutions.Select(solution => solution.Target.Value).Distinct().Count(),
                Is.EqualTo(result.Solutions.Count));
            Assert.That(result.Solutions.Count(solution => solution.Target.Value == 3), Is.EqualTo(1));
            foreach (var solution in result.Solutions)
            {
                Assert.That(solution.Steps.Select(step => step.Position).Distinct().Count(), Is.EqualTo(solution.Count));
                Assert.That(solution.Steps.Select(step => step.Block.Id).Distinct().Count(), Is.EqualTo(solution.Count));
            }
        }

        [Test]
        public void ExactExpansionBoundaryAllowsNthAppendAndRejectsNPlusOne()
        {
            var board = Line(2, 3);
            var allowed = Search(board, 5, 5, 2, 2, 2);
            Assert.That(allowed.Status, Is.EqualTo(TargetSearchStatus.Succeeded));
            Assert.That(allowed.NodeExpansions, Is.EqualTo(2));

            var limited = Search(board, 5, 6, 2, 2, 1);
            Assert.That(limited.Status, Is.EqualTo(TargetSearchStatus.SearchLimitExceeded));
            Assert.That(limited.NodeExpansions, Is.EqualTo(1));
            Assert.That(limited.Solutions, Is.Empty);
        }

        [Test]
        public void PositivePruningAndLargeSumsDoNotWrap()
        {
            var large = Line(int.MaxValue, int.MaxValue, 1);
            var result = Search(large, 1, int.MaxValue, 2, 3, 100);
            Assert.That(result.Status, Is.EqualTo(TargetSearchStatus.NoAvailableTarget));
            Assert.That(result.Solutions, Is.Empty);
        }

        [Test]
        public void SelectorFailurePrecedenceAndInvalidHistoryConsumeNoRandomness()
        {
            var random = new FaultRandom();
            var selector = new SafeTargetSelector(random);
            Assert.That(selector.Select(null, null, null).Status, Is.EqualTo(TargetSelectionStatus.MissingSearchResult));
            var failedSearch = Search(Line(1, 1), 9, 9, 2, 2, 20);
            Assert.That(selector.Select(failedSearch, null, null).Status, Is.EqualTo(TargetSelectionStatus.SearchNotSuccessful));
            var success = Search(Line(2, 3), 5, 5, 2, 2, 20);
            Assert.That(selector.Select(success, null, null).Status, Is.EqualTo(TargetSelectionStatus.MissingPolicy));
            Assert.That(selector.Select(success, new TargetSelectionPolicy(0), null).Status, Is.EqualTo(TargetSelectionStatus.InvalidPolicy));
            Assert.That(selector.Select(success, new TargetSelectionPolicy(1), new TargetHistory(null, 1)).Status,
                Is.EqualTo(TargetSelectionStatus.InvalidHistory));
            Assert.That(random.CallCount, Is.Zero);
        }

        [Test]
        public void SelectorRejectsOutOfRangeRandomAndPropagatesRandomException()
        {
            var search = Search(Line(2, 3), 5, 5, 2, 2, 20);
            var policy = new TargetSelectionPolicy(2);
            var history = new TargetHistory(null, 0);
            Assert.Throws<InvalidOperationException>(() => new SafeTargetSelector(new ConstantRandom(1)).Select(search, policy, history));
            Assert.Throws<TestRandomException>(() => new SafeTargetSelector(new ThrowRandom()).Select(search, policy, history));
        }

        [Test]
        public void ShufflePreservesTopologyBlocksCountAndProducesOrderedImmutableDeltas()
        {
            var source = Line(4, 5, 6, 7);
            var result = new BoardShuffler(new QueueRandom(0, 0, 0)).Shuffle(source);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Board.Topology, Is.SameAs(source.Topology));
            Assert.That(result.Board.BlockCount, Is.EqualTo(source.BlockCount));
            Assert.That(result.Deltas.Select(delta => delta.To),
                Is.EqualTo(result.Deltas.Select(delta => delta.To).OrderBy(position => position.Row).ThenBy(position => position.Column)));
            Assert.Throws<NotSupportedException>(() => ((IList<ShuffledBlockDelta>)result.Deltas).Add(default));
            foreach (var position in source.EnumerateActivePositions())
            {
                source.TryGetCell(position, out var sourceCell);
                Assert.That(result.Board.TryFindBlock(sourceCell.Block.Value.Id, out var finalPosition), Is.True);
                result.Board.TryGetCell(finalPosition, out var finalCell);
                Assert.That(finalCell.Block.Value, Is.EqualTo(sourceCell.Block.Value));
                Assert.That(finalCell.Access, Is.EqualTo(CellAccess.Open));
            }
        }

        [Test]
        public void ShuffleFailuresAndRandomFaultsDoNotMutateSource()
        {
            var shuffler = new BoardShuffler(new FaultRandom());
            Assert.That(shuffler.Shuffle(null).Status, Is.EqualTo(BoardShuffleStatus.MissingBoard));
            Assert.That(shuffler.Shuffle(Line(1)).Status, Is.EqualTo(BoardShuffleStatus.InsufficientMovableBlocks));
            var source = Line(1, 2);
            Assert.Throws<InvalidOperationException>(() => new BoardShuffler(new ConstantRandom(2)).Shuffle(source));
            Assert.Throws<TestRandomException>(() => new BoardShuffler(new ThrowRandom()).Shuffle(source));
            AssertId(source, 0, 1);
            AssertId(source, 1, 2);
        }

        [Test]
        public void SameSeedRecoveryIsDeterministicAndInvalidConfigMapsExactly()
        {
            var board = Square();
            var config = new TargetRecoveryConfig(
                new TargetSearchConfig(5, 5, 2, 2, 500),
                new TargetSelectionPolicy(2),
                4);
            var first = new TargetRecoveryCoordinator(new SystemRandomSource(123)).SelectNextTarget(board, new TargetHistory(null, 0), config);
            var second = new TargetRecoveryCoordinator(new SystemRandomSource(123)).SelectNextTarget(board, new TargetHistory(null, 0), config);
            Assert.That(second.Status, Is.EqualTo(first.Status));
            Assert.That(second.ShuffleAttemptCount, Is.EqualTo(first.ShuffleAttemptCount));
            Assert.That(second.Deltas.Select(delta => (delta.Block.Id, delta.From, delta.To)),
                Is.EqualTo(first.Deltas.Select(delta => (delta.Block.Id, delta.From, delta.To))));

            var invalid = new TargetRecoveryCoordinator(new ConstantRandom(0)).SelectNextTarget(
                board,
                new TargetHistory(null, 0),
                new TargetRecoveryConfig(null, new TargetSelectionPolicy(1), 1));
            Assert.That(invalid.Status, Is.EqualTo(TargetRecoveryStatus.InvalidConfiguration));
            Assert.That(invalid.Board, Is.Null);
        }

        private static TargetSearchResult Search(DomainBoard board, int min, int max, int minLength, int maxLength, int cap)
            => new TargetPathSearcher().Search(board, new TargetSearchConfig(min, max, minLength, maxLength, cap));

        private static DomainBoard Line(params int[] values)
        {
            var board = new DomainBoard(BoardTopology.CreateRectangular(values.Length, 1));
            for (var index = 0; index < values.Length; index++)
                board.TryPlaceBlock(new BoardPosition(index, 0), new NumberBlock(new BlockId(index + 1), values[index]));
            return board;
        }

        private static DomainBoard Filled(BoardTopology topology, int value)
        {
            var board = new DomainBoard(topology);
            var id = 1;
            foreach (var position in board.EnumerateActivePositions())
                board.TryPlaceBlock(position, new NumberBlock(new BlockId(id++), value == 0 ? 1 : value));
            return board;
        }

        private static DomainBoard Square()
        {
            var board = new DomainBoard(BoardTopology.CreateRectangular(2, 2));
            var values = new[] { 1, 2, 3, 4 };
            var index = 0;
            foreach (var position in board.EnumerateActivePositions())
                board.TryPlaceBlock(position, new NumberBlock(new BlockId(index + 1), values[index++]));
            return board;
        }

        private static void Replace(DomainBoard board, BoardPosition position, int id, int value)
        {
            board.TryRemoveBlock(position, out _);
            board.TryPlaceBlock(position, new NumberBlock(new BlockId(id), value));
        }

        private static void AssertId(DomainBoard board, int column, int id)
        {
            board.TryGetCell(new BoardPosition(column, 0), out var cell);
            Assert.That(cell.Block.Value.Id.Value, Is.EqualTo(id));
        }

        private sealed class QueueRandom : IRandomSource
        {
            private readonly Queue<int> values;
            public QueueRandom(params int[] values) { this.values = new Queue<int>(values); }
            public int NextInt(int minInclusive, int maxExclusive) => values.Dequeue();
            public float NextFloat() => throw new NotSupportedException();
        }

        private sealed class ConstantRandom : IRandomSource
        {
            private readonly int value;
            public ConstantRandom(int value) { this.value = value; }
            public int NextInt(int minInclusive, int maxExclusive) => value;
            public float NextFloat() => throw new NotSupportedException();
        }

        private sealed class FaultRandom : IRandomSource
        {
            public int CallCount { get; private set; }
            public int NextInt(int minInclusive, int maxExclusive) { CallCount++; return 0; }
            public float NextFloat() => throw new NotSupportedException();
        }

        private sealed class ThrowRandom : IRandomSource
        {
            public int NextInt(int minInclusive, int maxExclusive) => throw new TestRandomException();
            public float NextFloat() => throw new NotSupportedException();
        }

        private sealed class TestRandomException : Exception { }
    }
}
