using System;
using MathGame.Answer;
using MathGame.Board;
using MathGame.Connection;
using NUnit.Framework;
using DomainBoard = MathGame.Board.Board;

namespace MathGame.Tests.Answer
{
    public sealed class AnswerValidatorTests
    {
        private readonly AnswerValidator validator = new AnswerValidator(AnswerTimingThresholds.Prototype);

        [TestCase(4, AnswerRelation.BelowTarget)]
        [TestCase(5, AnswerRelation.MatchesTarget)]
        [TestCase(6, AnswerRelation.AboveTarget)]
        public void PreviewClassifiesWithoutSubmission(long sum, AnswerRelation relation)
        {
            Assert.That(validator.Preview(sum, new TargetNumber(5)), Is.EqualTo(relation));
        }

        [Test]
        public void EmptySnapshotIsNoSelection()
        {
            var result = validator.Evaluate(Snapshot(), new TargetNumber(5), 1);
            Assert.That(result.Outcome, Is.EqualTo(AnswerOutcome.NoSelection));
            Assert.That(result.Relation, Is.EqualTo(AnswerRelation.None));
            Assert.That(result.Grade, Is.EqualTo(SpeedGrade.None));
        }

        [TestCase(2, 3, AnswerMissReason.UnderTarget)]
        [TestCase(2, 7, AnswerMissReason.OverTarget)]
        public void UnderAndOverAreDistinctMisses(int count, int value, AnswerMissReason reason)
        {
            var result = validator.Evaluate(Snapshot(count, value), new TargetNumber(10), 1);
            Assert.That(result.Outcome, Is.EqualTo(AnswerOutcome.Miss));
            Assert.That(result.MissReason, Is.EqualTo(reason));
            Assert.That(result.Grade, Is.EqualTo(SpeedGrade.Miss));
        }

        [Test]
        public void OneBlockExactIsInsufficientMiss()
        {
            var snapshot = Snapshot(1, 10);
            var result = validator.Evaluate(snapshot, new TargetNumber(10), 0);
            Assert.That(result.MissReason, Is.EqualTo(AnswerMissReason.InsufficientConnectionLength));
            Assert.That(result.Snapshot, Is.SameAs(snapshot));
            Assert.That(result.SelectedBlockCount, Is.EqualTo(1));
            Assert.That(result.SubmittedSum, Is.EqualTo(10));
        }

        [TestCase(0, SpeedGrade.Perfect)]
        [TestCase(2, SpeedGrade.Perfect)]
        [TestCase(2.0001, SpeedGrade.Fast)]
        [TestCase(4, SpeedGrade.Fast)]
        [TestCase(4.0001, SpeedGrade.Normal)]
        [TestCase(1000, SpeedGrade.Normal)]
        public void ExactMultiBlockAnswersUseInclusiveSpeedBoundaries(double elapsed, SpeedGrade grade)
        {
            var result = validator.Evaluate(Snapshot(2, 5), new TargetNumber(10), elapsed);
            Assert.That(result.IsCorrect, Is.True);
            Assert.That(result.Grade, Is.EqualTo(grade));
            Assert.That(result.InteractiveElapsedSeconds, Is.EqualTo(elapsed));
        }

        [Test]
        public void InvalidInputsThrow()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TargetNumber(0));
            Assert.Throws<ArgumentException>(() => validator.Preview(1, default));
            Assert.Throws<ArgumentNullException>(() => new AnswerValidator(null));
            Assert.Throws<ArgumentNullException>(() => validator.Evaluate(null, new TargetNumber(1), 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => validator.Evaluate(Snapshot(), new TargetNumber(1), -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AnswerTimingThresholds(double.NaN, 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AnswerTimingThresholds(5, 4));
        }

        private static ConnectionPathSnapshot Snapshot(int count = 0, int value = 1)
        {
            var board = new DomainBoard(BoardTopology.CreateRectangular(Math.Max(1, count), 1));
            var path = new ConnectionPath(board);
            for (var index = 0; index < count; index++)
            {
                var position = new BoardPosition(index, 0);
                board.TryPlaceBlock(position, new NumberBlock(new BlockId(index + 1), value));
                path.TrySelect(position);
            }
            return path.CreateSnapshot();
        }
    }
}
