using NUnit.Framework;
using MathGame.Board;

namespace MathGame.Tests.Board
{
    public sealed class BoardPositionTests
    {
        [Test] public void EqualityHashAndDiagnosticAreValueBased()
        {
            var value = new BoardPosition(2, 3);
            Assert.That(value, Is.EqualTo(new BoardPosition(2, 3)));
            Assert.That(value.GetHashCode(), Is.EqualTo(new BoardPosition(2, 3).GetHashCode()));
            Assert.That(value, Is.Not.EqualTo(new BoardPosition(3, 2)));
            Assert.That(value.ToString(), Is.EqualTo("(2, 3)"));
        }
    }
}
