using System;
using MathGame.Connection;

namespace MathGame.Answer
{
    public sealed class AnswerValidator
    {
        private readonly AnswerTimingThresholds thresholds;
        public AnswerValidator(AnswerTimingThresholds thresholds)
        {
            this.thresholds = thresholds ?? throw new ArgumentNullException(nameof(thresholds));
        }

        public AnswerRelation Preview(long sum, TargetNumber target)
        {
            ValidateTarget(target);
            return sum < target.Value ? AnswerRelation.BelowTarget
                : sum > target.Value ? AnswerRelation.AboveTarget : AnswerRelation.MatchesTarget;
        }

        public AnswerResult Evaluate(ConnectionPathSnapshot snapshot, TargetNumber target, double interactiveElapsedSeconds)
        {
            ValidateTarget(target);
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (double.IsNaN(interactiveElapsedSeconds) || double.IsInfinity(interactiveElapsedSeconds) || interactiveElapsedSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(interactiveElapsedSeconds));
            if (snapshot.IsEmpty)
                return new AnswerResult(target, snapshot, AnswerRelation.None, AnswerOutcome.NoSelection, AnswerMissReason.None, SpeedGrade.None, interactiveElapsedSeconds);

            var relation = Preview(snapshot.Sum, target);
            if (relation == AnswerRelation.BelowTarget)
                return Miss(target, snapshot, relation, AnswerMissReason.UnderTarget, interactiveElapsedSeconds);
            if (relation == AnswerRelation.AboveTarget)
                return Miss(target, snapshot, relation, AnswerMissReason.OverTarget, interactiveElapsedSeconds);
            if (snapshot.Count < 2)
                return Miss(target, snapshot, relation, AnswerMissReason.InsufficientConnectionLength, interactiveElapsedSeconds);

            var grade = interactiveElapsedSeconds <= thresholds.PerfectSeconds ? SpeedGrade.Perfect
                : interactiveElapsedSeconds <= thresholds.FastSeconds ? SpeedGrade.Fast : SpeedGrade.Normal;
            return new AnswerResult(target, snapshot, relation, AnswerOutcome.Correct, AnswerMissReason.None, grade, interactiveElapsedSeconds);
        }

        private static AnswerResult Miss(TargetNumber target, ConnectionPathSnapshot snapshot, AnswerRelation relation,
            AnswerMissReason reason, double elapsed)
            => new AnswerResult(target, snapshot, relation, AnswerOutcome.Miss, reason, SpeedGrade.Miss, elapsed);
        private static void ValidateTarget(TargetNumber target)
        {
            if (!target.IsValid) throw new ArgumentException("A valid target is required.", nameof(target));
        }
    }
}
