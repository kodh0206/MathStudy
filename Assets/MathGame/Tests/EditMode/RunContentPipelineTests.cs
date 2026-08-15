using MathGame.Editor.SceneBuilder;
using MathGame.RunContent;
using NUnit.Framework;

namespace MathGame.Tests
{
    public sealed class RunContentPipelineTests
    {
        private const string RunCsv = "Id,InitialTime,MaximumTime,DrainPerSecond,NormalRecovery,FastRecovery,PerfectRecovery\nrun,35,45,1,1.5,2.75,4";
        private const string TierCsv = "TierId,UnlockCorrectCycles,TargetMin,TargetMax\n1,0,5,9\n2,6,7,11";

        [Test]
        public void ValidCsvConvertsDeterministicallyAndRepositoryResolvesConfig()
        {
            var first = RunContentCsvConverter.ConvertText(RunCsv, TierCsv);
            var second = RunContentCsvConverter.ConvertText(RunCsv, TierCsv);
            Assert.That(first.Succeeded, Is.True, first.Error);
            Assert.That(second.Json, Is.EqualTo(first.Json));
            var loaded = RunConfigJsonRepository.Parse(first.Json);
            Assert.That(loaded.Succeeded, Is.True, loaded.Error);
            Assert.That(loaded.Config.InitialTime, Is.EqualTo(35));
            Assert.That(loaded.Config.DifficultyTiers.Count, Is.EqualTo(2));
            Assert.That(loaded.Config.DifficultyTiers[1].UnlockCorrectCycles, Is.EqualTo(6));
        }

        [TestCase("TierId,UnlockCorrectCycles,TargetMin,TargetMax\n1,0,5,9\n1,6,7,11", "duplicate TierId")]
        [TestCase("TierId,UnlockCorrectCycles,TargetMin,TargetMax\n1,0,9,5", "range")]
        [TestCase("TierId,UnlockCorrectCycles,TargetMin,TargetMax\nnope,0,5,9", "malformed")]
        public void InvalidCsvIsRejectedWithActionableError(string tiers, string expected)
        {
            var result = RunContentCsvConverter.ConvertText(RunCsv, tiers);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error.ToLowerInvariant(), Does.Contain(expected.ToLowerInvariant()));
        }

        [Test]
        public void MissingAndInvalidJsonAreRejected()
        {
            Assert.That(RunConfigJsonRepository.Parse(null).Status, Is.EqualTo(RunConfigLoadStatus.MissingJson));
            Assert.That(RunConfigJsonRepository.Parse("{\"schemaVersion\":1}").Status, Is.EqualTo(RunConfigLoadStatus.InvalidJson));
        }
    }
}
