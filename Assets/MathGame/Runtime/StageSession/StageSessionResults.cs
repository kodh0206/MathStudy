using System;
using System.Collections.Generic;

namespace MathGame.StageSession
{
    using MathGame.Restoration.Contracts;
    public static class ConnectionLengthRewardClassifier
    {
        public static ConnectionLengthRewardTier Classify(int length) => length < 2 ? ConnectionLengthRewardTier.None : length == 2 ? ConnectionLengthRewardTier.StandardRemoval : length == 3 ? ConnectionLengthRewardTier.ExtraFeverRequested : length == 4 ? ConnectionLengthRewardTier.BasicSpecialRequested : ConnectionLengthRewardTier.EnhancedAreaSpecialRequested;
    }
    public readonly struct StageRewardBreakdown
    {
        public StageRewardBreakdown(int grade, int length, int streak, long score, ConnectionLengthRewardTier tier)
        { GradeFeverContribution = grade; LengthFeverContribution = length; FastStreakFeverContribution = streak; TotalFeverContribution = checked(grade + length + streak); ScoreAwarded = score; LengthRewardTier = tier; }
        public int GradeFeverContribution { get; } public int LengthFeverContribution { get; } public int FastStreakFeverContribution { get; }
        public int TotalFeverContribution { get; } public long ScoreAwarded { get; } public ConnectionLengthRewardTier LengthRewardTier { get; }
        public static StageRewardBreakdown None => default;
    }
    public sealed class ObjectiveProgressSnapshot
    {
        internal ObjectiveProgressSnapshot(int index, StageObjectiveDefinition definition, long current)
        { Index = index; Definition = definition; Current = current; Required = definition.RequiredCount; }
        public int Index { get; } public StageObjectiveDefinition Definition { get; } public long Current { get; } public long Required { get; }
        public long Remaining => Math.Max(0, Required - Current); public bool IsComplete => Current >= Required;
    }
    public sealed class StageSessionSnapshot
    {
        internal StageSessionSnapshot(StageSession session, ObjectiveProgressSnapshot[] objectives)
        {
            DefinitionId = session.Definition.Id; Status = session.Status; InitialMoves = session.Definition.InitialMoves;
            RemainingMoves = session.RemainingMoves; SpentMoves = session.SpentMoves; Score = session.Score;
            NextExpectedAttemptId = new StageAttemptId(session.NextId); NextExpectedSystemEffectId = new MathGame.BoardResolution.BoardSystemEffectId(session.NextEffectId); Objectives = Array.AsReadOnly(objectives);
            CorrectCount = session.CorrectCount; MissCount = session.MissCount; PerfectCount = session.PerfectCount;
            FastCount = session.FastCount; NormalCount = session.NormalCount; CurrentFastStreak = session.CurrentFastStreak;
            MaximumFastStreak = session.MaximumFastStreak; TotalRemovedNumberBlocks = session.TotalRemoved;
            TotalDestroyedDust = session.TotalDestroyedDust; TotalDestroyedBoxes = session.TotalDestroyedBoxes;
            TotalLongConnections = session.TotalLong; TotalFeverContribution = session.TotalFever;
            StageRunId = session.RunId; RestorationLifecycle = session.RestorationLifecycle;
            StageRestorationCapacity = session.Definition.RestorationConfig?.StageCapacity ?? 0;
            RestorationWorldId = session.Definition.RestorationConfig?.WorldId ?? default;
            ProvisionalRestoration = session.ProvisionalRestoration; GrossRestorationEarned = session.GrossRestoration;
            DiscardedRestorationExcess = session.DiscardedRestoration;
            ContinueUsed = session.ContinueUsed;
        }
        public StageDefinitionId DefinitionId { get; } public StageSessionStatus Status { get; } public int InitialMoves { get; }
        public int RemainingMoves { get; } public int SpentMoves { get; } public long Score { get; } public StageAttemptId NextExpectedAttemptId { get; }
        public MathGame.BoardResolution.BoardSystemEffectId NextExpectedSystemEffectId { get; }
        public IReadOnlyList<ObjectiveProgressSnapshot> Objectives { get; } public long CorrectCount { get; } public long MissCount { get; }
        public long PerfectCount { get; } public long FastCount { get; } public long NormalCount { get; } public int CurrentFastStreak { get; }
        public int MaximumFastStreak { get; } public long TotalRemovedNumberBlocks { get; } public long TotalLongConnections { get; } public long TotalFeverContribution { get; }
        public long TotalDestroyedDust { get; } public long TotalDestroyedBoxes { get; }
        public StageRunId StageRunId { get; }
        public RestorationLifecycle RestorationLifecycle { get; }
        public long StageRestorationCapacity { get; }
        public WorldRestorationId RestorationWorldId { get; }
        public long ProvisionalRestoration { get; }
        public long GrossRestorationEarned { get; }
        public long DiscardedRestorationExcess { get; }
        public bool ContinueUsed { get; }
    }
    public sealed class StageAttemptResult
    {
        internal StageAttemptResult(StageAttemptApplyStatus status, StageSessionSnapshot before, StageSessionSnapshot after, int moveCost, StageRewardBreakdown reward, StageSessionEvent[] events, StageAttemptId attemptId = default, StageAttemptMode mode = StageAttemptMode.Normal, int scoreMultiplier = 1)
        { Status = status; Before = before; After = after; MoveCost = moveCost; Reward = reward; Events = Array.AsReadOnly(events); AttemptId = attemptId; Mode = mode; ScoreMultiplier = scoreMultiplier; }
        public StageAttemptApplyStatus Status { get; } public StageSessionSnapshot Before { get; } public StageSessionSnapshot After { get; }
        public int MoveCost { get; } public StageRewardBreakdown Reward { get; } public IReadOnlyList<StageSessionEvent> Events { get; }
        public StageAttemptId AttemptId { get; } public StageAttemptMode Mode { get; } public int ScoreMultiplier { get; }
    }
}
