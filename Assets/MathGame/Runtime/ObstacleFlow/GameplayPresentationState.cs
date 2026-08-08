using System;
using System.Collections.Generic;
using MathGame.Board;
using MathGame.Restoration.Contracts;
using DomainBoard = MathGame.Board.Board;

namespace MathGame.ObstacleFlow
{
    public enum GameplayStateSource
    {
        Initial,
        Answer,
        SystemEffect,
        TargetRecovery,
        Continue,
        Retry,
        Terminal
    }

    public readonly struct GameplayStateToken : IEquatable<GameplayStateToken>
    {
        public GameplayStateToken(StageRunId runId, long revision, GameplayStateSource source, long sourceId)
        {
            if (!runId.IsValid) throw new ArgumentException("A valid run is required.", nameof(runId));
            if (revision <= 0) throw new ArgumentOutOfRangeException(nameof(revision));
            if (sourceId < 0) throw new ArgumentOutOfRangeException(nameof(sourceId));
            RunId = runId;
            Revision = revision;
            Source = source;
            SourceId = sourceId;
        }

        public StageRunId RunId { get; }
        public long Revision { get; }
        public GameplayStateSource Source { get; }
        public long SourceId { get; }
        public bool IsValid => RunId.IsValid && Revision > 0;

        public bool Equals(GameplayStateToken other) =>
            RunId.Equals(other.RunId) && Revision == other.Revision && Source == other.Source && SourceId == other.SourceId;
        public override bool Equals(object obj) => obj is GameplayStateToken other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(RunId, Revision, (int)Source, SourceId);
        public static bool operator ==(GameplayStateToken left, GameplayStateToken right) => left.Equals(right);
        public static bool operator !=(GameplayStateToken left, GameplayStateToken right) => !left.Equals(right);
    }

    public sealed class GameplayStateSnapshot
    {
        public GameplayStateSnapshot(GameplayStateToken token, DomainBoard board, int nextBlockId)
        {
            if (!token.IsValid) throw new ArgumentException("A valid token is required.", nameof(token));
            if (board == null) throw new ArgumentNullException(nameof(board));
            if (nextBlockId <= 0) throw new ArgumentOutOfRangeException(nameof(nextBlockId));
            Token = token;
            Board = Clone(board);
            NextBlockId = nextBlockId;
        }

        public GameplayStateToken Token { get; }
        public DomainBoard Board { get; }
        public int NextBlockId { get; }

        static DomainBoard Clone(DomainBoard source)
        {
            var boxes = new List<BoardPosition>();
            foreach (var position in source.EnumerateActivePositions())
            { source.TryGetCell(position, out var cell); if (cell.HasBox) boxes.Add(position); }
            var copy = new DomainBoard(BoardLayout.Create(source.Topology, boxes));
            foreach (var position in source.EnumerateActivePositions())
            {
                source.TryGetCell(position, out var cell);
                if (cell.HasBox) copy.TryPlaceBox(position, cell.Box.Value);
                else { copy.TryPlaceBlock(position, cell.Block.Value); if (cell.HasDust) copy.TryPlaceDust(position, cell.Dust.Value); }
            }
            return copy;
        }
    }
}
