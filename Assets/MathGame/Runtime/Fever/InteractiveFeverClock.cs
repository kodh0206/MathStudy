using System;
using MathGame.Core.Time;
using MathGame.Stage;

namespace MathGame.Fever
{
    public sealed class InteractiveFeverClock : IDisposable
    {
        private readonly StageController stage; private readonly ITimeProvider time; private double elapsed; private double last;
        internal event Action Faulted;
        public InteractiveFeverClock(StageController stage, ITimeProvider time, double durationSeconds)
        { this.stage = stage ?? throw new ArgumentNullException(nameof(stage)); this.time = time ?? throw new ArgumentNullException(nameof(time)); if (!Finite(durationSeconds) || durationSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(durationSeconds)); DurationSeconds = durationSeconds; stage.StateChanged += Changed; }
        public FeverClockState State { get; private set; } public FeverClockFault Fault { get; private set; }
        public double DurationSeconds { get; } public double ElapsedSeconds => Math.Min(DurationSeconds, Math.Max(0, elapsed)); public double RemainingSeconds => Math.Max(0, DurationSeconds - ElapsedSeconds);
        public FeverClockResult Arm() { if (State == FeverClockState.Disposed) return FeverClockResult.Disposed; if (State != FeverClockState.Idle) return FeverClockResult.InvalidFromCurrentState; State = FeverClockState.Armed; if (stage.State == StageState.FeverInput) Start(); return State == FeverClockState.Faulted ? FeverClockResult.InvalidTimeSource : FeverClockResult.Succeeded; }
        public FeverClockResult Tick() { if (State == FeverClockState.Disposed) return FeverClockResult.Disposed; if (State == FeverClockState.Expired) return FeverClockResult.AlreadyExpired; if (State != FeverClockState.Running) return FeverClockResult.InvalidFromCurrentState; if (!Accumulate()) return FeverClockResult.InvalidTimeSource; if (elapsed < DurationSeconds) return FeverClockResult.Succeeded; elapsed = DurationSeconds; State = FeverClockState.Expired; return FeverClockResult.JustExpired; }
        public FeverClockResult Stop() { if (State == FeverClockState.Disposed) return FeverClockResult.Disposed; if (State == FeverClockState.Stopped) return FeverClockResult.AlreadyInRequestedState; if (State is not (FeverClockState.Armed or FeverClockState.Running or FeverClockState.Suspended)) return FeverClockResult.InvalidFromCurrentState; if (State == FeverClockState.Running && !Accumulate()) return FeverClockResult.InvalidTimeSource; State = FeverClockState.Stopped; return FeverClockResult.Succeeded; }
        public FeverClockResult Reset() { if (State == FeverClockState.Disposed) return FeverClockResult.Disposed; if (State is FeverClockState.Armed or FeverClockState.Running or FeverClockState.Suspended) return FeverClockResult.InvalidFromCurrentState; elapsed = last = 0; Fault = FeverClockFault.None; State = FeverClockState.Idle; return FeverClockResult.Succeeded; }
        public void Dispose() { if (State == FeverClockState.Disposed) return; stage.StateChanged -= Changed; State = FeverClockState.Disposed; }
        private void Changed(StageTransition transition) { if (transition.Current is StageState.Success or StageState.Failure or StageState.Exited) { if (State is FeverClockState.Running or FeverClockState.Suspended or FeverClockState.Armed) Stop(); return; } if (State == FeverClockState.Running && transition.Current != StageState.FeverInput) { if (Accumulate()) State = FeverClockState.Suspended; } else if (transition.Current == StageState.FeverInput && State is FeverClockState.Armed or FeverClockState.Suspended) Start(); }
        private void Start() { if (Sample(out var value)) { if (State == FeverClockState.Suspended && value < last) { Fail(FeverClockFault.TimeRegressed); return; } last = value; State = FeverClockState.Running; } }
        private bool Accumulate() { if (!Sample(out var value)) return false; if (value < last) { Fail(FeverClockFault.TimeRegressed); return false; } elapsed += value - last; last = value; return true; }
        private bool Sample(out double value) { value = time.RealtimeSeconds; if (!Finite(value)) { Fail(FeverClockFault.NonFiniteSample); return false; } return true; }
        private void Fail(FeverClockFault fault) { Fault = fault; State = FeverClockState.Faulted; Faulted?.Invoke(); }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
