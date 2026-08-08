# STEP 5 Design — Addition Validation and Interactive Timing

Status: **APPROVED FOR IMPLEMENTATION**
Designed: 2026-08-08
Source: `Docs/GAME_DESIGN.md` v1.0

## Goal and scope

Evaluate an immutable connection snapshot against one positive target, classify accepted releases as Correct or Miss, grade correct answers using only accumulated interactive time, and expose semantic result data. Add only the Stage phase commands required to make target presentation, player input, and answer resolution authoritative timing boundaries.

No Board mutation, moves, rewards, Fever gauge, target search, UI, analytics provider, or persistence belongs here.

## GDD rules and resolved interpretations

- §§4.4–4.5: addition only; sum below/equal/above is preview data; equality does not auto-submit; exact equality on release is arithmetically correct.
- §§5.2, 6, and 13: MVP solutions/rewards/tutorial begin at two connected blocks. Therefore Correct requires `Count >= 2`; a one-block equal release is Miss/InsufficientConnectionLength.
- §4.6: misses remove nothing and spend no move. STEP 5 reports Miss only; downstream systems own consequences.
- §§5.3, 9.2–9.3: timing begins only when the target is visible and input is enabled, and excludes presentation, resolution, removal/fall, transitions, pause, deactivation, and ads.
- §§9.2 and 27: correct elapsed `<=2.0` is Perfect, `>2.0 && <=4.0` Fast, and `>4.0` Normal. Miss is Miss. Slow correctness remains correct.
- A Miss does not reset elapsed time for the current target because no new target appeared. The timer continues after any excluded feedback interval.

## Architecture

Add Unity-free `MathGame.Answer`, referencing Connection, Core, and Stage. It is non-auto-referenced and has no UnityEngine reference.

Pure model:

- `TargetNumber`: positive int value with default-value defense.
- `AnswerRelation`: None, BelowTarget, MatchesTarget, AboveTarget.
- `AnswerOutcome`: NoSelection, Correct, Miss.
- `AnswerMissReason`: None, UnderTarget, OverTarget, InsufficientConnectionLength.
- `SpeedGrade`: None, Perfect, Fast, Normal, Miss.
- `AnswerTimingThresholds`: validated finite nonnegative Perfect/Fast boundaries, prototype defaults 2 and 4 seconds.
- `AnswerResult`: immutable target, submitted snapshot/sum/count, relation, outcome/reason, grade, and elapsed interactive seconds.
- `AnswerValidator`: pure preview and evaluation. Null snapshot and invalid target/time are programmer errors. Empty snapshot is NoSelection/None and emits no gameplay submission. Correct requires count at least two and exact sum.

```csharp
public sealed class AnswerValidator
{
    public AnswerValidator(AnswerTimingThresholds thresholds);
    public AnswerRelation Preview(long sum, TargetNumber target);
    public AnswerResult Evaluate(
        ConnectionPathSnapshot snapshot,
        TargetNumber target,
        double interactiveElapsedSeconds);
}
```

Threshold construction rejects NaN, infinity, negatives, or Perfect greater than Fast with `ArgumentOutOfRangeException`; `Prototype` is 2.0/4.0. Validator construction rejects null thresholds. Preview rejects invalid targets. Evaluate rejects null snapshots and invalid/nonfinite/negative elapsed values. `AnswerResult` publicly exposes `Snapshot`, `Target`, `SubmittedSum`, `SelectedBlockCount`, `InteractiveElapsedSeconds`, `Relation`, `Outcome`, `MissReason`, `Grade`, and derived `IsCorrect`.

Interactive timing:

- `InteractiveAnswerClock` depends on `StageController` and `ITimeProvider`, subscribes to Stage state changes, and implements `IDisposable`.
- States: Idle, Armed, Running, Suspended, Stopped, Faulted, Disposed.
- Commands return explicit results: Arm, Stop, Reset. Arm starts immediately only when `Stage.AcceptsPlayerInput`; otherwise it waits. Running accumulates until input becomes unavailable, then suspends. Final input restoration resumes it.
- Time samples must be finite and nondecreasing. Invalid samples fault the clock instead of clamping or awarding a faster grade.
- Stop freezes elapsed after timing has begun. Reset is explicit; a miss normally neither stops nor resets the target clock.

```csharp
public sealed class InteractiveAnswerClock : IDisposable
{
    public InteractiveAnswerClock(StageController stage, ITimeProvider timeProvider);
    public AnswerClockState State { get; }
    public double ElapsedSeconds { get; }
    public AnswerClockFault Fault { get; }
    public AnswerClockCommandResult Arm();
    public AnswerClockCommandResult Stop();
    public AnswerClockCommandResult Reset();
    public void Dispose();
}
```

Constructor dependencies are non-null or throw `ArgumentNullException`. Results are `Succeeded`, `AlreadyArmed`, `NotStarted`, `AlreadyStopped`, `InvalidFromCurrentState`, `Faulted`, and `Disposed` as applicable. Fault values are `None`, `NonFiniteTime`, and `TimeMovedBackward`.

Command/state contract:

| Command | Valid state | Result/state |
|---|---|---|
| Arm | Idle | Armed, or Running after sampling now when Stage accepts input |
| Arm | Armed/Running/Suspended | AlreadyArmed, unchanged |
| Stop | Running | sample final delta, Stopped |
| Stop | Suspended | Stopped with accumulated elapsed |
| Stop | Armed | NotStarted, unchanged |
| Stop | Stopped | AlreadyStopped |
| Reset | Stopped or Faulted | Idle, elapsed/fault cleared |
| Reset | Idle | Succeeded idempotently, zero |
| Reset | Armed/Running/Suspended | InvalidFromCurrentState |
| Any command | Disposed | Disposed, unchanged |

`ElapsedSeconds` samples the provider while Running so it is live; otherwise it returns accumulated/final elapsed without sampling. Sampling occurs on Arm when already interactive, every live elapsed read, the Stage transition that ends interaction, and Stop while Running. Every sample must be finite and not less than the last sample; violation atomically enters Faulted, preserves the last valid accumulated value, records the fault, and commands return Faulted until Reset. Stage events in Idle/Stopped/Faulted/Disposed are ignored. Dispose is idempotent, unsubscribes, and sets Disposed; properties remain readable but do not sample.

Minimal Stage commands:

```text
Ready or ResolvingAnswer -> BeginTargetPresentation -> PresentingTarget
PresentingTarget -> EnablePlayerInput -> PlayerInput
PlayerInput -> BeginAnswerResolution -> ResolvingAnswer
ResolvingAnswer -> FinishMissResolution -> PlayerInput (same target)
```

Each command has an explicit transition cause/result and rejected calls publish no event. PresentingTarget and ResolvingAnswer reject input; PlayerInput accepts it. `FinishMissResolution` resumes the same target and does not reset/rearm its clock. Correct resolution proceeds later through new-target presentation after STEP 6. Pause restores the exact underlying phase. Every new command rejects wrong phases, Paused, and terminal states without an event. Existing terminal commands remain low-level reserved contracts; objective legality is still owned by STEP 8. No Board mutation, objectives, or Fever transition is added.

## Public behavior

1. Preview returns Below/Matches/Above and has no side effects.
2. Empty release is NoSelection; it is not a gameplay Miss.
3. Under, over, and one-block-equal releases are Miss with distinct reasons and Miss grade.
4. Exact two-or-more-block releases are Correct and graded inclusively at 2/4 seconds.
5. AnswerResult retains the immutable Connection snapshot for later identity-safe resolution.
6. Clock elapsed advances only while Stage accepts input and is frozen/excluded in every other phase or pause state.
7. Nested pause reasons cannot resume the clock early because Stage remains Paused until the final reason clears.
8. Validator and clock do not mutate Connection, Board, Stage consequences, moves, rewards, or SDKs.

## Expected files

Add `Assets/MathGame/Runtime/Answer` with its asmdef and the model, validator, clock state/result, and clock classes. Add Edit Mode Answer tests and targeted Play Mode lifecycle clock tests. Modify StageController/transition causes only for the four approved phase commands and update Edit/Play test assembly references.

## Acceptance tests

- Preview under/equal/over; equal can later become over/backtrack without auto-submit.
- Empty, under, over, one-block equal, and valid exact outcomes/reasons.
- Exact 0/2/just-over-2/4/just-over-4 boundaries; very slow exact remains Correct/Normal; all misses grade Miss.
- Invalid targets, thresholds, elapsed values, null snapshot.
- Clock arm before input remains zero; PlayerInput begins; pause/background/focus/ad/system and phase transitions exclude time; nested reasons resume only after final clear.
- Miss continuation is represented by leaving the clock running; correct Stop freezes it.
- Repeated/out-of-order commands and disposal are deterministic; invalid/backward/nonfinite time faults.
- Stage transitions and causes are legal only in the approved graph and preserve input/pause contracts.
- FinishMissResolution returns same-target resolution to PlayerInput without resetting elapsed; all new commands reject wrong-phase, paused, and terminal calls without events, and pause/resume restores each new phase.
- Full Edit Mode and targeted Play Mode regression pass with no P0 and no STEP 6+ behavior.

## Deferred/out of scope

Move spending, streak reset implementation, Fever gain, Board ID applicability/removal, gravity/refill, target selection/search, objectives, feedback presentation, release deduplication in the input adapter, analytics delivery, and all later systems.

## Disposition

STEP 5 is implementation-ready. STEP 7 must share the minimum path length of two.
