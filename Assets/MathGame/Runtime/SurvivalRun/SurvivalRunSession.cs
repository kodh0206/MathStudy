using System;
using MathGame.Answer;

namespace MathGame.SurvivalRun
{
    public enum SurvivalRunStatus { Active = 0, Ended = 1 }
    public enum CorrectCyclePrepareStatus { Prepared = 0, MissingOrInvalidId = 1, InvalidGrade = 2, RunEnded = 3, Duplicate = 4, OutOfOrder = 5 }
    public enum CorrectCycleCommitStatus { Committed = 0, MissingPlan = 1, StalePlan = 2, RunEnded = 3 }

    public sealed class RunResult
    {
        internal RunResult(string runId, double activeDuration, long score, int maximumFeverCombo, int highestDifficultyTier)
        { RunId = runId; ActiveDuration = activeDuration; Score = score; MaximumFeverCombo = maximumFeverCombo; HighestDifficultyTier = highestDifficultyTier; }
        public string RunId { get; }
        public double ActiveDuration { get; }
        public long Score { get; }
        public int MaximumFeverCombo { get; }
        public int HighestDifficultyTier { get; }
    }

    public sealed class CorrectCyclePlan
    {
        internal CorrectCyclePlan(SurvivalRunSession owner, long version, long cycleId, double recovery)
        { Owner = owner; Version = version; CycleId = cycleId; Recovery = recovery; }
        internal SurvivalRunSession Owner { get; }
        internal long Version { get; }
        public long CycleId { get; }
        public double Recovery { get; }
    }

    public sealed class SurvivalRunSession
    {
        private readonly SurvivalRunConfig config;
        private readonly string runId;
        private long version;
        private long lastCommittedCycleSourceId;
        private long score;
        private int maximumFeverCombo;
        private RunResult result;

        public SurvivalRunSession(SurvivalRunConfig config) : this(config, Guid.NewGuid().ToString("N")) { }

        public SurvivalRunSession(SurvivalRunConfig config, string runId)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            if (string.IsNullOrWhiteSpace(runId)) throw new ArgumentException("A run identity is required.", nameof(runId));
            this.runId = runId;
            RemainingTime = config.InitialTime;
        }

        public SurvivalRunStatus Status { get; private set; }
        public double RemainingTime { get; private set; }
        public double ActiveDuration { get; private set; }
        public long CommittedCorrectCycles { get; private set; }
        public int DifficultyTier => config.TierIndexFor(CommittedCorrectCycles);
        public RunTargetRange TargetRange => config.DifficultyTiers[DifficultyTier].TargetRange;
        public RunTargetRange ProspectiveCorrectTargetRange => config.DifficultyTiers[config.TierIndexFor(
            CommittedCorrectCycles == long.MaxValue ? long.MaxValue : CommittedCorrectCycles + 1)].TargetRange;
        public RunResult Result => result;

        // The caller supplies elapsed wall time only while the application is live. Paused/interrupted
        // samples are explicitly ignored, while every non-paused gameplay phase drains uniformly.
        public bool Tick(double elapsedSeconds, bool isPaused)
        {
            if (Status == SurvivalRunStatus.Ended || isPaused || elapsedSeconds == 0) return false;
            if (elapsedSeconds < 0 || double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds))
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            var activeSample = Math.Min(elapsedSeconds, RemainingTime / config.DrainPerSecond);
            ActiveDuration = checked(ActiveDuration + activeSample);
            RemainingTime = Math.Max(0, RemainingTime - elapsedSeconds * config.DrainPerSecond);
            version++;
            if (RemainingTime > 0) return false;
            EndExactlyOnce();
            return true;
        }

        public CorrectCyclePrepareStatus PrepareCorrectCycle(long cycleId, SpeedGrade grade, out CorrectCyclePlan plan)
        {
            plan = null;
            if (Status == SurvivalRunStatus.Ended) return CorrectCyclePrepareStatus.RunEnded;
            if (cycleId <= 0) return CorrectCyclePrepareStatus.MissingOrInvalidId;
            if (grade is not (SpeedGrade.Normal or SpeedGrade.Fast or SpeedGrade.Perfect)) return CorrectCyclePrepareStatus.InvalidGrade;
            if (cycleId <= lastCommittedCycleSourceId) return CorrectCyclePrepareStatus.Duplicate;
            plan = new CorrectCyclePlan(this, version, cycleId, config.RecoveryFor(grade));
            return CorrectCyclePrepareStatus.Prepared;
        }

        // Commit this only after the correlated StageSession answer transaction has committed.
        public CorrectCycleCommitStatus CommitCorrectCycle(CorrectCyclePlan plan)
        {
            if (plan == null) return CorrectCycleCommitStatus.MissingPlan;
            if (Status == SurvivalRunStatus.Ended) return CorrectCycleCommitStatus.RunEnded;
            if (!ReferenceEquals(plan.Owner, this) || plan.Version != version || plan.CycleId <= lastCommittedCycleSourceId)
                return CorrectCycleCommitStatus.StalePlan;
            RemainingTime = Math.Min(config.MaximumTime, RemainingTime + plan.Recovery);
            CommittedCorrectCycles = checked(CommittedCorrectCycles + 1);
            lastCommittedCycleSourceId = plan.CycleId;
            version++;
            return CorrectCycleCommitStatus.Committed;
        }

        public void RecordStatistics(long currentScore, int currentFeverCombo)
        {
            if (currentScore < 0 || currentFeverCombo < 0) throw new ArgumentOutOfRangeException();
            if (Status == SurvivalRunStatus.Ended) return;
            score = Math.Max(score, currentScore);
            maximumFeverCombo = Math.Max(maximumFeverCombo, currentFeverCombo);
        }

        public RunResult EndExactlyOnce()
        {
            if (result != null) return result;
            Status = SurvivalRunStatus.Ended;
            RemainingTime = 0;
            version++;
            result = new RunResult(runId, ActiveDuration, score, maximumFeverCombo, DifficultyTier);
            return result;
        }

    }
}
