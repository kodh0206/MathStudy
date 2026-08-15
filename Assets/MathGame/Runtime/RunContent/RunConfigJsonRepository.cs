using System;
using MathGame.SurvivalRun;
using UnityEngine;

namespace MathGame.RunContent
{
    [Serializable]
    public sealed class RunConfigJsonData
    {
        public int schemaVersion;
        public string id;
        public double initialTime;
        public double maximumTime;
        public double drainPerSecond;
        public double normalRecovery;
        public double fastRecovery;
        public double perfectRecovery;
        public RunDifficultyTierJsonData[] tiers;
    }

    [Serializable]
    public sealed class RunDifficultyTierJsonData
    {
        public int id;
        public long unlockCorrectCycles;
        public int targetMin;
        public int targetMax;
    }

    public enum RunConfigLoadStatus { Succeeded = 0, MissingJson = 1, InvalidJson = 2, InvalidConfiguration = 3 }

    public sealed class RunConfigLoadResult
    {
        internal RunConfigLoadResult(RunConfigLoadStatus status, SurvivalRunConfig config, string error)
        { Status = status; Config = config; Error = error; }
        public RunConfigLoadStatus Status { get; }
        public SurvivalRunConfig Config { get; }
        public string Error { get; }
        public bool Succeeded => Status == RunConfigLoadStatus.Succeeded;
    }

    public interface IRunConfigRepository
    {
        RunConfigLoadResult Load();
    }

    public sealed class RunConfigJsonRepository : IRunConfigRepository
    {
        private readonly string resourcePath;
        public RunConfigJsonRepository(string resourcePath) { this.resourcePath = resourcePath; }

        public RunConfigLoadResult Load()
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            return asset == null
                ? new RunConfigLoadResult(RunConfigLoadStatus.MissingJson, null, "Missing runtime JSON resource: " + resourcePath)
                : Parse(asset.text);
        }

        public static RunConfigLoadResult Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new RunConfigLoadResult(RunConfigLoadStatus.MissingJson, null, "Runtime JSON is empty.");
            RunConfigJsonData data;
            try { data = JsonUtility.FromJson<RunConfigJsonData>(json); }
            catch (Exception error) { return new RunConfigLoadResult(RunConfigLoadStatus.InvalidJson, null, error.Message); }
            if (data == null || data.schemaVersion != 1 || string.IsNullOrWhiteSpace(data.id) || data.tiers == null)
                return new RunConfigLoadResult(RunConfigLoadStatus.InvalidJson, null, "Required fields schemaVersion, id, or tiers are missing/invalid.");
            try
            {
                var tiers = new DifficultyTierConfig[data.tiers.Length];
                for (var index = 0; index < tiers.Length; index++)
                {
                    var tier = data.tiers[index] ?? throw new ArgumentException("Null tier at index " + index + ".");
                    tiers[index] = new DifficultyTierConfig(tier.id, tier.unlockCorrectCycles,
                        new RunTargetRange(tier.targetMin, tier.targetMax));
                }
                var config = new SurvivalRunConfig(
                    new SurvivalTimeSettings(data.initialTime, data.maximumTime, data.drainPerSecond),
                    new TimingRecoverySettings(data.normalRecovery, data.fastRecovery, data.perfectRecovery), tiers);
                return new RunConfigLoadResult(RunConfigLoadStatus.Succeeded, config, null);
            }
            catch (Exception error)
            { return new RunConfigLoadResult(RunConfigLoadStatus.InvalidConfiguration, null, error.Message); }
        }
    }
}
