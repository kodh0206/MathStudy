using System;
using MathGame.Connection;

namespace MathGame.Answer
{
    public readonly struct TargetNumber
    {
        public TargetNumber(int value)
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            Value = value;
        }
        public int Value { get; }
        public bool IsValid => Value > 0;
    }

    public enum AnswerRelation { None = 0, BelowTarget = 1, MatchesTarget = 2, AboveTarget = 3 }
    public enum AnswerOutcome { NoSelection = 0, Correct = 1, Miss = 2 }
    public enum AnswerMissReason { None = 0, UnderTarget = 1, OverTarget = 2, InsufficientConnectionLength = 3 }
    public enum SpeedGrade { None = 0, Perfect = 1, Fast = 2, Normal = 3, Miss = 4 }

    public sealed class AnswerTimingThresholds
    {
        public AnswerTimingThresholds(double perfectSeconds, double fastSeconds)
        {
            if (!IsFinite(perfectSeconds) || perfectSeconds < 0) throw new ArgumentOutOfRangeException(nameof(perfectSeconds));
            if (!IsFinite(fastSeconds) || fastSeconds < perfectSeconds) throw new ArgumentOutOfRangeException(nameof(fastSeconds));
            PerfectSeconds = perfectSeconds;
            FastSeconds = fastSeconds;
        }
        public double PerfectSeconds { get; }
        public double FastSeconds { get; }
        public static AnswerTimingThresholds Prototype => new AnswerTimingThresholds(2, 4);
        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    public sealed class AnswerResult
    {
        internal AnswerResult(TargetNumber target, ConnectionPathSnapshot snapshot, AnswerRelation relation,
            AnswerOutcome outcome, AnswerMissReason missReason, SpeedGrade grade, double elapsedSeconds)
        {
            Target = target; Snapshot = snapshot; Relation = relation; Outcome = outcome;
            MissReason = missReason; Grade = grade; InteractiveElapsedSeconds = elapsedSeconds;
        }
        public TargetNumber Target { get; }
        public ConnectionPathSnapshot Snapshot { get; }
        public long SubmittedSum => Snapshot.Sum;
        public int SelectedBlockCount => Snapshot.Count;
        public AnswerRelation Relation { get; }
        public AnswerOutcome Outcome { get; }
        public AnswerMissReason MissReason { get; }
        public SpeedGrade Grade { get; }
        public double InteractiveElapsedSeconds { get; }
        public bool IsCorrect => Outcome == AnswerOutcome.Correct;
    }
}
