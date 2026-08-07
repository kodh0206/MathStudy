using System;
using UnityEngine;

namespace MathGame.App
{
    public sealed class ApplicationLifecycleRelay : MonoBehaviour
    {
        public event Action<bool> ApplicationPauseChanged;

        public event Action<bool> ApplicationFocusChanged;

        private void OnApplicationPause(bool pauseStatus)
        {
            ApplicationPauseChanged?.Invoke(pauseStatus);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            ApplicationFocusChanged?.Invoke(hasFocus);
        }
    }
}
