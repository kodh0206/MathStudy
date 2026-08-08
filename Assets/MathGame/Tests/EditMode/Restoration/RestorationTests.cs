using MathGame.Restoration;
using MathGame.Restoration.Contracts;
using MathGame.StageSession;
using NUnit.Framework;

namespace MathGame.Tests.Restoration
{
    public sealed class RestorationTests
    {
        [TestCase(1, StageAttemptMode.Normal, 10)]
        [TestCase(2, StageAttemptMode.Normal, 10)]
        [TestCase(3, StageAttemptMode.Normal, 12)]
        [TestCase(4, StageAttemptMode.Normal, 15)]
        [TestCase(5, StageAttemptMode.Normal, 20)]
        [TestCase(3, StageAttemptMode.Fever, 24)]
        [TestCase(4, StageAttemptMode.Fever, 30)]
        [TestCase(5, StageAttemptMode.Fever, 40)]
        public void Calculator_UsesApprovedExactFactors(int length, StageAttemptMode mode, long expected)
        {
            Assert.That(RestorationAwardCalculator.ForAnswer(length, mode), Is.EqualTo(expected));
        }

        [Test]
        public void WorldCommit_IsAdditiveClampedAndIdempotent()
        {
            var world = new WorldRestorationProgress(new WorldRestorationId(1), 100, 20);
            var prepared = world.Prepare(new WorldRestorationId(1), new WorldCommitId(new StageRunId(7)), 70);
            Assert.That(prepared.Status, Is.EqualTo(WorldRestorationCommitStatus.Committed));
            Assert.That(prepared.Plan.CrossedMilestones, Is.EqualTo(new[] { WorldRestorationMilestone.Quarter, WorldRestorationMilestone.Half, WorldRestorationMilestone.ThreeQuarters }));
            var committed = world.Commit(prepared.Plan);
            Assert.That(committed.After.Current, Is.EqualTo(90));
            var duplicate = world.Prepare(new WorldRestorationId(1), new WorldCommitId(new StageRunId(7)), 70);
            Assert.That(duplicate.Status, Is.EqualTo(WorldRestorationCommitStatus.AlreadyCommitted));
            Assert.That(duplicate.After.Current, Is.EqualTo(90));
        }

        [Test]
        public void WorldCommit_CrossesAllMilestonesInAscendingOrderAndDiscardsExcess()
        {
            var world = new WorldRestorationProgress(new WorldRestorationId(2), 80);
            var prepared = world.Prepare(new WorldRestorationId(2), new WorldCommitId(new StageRunId(8)), 100);
            Assert.That(prepared.Plan.CrossedMilestones, Is.EqualTo(new[] { WorldRestorationMilestone.Quarter, WorldRestorationMilestone.Half, WorldRestorationMilestone.ThreeQuarters, WorldRestorationMilestone.Complete }));
            Assert.That(prepared.Plan.AppliedAmount, Is.EqualTo(80));
            Assert.That(prepared.Plan.DiscardedExcess, Is.EqualTo(20));
        }

        [Test]
        public void StaleWorldPlan_DoesNotMutate()
        {
            var world = new WorldRestorationProgress(new WorldRestorationId(3), 100);
            var first = world.Prepare(new WorldRestorationId(3), new WorldCommitId(new StageRunId(9)), 10).Plan;
            var stale = world.Prepare(new WorldRestorationId(3), new WorldCommitId(new StageRunId(10)), 10).Plan;
            world.Commit(first);
            var rejected = world.Commit(stale);
            Assert.That(rejected.Status, Is.EqualTo(WorldRestorationCommitStatus.StalePlan));
            Assert.That(world.Snapshot.Current, Is.EqualTo(10));
        }

        [Test]
        public void Milestones_HandleLongCapacityWithoutMultiplicationOverflow()
        {
            var capacity = long.MaxValue - 7;
            var world = new WorldRestorationProgress(new WorldRestorationId(4), capacity, capacity / 2);
            Assert.That(world.Snapshot.ReachedMilestones, Does.Contain(WorldRestorationMilestone.Quarter));
            Assert.That(world.Snapshot.ReachedMilestones, Does.Not.Contain(WorldRestorationMilestone.ThreeQuarters));
        }
    }
}
