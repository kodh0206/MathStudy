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
        public AnswerResolutionOrigin ResolutionOrigin { get; private set; }

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

            ResolutionOrigin = AnswerResolutionOrigin.None;
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

            if (State is not (StageState.PlayerInput or StageState.FeverInput))
            {
                return InvalidTransition(StageState.ResolvingAnswer);
            }

            ResolutionOrigin = State == StageState.FeverInput ? AnswerResolutionOrigin.Fever : AnswerResolutionOrigin.Normal;
            return TransitionTo(StageState.ResolvingAnswer, StageTransitionCause.AnswerResolutionBegan);
        }

        public TransitionResult FinishMissResolution()
        {
            if (State == StageState.Exited || State is StageState.Success or StageState.Failure)
            {
                return TransitionResult.StageAlreadyTerminated;
            }

            if (State != StageState.ResolvingAnswer || ResolutionOrigin != AnswerResolutionOrigin.Normal)
            {
                return InvalidTransition(StageState.PlayerInput);
            }

            ResolutionOrigin = AnswerResolutionOrigin.None;
            return TransitionTo(StageState.PlayerInput, StageTransitionCause.MissResolutionFinished);
        }

        public TransitionResult BeginFeverEntry() => GuardedTransition(StageState.PresentingTarget, StageState.EnteringFever, StageTransitionCause.FeverEntryBegan);
        public TransitionResult CompleteFeverEntry() => GuardedTransition(StageState.EnteringFever, StageState.FeverInput, StageTransitionCause.FeverEntryCompleted);
        public TransitionResult EnableFeverInput() => GuardedTransition(StageState.PresentingTarget, StageState.FeverInput, StageTransitionCause.FeverInputEnabled);

        public TransitionResult FinishFeverMissResolution()
        {
            if (IsTerminal) return TransitionResult.StageAlreadyTerminated;
            if (State != StageState.ResolvingAnswer || ResolutionOrigin != AnswerResolutionOrigin.Fever)
                return InvalidTransition(StageState.FeverInput);
            ResolutionOrigin = AnswerResolutionOrigin.None;
            return TransitionTo(StageState.FeverInput, StageTransitionCause.FeverMissResolutionFinished);
        }

        public TransitionResult BeginFeverEnding()
        {
            var result = GuardedTransition(StageState.FeverInput, StageState.EndingFever, StageTransitionCause.FeverEndingBegan);
            if (result == TransitionResult.Succeeded) ResolutionOrigin = AnswerResolutionOrigin.None;
            return result;
        }

        public TransitionResult FinishFeverEnding()
        {
            var result = GuardedTransition(StageState.EndingFever, StageState.ResolvingAnswer, StageTransitionCause.FeverEndingFinished);
            if (result == TransitionResult.Succeeded) ResolutionOrigin = AnswerResolutionOrigin.None;
            return result;
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

        public TransitionResult EnterFailedPendingDecision()
        {
            return GuardedTransition(StageState.ResolvingAnswer, StageState.FailedPendingDecision, StageTransitionCause.FailedDecisionBegan);
        }

        public TransitionResult ResumeFromContinue()
        {
            return GuardedTransition(StageState.FailedPendingDecision, StageState.RecoveringBoard, StageTransitionCause.ContinueResumed);
        }

        public TransitionResult Exit(StageExitReason reason)
        {
            if (State == StageState.Exited)
            {
                return TransitionResult.StageAlreadyTerminated;
            }

            _activePauseReasons.Clear();
            _stateBeforePause = null;
            ResolutionOrigin = AnswerResolutionOrigin.None;
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
            ResolutionOrigin = AnswerResolutionOrigin.None;
            return TransitionTo(target, cause);
        }

        private TransitionResult GuardedTransition(StageState source, StageState target, StageTransitionCause cause)
        {
            if (IsTerminal) return TransitionResult.StageAlreadyTerminated;
            if (State != source) return InvalidTransition(target);
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
                or StageState.EndingFever
                or StageState.FailedPendingDecision;
        }

        private static bool CanFinish(StageState state)
        {
            return state is StageState.PresentingTarget
                or StageState.PlayerInput
                or StageState.ResolvingAnswer
                or StageState.EnteringFever
                or StageState.FeverInput
                or StageState.EndingFever
                or StageState.FailedPendingDecision;
        }
    }
}
