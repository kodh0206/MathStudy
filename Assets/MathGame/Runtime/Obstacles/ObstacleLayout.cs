using System;
using System.Collections.Generic;
using System.Linq;
using MathGame.Board;
using DomainBoard = MathGame.Board.Board;

namespace MathGame.Obstacles
{
    public sealed class ObstacleLayoutEntry
    {
        private ObstacleLayoutEntry(BoardPosition position, ObstacleId id, ObstacleKind kind) { Position = position; Id = id; Kind = kind; }
        public BoardPosition Position { get; }
        public ObstacleId Id { get; }
        public ObstacleKind Kind { get; }
        public static ObstacleLayoutEntry Dust(BoardPosition position, ObstacleId id) => new ObstacleLayoutEntry(position, id, ObstacleKind.Dust);
        public static ObstacleLayoutEntry Box(BoardPosition position, ObstacleId id) => new ObstacleLayoutEntry(position, id, ObstacleKind.Box);
    }

    public sealed class ObstacleLayout
    {
        public ObstacleLayout(IEnumerable<ObstacleLayoutEntry> entries) { Entries = entries == null ? null : Array.AsReadOnly(entries.ToArray()); }
        public IReadOnlyList<ObstacleLayoutEntry> Entries { get; }
    }

    public enum ObstacleBoardBuildStatus { MissingSource, MissingLayout, MissingEntry, InvalidObstacleId, DuplicateObstacleId, DuplicatePosition, OutOfBounds, InactivePosition, UnsupportedKind, IncompatibleSource, FinalMutationRejected, Succeeded }
    public sealed class ObstacleBoardBuildResult
    {
        internal ObstacleBoardBuildResult(ObstacleBoardBuildStatus status, DomainBoard board, IEnumerable<NumberBlock> discarded)
        { Status = status; Board = board; DiscardedSetupBlocks = Array.AsReadOnly(discarded.ToArray()); }
        public ObstacleBoardBuildStatus Status { get; }
        public bool Succeeded => Status == ObstacleBoardBuildStatus.Succeeded;
        public DomainBoard Board { get; }
        public IReadOnlyList<NumberBlock> DiscardedSetupBlocks { get; }
    }

    public sealed class ObstacleBoardBuilder
    {
        public ObstacleBoardBuildResult Build(DomainBoard source, ObstacleLayout layout)
        {
            if (source == null) return Fail(ObstacleBoardBuildStatus.MissingSource);
            if (layout == null || layout.Entries == null) return Fail(ObstacleBoardBuildStatus.MissingLayout);
            var ids = new HashSet<ObstacleId>(); var positions = new HashSet<BoardPosition>(); var boxes = new HashSet<BoardPosition>();
            foreach (var entry in layout.Entries)
            {
                if (entry == null) return Fail(ObstacleBoardBuildStatus.MissingEntry);
                if (!entry.Id.IsValid) return Fail(ObstacleBoardBuildStatus.InvalidObstacleId);
                if (!ids.Add(entry.Id)) return Fail(ObstacleBoardBuildStatus.DuplicateObstacleId);
                if (!positions.Add(entry.Position)) return Fail(ObstacleBoardBuildStatus.DuplicatePosition);
                if (!source.IsWithinBounds(entry.Position)) return Fail(ObstacleBoardBuildStatus.OutOfBounds);
                if (!source.IsActive(entry.Position)) return Fail(ObstacleBoardBuildStatus.InactivePosition);
                if (!Enum.IsDefined(typeof(ObstacleKind), entry.Kind)) return Fail(ObstacleBoardBuildStatus.UnsupportedKind);
                if (entry.Kind == ObstacleKind.Box) boxes.Add(entry.Position);
            }
            foreach (var p in source.EnumerateActivePositions()) { source.TryGetCell(p, out var c); if (!c.Block.HasValue || c.Role != CellRole.NumberSlot) return Fail(ObstacleBoardBuildStatus.IncompatibleSource); }
            var result = new DomainBoard(BoardLayout.Create(source.Topology, boxes)); var discarded = new List<NumberBlock>();
            foreach (var p in source.EnumerateActivePositions())
            {
                source.TryGetCell(p, out var c);
                var entry = layout.Entries.FirstOrDefault(e => e.Position == p);
                if (entry != null && entry.Kind == ObstacleKind.Box) { discarded.Add(c.Block.Value); if (result.TryPlaceBox(p, new BoxState(entry.Id)) != BoardMutationResult.Succeeded) return Fail(ObstacleBoardBuildStatus.FinalMutationRejected); }
                else { if (result.TryPlaceBlock(p, c.Block.Value) != BoardMutationResult.Succeeded) return Fail(ObstacleBoardBuildStatus.FinalMutationRejected); if (entry != null && result.TryPlaceDust(p, new DustState(entry.Id)) != BoardMutationResult.Succeeded) return Fail(ObstacleBoardBuildStatus.FinalMutationRejected); }
            }
            return new ObstacleBoardBuildResult(ObstacleBoardBuildStatus.Succeeded, result, discarded);
        }
        private static ObstacleBoardBuildResult Fail(ObstacleBoardBuildStatus status) => new ObstacleBoardBuildResult(status, null, Array.Empty<NumberBlock>());
    }
}
