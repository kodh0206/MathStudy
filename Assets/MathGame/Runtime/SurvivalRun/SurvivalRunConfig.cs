using System;
using System.Collections.Generic;
using System.Linq;
using MathGame.Answer;

namespace MathGame.SurvivalRun
{
    public readonly struct RunTargetRange
    {
        public RunTargetRange(int minimum, int maximum)
        {
            if (minimum <= 0 || maximum < minimum) throw new ArgumentOutOfRangeException(nameof(minimum));
            Minimum = minimum;
            Maximum = maximum;
        }
        public int Minimum { get; }
        public int Maximum { get; }
    }

    public sealed class SurvivalTimeSettings
    {
        public SurvivalTimeSettings(double initialTime, double maximumTime, double drainPerSecond)
        {
            if (!FinitePositive(initialTime) || !FinitePositive(maximumTime) || maximumTime < initialTime)
                throw new ArgumentOutOfRangeException(nameof(initialTime));
            if (!FiniteNonNegative(drainPerSecond)) throw new ArgumentOutOfRangeException(nameof(drainPerSecond));
            InitialTime = initialTime;
            MaximumTime = maximumTime;
            DrainPerSecond = drainPerSecond;
        }
        public double InitialTime { get; }
        public double MaximumTime { get; }
        public double DrainPerSecond { get; }
        internal static bool FinitePositive(double value) => value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);
        internal static bool FiniteNonNegative(double value) => value >= 0 && !double.IsNaN(value) && !double.IsInfinity(value);
    }

    public sealed class TimingRecoverySettings
    {
        public TimingRecoverySettings(double normal, double fast, double perfect)
        {
            if (!SurvivalTimeSettings.FiniteNonNegative(normal) || !SurvivalTimeSettings.FiniteNonNegative(fast) ||
                !SurvivalTimeSettings.FiniteNonNegative(perfect)) throw new ArgumentOutOfRangeException(nameof(normal));
            if (fast < normal || perfect < fast)
                throw new ArgumentException("Recovery must preserve Normal <= Fast <= Perfect.");
            Normal = normal;
            Fast = fast;
            Perfect = perfect;
        }
        public double Normal { get; }
        public double Fast { get; }
        public double Perfect { get; }
        public double For(SpeedGrade grade) => grade == SpeedGrade.Perfect ? Perfect
            : grade == SpeedGrade.Fast ? Fast
            : grade == SpeedGrade.Normal ? Normal
            : throw new ArgumentOutOfRangeException(nameof(grade));
    }

    public sealed class DifficultyTierConfig
    {
        public DifficultyTierConfig(int id, long unlockCorrectCycles, RunTargetRange targetRange)
        {
            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
            if (unlockCorrectCycles < 0) throw new ArgumentOutOfRangeException(nameof(unlockCorrectCycles));
            if (targetRange.Minimum <= 0 || targetRange.Maximum < targetRange.Minimum)
                throw new ArgumentOutOfRangeException(nameof(targetRange));
            Id = id;
            UnlockCorrectCycles = unlockCorrectCycles;
            TargetRange = targetRange;
        }
        public int Id { get; }
        public long UnlockCorrectCycles { get; }
        public RunTargetRange TargetRange { get; }
    }

    public sealed class SurvivalRunConfig
    {
        public SurvivalRunConfig(SurvivalTimeSettings survival, TimingRecoverySettings recovery,
            IEnumerable<DifficultyTierConfig> difficultyTiers)
        {
            Survival = survival ?? throw new ArgumentNullException(nameof(survival));
            Recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
            var tiers = difficultyTiers?.ToArray() ?? throw new ArgumentNullException(nameof(difficultyTiers));
            if (tiers.Length == 0) throw new ArgumentException("At least one difficulty tier is required.", nameof(difficultyTiers));
            for (var index = 0; index < tiers.Length; index++)
            {
                if (tiers[index] == null || tiers[index].Id != index + 1)
                    throw new ArgumentException("Difficulty tier IDs must be contiguous and ordered from 1.", nameof(difficultyTiers));
                if (index == 0 && tiers[index].UnlockCorrectCycles != 0)
                    throw new ArgumentException("The first difficulty tier must unlock at zero correct cycles.", nameof(difficultyTiers));
                if (index > 0 && tiers[index].UnlockCorrectCycles <= tiers[index - 1].UnlockCorrectCycles)
                    throw new ArgumentException("Difficulty thresholds must be strictly increasing.", nameof(difficultyTiers));
            }
            DifficultyTiers = Array.AsReadOnly(tiers);
        }

        // Compatibility constructor retained for tests and legacy composition.
        public SurvivalRunConfig(double initialTime, double maximumTime, double drainPerSecond,
            double normalRecovery, double fastRecovery, double perfectRecovery,
            int correctCyclesPerTier, IEnumerable<RunTargetRange> targetRanges)
            : this(new SurvivalTimeSettings(initialTime, maximumTime, drainPerSecond),
                new TimingRecoverySettings(normalRecovery, fastRecovery, perfectRecovery),
                BuildUniformTiers(correctCyclesPerTier, targetRanges))
        { }

        public SurvivalTimeSettings Survival { get; }
        public TimingRecoverySettings Recovery { get; }
        public IReadOnlyList<DifficultyTierConfig> DifficultyTiers { get; }
        public double InitialTime => Survival.InitialTime;
        public double MaximumTime => Survival.MaximumTime;
        public double DrainPerSecond => Survival.DrainPerSecond;
        public double NormalRecovery => Recovery.Normal;
        public double FastRecovery => Recovery.Fast;
        public double PerfectRecovery => Recovery.Perfect;
        public int CorrectCyclesPerTier
        {
            get
            {
                if (DifficultyTiers.Count < 2) return 0;
                var interval = DifficultyTiers[1].UnlockCorrectCycles;
                if (interval <= 0 || interval > int.MaxValue) return 0;
                for (var index = 2; index < DifficultyTiers.Count; index++)
                    if (DifficultyTiers[index].UnlockCorrectCycles - DifficultyTiers[index - 1].UnlockCorrectCycles != interval)
                        return 0;
                return (int)interval;
            }
        }
        public IReadOnlyList<RunTargetRange> TargetRanges => DifficultyTiers.Select(tier => tier.TargetRange).ToArray();
        public double RecoveryFor(SpeedGrade grade) => Recovery.For(grade);

        public int TierIndexFor(long committedCorrectCycles)
        {
            if (committedCorrectCycles < 0) throw new ArgumentOutOfRangeException(nameof(committedCorrectCycles));
            var index = 0;
            while (index + 1 < DifficultyTiers.Count &&
                   committedCorrectCycles >= DifficultyTiers[index + 1].UnlockCorrectCycles) index++;
            return index;
        }

        // TEMPORARY P06 PLAYTEST CONTENT. Runtime composition now loads equivalent JSON.
        public static SurvivalRunConfig TemporaryPrototype => new SurvivalRunConfig(
            new SurvivalTimeSettings(35, 45, 1),
            new TimingRecoverySettings(1.5, 2.75, 4),
            new[]
            {
                new DifficultyTierConfig(1, 0, new RunTargetRange(5, 9)),
                new DifficultyTierConfig(2, 6, new RunTargetRange(7, 11)),
                new DifficultyTierConfig(3, 12, new RunTargetRange(9, 13)),
                new DifficultyTierConfig(4, 18, new RunTargetRange(11, 15)),
                new DifficultyTierConfig(5, 24, new RunTargetRange(13, 16))
            });

        private static IEnumerable<DifficultyTierConfig> BuildUniformTiers(int cyclesPerTier,
            IEnumerable<RunTargetRange> targetRanges)
        {
            if (cyclesPerTier <= 0) throw new ArgumentOutOfRangeException(nameof(cyclesPerTier));
            var ranges = targetRanges?.ToArray() ?? throw new ArgumentNullException(nameof(targetRanges));
            if (ranges.Length == 0) throw new ArgumentException("At least one target range is required.", nameof(targetRanges));
            return ranges.Select((range, index) => new DifficultyTierConfig(index + 1, checked((long)index * cyclesPerTier), range));
        }
    }
}
