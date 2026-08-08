using System;
using System.Collections.Generic;
using System.Linq;
using MathGame.Board;
using MathGame.BoardGeneration;
using MathGame.Core.Random;
using NUnit.Framework;

namespace MathGame.Tests.BoardGeneration
{
    public sealed class BoardGeneratorTests
    {
        [Test]
        public void PrototypeConfigurationFillsFiveByFiveInRowMajorOrder()
        {
            var random = new RecordingRandomSource(Enumerable.Range(0, 25).Select(index => index % 9 + 1));
            var result = new BoardGenerator(random).Generate(
                new BoardGenerationConfig(BoardTopology.CreateRectangular(5, 5), 1, 9));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Failure, Is.EqualTo(BoardGenerationFailure.None));
            Assert.That(result.Board.BlockCount, Is.EqualTo(25));
            Assert.That(result.NextBlockIdValue, Is.EqualTo(26));
            Assert.That(random.Calls, Has.Count.EqualTo(25));

            var index = 0;
            foreach (var position in result.Board.EnumerateActivePositions())
            {
                Assert.That(result.Board.TryGetCell(position, out var cell), Is.EqualTo(CellLookupResult.Succeeded));
                Assert.That(cell.Access, Is.EqualTo(CellAccess.Open));
                Assert.That(cell.HasBlock, Is.True);
                Assert.That(cell.Block.Value.Id.Value, Is.EqualTo(index + 1));
                Assert.That(cell.Block.Value.Value, Is.EqualTo(index % 9 + 1));
                Assert.That(result.Board.TryFindBlock(cell.Block.Value.Id, out var found), Is.True);
                Assert.That(found, Is.EqualTo(position));
                index++;
            }
        }

        [Test]
        public void InclusiveBoundariesUseOneExclusiveRangeCallPerCell()
        {
            var random = new RecordingRandomSource(new[] { 3, 7, 3 });
            var result = new BoardGenerator(random).Generate(
                new BoardGenerationConfig(BoardTopology.CreateRectangular(3, 1), 3, 7));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(random.Calls, Is.EqualTo(new[]
            {
                new RandomCall(3, 8), new RandomCall(3, 8), new RandomCall(3, 8)
            }));
            Assert.That(Values(result), Is.EqualTo(new[] { 3, 7, 3 }));
        }

        [Test]
        public void MaskFillsOnlyActivePositionsWithoutConsumingHoleDrawsOrIds()
        {
            var topology = BoardTopology.CreateMasked(4, 2, new[]
            {
                new BoardPosition(3, 1), new BoardPosition(0, 0), new BoardPosition(2, 0)
            });
            var random = new RecordingRandomSource(new[] { 4, 5, 6 });
            var result = new BoardGenerator(random).Generate(new BoardGenerationConfig(topology, 1, 9, 10));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Board.BlockCount, Is.EqualTo(3));
            Assert.That(result.NextBlockIdValue, Is.EqualTo(13));
            Assert.That(random.Calls, Has.Count.EqualTo(3));
            AssertCell(result, new BoardPosition(0, 0), 10, 4);
            AssertCell(result, new BoardPosition(2, 0), 11, 5);
            AssertCell(result, new BoardPosition(3, 1), 12, 6);
            Assert.That(result.Board.TryGetCell(new BoardPosition(1, 0), out _), Is.EqualTo(CellLookupResult.InactivePosition));
        }

        [Test]
        public void EquivalentSeedsProduceEquivalentMappings()
        {
            var config = new BoardGenerationConfig(BoardTopology.CreateRectangular(4, 3), 1, 9, 30);
            var first = new BoardGenerator(new SystemRandomSource(12345)).Generate(config);
            var second = new BoardGenerator(new SystemRandomSource(12345)).Generate(config);

            Assert.That(Snapshot(first), Is.EqualTo(Snapshot(second)));
            Assert.That(first.NextBlockIdValue, Is.EqualTo(second.NextBlockIdValue));
        }

        [Test]
        public void RepeatedGenerationReturnsIndependentBoards()
        {
            var topology = BoardTopology.CreateRectangular(1, 1);
            var generator = new BoardGenerator(new RecordingRandomSource(new[] { 2, 2 }));
            var first = generator.Generate(new BoardGenerationConfig(topology, 1, 9));
            var second = generator.Generate(new BoardGenerationConfig(topology, 1, 9));

            Assert.That(first.Board, Is.Not.SameAs(second.Board));
            Assert.That(first.Board.TryRemoveBlock(default, out _), Is.EqualTo(BoardMutationResult.Succeeded));
            Assert.That(first.Board.BlockCount, Is.Zero);
            Assert.That(second.Board.BlockCount, Is.EqualTo(1));
            Assert.That(topology.IsActive(default), Is.True);
        }

        [TestCase(0, 9, BoardGenerationFailure.InvalidValueRange)]
        [TestCase(-1, 9, BoardGenerationFailure.InvalidValueRange)]
        [TestCase(5, 4, BoardGenerationFailure.InvalidValueRange)]
        [TestCase(1, int.MaxValue, BoardGenerationFailure.InvalidValueRange)]
        public void InvalidRangesFailBeforeRandomConsumption(
            int minimum,
            int maximum,
            BoardGenerationFailure expected)
        {
            AssertFailure(new BoardGenerationConfig(BoardTopology.CreateRectangular(1, 1), minimum, maximum), expected);
        }

        [Test]
        public void MissingInputsAndInvalidIdReturnStableFailures()
        {
            AssertFailure(null, BoardGenerationFailure.MissingConfiguration);
            AssertFailure(new BoardGenerationConfig(null, 1, 9), BoardGenerationFailure.MissingTopology);
            AssertFailure(new BoardGenerationConfig(BoardTopology.CreateRectangular(1, 1), 1, 9, 0), BoardGenerationFailure.InvalidFirstBlockId);
        }

        [Test]
        public void BlockIdCapacityBoundariesDoNotOverflow()
        {
            var topology = BoardTopology.CreateRectangular(1, 1);
            var random = new RecordingRandomSource(new[] { 1 });
            var valid = new BoardGenerator(random).Generate(
                new BoardGenerationConfig(topology, 1, 1, int.MaxValue - 1));
            Assert.That(valid.Succeeded, Is.True);
            Assert.That(valid.NextBlockIdValue, Is.EqualTo(int.MaxValue));

            AssertFailure(
                new BoardGenerationConfig(topology, 1, 1, int.MaxValue),
                BoardGenerationFailure.BlockIdRangeExhausted);
        }

        [Test]
        public void EqualMinimumMaximumStillConsumesOneDrawPerCell()
        {
            var random = new RecordingRandomSource(new[] { 6, 6 });
            var result = new BoardGenerator(random).Generate(
                new BoardGenerationConfig(BoardTopology.CreateRectangular(1, 2), 6, 6));

            Assert.That(Values(result), Is.EqualTo(new[] { 6, 6 }));
            Assert.That(random.Calls, Is.EqualTo(new[] { new RandomCall(6, 7), new RandomCall(6, 7) }));
        }

        [TestCase(0)]
        [TestCase(10)]
        public void OutOfContractRandomValueThrowsWithoutPublishingResult(int value)
        {
            var generator = new BoardGenerator(new RecordingRandomSource(new[] { value }));
            Assert.Throws<InvalidOperationException>(() => generator.Generate(
                new BoardGenerationConfig(BoardTopology.CreateRectangular(1, 1), 1, 9)));
        }

        [Test]
        public void RandomExceptionPropagates()
        {
            var expected = new TestRandomException();
            var generator = new BoardGenerator(new ThrowingRandomSource(expected));
            Assert.That(() => generator.Generate(
                new BoardGenerationConfig(BoardTopology.CreateRectangular(1, 1), 1, 9)), Throws.TypeOf<TestRandomException>());
        }

        [Test]
        public void ConstructorRejectsMissingRandomSource()
        {
            Assert.Throws<ArgumentNullException>(() => new BoardGenerator(null));
        }

        private static void AssertFailure(BoardGenerationConfig config, BoardGenerationFailure failure)
        {
            var random = new RecordingRandomSource(Array.Empty<int>());
            var result = new BoardGenerator(random).Generate(config);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Failure, Is.EqualTo(failure));
            Assert.That(result.Board, Is.Null);
            Assert.That(result.NextBlockIdValue, Is.Zero);
            Assert.That(random.Calls, Is.Empty);
        }

        private static void AssertCell(BoardGenerationResult result, BoardPosition position, int id, int value)
        {
            result.Board.TryGetCell(position, out var cell);
            Assert.That(cell.Block.Value.Id.Value, Is.EqualTo(id));
            Assert.That(cell.Block.Value.Value, Is.EqualTo(value));
        }

        private static int[] Values(BoardGenerationResult result)
        {
            return result.Board.EnumerateActivePositions()
                .Select(position =>
                {
                    result.Board.TryGetCell(position, out var cell);
                    return cell.Block.Value.Value;
                })
                .ToArray();
        }

        private static string[] Snapshot(BoardGenerationResult result)
        {
            return result.Board.EnumerateActivePositions()
                .Select(position =>
                {
                    result.Board.TryGetCell(position, out var cell);
                    return $"{position}:{cell.Block.Value.Id.Value}:{cell.Block.Value.Value}";
                })
                .ToArray();
        }

        private readonly struct RandomCall
        {
            public RandomCall(int minimum, int maximum)
            {
                Minimum = minimum;
                Maximum = maximum;
            }

            public int Minimum { get; }
            public int Maximum { get; }

            public override bool Equals(object obj)
            {
                return obj is RandomCall other && Minimum == other.Minimum && Maximum == other.Maximum;
            }

            public override int GetHashCode()
            {
                return (Minimum * 397) ^ Maximum;
            }
        }

        private sealed class RecordingRandomSource : IRandomSource
        {
            private readonly Queue<int> values;

            public RecordingRandomSource(IEnumerable<int> values)
            {
                this.values = new Queue<int>(values);
            }

            public List<RandomCall> Calls { get; } = new List<RandomCall>();

            public int NextInt(int minInclusive, int maxExclusive)
            {
                Calls.Add(new RandomCall(minInclusive, maxExclusive));
                return values.Dequeue();
            }

            public float NextFloat()
            {
                throw new NotSupportedException();
            }
        }

        private sealed class ThrowingRandomSource : IRandomSource
        {
            private readonly Exception exception;

            public ThrowingRandomSource(Exception exception)
            {
                this.exception = exception;
            }

            public int NextInt(int minInclusive, int maxExclusive)
            {
                throw exception;
            }

            public float NextFloat()
            {
                throw new NotSupportedException();
            }
        }

        private sealed class TestRandomException : Exception
        {
        }
    }
}
