using MathGame.Answer;
using MathGame.Board;
using MathGame.SurvivalRun;
using MathGame.Targets;
using NUnit.Framework;

namespace MathGame.Tests
{
    public sealed class SurvivalRunTests
    {
        [Test]
        public void TickDrainsEveryLiveSampleButExcludesPausedTimeAndFreezesExactlyOnce()
        {
            var run = new SurvivalRunSession(Config(initial: 5, maximum: 10));
            Assert.That(run.Tick(2, true), Is.False);
            Assert.That(run.ActiveDuration, Is.Zero);
            Assert.That(run.Tick(2, false), Is.False);
            Assert.That(run.RemainingTime, Is.EqualTo(3));
            Assert.That(run.Tick(3, false), Is.True);
            var first = run.Result;
            Assert.That(run.Tick(10, false), Is.False);
            Assert.That(run.EndExactlyOnce(), Is.SameAs(first));
            Assert.That(first.ActiveDuration, Is.EqualTo(5));
        }

        [Test]
        public void CorrectCycleRecoveryIsAtomicExactlyOnceAndClamped()
        {
            var run = new SurvivalRunSession(Config(initial: 5, maximum: 10));
            run.Tick(1, false);
            Assert.That(run.PrepareCorrectCycle(1, SpeedGrade.Perfect, out var plan), Is.EqualTo(CorrectCyclePrepareStatus.Prepared));
            Assert.That(run.CommitCorrectCycle(plan), Is.EqualTo(CorrectCycleCommitStatus.Committed));
            Assert.That(run.RemainingTime, Is.EqualTo(10));
            Assert.That(run.CommitCorrectCycle(plan), Is.EqualTo(CorrectCycleCommitStatus.StalePlan));
            Assert.That(run.PrepareCorrectCycle(1, SpeedGrade.Fast, out _), Is.EqualTo(CorrectCyclePrepareStatus.Duplicate));
            Assert.That(run.CommittedCorrectCycles, Is.EqualTo(1));
        }

        [Test]
        public void CorrectCycleAcceptsIncreasingAttemptIdAcrossMissGaps()
        {
            var run = new SurvivalRunSession(Config(initial: 5, maximum: 20));
            Assert.That(run.PrepareCorrectCycle(1, SpeedGrade.Normal, out var first), Is.EqualTo(CorrectCyclePrepareStatus.Prepared));
            Assert.That(run.CommitCorrectCycle(first), Is.EqualTo(CorrectCycleCommitStatus.Committed));

            // Stage attempt 2 may be a miss. The next committed correct answer therefore has ID 3.
            Assert.That(run.PrepareCorrectCycle(3, SpeedGrade.Fast, out var afterMiss), Is.EqualTo(CorrectCyclePrepareStatus.Prepared));
            Assert.That(run.CommitCorrectCycle(afterMiss), Is.EqualTo(CorrectCycleCommitStatus.Committed));
            Assert.That(run.CommittedCorrectCycles, Is.EqualTo(2));
            Assert.That(run.PrepareCorrectCycle(2, SpeedGrade.Perfect, out _), Is.EqualTo(CorrectCyclePrepareStatus.Duplicate));
        }

        [Test]
        public void ExpiryBeforeCommitWinsButCommittedRecoveryBeforeExpirySurvives()
        {
            var expired = new SurvivalRunSession(Config(initial: 1, maximum: 10));
            expired.PrepareCorrectCycle(1, SpeedGrade.Normal, out var stale);
            expired.Tick(1, false);
            Assert.That(expired.CommitCorrectCycle(stale), Is.EqualTo(CorrectCycleCommitStatus.RunEnded));

            var committed = new SurvivalRunSession(Config(initial: 1, maximum: 10));
            committed.PrepareCorrectCycle(1, SpeedGrade.Normal, out var valid);
            Assert.That(committed.CommitCorrectCycle(valid), Is.EqualTo(CorrectCycleCommitStatus.Committed));
            Assert.That(committed.Tick(1, false), Is.False);
            Assert.That(committed.Status, Is.EqualTo(SurvivalRunStatus.Active));
        }

        [Test]
        public void DifficultyAdvancesByCommittedCyclesAndCapsAtLastTargetRange()
        {
            var run = new SurvivalRunSession(Config(initial: 30, maximum: 60, cyclesPerTier: 2));
            Assert.That(run.ProspectiveCorrectTargetRange.Minimum, Is.EqualTo(5));
            run.PrepareCorrectCycle(1, SpeedGrade.Normal, out var first);
            run.CommitCorrectCycle(first);
            Assert.That(run.TargetRange.Minimum, Is.EqualTo(5));
            Assert.That(run.ProspectiveCorrectTargetRange.Minimum, Is.EqualTo(8));
            for (var id = 2; id <= 8; id++)
            {
                run.PrepareCorrectCycle(id, SpeedGrade.Normal, out var plan);
                Assert.That(run.CommitCorrectCycle(plan), Is.EqualTo(CorrectCycleCommitStatus.Committed));
            }
            Assert.That(run.DifficultyTier, Is.EqualTo(2));
            Assert.That(run.TargetRange.Minimum, Is.EqualTo(10));
            Assert.That(run.TargetRange.Maximum, Is.EqualTo(20));
        }

        [Test]
        public void ResultCapturesActiveStatisticsAndHighestTier()
        {
            var run = new SurvivalRunSession(Config(initial: 30, maximum: 60, cyclesPerTier: 1));
            run.PrepareCorrectCycle(1, SpeedGrade.Normal, out var plan);
            run.CommitCorrectCycle(plan);
            run.RecordStatistics(1234, 5);
            run.RecordStatistics(1500, 3);
            run.Tick(2.5, false);
            var result = run.EndExactlyOnce();
            Assert.That(result.Score, Is.EqualTo(1500));
            Assert.That(result.MaximumFeverCombo, Is.EqualTo(5));
            Assert.That(result.ActiveDuration, Is.EqualTo(2.5));
            Assert.That(result.HighestDifficultyTier, Is.EqualTo(1));
        }

        [Test]
        public void TemporaryP03BalanceIsCentralizedAndEveryTierHasAProvenTarget()
        {
            var config = SurvivalRunConfig.TemporaryPrototype;
            Assert.That(config.InitialTime, Is.EqualTo(35));
            Assert.That(config.MaximumTime, Is.EqualTo(45));
            Assert.That(config.DrainPerSecond, Is.EqualTo(1));
            Assert.That(config.NormalRecovery, Is.EqualTo(1.5));
            Assert.That(config.FastRecovery, Is.EqualTo(2.75));
            Assert.That(config.PerfectRecovery, Is.EqualTo(4));
            Assert.That(config.CorrectCyclesPerTier, Is.EqualTo(6));
            Assert.That(config.TargetRanges.Count, Is.EqualTo(5));

            var board = new MathGame.Board.Board(BoardTopology.CreateRectangular(5, 5));
            var nextId = 1;
            foreach (var position in board.EnumerateActivePositions())
                Assert.That(board.TryPlaceBlock(position, new NumberBlock(new BlockId(nextId++), 4)),
                    Is.EqualTo(BoardMutationResult.Succeeded));

            foreach (var range in config.TargetRanges)
            {
                var result = new TargetPathSearcher().Search(board,
                    new TargetSearchConfig(range.Minimum, range.Maximum, 2, 4, 250000));
                Assert.That(result.Status, Is.EqualTo(TargetSearchStatus.Succeeded),
                    $"P03 tier {range.Minimum}-{range.Maximum} must retain a current-board witness.");
                Assert.That(result.Solutions.Count, Is.GreaterThan(0));
            }
        }

        [Test]
        public void ProductionConfigRejectsInvalidTierIdentityAndOrdering()
        {
            var survival = new SurvivalTimeSettings(10, 20, 1);
            var recovery = new TimingRecoverySettings(1, 2, 3);
            Assert.Throws<System.ArgumentException>(() => new TimingRecoverySettings(2, 1, 3));
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new DifficultyTierConfig(1, 0, default));
            Assert.Throws<System.ArgumentException>(() => new SurvivalRunConfig(survival, recovery, new[]
            {
                new DifficultyTierConfig(2, 0, new RunTargetRange(5, 9))
            }));
            Assert.Throws<System.ArgumentException>(() => new SurvivalRunConfig(survival, recovery, new[]
            {
                new DifficultyTierConfig(1, 0, new RunTargetRange(5, 9)),
                new DifficultyTierConfig(2, 0, new RunTargetRange(7, 11))
            }));
        }

        private static SurvivalRunConfig Config(double initial, double maximum, int cyclesPerTier = 5)
            => new SurvivalRunConfig(initial, maximum, 1, 3, 5, 8, cyclesPerTier,
                new[] { new RunTargetRange(5, 10), new RunTargetRange(8, 15), new RunTargetRange(10, 20) });
    }
}
