using UnityEngine;

namespace MathGame.Core.Time
{
    public sealed class UnityTimeProvider : ITimeProvider
    {
        public double RealtimeSeconds => UnityEngine.Time.realtimeSinceStartupAsDouble;
    }
}
