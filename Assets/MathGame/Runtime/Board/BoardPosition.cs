using System;

namespace MathGame.Board
{
    public readonly struct BoardPosition : IEquatable<BoardPosition>
    {
        public BoardPosition(int column, int row)
        {
            Column = column;
            Row = row;
        }
        public int Column { get; }
        public int Row { get; }
        public bool Equals(BoardPosition other) => Column == other.Column && Row == other.Row;
        public override bool Equals(object obj) => obj is BoardPosition other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                return (Column * 397) ^ Row;
            }
        }
        public override string ToString() => $"({Column}, {Row})";
        public static bool operator ==(BoardPosition left, BoardPosition right) => left.Equals(right);
        public static bool operator !=(BoardPosition left, BoardPosition right) => !left.Equals(right);
    }
}
