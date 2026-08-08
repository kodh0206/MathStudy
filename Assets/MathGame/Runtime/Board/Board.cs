using System.Collections.Generic;

namespace MathGame.Board
{
    public sealed class Board
    {
        private struct CellState
        {
            public NumberBlock? Block;
            public CellRole Role;
            public DustState? Dust;
            public BoxState? Box;
        }
        private readonly CellState[] cells;
        private readonly Dictionary<BlockId, BoardPosition> blockPositions = new Dictionary<BlockId, BoardPosition>();
        public Board(BoardTopology topology)
        {
            Topology = topology ?? throw new System.ArgumentNullException(nameof(topology));
            Layout = BoardLayout.CreateAllNumberSlots(topology);
            cells = new CellState[checked(topology.Width * topology.Height)];
        }
        public Board(BoardLayout layout)
        {
            Layout = layout ?? throw new System.ArgumentNullException(nameof(layout));
            Topology = layout.Topology;
            cells = new CellState[checked(Topology.Width * Topology.Height)];
            foreach (var position in Topology.EnumerateActivePositions()) cells[Index(position)].Role = layout.GetRole(position);
        }
        public BoardTopology Topology { get; }
        public BoardLayout Layout { get; }
        public int BlockCount => blockPositions.Count;
        public bool IsWithinBounds(BoardPosition position) => Topology.IsWithinBounds(position);
        public bool IsActive(BoardPosition position) => Topology.IsActive(position);
        public IEnumerable<BoardPosition> EnumerateActivePositions() => Topology.EnumerateActivePositions();
        public IEnumerable<BoardPosition> EnumerateOrthogonalNeighbors(BoardPosition position) => Topology.EnumerateOrthogonalNeighbors(position);
        public CellLookupResult TryGetCell(BoardPosition position, out BoardCellSnapshot cell)
        {
            cell = default;
            if (!IsWithinBounds(position)) return CellLookupResult.OutOfBounds;
            if (!IsActive(position)) return CellLookupResult.InactivePosition;
            var state = cells[Index(position)];
            cell = new BoardCellSnapshot(position, state.Role, state.Block, state.Dust, state.Box);
            return CellLookupResult.Succeeded;
        }
        public bool TryFindBlock(BlockId id, out BoardPosition position)
        {
            position = default;
            return id.IsValid && blockPositions.TryGetValue(id, out position);
        }
        public BoardMutationResult TryPlaceBlock(BoardPosition position, NumberBlock block)
        {
            if (!block.IsValid) return BoardMutationResult.InvalidBlock;
            var location = ValidatePosition(position);
            if (location != BoardMutationResult.Succeeded) return location;
            var state = cells[Index(position)];
            if (state.Role == CellRole.BoxSlot) return BoardMutationResult.Blocked;
            if (state.Block.HasValue) return BoardMutationResult.Occupied;
            if (blockPositions.ContainsKey(block.Id)) return BoardMutationResult.DuplicateBlockId;
            state.Block = block;
            cells[Index(position)] = state;
            blockPositions.Add(block.Id, position);
            return BoardMutationResult.Succeeded;
        }
        public BoardMutationResult TryRemoveBlock(BoardPosition position, out NumberBlock removed)
        {
            removed = default;
            var location = ValidatePosition(position);
            if (location != BoardMutationResult.Succeeded) return location;
            var state = cells[Index(position)];
            if (state.Role == CellRole.BoxSlot) return BoardMutationResult.Blocked;
            if (!state.Block.HasValue) return BoardMutationResult.Empty;
            removed = state.Block.Value;
            state.Block = null;
            cells[Index(position)] = state;
            blockPositions.Remove(removed.Id);
            return BoardMutationResult.Succeeded;
        }
        public BoardMutationResult TryRelocateBlock(BoardPosition source, BoardPosition destination)
        {
            var sourceResult = ValidatePosition(source);
            if (sourceResult != BoardMutationResult.Succeeded) return sourceResult;
            var destinationResult = ValidatePosition(destination);
            if (destinationResult != BoardMutationResult.Succeeded) return destinationResult;
            if (source == destination) return BoardMutationResult.SourceEqualsDestination;
            var sourceState = cells[Index(source)];
            var destinationState = cells[Index(destination)];
            if (sourceState.Role == CellRole.BoxSlot || destinationState.Role == CellRole.BoxSlot) return BoardMutationResult.Blocked;
            if (!sourceState.Block.HasValue) return BoardMutationResult.Empty;
            if (destinationState.Block.HasValue) return BoardMutationResult.Occupied;
            var block = sourceState.Block.Value;
            sourceState.Block = null;
            destinationState.Block = block;
            cells[Index(source)] = sourceState;
            cells[Index(destination)] = destinationState;
            blockPositions[block.Id] = destination;
            return BoardMutationResult.Succeeded;
        }
        public BoardMutationResult TryPlaceDust(BoardPosition position, DustState dust)
        {
            var result = ValidatePosition(position); if (result != BoardMutationResult.Succeeded) return result;
            var state = cells[Index(position)]; if (state.Role != CellRole.NumberSlot) return BoardMutationResult.Blocked;
            if (!dust.IsValid) return BoardMutationResult.InvalidBlock; if (state.Dust.HasValue) return BoardMutationResult.Occupied;
            if (ContainsObstacle(dust.Id)) return BoardMutationResult.DuplicateBlockId; state.Dust = dust; cells[Index(position)] = state; return BoardMutationResult.Succeeded;
        }
        public BoardMutationResult TryRemoveDust(BoardPosition position, out DustState dust)
        { dust = default; var r = ValidatePosition(position); if (r != BoardMutationResult.Succeeded) return r; var s = cells[Index(position)]; if (!s.Dust.HasValue) return BoardMutationResult.Empty; dust = s.Dust.Value; s.Dust = null; cells[Index(position)] = s; return BoardMutationResult.Succeeded; }
        public BoardMutationResult TryPlaceBox(BoardPosition position, BoxState box)
        { var r = ValidatePosition(position); if (r != BoardMutationResult.Succeeded) return r; var s = cells[Index(position)]; if (s.Role != CellRole.BoxSlot || s.Block.HasValue) return BoardMutationResult.Blocked; if (s.Box.HasValue) return BoardMutationResult.Occupied; if (ContainsObstacle(box.Id)) return BoardMutationResult.DuplicateBlockId; s.Box = box; cells[Index(position)] = s; return BoardMutationResult.Succeeded; }
        public BoardMutationResult TryUpdateBox(BoardPosition position, BoxState box)
        { var r = ValidatePosition(position); if (r != BoardMutationResult.Succeeded) return r; var s = cells[Index(position)]; if (!s.Box.HasValue || s.Box.Value.Id != box.Id) return BoardMutationResult.Empty; s.Box = box; cells[Index(position)] = s; return BoardMutationResult.Succeeded; }
        public BoardMutationResult TryRemoveBox(BoardPosition position, out BoxState box)
        { box = default; var r = ValidatePosition(position); if (r != BoardMutationResult.Succeeded) return r; var s = cells[Index(position)]; if (!s.Box.HasValue) return BoardMutationResult.Empty; box = s.Box.Value; s.Box = null; cells[Index(position)] = s; return BoardMutationResult.Succeeded; }
        public BoardStabilityResult ValidateStable()
        {
            var obstacleIds = new HashSet<ObstacleId>();
            foreach (var p in EnumerateActivePositions()) { var s = cells[Index(p)]; if (s.Role == CellRole.NumberSlot) { if (!s.Block.HasValue) return new BoardStabilityResult(BoardStabilityStatus.EmptyNumberSlot, p); if (s.Dust.HasValue && !obstacleIds.Add(s.Dust.Value.Id)) return new BoardStabilityResult(BoardStabilityStatus.DuplicateObstacleId, p); } else { if (!s.Box.HasValue) return new BoardStabilityResult(BoardStabilityStatus.MissingBox, p); if (!obstacleIds.Add(s.Box.Value.Id)) return new BoardStabilityResult(BoardStabilityStatus.DuplicateObstacleId, p); } }
            return new BoardStabilityResult(BoardStabilityStatus.Stable, null);
        }
        private bool ContainsObstacle(ObstacleId id) { foreach (var p in EnumerateActivePositions()) { var s = cells[Index(p)]; if ((s.Dust.HasValue && s.Dust.Value.Id == id) || (s.Box.HasValue && s.Box.Value.Id == id)) return true; } return false; }
        private int Index(BoardPosition position) => position.Row * Topology.Width + position.Column;
        private BoardMutationResult ValidatePosition(BoardPosition position)
        {
            if (!IsWithinBounds(position)) return BoardMutationResult.OutOfBounds;
            return IsActive(position) ? BoardMutationResult.Succeeded : BoardMutationResult.InactivePosition;
        }
    }
}
