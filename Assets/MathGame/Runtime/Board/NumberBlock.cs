using System;

namespace MathGame.Board
{
    public readonly struct NumberBlock : IEquatable<NumberBlock>
    {
        public NumberBlock(BlockId id, int value)
        {
            if (!id.IsValid) throw new ArgumentException("A valid block ID is required.", nameof(id));
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value), "Block values must be positive.");
            Id = id;
            Value = value;
        }
        public BlockId Id { get; }
        public int Value { get; }
        public bool IsValid => Id.IsValid && Value > 0;
        public bool Equals(NumberBlock other) => Id == other.Id && Value == other.Value;
        public override bool Equals(object obj) => obj is NumberBlock other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                return (Id.GetHashCode() * 397) ^ Value;
            }
        }
        public static bool operator ==(NumberBlock left, NumberBlock right) => left.Equals(right);
        public static bool operator !=(NumberBlock left, NumberBlock right) => !left.Equals(right);
    }
}
