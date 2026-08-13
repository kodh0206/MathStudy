using System;
using UnityEngine;

namespace MathGame.App
{
    public sealed class ApplicationLifecycleRelay : MonoBehaviour
    {
#if UNITY_EDITOR
        [SerializeField]
        [Tooltip("Enable only when explicitly testing mobile-style focus pausing in the Editor.")]
        private bool pauseOnEditorFocusLoss;
#endif

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
#if UNITY_EDITOR
            // Selecting the Console, Inspector, or another desktop window causes the
            // Unity Game View to lose focus. That should not suspend routine prototype
            // testing unless lifecycle-pause behavior is being tested deliberately.
            if (!pauseOnEditorFocusLoss)
            {
                HasApplicationFocus = true;
                ApplicationFocusChanged?.Invoke(true);
                return;
            }
#endif
            HasApplicationFocus = hasFocus;
            ApplicationFocusChanged?.Invoke(hasFocus);
        }
    }
}
