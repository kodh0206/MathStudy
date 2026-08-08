using System;
using MathGame.StageSession;

namespace MathGame.Fever
{
    public sealed class FeverChargeTracker
    {
        public FeverChargeTracker(int maximumGauge)
        { if (maximumGauge <= 0) throw new ArgumentOutOfRangeException(nameof(maximumGauge)); MaximumGauge = maximumGauge; }
        public int MaximumGauge { get; } public int Gauge { get; private set; } public bool IsFull => Gauge == MaximumGauge;
        public StageAttemptId LastConsumedNormalAttemptId { get; private set; }
        internal bool Charging { get; set; } = true;
        public FeverChargeApplyResult ApplyNormalAttempt(StageAttemptResult result)
        {
            if (result == null) return FeverChargeApplyResult.MissingResult;
            if (!Charging) return FeverChargeApplyResult.NotCharging;
            if (result.Status is not (StageAttemptApplyStatus.AppliedContinue or StageAttemptApplyStatus.AppliedMiss or StageAttemptApplyStatus.AppliedSuccess or StageAttemptApplyStatus.AppliedFailure)) return FeverChargeApplyResult.NotApplied;
            if (result.Mode != StageAttemptMode.Normal) return FeverChargeApplyResult.WrongMode;
            if (!result.AttemptId.IsValid || result.AttemptId.Value <= LastConsumedNormalAttemptId.Value) return FeverChargeApplyResult.StaleOrDuplicateAttempt;
            LastConsumedNormalAttemptId = result.AttemptId;
            if (result.Status == StageAttemptApplyStatus.AppliedMiss) return FeverChargeApplyResult.AppliedMiss;
            Gauge = (int)Math.Min(MaximumGauge, (long)Gauge + result.Reward.TotalFeverContribution);
            return IsFull ? FeverChargeApplyResult.ReachedMaximum : FeverChargeApplyResult.Applied;
        }
        internal void ResetGauge() { Gauge = 0; Charging = true; }
    }
}
