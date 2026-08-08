namespace MathGame.Board
{
    public readonly struct BoardCellSnapshot
    {
        public BoardCellSnapshot(BoardPosition position, CellAccess access, NumberBlock? block)
        {
            Position = position;
            Access = access;
            Block = block;
        }
        public BoardPosition Position { get; }
        public CellAccess Access { get; }
        public NumberBlock? Block { get; }
        public bool HasBlock => Block.HasValue;
        public bool IsNormallyAccessible => Access == CellAccess.Open && HasBlock;
    }
}
