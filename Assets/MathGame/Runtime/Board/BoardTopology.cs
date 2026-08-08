using System;
using System.Collections.Generic;

namespace MathGame.Board
{
    public sealed class BoardTopology
    {
        private readonly bool[] active;
        private BoardTopology(int width, int height, bool[] active)
        {
            Width = width;
            Height = height;
            this.active = active;
        }
        public int Width { get; }
        public int Height { get; }

        public static BoardTopology CreateRectangular(int width, int height)
        {
            ValidateDimensions(width, height);
            var mask = new bool[checked(width * height)];
            for (var i = 0; i < mask.Length; i++)
            {
                mask[i] = true;
            }
            return new BoardTopology(width, height, mask);
        }

        public static BoardTopology CreateMasked(int width, int height, IEnumerable<BoardPosition> activePositions)
        {
            ValidateDimensions(width, height);
            if (activePositions == null) throw new ArgumentNullException(nameof(activePositions));
            var mask = new bool[checked(width * height)];
            var count = 0;
            foreach (var position in activePositions)
            {
                if (position.Column < 0 || position.Column >= width || position.Row < 0 || position.Row >= height)
                    throw new ArgumentOutOfRangeException(nameof(activePositions), $"Active position {position} is outside the topology.");
                var index = position.Row * width + position.Column;
                if (mask[index]) throw new ArgumentException($"Active position {position} is duplicated.", nameof(activePositions));
                mask[index] = true;
                count++;
            }
            if (count == 0) throw new ArgumentException("At least one active position is required.", nameof(activePositions));
            return new BoardTopology(width, height, mask);
        }

        public bool IsWithinBounds(BoardPosition position) => position.Column >= 0 && position.Column < Width && position.Row >= 0 && position.Row < Height;
        public bool IsActive(BoardPosition position) => IsWithinBounds(position) && active[position.Row * Width + position.Column];
        public IEnumerable<BoardPosition> EnumerateActivePositions()
        {
            for (var row = 0; row < Height; row++)
            {
                for (var column = 0; column < Width; column++)
                {
                    var position = new BoardPosition(column, row);
                    if (IsActive(position))
                    {
                        yield return position;
                    }
                }
            }
        }
        public IEnumerable<BoardPosition> EnumerateOrthogonalNeighbors(BoardPosition position)
        {
            if (!IsActive(position)) yield break;
            var candidates = new[] { new BoardPosition(position.Column, position.Row + 1), new BoardPosition(position.Column + 1, position.Row), new BoardPosition(position.Column, position.Row - 1), new BoardPosition(position.Column - 1, position.Row) };
            foreach (var candidate in candidates) if (IsActive(candidate)) yield return candidate;
        }
        private static void ValidateDimensions(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            checked
            {
                _ = width * height;
            }
        }
    }
}
