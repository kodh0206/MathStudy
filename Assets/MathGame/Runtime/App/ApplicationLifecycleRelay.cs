using System;
using UnityEngine;

namespace MathGame.App
{
    public sealed class ApplicationLifecycleRelay : MonoBehaviour
    {
        public bool? IsApplicationPaused { get; private set; }

        public bool? HasApplicationFocus { get; private set; }

        public event Action<bool> ApplicationPauseChanged;

        public event Action<bool> ApplicationFocusChanged;

        private void OnApplicationPause(bool pauseStatus)
        {
            IsApplicationPaused = pauseStatus;
            ApplicationPauseChanged?.Invoke(pauseStatus);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            HasApplicationFocus = hasFocus;
            ApplicationFocusChanged?.Invoke(hasFocus);
        }
    }
}
