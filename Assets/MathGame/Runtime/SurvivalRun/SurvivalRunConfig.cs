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

    public sealed class SurvivalRunConfig
    {
        public SurvivalRunConfig(double initialTime, double maximumTime, double drainPerSecond,
            double normalRecovery, double fastRecovery, double perfectRecovery,
            int correctCyclesPerTier, IEnumerable<RunTargetRange> targetRanges)
        {
            if (!FinitePositive(initialTime) || !FinitePositive(maximumTime) || initialTime > maximumTime)
                throw new ArgumentOutOfRangeException(nameof(initialTime));
            if (!FinitePositive(drainPerSecond) || !FiniteNonNegative(normalRecovery) ||
                !FiniteNonNegative(fastRecovery) || !FiniteNonNegative(perfectRecovery))
                throw new ArgumentOutOfRangeException(nameof(drainPerSecond));
            if (correctCyclesPerTier <= 0) throw new ArgumentOutOfRangeException(nameof(correctCyclesPerTier));
            var ranges = targetRanges?.ToArray();
            if (ranges == null || ranges.Length == 0) throw new ArgumentException("At least one target range is required.", nameof(targetRanges));
            InitialTime = initialTime; MaximumTime = maximumTime; DrainPerSecond = drainPerSecond;
            NormalRecovery = normalRecovery; FastRecovery = fastRecovery; PerfectRecovery = perfectRecovery;
            CorrectCyclesPerTier = correctCyclesPerTier; TargetRanges = Array.AsReadOnly(ranges);
        }

        public double InitialTime { get; }
        public double MaximumTime { get; }
        public double DrainPerSecond { get; }
        public double NormalRecovery { get; }
        public double FastRecovery { get; }
        public double PerfectRecovery { get; }
        public int CorrectCyclesPerTier { get; }
        public IReadOnlyList<RunTargetRange> TargetRanges { get; }

        public double RecoveryFor(SpeedGrade grade) => grade == SpeedGrade.Perfect ? PerfectRecovery
            : grade == SpeedGrade.Fast ? FastRecovery
            : grade == SpeedGrade.Normal ? NormalRecovery
            : throw new ArgumentOutOfRangeException(nameof(grade));

        // Temporary, non-final prototype balance. Production composition may replace every value.
        public static SurvivalRunConfig TemporaryPrototype => new SurvivalRunConfig(
            30, 60, 1, 3, 5, 8, 5,
            new[] { new RunTargetRange(5, 10), new RunTargetRange(8, 15), new RunTargetRange(10, 20) });

        private static bool FinitePositive(double value) => value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool FiniteNonNegative(double value) => value >= 0 && !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
