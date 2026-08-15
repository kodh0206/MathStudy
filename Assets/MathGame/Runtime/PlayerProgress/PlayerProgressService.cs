using System;
using System.Collections.Generic;
using MathGame.SurvivalRun;

namespace MathGame.PlayerProgress
{
    public sealed class PlayerProgressService
    {
        private PlayerProgress current;

        public PlayerProgressService(PlayerProgress initialProgress)
        {
            current = initialProgress ?? throw new ArgumentNullException(nameof(initialProgress));
        }

        public PlayerProgress Current => current;

        public ProgressUpdateResult ApplyCompletedRun(RunResult result)
        {
            var before = current;
            if (result == null) return Result(ProgressUpdateStatus.MissingResult, before, before);
            if (string.IsNullOrWhiteSpace(result.RunId) || result.Score < 0 || result.ActiveDuration < 0 ||
                double.IsNaN(result.ActiveDuration) || double.IsInfinity(result.ActiveDuration) ||
                result.MaximumFeverCombo < 0 || result.HighestDifficultyTier < 0)
                return Result(ProgressUpdateStatus.InvalidResult, before, before);
            for (var i = 0; i < before.AppliedRunIds.Count; i++)
                if (string.Equals(before.AppliedRunIds[i], result.RunId, StringComparison.Ordinal))
                    return Result(ProgressUpdateStatus.DuplicateRun, before, before);

            var old = before.RunRecords;
            try
            {
                var scoreBest = result.Score > old.BestScore;
                var durationBest = result.ActiveDuration > old.BestSurvivalDuration;
                var difficultyBest = result.HighestDifficultyTier > old.HighestDifficultyReached;
                var comboBest = result.MaximumFeverCombo > old.BestCombo;
                var records = new RunRecords(Math.Max(old.BestScore, result.Score),
                    Math.Max(old.BestSurvivalDuration, result.ActiveDuration),
                    Math.Max(old.HighestDifficultyReached, result.HighestDifficultyTier),
                    Math.Max(old.BestCombo, result.MaximumFeverCombo), checked(old.TotalRuns + 1));
                var ids = new List<string>(before.AppliedRunIds) { result.RunId };
                current = new PlayerProgress(records, ids, before.Settings);
                return new ProgressUpdateResult(ProgressUpdateStatus.Applied, before, current,
                    scoreBest, durationBest, difficultyBest, comboBest);
            }
            catch (OverflowException)
            {
                return Result(ProgressUpdateStatus.Overflow, before, before);
            }
        }

        public PlayerProgress SetLocale(string localeCode)
        {
            current = current.WithLocale(localeCode);
            return current;
        }

        private static ProgressUpdateResult Result(ProgressUpdateStatus status, PlayerProgress before, PlayerProgress after) =>
            new ProgressUpdateResult(status, before, after, false, false, false, false);
    }
}
