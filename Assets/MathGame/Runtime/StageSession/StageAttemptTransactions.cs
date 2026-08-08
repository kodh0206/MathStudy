using System;
using MathGame.Restoration.Contracts;

namespace MathGame.StageSession
{
    public enum StageAttemptPrepareStatus
    {
        PreparedContinue, PreparedMiss, PreparedSuccess, PreparedFailedPendingDecision,
        Rejected, MissingRestorationEvidence, UnexpectedRestorationEvidence,
        RestorationSourceMismatch, InvalidRestorationAward, ArithmeticOverflow
    }
    public enum StageAttemptBindStatus { Bound, MissingAttemptPlan, AttemptPlanNotSuccessful, MissingWorldPlan, WorldPlanMismatch, StalePlan }
    public enum StageAttemptCommitStatus { CommittedContinue, CommittedMiss, CommittedSuccess, CommittedFailedPendingDecision, MissingPlan, WorldPlanRequired, StalePlan }

    public sealed class StageAttemptPlan
    {
        internal StageAttemptPlan(StageSession owner, long version, StageSession prospective, StageAttemptResult result, IWorldCommitPlan worldPlan)
        { Owner = owner; PreparedVersion = version; Prospective = prospective; Result = result; WorldPlan = worldPlan; }
        internal StageSession Owner { get; }
        internal StageSession Prospective { get; }
        internal IWorldCommitPlan WorldPlan { get; }
        public long PreparedVersion { get; }
        public StageAttemptResult Result { get; }
        public StageSessionSnapshot ProspectiveAfter => Result.After;
        public bool WouldSucceed => Result.Status == StageAttemptApplyStatus.AppliedSuccess;
        public bool IsWorldBound => WorldPlan != null;
    }

    public sealed class StageAttemptPrepareResult
    {
        internal StageAttemptPrepareResult(StageAttemptPrepareStatus status, StageAttemptPlan plan, StageAttemptResult rejection)
        { Status = status; Plan = plan; Rejection = rejection; }
        public StageAttemptPrepareStatus Status { get; }
        public StageAttemptPlan Plan { get; }
        public StageAttemptResult Rejection { get; }
        public StageSessionSnapshot ProspectiveAfter => Plan?.ProspectiveAfter ?? Rejection?.After;
    }

    public sealed class StageAttemptBindResult
    {
        internal StageAttemptBindResult(StageAttemptBindStatus status, StageAttemptPlan plan) { Status = status; Plan = plan; }
        public StageAttemptBindStatus Status { get; }
        public StageAttemptPlan Plan { get; }
    }

    public sealed class StageAttemptCommitResult
    {
        internal StageAttemptCommitResult(StageAttemptCommitStatus status, StageAttemptResult result) { Status = status; Result = result; }
        public StageAttemptCommitStatus Status { get; }
        public StageAttemptResult Result { get; }
    }
}
