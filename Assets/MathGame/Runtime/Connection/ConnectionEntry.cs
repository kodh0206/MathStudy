using MathGame.Board;

namespace MathGame.Connection
{
    public readonly struct ConnectionEntry
    {
        public ConnectionEntry(BoardPosition position, NumberBlock block)
        {
            Position = position;
            Block = block;
        }

        public BoardPosition Position { get; }
        public NumberBlock Block { get; }
    }
}
