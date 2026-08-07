using MathGame.Core.Diagnostics;
using MathGame.Core.Random;
using MathGame.Core.Time;
using MathGame.Stage;
using UnityEngine;

namespace MathGame.App
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ApplicationLifecycleRelay))]
    public sealed class MathGameBootstrap : MonoBehaviour
    {
        private const string LogCategory = "App";

        private static MathGameBootstrap _instance;

        private ApplicationLifecycleRelay _lifecycleRelay;
        private IGameLogger _logger;

        public StageController StageController { get; private set; }

        public ITimeProvider TimeProvider { get; private set; }

        public IRandomSource RandomSource { get; private set; }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            _logger = new UnityGameLogger();
            TimeProvider = new UnityTimeProvider();
            RandomSource = new SystemRandomSource();
            StageController = new StageController(_logger);

            _lifecycleRelay = GetComponent<ApplicationLifecycleRelay>();
            _lifecycleRelay.ApplicationPauseChanged += HandleApplicationPauseChanged;
            _lifecycleRelay.ApplicationFocusChanged += HandleApplicationFocusChanged;

            _logger.Info(LogCategory, "Bootstrap initialized.");
        }

        private void Start()
        {
            if (StageController == null)
            {
                return;
            }

            StageController.Start();
            StageController.FinishInitialization();
        }

        private void OnDestroy()
        {
            if (_lifecycleRelay != null)
            {
                _lifecycleRelay.ApplicationPauseChanged -= HandleApplicationPauseChanged;
                _lifecycleRelay.ApplicationFocusChanged -= HandleApplicationFocusChanged;
            }

            if (_instance == this)
            {
                StageController?.Exit(StageExitReason.ApplicationShutdown);
                _instance = null;
            }
        }

        private void HandleApplicationPauseChanged(bool isPaused)
        {
            if (isPaused)
            {
                StageController.Pause(PauseReason.ApplicationBackground);
            }
            else
            {
                StageController.Resume(PauseReason.ApplicationBackground);
            }
        }

        private void HandleApplicationFocusChanged(bool hasFocus)
        {
            if (hasFocus)
            {
                StageController.Resume(PauseReason.ApplicationFocusLost);
            }
            else
            {
                StageController.Pause(PauseReason.ApplicationFocusLost);
            }
        }
    }
}
