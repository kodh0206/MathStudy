using System;
using System.Collections.Generic;
using MathGame.Answer;
using MathGame.Board;
using DomainBoard = MathGame.Board.Board;
namespace MathGame.BoardResolution
{
    public readonly struct BoardSystemEffectId { public BoardSystemEffectId(long value) { if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); Value = value; } public long Value { get; } public bool IsValid => Value > 0; }
    public enum ObstacleResolutionMode { NormalAnswer, FeverAnswer, FeverEnd }
    public enum FeverEndPattern { None, RandomThree, Small, Center, Large }
    public enum RemovedNumberCause { Selected, FeverExpanded, FeverEndRandom, FeverEndSmall, FeverEndCenter, FeverEndLarge }
    public enum RemovalOrigin { Normal, Fever }
    public enum ObstacleDamageOrigin { Normal, Fever }
    public enum ObstacleResolutionFailure { None, MissingRequest, MissingBoard, MissingAnswer, MissingRefillRange, InvalidMode, InvalidSystemEffectId, MissingCenter, UnexpectedCenter, InvalidRefillRange, InvalidNextBlockId, InvalidLayeredBoard, AnswerNotCorrect, EmptySelection, DuplicateSelection, SelectedPositionMissing, SelectedBlockMismatch, InvalidCenter, NextBlockIdCollision, BlockIdRangeExhausted, FinalBoardMutationRejected }
    public sealed class ObstacleResolutionRequest
    {
        private ObstacleResolutionRequest(ObstacleResolutionMode mode, DomainBoard board, AnswerResult answer, FeverEndPattern pattern, BoardSystemEffectId effectId, BoardPosition? center, RefillValueRange range, int nextId) { Mode = mode; Board = board; Answer = answer; Pattern = pattern; SystemEffectId = effectId; Center = center; RefillValues = range; NextBlockIdValue = nextId; }
        public ObstacleResolutionMode Mode { get; } public DomainBoard Board { get; } public AnswerResult Answer { get; } public FeverEndPattern Pattern { get; } public BoardSystemEffectId SystemEffectId { get; } public BoardPosition? Center { get; } public RefillValueRange RefillValues { get; } public int NextBlockIdValue { get; }
        public static ObstacleResolutionRequest NormalAnswer(DomainBoard board, AnswerResult answer, RefillValueRange range, int nextId) => new ObstacleResolutionRequest(ObstacleResolutionMode.NormalAnswer, board, answer, default, default, null, range, nextId);
        public static ObstacleResolutionRequest FeverAnswer(DomainBoard board, AnswerResult answer, RefillValueRange range, int nextId) => new ObstacleResolutionRequest(ObstacleResolutionMode.FeverAnswer, board, answer, default, default, null, range, nextId);
        public static ObstacleResolutionRequest FeverEnd(DomainBoard board, FeverEndPattern pattern, BoardSystemEffectId effectId, BoardPosition? center, RefillValueRange range, int nextId) => new ObstacleResolutionRequest(ObstacleResolutionMode.FeverEnd, board, null, pattern, effectId, center, range, nextId);
    }
    public readonly struct RemovedNumberDelta { public RemovedNumberDelta(BoardPosition position, NumberBlock block, RemovedNumberCause cause, RemovalOrigin origin) { Position = position; Block = block; Cause = cause; Origin = origin; } public BoardPosition Position { get; } public NumberBlock Block { get; } public RemovedNumberCause Cause { get; } public RemovalOrigin Origin { get; } }
    public readonly struct ObstacleDamageDelta { public ObstacleDamageDelta(ObstacleId id, ObstacleKind kind, BoardPosition position, int before, int potency, int applied, int after, ObstacleDamageOrigin origin) { Id = id; Kind = kind; Position = position; HitPointsBefore = before; Potency = potency; DamageApplied = applied; HitPointsAfter = after; Origin = origin; } public ObstacleId Id { get; } public ObstacleKind Kind { get; } public BoardPosition Position { get; } public int HitPointsBefore { get; } public int Potency { get; } public int DamageApplied { get; } public int HitPointsAfter { get; } public ObstacleDamageOrigin Origin { get; } public bool WasDestroyed => HitPointsAfter == 0; }
    public readonly struct ObstacleDestroyedEvidence { public ObstacleDestroyedEvidence(ObstacleId id, ObstacleKind kind, BoardPosition position, ObstacleDamageOrigin origin) { Id = id; Kind = kind; Position = position; Origin = origin; } public ObstacleId Id { get; } public ObstacleKind Kind { get; } public BoardPosition Position { get; } public ObstacleDamageOrigin Origin { get; } }
    public sealed class ObstacleResolutionResult
    {
        internal ObstacleResolutionResult(ObstacleResolutionFailure failure, DomainBoard board, ObstacleResolutionMode mode, BoardSystemEffectId effectId, int nextId, List<RemovedNumberDelta> selected, List<RemovedNumberDelta> collateral, List<MovedBlockDelta> moved, List<SpawnedBlockDelta> spawned, List<ObstacleDamageDelta> damage, List<ObstacleDestroyedEvidence> destroyed) { Failure = failure; Board = board; Mode = mode; SystemEffectId = effectId; NextBlockIdValue = nextId; SelectedRemoved = Array.AsReadOnly(selected.ToArray()); CollateralRemoved = Array.AsReadOnly(collateral.ToArray()); var all = new List<RemovedNumberDelta>(selected); all.AddRange(collateral); Removed = Array.AsReadOnly(all.ToArray()); Moved = Array.AsReadOnly(moved.ToArray()); Spawned = Array.AsReadOnly(spawned.ToArray()); ObstacleDamage = Array.AsReadOnly(damage.ToArray()); DestroyedObstacles = Array.AsReadOnly(destroyed.ToArray()); }
        public bool Succeeded => Failure == ObstacleResolutionFailure.None; public ObstacleResolutionFailure Failure { get; } public DomainBoard Board { get; } public ObstacleResolutionMode Mode { get; } public BoardSystemEffectId SystemEffectId { get; } public int NextBlockIdValue { get; }
        public IReadOnlyList<RemovedNumberDelta> SelectedRemoved { get; } public IReadOnlyList<RemovedNumberDelta> CollateralRemoved { get; } public IReadOnlyList<RemovedNumberDelta> Removed { get; } public IReadOnlyList<MovedBlockDelta> Moved { get; } public IReadOnlyList<SpawnedBlockDelta> Spawned { get; } public IReadOnlyList<ObstacleDamageDelta> ObstacleDamage { get; } public IReadOnlyList<ObstacleDestroyedEvidence> DestroyedObstacles { get; }
        internal static ObstacleResolutionResult Fail(ObstacleResolutionFailure f) => new ObstacleResolutionResult(f, null, default, default, 0, new List<RemovedNumberDelta>(), new List<RemovedNumberDelta>(), new List<MovedBlockDelta>(), new List<SpawnedBlockDelta>(), new List<ObstacleDamageDelta>(), new List<ObstacleDestroyedEvidence>());
    }
}
