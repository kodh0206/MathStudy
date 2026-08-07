using MathGame.Core.Random;
using MathGame.Save;
using NUnit.Framework;

namespace MathGame.Tests.EditMode
{
    public sealed class CoreServiceTests
    {
        [Test]
        public void SeededRandomSources_ProduceSameSequence()
        {
            var first = new SystemRandomSource(12345);
            var second = new SystemRandomSource(12345);

            for (int i = 0; i < 20; i++)
            {
                Assert.That(first.NextInt(1, 10), Is.EqualTo(second.NextInt(1, 10)));
            }
        }

        [Test]
        public void NewSaveData_UsesCurrentSchemaVersion()
        {
            var saveData = new SaveData();

            Assert.That(saveData.SchemaVersion, Is.EqualTo(SaveData.CurrentSchemaVersion));
        }
    }
}
