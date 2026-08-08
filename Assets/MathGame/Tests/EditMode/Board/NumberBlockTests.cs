using System;
using NUnit.Framework;
using MathGame.Board;

namespace MathGame.Tests.Board
{
    public sealed class NumberBlockTests
    {
        [TestCase(0)] [TestCase(-1)] public void BlockIdRejectsNonPositiveValues(int value) => Assert.Throws<ArgumentOutOfRangeException>(() => new BlockId(value));
        [Test] public void NumberBlockRejectsInvalidId() => Assert.Throws<ArgumentException>(() => new NumberBlock(default, 1));
        [TestCase(0)] [TestCase(-1)] public void NumberBlockRejectsNonPositiveValues(int value) => Assert.Throws<ArgumentOutOfRangeException>(() => new NumberBlock(new BlockId(1), value));
        [Test] public void EqualValuesWithDifferentIdsAreDistinctValidBlocks()
        {
            var first = new NumberBlock(new BlockId(1), 12);
            var second = new NumberBlock(new BlockId(2), 12);
            Assert.That(first.IsValid && second.IsValid, Is.True);
            Assert.That(first, Is.Not.EqualTo(second));
        }
    }
}
