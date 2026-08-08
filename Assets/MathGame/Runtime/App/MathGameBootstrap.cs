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
        private bool _isStageInitialized;

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

            if (StageController.Start() != TransitionResult.Succeeded)
            {
                _logger.Error(LogCategory, "Stage failed to start initialization.");
                return;
            }

            if (StageController.FinishInitialization() != TransitionResult.Succeeded)
            {
                _logger.Error(LogCategory, "Stage failed to finish initialization.");
                return;
            }

            _isStageInitialized = true;
            ReconcileLifecycleState();
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
            if (!_isStageInitialized)
            {
                return;
            }

            SynchronizePauseReason(PauseReason.ApplicationBackground, isPaused);
        }

        private void HandleApplicationFocusChanged(bool hasFocus)
        {
            if (!_isStageInitialized)
            {
                return;
            }

            SynchronizePauseReason(PauseReason.ApplicationFocusLost, !hasFocus);
        }

        private void ReconcileLifecycleState()
        {
            if (_lifecycleRelay.IsApplicationPaused.HasValue)
            {
                SynchronizePauseReason(
                    PauseReason.ApplicationBackground,
                    _lifecycleRelay.IsApplicationPaused.Value);
            }

            if (_lifecycleRelay.HasApplicationFocus.HasValue)
            {
                SynchronizePauseReason(
                    PauseReason.ApplicationFocusLost,
                    !_lifecycleRelay.HasApplicationFocus.Value);
            }
        }

        private void SynchronizePauseReason(PauseReason reason, bool shouldBePaused)
        {
            bool isPausedForReason = StageController.HasPauseReason(reason);
            if (shouldBePaused && !isPausedForReason)
            {
                StageController.Pause(reason);
            }
            else if (!shouldBePaused && isPausedForReason)
            {
                StageController.Resume(reason);
            }
        }
    }
}
