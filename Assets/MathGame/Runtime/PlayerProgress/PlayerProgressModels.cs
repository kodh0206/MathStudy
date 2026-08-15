using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MathGame.PlayerProgress
{
    public sealed class RunRecords
    {
        public static readonly RunRecords Empty = new RunRecords(0, 0, 0, 0, 0);

        public RunRecords(long bestScore, double bestSurvivalDuration, int highestDifficultyReached,
            int bestCombo, long totalRuns)
        {
            if (bestScore < 0 || bestSurvivalDuration < 0 || double.IsNaN(bestSurvivalDuration) ||
                double.IsInfinity(bestSurvivalDuration) || highestDifficultyReached < 0 || bestCombo < 0 || totalRuns < 0)
                throw new ArgumentOutOfRangeException(nameof(bestScore), "Run records cannot contain negative or non-finite values.");
            BestScore = bestScore;
            BestSurvivalDuration = bestSurvivalDuration;
            HighestDifficultyReached = highestDifficultyReached;
            BestCombo = bestCombo;
            TotalRuns = totalRuns;
        }

        public long BestScore { get; }
        public double BestSurvivalDuration { get; }
        public int HighestDifficultyReached { get; }
        public int BestCombo { get; }
        public long TotalRuns { get; }
    }

    public sealed class PlayerProgress
    {
        public static PlayerProgress NewPlayer => new PlayerProgress(RunRecords.Empty, Array.Empty<string>(), PlayerSettings.Default);

        public PlayerProgress(RunRecords runRecords, IEnumerable<string> appliedRunIds, PlayerSettings settings = null)
        {
            RunRecords = runRecords ?? throw new ArgumentNullException(nameof(runRecords));
            if (appliedRunIds == null) throw new ArgumentNullException(nameof(appliedRunIds));
            var ids = appliedRunIds.ToArray();
            if (ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
                throw new ArgumentException("Applied run identities must be non-empty and unique.", nameof(appliedRunIds));
            AppliedRunIds = new ReadOnlyCollection<string>(ids);
            Settings = settings ?? PlayerSettings.Default;
        }

        public RunRecords RunRecords { get; }
        public IReadOnlyList<string> AppliedRunIds { get; }
        public PlayerSettings Settings { get; }
        public PlayerProgress WithLocale(string localeCode) =>
            new PlayerProgress(RunRecords, AppliedRunIds, new PlayerSettings(localeCode));
    }

    public sealed class PlayerSettings
    {
        public static readonly PlayerSettings Default = new PlayerSettings(null);
        public PlayerSettings(string localeCode)
        {
            if (localeCode != null && string.IsNullOrWhiteSpace(localeCode)) throw new ArgumentException("Locale code cannot be whitespace.", nameof(localeCode));
            LocaleCode = localeCode;
        }
        public string LocaleCode { get; }
    }

    public enum ProgressUpdateStatus { Applied = 0, DuplicateRun = 1, MissingResult = 2, InvalidResult = 3, Overflow = 4 }

    public sealed class ProgressUpdateResult
    {
        internal ProgressUpdateResult(ProgressUpdateStatus status, PlayerProgress before, PlayerProgress after,
            bool newBestScore, bool newBestSurvivalDuration, bool newHighestDifficulty, bool newBestCombo)
        {
            Status = status; Before = before; After = after; NewBestScore = newBestScore;
            NewBestSurvivalDuration = newBestSurvivalDuration; NewHighestDifficulty = newHighestDifficulty;
            NewBestCombo = newBestCombo;
        }

        public ProgressUpdateStatus Status { get; }
        public PlayerProgress Before { get; }
        public PlayerProgress After { get; }
        public bool NewBestScore { get; }
        public bool NewBestSurvivalDuration { get; }
        public bool NewHighestDifficulty { get; }
        public bool NewBestCombo { get; }
        public bool Changed => Status == ProgressUpdateStatus.Applied;
    }
}
