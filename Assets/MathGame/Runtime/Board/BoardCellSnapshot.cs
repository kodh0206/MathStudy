namespace MathGame.Board
{
    public readonly struct BoardCellSnapshot
    {
        public BoardCellSnapshot(BoardPosition position, CellAccess access, NumberBlock? block)
            : this(position, access == CellAccess.Blocked ? CellRole.BoxSlot : CellRole.NumberSlot, block, null, null) { }
        internal BoardCellSnapshot(BoardPosition position, CellRole role, NumberBlock? block, DustState? dust, BoxState? box)
        {
            Position = position;
            Role = role;
            Block = block;
            Dust = dust;
            Box = box;
        }
        public BoardPosition Position { get; }
        public CellRole Role { get; }
        public CellAccess Access => Role == CellRole.BoxSlot ? CellAccess.Blocked : CellAccess.Open;
        public NumberBlock? Block { get; }
        public DustState? Dust { get; }
        public BoxState? Box { get; }
        public bool HasBlock => Block.HasValue;
        public bool HasDust => Dust.HasValue;
        public bool HasBox => Box.HasValue;
        public bool IsSelectable => Role == CellRole.NumberSlot && Access == CellAccess.Open && HasBlock;
        public bool IsRemovableNumber => IsSelectable;
        public bool IsMovableNumber => IsSelectable;
        public bool IsRefillable => Role == CellRole.NumberSlot;
        public bool IsGravityBarrier => Role == CellRole.BoxSlot;
        public bool IsNormallyAccessible => IsSelectable;
    }
}
