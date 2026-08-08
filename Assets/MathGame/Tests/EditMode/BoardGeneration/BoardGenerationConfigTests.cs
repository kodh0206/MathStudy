using MathGame.Board;
using MathGame.BoardGeneration;
using NUnit.Framework;

namespace MathGame.Tests.BoardGeneration
{
    public sealed class BoardGenerationConfigTests
    {
        [Test]
        public void ConstructorRecordsRawInputsWithoutValidation()
        {
            var config = new BoardGenerationConfig(null, -1, -2, 0);

            Assert.That(config.Topology, Is.Null);
            Assert.That(config.MinimumValue, Is.EqualTo(-1));
            Assert.That(config.MaximumValue, Is.EqualTo(-2));
            Assert.That(config.FirstBlockIdValue, Is.Zero);
        }

        [Test]
        public void ConstructorRecordsValidTopologyByReference()
        {
            var topology = BoardTopology.CreateRectangular(2, 2);
            var config = new BoardGenerationConfig(topology, 1, 9, 7);

            Assert.That(config.Topology, Is.SameAs(topology));
            Assert.That(config.MinimumValue, Is.EqualTo(1));
            Assert.That(config.MaximumValue, Is.EqualTo(9));
            Assert.That(config.FirstBlockIdValue, Is.EqualTo(7));
        }
    }
}
