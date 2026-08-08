using System;
using System.Collections.Generic;

namespace MathGame.Board
{
    public enum CellRole { NumberSlot, BoxSlot }
    public enum ObstacleKind { Dust, Box }

    public readonly struct ObstacleId : IEquatable<ObstacleId>
    {
        public ObstacleId(long value) { if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value)); Value = value; }
        public long Value { get; }
        public bool IsValid => Value > 0;
        public bool Equals(ObstacleId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ObstacleId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public static bool operator ==(ObstacleId left, ObstacleId right) => left.Equals(right);
        public static bool operator !=(ObstacleId left, ObstacleId right) => !left.Equals(right);
    }

    public readonly struct DustState
    {
        public DustState(ObstacleId id) { if (!id.IsValid) throw new ArgumentException(nameof(id)); Id = id; }
        public ObstacleId Id { get; }
        public int CurrentHitPoints => 1;
        public bool IsValid => Id.IsValid;
    }

    public readonly struct BoxState
    {
        public BoxState(ObstacleId id, int currentHitPoints = 2)
        {
            if (!id.IsValid) throw new ArgumentException(nameof(id));
            if (currentHitPoints < 1 || currentHitPoints > 2) throw new ArgumentOutOfRangeException(nameof(currentHitPoints));
            Id = id; CurrentHitPoints = currentHitPoints;
        }
        public ObstacleId Id { get; }
        public int CurrentHitPoints { get; }
        public int MaximumHitPoints => 2;
        public bool IsValid => Id.IsValid && CurrentHitPoints >= 1 && CurrentHitPoints <= 2;
    }

    public sealed class BoardLayout
    {
        private readonly HashSet<BoardPosition> boxSlots;
        private BoardLayout(BoardTopology topology, IEnumerable<BoardPosition> boxes)
        { Topology = topology ?? throw new ArgumentNullException(nameof(topology)); boxSlots = new HashSet<BoardPosition>(boxes ?? Array.Empty<BoardPosition>()); }
        public BoardTopology Topology { get; }
        public static BoardLayout CreateAllNumberSlots(BoardTopology topology) => new BoardLayout(topology, null);
        public static BoardLayout Create(BoardTopology topology, IEnumerable<BoardPosition> boxSlots)
        {
            if (topology == null) throw new ArgumentNullException(nameof(topology));
            var copy = new HashSet<BoardPosition>();
            foreach (var position in boxSlots ?? throw new ArgumentNullException(nameof(boxSlots)))
                if (!topology.IsActive(position) || !copy.Add(position)) throw new ArgumentException("Invalid Box slot.", nameof(boxSlots));
            return new BoardLayout(topology, copy);
        }
        public CellRole GetRole(BoardPosition position)
        { if (!Topology.IsActive(position)) throw new ArgumentOutOfRangeException(nameof(position)); return boxSlots.Contains(position) ? CellRole.BoxSlot : CellRole.NumberSlot; }
        public IEnumerable<BoardPosition> EnumerateNumberSlots() { foreach (var p in Topology.EnumerateActivePositions()) if (!boxSlots.Contains(p)) yield return p; }
        public IEnumerable<BoardPosition> EnumerateBoxSlots() { foreach (var p in Topology.EnumerateActivePositions()) if (boxSlots.Contains(p)) yield return p; }
    }

    public enum BoardStabilityStatus { Stable, EmptyNumberSlot, MissingBox, NumberInBoxSlot, DustInBoxSlot, BoxInNumberSlot, DustWithoutNumber, InvalidNumber, InvalidObstacle, DuplicateBlockId, DuplicateObstacleId }
    public readonly struct BoardStabilityResult
    {
        public BoardStabilityResult(BoardStabilityStatus status, BoardPosition? position) { Status = status; Position = position; }
        public BoardStabilityStatus Status { get; }
        public BoardPosition? Position { get; }
        public bool IsStable => Status == BoardStabilityStatus.Stable;
    }
}
