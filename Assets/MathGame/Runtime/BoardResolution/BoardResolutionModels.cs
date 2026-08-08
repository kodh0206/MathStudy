using System;
using System.Collections.Generic;
using MathGame.Answer;
using MathGame.Board;
using DomainBoard = MathGame.Board.Board;

namespace MathGame.BoardResolution
{
    public sealed class RefillValueRange
    {
        public RefillValueRange(int minimumValue, int maximumValue)
        {
            MinimumValue = minimumValue;
            MaximumValue = maximumValue;
        }
        public int MinimumValue { get; }
        public int MaximumValue { get; }
        internal bool IsValid => MinimumValue > 0 && MaximumValue >= MinimumValue && MaximumValue < int.MaxValue;
    }

    public sealed class BoardResolutionRequest
    {
        public BoardResolutionRequest(DomainBoard board, AnswerResult answer, RefillValueRange refillValues, int nextBlockIdValue)
        {
            Board = board;
            Answer = answer;
            RefillValues = refillValues;
            NextBlockIdValue = nextBlockIdValue;
        }
        public DomainBoard Board { get; }
        public AnswerResult Answer { get; }
        public RefillValueRange RefillValues { get; }
        public int NextBlockIdValue { get; }
    }

    public enum BoardResolutionFailure
    {
        None, MissingRequest, MissingBoard, MissingAnswer, MissingRefillRange, AnswerNotCorrect, EmptySelection,
        InvalidRefillRange, InvalidNextBlockId, UnsupportedBoardState, DuplicateSelectionPosition,
        DuplicateSelectionBlockId, SelectedPositionMissing, SelectedBlockMismatch, NextBlockIdCollision,
        BlockIdRangeExhausted, FinalBoardMutationRejected
    }

    public readonly struct RemovedBlockDelta
    {
        public RemovedBlockDelta(BoardPosition position, NumberBlock block)
        {
            Position = position;
            Block = block;
        }
        public BoardPosition Position { get; }
        public NumberBlock Block { get; }
    }

    public readonly struct MovedBlockDelta
    {
        public MovedBlockDelta(BoardPosition from, BoardPosition to, NumberBlock block)
        {
            From = from;
            To = to;
            Block = block;
        }
        public BoardPosition From { get; }
        public BoardPosition To { get; }
        public NumberBlock Block { get; }
    }

    public readonly struct SpawnedBlockDelta
    {
        public SpawnedBlockDelta(BoardPosition destination, NumberBlock block)
        {
            Destination = destination;
            Block = block;
        }
        public BoardPosition Destination { get; }
        public NumberBlock Block { get; }
    }

    public sealed class BoardResolutionResult
    {
        private BoardResolutionResult(BoardResolutionFailure failure, DomainBoard board, int nextId,
            RemovedBlockDelta[] removed, MovedBlockDelta[] moved, SpawnedBlockDelta[] spawned)
        {
            Failure = failure;
            Board = board;
            NextBlockIdValue = nextId;
            Removed = Array.AsReadOnly(removed);
            Moved = Array.AsReadOnly(moved);
            Spawned = Array.AsReadOnly(spawned);
        }
        public bool Succeeded => Failure == BoardResolutionFailure.None;
        public BoardResolutionFailure Failure { get; }
        public DomainBoard Board { get; }
        public int NextBlockIdValue { get; }
        public IReadOnlyList<RemovedBlockDelta> Removed { get; }
        public IReadOnlyList<MovedBlockDelta> Moved { get; }
        public IReadOnlyList<SpawnedBlockDelta> Spawned { get; }
        internal static BoardResolutionResult Success(DomainBoard board, int nextId, List<RemovedBlockDelta> removed,
            List<MovedBlockDelta> moved, List<SpawnedBlockDelta> spawned)
            => new BoardResolutionResult(BoardResolutionFailure.None, board, nextId, removed.ToArray(), moved.ToArray(), spawned.ToArray());
        internal static BoardResolutionResult Failed(BoardResolutionFailure failure)
            => new BoardResolutionResult(failure, null, 0, Array.Empty<RemovedBlockDelta>(), Array.Empty<MovedBlockDelta>(), Array.Empty<SpawnedBlockDelta>());
    }
}
