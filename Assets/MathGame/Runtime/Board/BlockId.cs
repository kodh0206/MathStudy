using System;

namespace MathGame.Board
{
    public readonly struct BlockId : IEquatable<BlockId>
    {
        public BlockId(int value)
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value), "Block IDs must be positive.");
            Value = value;
        }
        public int Value { get; }
        public bool IsValid => Value > 0;
        public bool Equals(BlockId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is BlockId other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();
        public static bool operator ==(BlockId left, BlockId right) => left.Equals(right);
        public static bool operator !=(BlockId left, BlockId right) => !left.Equals(right);
    }
}
