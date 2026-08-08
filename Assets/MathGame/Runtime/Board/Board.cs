using System.Collections.Generic;

namespace MathGame.Board
{
    public sealed class Board
    {
        private struct CellState
        {
            public CellAccess Access;
            public NumberBlock? Block;
        }
        private readonly CellState[] cells;
        private readonly Dictionary<BlockId, BoardPosition> blockPositions = new Dictionary<BlockId, BoardPosition>();
        public Board(BoardTopology topology)
        {
            Topology = topology ?? throw new System.ArgumentNullException(nameof(topology));
            cells = new CellState[checked(topology.Width * topology.Height)];
        }
        public BoardTopology Topology { get; }
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
            cell = new BoardCellSnapshot(position, state.Access, state.Block);
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
            if (state.Access == CellAccess.Blocked) return BoardMutationResult.Blocked;
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
            if (state.Access == CellAccess.Blocked) return BoardMutationResult.Blocked;
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
            if (sourceState.Access == CellAccess.Blocked || destinationState.Access == CellAccess.Blocked) return BoardMutationResult.Blocked;
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
        public BoardMutationResult TrySetAccess(BoardPosition position, CellAccess access)
        {
            if (access != CellAccess.Open && access != CellAccess.Blocked) return BoardMutationResult.InvalidAccess;
            var location = ValidatePosition(position);
            if (location != BoardMutationResult.Succeeded) return location;
            var state = cells[Index(position)];
            if (state.Access == access) return BoardMutationResult.AlreadyInRequestedState;
            state.Access = access;
            cells[Index(position)] = state;
            return BoardMutationResult.Succeeded;
        }
        private int Index(BoardPosition position) => position.Row * Topology.Width + position.Column;
        private BoardMutationResult ValidatePosition(BoardPosition position)
        {
            if (!IsWithinBounds(position)) return BoardMutationResult.OutOfBounds;
            return IsActive(position) ? BoardMutationResult.Succeeded : BoardMutationResult.InactivePosition;
        }
    }
}
