using UnityEngine;

namespace MathGame.Core.Diagnostics
{
    public sealed class UnityGameLogger : IGameLogger
    {
        public void Info(string category, string message)
        {
            Debug.Log(Format(category, message));
        }

        public void Warning(string category, string message)
        {
            Debug.LogWarning(Format(category, message));
        }

        public void Error(string category, string message)
        {
            Debug.LogError(Format(category, message));
        }

        private static string Format(string category, string message)
        {
            return $"[MathGame][{category}] {message}";
        }
    }
}
