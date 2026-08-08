using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using MathGame.Board;

namespace MathGame.Tests.Board
{
    public sealed class BoardTopologyTests
    {
        [Test] public void RectangleEnumeratesRowMajor()
        {
            var positions = BoardTopology.CreateRectangular(5, 5).EnumerateActivePositions().ToArray();
            Assert.That(positions, Has.Length.EqualTo(25));
            Assert.That(positions[0], Is.EqualTo(new BoardPosition(0, 0)));
            Assert.That(positions[4], Is.EqualTo(new BoardPosition(4, 0)));
            Assert.That(positions[5], Is.EqualTo(new BoardPosition(0, 1)));
            Assert.That(positions[24], Is.EqualTo(new BoardPosition(4, 4)));
        }
        [Test] public void MaskIsCopiedAndSupportsDisconnectedHoles()
        {
            var source = new List<BoardPosition> { new BoardPosition(0, 0), new BoardPosition(2, 2) };
            var topology = BoardTopology.CreateMasked(3, 3, source);
            source.Clear();
            Assert.That(topology.IsActive(new BoardPosition(0, 0)), Is.True);
            Assert.That(topology.IsActive(new BoardPosition(1, 1)), Is.False);
            Assert.That(topology.IsWithinBounds(new BoardPosition(1, 1)), Is.True);
        }
        [Test] public void NeighborsUseUpRightDownLeftAndOmitHole()
        {
            var topology = BoardTopology.CreateMasked(3, 3, new[] {
                new BoardPosition(1, 1), new BoardPosition(1, 2), new BoardPosition(2, 1), new BoardPosition(1, 0) });
            Assert.That(topology.EnumerateOrthogonalNeighbors(new BoardPosition(1, 1)), Is.EqualTo(new[] {
                new BoardPosition(1, 2), new BoardPosition(2, 1), new BoardPosition(1, 0) }));
            Assert.That(topology.EnumerateOrthogonalNeighbors(new BoardPosition(0, 0)), Is.Empty);
        }
        [TestCase(0, 1)] [TestCase(1, 0)] public void DimensionsMustBePositive(int width, int height) => Assert.Throws<ArgumentOutOfRangeException>(() => BoardTopology.CreateRectangular(width, height));
        [TestCase(-1, 1)]
        [TestCase(1, -1)]
        public void NegativeDimensionsAreRejected(int width, int height)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BoardTopology.CreateRectangular(width, height));
        }

        [TestCase(1, 1)]
        [TestCase(1, 4)]
        [TestCase(4, 1)]
        [TestCase(3, 7)]
        public void ArbitraryPositiveRectanglesAreRepresentable(int width, int height)
        {
            Assert.That(BoardTopology.CreateRectangular(width, height).EnumerateActivePositions().Count(), Is.EqualTo(width * height));
        }

        [Test]
        public void ConcaveMaskAndCornerEdgeNeighborsRemainOrdered()
        {
            var topology = BoardTopology.CreateMasked(3, 3, new[]
            {
                new BoardPosition(0, 0), new BoardPosition(1, 0),
                new BoardPosition(0, 1), new BoardPosition(1, 1), new BoardPosition(2, 1),
                new BoardPosition(1, 2)
            });
            Assert.That(topology.EnumerateOrthogonalNeighbors(new BoardPosition(0, 0)), Is.EqualTo(new[]
            {
                new BoardPosition(0, 1), new BoardPosition(1, 0)
            }));
            Assert.That(topology.EnumerateOrthogonalNeighbors(new BoardPosition(1, 1)), Is.EqualTo(new[]
            {
                new BoardPosition(1, 2), new BoardPosition(2, 1),
                new BoardPosition(1, 0), new BoardPosition(0, 1)
            }));
        }

        [Test]
        public void NegativeMaskCoordinatesAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BoardTopology.CreateMasked(
                2,
                2,
                new[] { new BoardPosition(-1, 0) }));
            Assert.Throws<ArgumentOutOfRangeException>(() => BoardTopology.CreateMasked(
                2,
                2,
                new[] { new BoardPosition(0, -1) }));
        }
        [Test] public void MaskRejectsNullEmptyDuplicatesAndOutOfBounds()
        {
            Assert.Throws<ArgumentNullException>(() => BoardTopology.CreateMasked(1, 1, null));
            Assert.Throws<ArgumentException>(() => BoardTopology.CreateMasked(1, 1, Array.Empty<BoardPosition>()));
            Assert.Throws<ArgumentException>(() => BoardTopology.CreateMasked(1, 1, new[] { default(BoardPosition), default(BoardPosition) }));
            Assert.Throws<ArgumentOutOfRangeException>(() => BoardTopology.CreateMasked(1, 1, new[] { new BoardPosition(1, 0) }));
        }
    }
}
