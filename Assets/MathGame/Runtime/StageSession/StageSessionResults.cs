using System;
using System.Collections.Generic;

namespace MathGame.StageSession
{
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
            NextExpectedAttemptId = new StageAttemptId(session.NextId); Objectives = Array.AsReadOnly(objectives);
            CorrectCount = session.CorrectCount; MissCount = session.MissCount; PerfectCount = session.PerfectCount;
            FastCount = session.FastCount; NormalCount = session.NormalCount; CurrentFastStreak = session.CurrentFastStreak;
            MaximumFastStreak = session.MaximumFastStreak; TotalRemovedNumberBlocks = session.TotalRemoved;
            TotalLongConnections = session.TotalLong; TotalFeverContribution = session.TotalFever;
        }
        public StageDefinitionId DefinitionId { get; } public StageSessionStatus Status { get; } public int InitialMoves { get; }
        public int RemainingMoves { get; } public int SpentMoves { get; } public long Score { get; } public StageAttemptId NextExpectedAttemptId { get; }
        public IReadOnlyList<ObjectiveProgressSnapshot> Objectives { get; } public long CorrectCount { get; } public long MissCount { get; }
        public long PerfectCount { get; } public long FastCount { get; } public long NormalCount { get; } public int CurrentFastStreak { get; }
        public int MaximumFastStreak { get; } public long TotalRemovedNumberBlocks { get; } public long TotalLongConnections { get; } public long TotalFeverContribution { get; }
    }
    public sealed class StageAttemptResult
    {
        internal StageAttemptResult(StageAttemptApplyStatus status, StageSessionSnapshot before, StageSessionSnapshot after, int moveCost, StageRewardBreakdown reward, StageSessionEvent[] events)
        { Status = status; Before = before; After = after; MoveCost = moveCost; Reward = reward; Events = Array.AsReadOnly(events); }
        public StageAttemptApplyStatus Status { get; } public StageSessionSnapshot Before { get; } public StageSessionSnapshot After { get; }
        public int MoveCost { get; } public StageRewardBreakdown Reward { get; } public IReadOnlyList<StageSessionEvent> Events { get; }
    }
}
