using System;
using MathGame.Core.Time;
using MathGame.Stage;

namespace MathGame.Answer
{
    public enum AnswerClockState { Idle, Armed, Running, Suspended, Stopped, Faulted, Disposed }
    public enum AnswerClockFault { None, NonFiniteTime, TimeMovedBackward }
    public enum AnswerClockCommandResult
    {
        Succeeded, AlreadyArmed, NotStarted, AlreadyStopped, InvalidFromCurrentState, Faulted, Disposed
    }

    public sealed class InteractiveAnswerClock : IDisposable
    {
        private readonly StageController stage;
        private readonly ITimeProvider time;
        private double elapsed;
        private double lastSample;

        public InteractiveAnswerClock(StageController stage, ITimeProvider time)
        {
            this.stage = stage ?? throw new ArgumentNullException(nameof(stage));
            this.time = time ?? throw new ArgumentNullException(nameof(time));
            stage.StateChanged += OnStageChanged;
        }

        public AnswerClockState State { get; private set; } = AnswerClockState.Idle;
        public AnswerClockFault Fault { get; private set; } = AnswerClockFault.None;
        public double ElapsedSeconds
        {
            get { if (State == AnswerClockState.Running) Accumulate(); return elapsed; }
        }

        public AnswerClockCommandResult Arm()
        {
            if (State == AnswerClockState.Disposed) return AnswerClockCommandResult.Disposed;
            if (State == AnswerClockState.Faulted) return AnswerClockCommandResult.Faulted;
            if (State is AnswerClockState.Armed or AnswerClockState.Running or AnswerClockState.Suspended)
                return AnswerClockCommandResult.AlreadyArmed;
            if (State != AnswerClockState.Idle) return AnswerClockCommandResult.InvalidFromCurrentState;
            State = AnswerClockState.Armed;
            if (stage.AcceptsPlayerInput) StartRunning();
            return State == AnswerClockState.Faulted ? AnswerClockCommandResult.Faulted : AnswerClockCommandResult.Succeeded;
        }

        public AnswerClockCommandResult Stop()
        {
            if (State == AnswerClockState.Disposed) return AnswerClockCommandResult.Disposed;
            if (State == AnswerClockState.Faulted) return AnswerClockCommandResult.Faulted;
            if (State == AnswerClockState.Stopped) return AnswerClockCommandResult.AlreadyStopped;
            if (State == AnswerClockState.Armed) return AnswerClockCommandResult.NotStarted;
            if (State is not (AnswerClockState.Running or AnswerClockState.Suspended)) return AnswerClockCommandResult.InvalidFromCurrentState;
            if (State == AnswerClockState.Running && !Accumulate()) return AnswerClockCommandResult.Faulted;
            State = AnswerClockState.Stopped;
            return AnswerClockCommandResult.Succeeded;
        }

        public AnswerClockCommandResult Reset()
        {
            if (State == AnswerClockState.Disposed) return AnswerClockCommandResult.Disposed;
            if (State is AnswerClockState.Armed or AnswerClockState.Running or AnswerClockState.Suspended)
                return AnswerClockCommandResult.InvalidFromCurrentState;
            elapsed = 0; lastSample = 0; Fault = AnswerClockFault.None; State = AnswerClockState.Idle;
            return AnswerClockCommandResult.Succeeded;
        }

        public void Dispose()
        {
            if (State == AnswerClockState.Disposed) return;
            stage.StateChanged -= OnStageChanged;
            State = AnswerClockState.Disposed;
        }

        private void OnStageChanged(StageTransition transition)
        {
            if (State == AnswerClockState.Running && !stage.AcceptsPlayerInput)
            {
                if (Accumulate()) State = AnswerClockState.Suspended;
            }
            else if ((State == AnswerClockState.Armed || State == AnswerClockState.Suspended) && stage.AcceptsPlayerInput)
            {
                StartRunning();
            }
        }

        private void StartRunning()
        {
            if (TrySample(out var sample))
            {
                if (State == AnswerClockState.Suspended && sample < lastSample)
                {
                    FaultClock(AnswerClockFault.TimeMovedBackward);
                    return;
                }
                lastSample = sample;
                State = AnswerClockState.Running;
            }
        }

        private bool Accumulate()
        {
            if (!TrySample(out var current)) return false;
            if (current < lastSample) { FaultClock(AnswerClockFault.TimeMovedBackward); return false; }
            elapsed += current - lastSample; lastSample = current; return true;
        }

        private bool TrySample(out double sample)
        {
            sample = time.RealtimeSeconds;
            if (double.IsNaN(sample) || double.IsInfinity(sample))
            {
                FaultClock(AnswerClockFault.NonFiniteTime); return false;
            }
            return true;
        }

        private void FaultClock(AnswerClockFault fault)
        {
            Fault = fault;
            State = AnswerClockState.Faulted;
        }
    }
}
