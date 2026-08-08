using System;
using System.Collections.Generic;
using MathGame.Core.Diagnostics;

namespace MathGame.Stage
{
    public sealed class StageController
    {
        private const string LogCategory = "Stage";

        private readonly HashSet<PauseReason> _activePauseReasons = new();
        private readonly IGameLogger _logger;
        private StageState? _stateBeforePause;

        public StageController(IGameLogger logger = null)
        {
            _logger = logger;
        }

        public event Action<StageTransition> StateChanged;

        public StageState State { get; private set; } = StageState.None;

        public StageState? StateBeforePause => _stateBeforePause;

        public bool IsTerminal => State is StageState.Success or StageState.Failure or StageState.Exited;

        public bool AcceptsPlayerInput => State is StageState.PlayerInput or StageState.FeverInput;

        public int ActivePauseReasonCount => _activePauseReasons.Count;

        public bool HasPauseReason(PauseReason reason)
        {
            return _activePauseReasons.Contains(reason);
        }

        public TransitionResult Start()
        {
            if (State == StageState.Exited)
            {
                return TransitionResult.StageAlreadyTerminated;
            }

            if (State != StageState.None)
            {
                return State == StageState.Initializing
                    ? TransitionResult.AlreadyInRequestedState
                    : InvalidTransition(StageState.Initializing);
            }

            return TransitionTo(StageState.Initializing, StageTransitionCause.StartRequested);
        }

        public TransitionResult FinishInitialization()
        {
            if (State == StageState.Exited)
            {
                return TransitionResult.StageAlreadyTerminated;
            }

            if (State != StageState.Initializing)
            {
                return InvalidTransition(StageState.Ready);
            }

            return TransitionTo(StageState.Ready, StageTransitionCause.InitializationCompleted);
        }

        public TransitionResult BeginTargetPresentation()
        {
            if (State == StageState.Exited || State is StageState.Success or StageState.Failure)
            {
                return TransitionResult.StageAlreadyTerminated;
            }

            if (State is not (StageState.Ready or StageState.ResolvingAnswer or StageState.RecoveringBoard))
            {
                return InvalidTransition(StageState.PresentingTarget);
            }

            return TransitionTo(StageState.PresentingTarget, StageTransitionCause.TargetPresentationBegan);
        }

        public TransitionResult EnablePlayerInput()
        {
            if (State == StageState.Exited || State is StageState.Success or StageState.Failure)
            {
                return TransitionResult.StageAlreadyTerminated;
            }

            if (State != StageState.PresentingTarget)
            {
                return InvalidTransition(StageState.PlayerInput);
            }

            return TransitionTo(StageState.PlayerInput, StageTransitionCause.PlayerInputEnabled);
        }

        public TransitionResult BeginAnswerResolution()
        {
            if (State == StageState.Exited || State is StageState.Success or StageState.Failure)
            {
                return TransitionResult.StageAlreadyTerminated;
            }

            if (State != StageState.PlayerInput)
            {
                return InvalidTransition(StageState.ResolvingAnswer);
            }

            return TransitionTo(StageState.ResolvingAnswer, StageTransitionCause.AnswerResolutionBegan);
        }

        public TransitionResult FinishMissResolution()
        {
            if (State == StageState.Exited || State is StageState.Success or StageState.Failure)
            {
                return TransitionResult.StageAlreadyTerminated;
            }

            if (State != StageState.ResolvingAnswer)
            {
                return InvalidTransition(StageState.PlayerInput);
            }

            return TransitionTo(StageState.PlayerInput, StageTransitionCause.MissResolutionFinished);
        }

        public TransitionResult BeginDeadlockRecovery()
        {
            if (State == StageState.Exited || State is StageState.Success or StageState.Failure)
                return TransitionResult.StageAlreadyTerminated;
            if (State != StageState.PlayerInput)
                return InvalidTransition(StageState.RecoveringBoard);
            return TransitionTo(StageState.RecoveringBoard, StageTransitionCause.DeadlockRecoveryBegan);
        }

        public TransitionResult Pause(PauseReason reason)
        {
            if (State == StageState.Exited || State is StageState.Success or StageState.Failure)
            {
                return TransitionResult.StageAlreadyTerminated;
            }

            if (!_activePauseReasons.Add(reason))
            {
                return TransitionResult.AlreadyInRequestedState;
            }

            if (State == StageState.Paused)
            {
                return TransitionResult.Succeeded;
            }

            if (!CanPause(State))
            {
                _activePauseReasons.Remove(reason);
                return InvalidTransition(StageState.Paused);
            }

            _stateBeforePause = State;
            return TransitionTo(StageState.Paused, StageTransitionCause.PauseRequested);
        }

        public TransitionResult Resume(PauseReason reason)
        {
            if (State == StageState.Exited || State is StageState.Success or StageState.Failure)
            {
                return TransitionResult.StageAlreadyTerminated;
            }

            if (!_activePauseReasons.Remove(reason))
            {
                return TransitionResult.BlockedByPauseReason;
            }

            if (_activePauseReasons.Count > 0)
            {
                return TransitionResult.BlockedByPauseReason;
            }

            if (State != StageState.Paused || !_stateBeforePause.HasValue)
            {
                return InvalidTransition(State);
            }

            StageState resumedState = _stateBeforePause.Value;
            _stateBeforePause = null;
            return TransitionTo(resumedState, StageTransitionCause.AllPauseReasonsCleared);
        }

        public TransitionResult Complete()
        {
            return Finish(StageState.Success, StageTransitionCause.StageCompleted);
        }

        public TransitionResult Fail()
        {
            return Finish(StageState.Failure, StageTransitionCause.StageFailed);
        }

        public TransitionResult Exit(StageExitReason reason)
        {
            if (State == StageState.Exited)
            {
                return TransitionResult.StageAlreadyTerminated;
            }

            _activePauseReasons.Clear();
            _stateBeforePause = null;
            _logger?.Info(LogCategory, $"Exit requested: {reason}.");
            return TransitionTo(StageState.Exited, StageTransitionCause.ExitRequested);
        }

        private TransitionResult Finish(StageState target, StageTransitionCause cause)
        {
            if (State == StageState.Exited || State is StageState.Success or StageState.Failure)
            {
                return TransitionResult.StageAlreadyTerminated;
            }

            if (!CanFinish(State))
            {
                return InvalidTransition(target);
            }

            _activePauseReasons.Clear();
            _stateBeforePause = null;
            return TransitionTo(target, cause);
        }

        private TransitionResult TransitionTo(StageState target, StageTransitionCause cause)
        {
            StageState previous = State;
            State = target;

            var transition = new StageTransition(previous, target, cause);
            _logger?.Info(LogCategory, $"{previous} -> {target} ({cause})");
            StateChanged?.Invoke(transition);

            return TransitionResult.Succeeded;
        }

        private TransitionResult InvalidTransition(StageState target)
        {
            _logger?.Warning(LogCategory, $"Invalid transition: {State} -> {target}");
            return TransitionResult.InvalidFromCurrentState;
        }

        private static bool CanPause(StageState state)
        {
            return state is StageState.Initializing
                or StageState.Ready
                or StageState.PresentingTarget
                or StageState.PlayerInput
                or StageState.ResolvingAnswer
                or StageState.RecoveringBoard
                or StageState.EnteringFever
                or StageState.FeverInput
                or StageState.EndingFever;
        }

        private static bool CanFinish(StageState state)
        {
            return state is StageState.PresentingTarget
                or StageState.PlayerInput
                or StageState.ResolvingAnswer
                or StageState.EnteringFever
                or StageState.FeverInput
                or StageState.EndingFever;
        }
    }
}
