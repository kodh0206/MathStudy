using System;

namespace MathGame.Core.Random
{
    public sealed class SystemRandomSource : IRandomSource
    {
        private readonly System.Random _random;

        public SystemRandomSource()
            : this(Environment.TickCount)
        {
        }

        public SystemRandomSource(int seed)
        {
            _random = new System.Random(seed);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            return _random.Next(minInclusive, maxExclusive);
        }

        public float NextFloat()
        {
            return (float)_random.NextDouble();
        }
    }
}
