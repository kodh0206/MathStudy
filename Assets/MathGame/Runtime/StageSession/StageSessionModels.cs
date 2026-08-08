using System;
using System.Collections.Generic;
using System.Linq;
using MathGame.Answer;
using MathGame.BoardResolution;
using MathGame.Restoration.Contracts;

namespace MathGame.StageSession
{
    public readonly struct StageDefinitionId : IEquatable<StageDefinitionId>
    {
        public StageDefinitionId(int value) { if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); Value = value; }
        public int Value { get; } public bool IsValid => Value > 0;
        public bool Equals(StageDefinitionId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is StageDefinitionId other && Equals(other);
        public override int GetHashCode() => Value;
    }
    public readonly struct StageAttemptId : IEquatable<StageAttemptId>
    {
        public StageAttemptId(long value) { if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); Value = value; }
        public long Value { get; } public bool IsValid => Value > 0;
        public bool Equals(StageAttemptId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is StageAttemptId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
    }
    public enum StageObjectiveKind { RemoveNumberBlocks, CompleteTarget, CompleteLongConnection, RemoveObstacle, EarnRestorationEnergy, CreateSpecial, UseSpecial }
    public sealed class StageObjectiveDefinition
    {
        public StageObjectiveDefinition(StageObjectiveKind kind, int requiredCount, TargetNumber target, int minimumConnectionLength)
            : this(kind, requiredCount, target, minimumConnectionLength, null) { }
        public StageObjectiveDefinition(StageObjectiveKind kind, int requiredCount, TargetNumber target, int minimumConnectionLength, MathGame.Board.ObstacleKind? obstacleKind)
        { Kind = kind; RequiredCount = requiredCount; Target = target; MinimumConnectionLength = minimumConnectionLength; ObstacleKind = obstacleKind; }
        public StageObjectiveKind Kind { get; } public int RequiredCount { get; } public TargetNumber Target { get; } public int MinimumConnectionLength { get; } public MathGame.Board.ObstacleKind? ObstacleKind { get; }
    }
    public readonly struct ConnectionLengthScoreRule
    {
        public ConnectionLengthScoreRule(int minimumLength, long bonus) { MinimumLength = minimumLength; Bonus = bonus; }
        public int MinimumLength { get; } public long Bonus { get; }
    }
    public sealed class ScoreRewardConfig
    {
        public ScoreRewardConfig(long baseCorrectScore, long perfectBonus, long fastBonus, long normalBonus, IEnumerable<ConnectionLengthScoreRule> lengthRules)
        { BaseCorrectScore = baseCorrectScore; PerfectBonus = perfectBonus; FastBonus = fastBonus; NormalBonus = normalBonus; LengthRules = lengthRules == null ? null : Array.AsReadOnly(lengthRules.ToArray()); }
        public long BaseCorrectScore { get; } public long PerfectBonus { get; } public long FastBonus { get; } public long NormalBonus { get; }
        public IReadOnlyList<ConnectionLengthScoreRule> LengthRules { get; }
    }
    public sealed class StageDefinition
    {
        public StageDefinition(StageDefinitionId id, int initialMoves, IEnumerable<StageObjectiveDefinition> objectives, ScoreRewardConfig scoreConfig)
            : this(id, initialMoves, objectives, scoreConfig, null) { }
        public StageDefinition(StageDefinitionId id, int initialMoves, IEnumerable<StageObjectiveDefinition> objectives, ScoreRewardConfig scoreConfig, StageRestorationConfig restorationConfig)
        { Id = id; InitialMoves = initialMoves; Objectives = objectives == null ? null : Array.AsReadOnly(objectives.ToArray()); ScoreConfig = scoreConfig; RestorationConfig = restorationConfig; }
        public StageDefinitionId Id { get; } public int InitialMoves { get; } public IReadOnlyList<StageObjectiveDefinition> Objectives { get; } public ScoreRewardConfig ScoreConfig { get; }
        public StageRestorationConfig RestorationConfig { get; }
    }
    public sealed class StageAttemptCommand
    {
        public StageAttemptCommand(StageAttemptId id, AnswerResult answer, BoardResolutionResult resolution)
            : this(id, answer, resolution, StageAttemptRules.Normal) { }
        public StageAttemptCommand(StageAttemptId id, AnswerResult answer, BoardResolutionResult resolution, StageAttemptRules rules)
        { Id = id; Answer = answer; Resolution = resolution; Rules = rules ?? throw new ArgumentNullException(nameof(rules)); }
        public StageAttemptCommand(StageAttemptId id, AnswerResult answer, ObstacleResolutionResult resolution, StageAttemptRules rules)
            : this(id, answer, resolution, rules, null) { }
        public StageAttemptCommand(StageAttemptId id, AnswerResult answer, ObstacleResolutionResult resolution, StageAttemptRules rules, RestorationAwardEvidence restoration)
        { Id = id; Answer = answer; ObstacleResolution = resolution; Rules = rules ?? throw new ArgumentNullException(nameof(rules)); Restoration = restoration; }
        public StageAttemptId Id { get; } public AnswerResult Answer { get; } public BoardResolutionResult Resolution { get; }
        public ObstacleResolutionResult ObstacleResolution { get; }
        public StageAttemptRules Rules { get; }
        public RestorationAwardEvidence Restoration { get; }
    }
    public enum StageAttemptMode { Normal, Fever }
    public sealed class StageAttemptRules
    {
        private StageAttemptRules(StageAttemptMode mode, int correctMoveCost, int scoreMultiplier)
        { Mode = mode; CorrectMoveCost = correctMoveCost; ScoreMultiplier = scoreMultiplier; }
        public StageAttemptMode Mode { get; }
        public int CorrectMoveCost { get; }
        public int ScoreMultiplier { get; }
        public static StageAttemptRules Normal { get; } = new StageAttemptRules(StageAttemptMode.Normal, 1, 1);
        public static StageAttemptRules CreateFever(int comboMultiplier)
        {
            if (comboMultiplier != 1 && comboMultiplier != 2 && comboMultiplier != 3 && comboMultiplier != 5)
                throw new ArgumentOutOfRangeException(nameof(comboMultiplier));
            return new StageAttemptRules(StageAttemptMode.Fever, 0, comboMultiplier);
        }
    }
    public enum StageSessionCreateStatus { MissingDefinition, InvalidDefinitionId, InvalidMoves, MissingObjectives, InvalidObjectiveCount, MissingObjective, UnsupportedObjective, InvalidObjective, DuplicateObjective, MissingScoreConfig, InvalidScoreConfig, Succeeded }
    public enum StageSessionStatus { Active, Success, Failure, FailedPendingDecision }
    public enum StageAttemptApplyStatus { AppliedContinue, AppliedMiss, AppliedSuccess, AppliedFailure, MissingCommand, SessionAlreadyTerminal, InvalidAttempt, DuplicateAttempt, OutOfOrderAttempt, InvalidAnswer, UnexpectedResolution, AnswerResolutionMismatch, NoMovesRemaining, ArithmeticOverflow, MissingRestorationEvidence, UnexpectedRestorationEvidence, RestorationSourceMismatch, InvalidRestorationAward, PreparationRequired }
    public enum ConnectionLengthRewardTier { None, StandardRemoval, ExtraFeverRequested, BasicSpecialRequested, EnhancedAreaSpecialRequested }
    public enum StageSessionEventKind { AnswerAccepted, MissRecorded, ScoreAwarded, ObjectiveProgressed, MoveConsumed, StageSucceeded, StageFailed }
    public readonly struct StageSessionEvent
    {
        public StageSessionEvent(StageSessionEventKind kind, int objectiveIndex, long amount) { Kind = kind; ObjectiveIndex = objectiveIndex; Amount = amount; }
        public StageSessionEventKind Kind { get; } public int ObjectiveIndex { get; } public long Amount { get; }
    }
}
