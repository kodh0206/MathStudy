# STEP 9 Design - Fever Core

Status: **APPROVED FOR IMPLEMENTATION WITH SEMANTIC DOWNSTREAM EFFECTS**  
Designed: 2026-08-08  
Source: `Docs/GAME_DESIGN.md` v1.0 sections 9.1-9.10

## Goal and boundary

Implement one deterministic Unity-free Fever lifecycle: capped charge, deferred safe entry, eight seconds of Fever-input time, zero-move Fever attempts, correct-answer combo multipliers, Miss reset, terminal precedence, and immutable end-effect intents.

STEP 9 does not invent expanded-removal geometry, obstacle damage execution, restoration values, random end-effect cells, particles, audio, or UI. It emits typed modifiers/intents for STEPs 10-12.

## GDD rules

- Gauge maximum is 100. Normal Correct contributions are the committed STEP 8 grade, length, and FAST-streak Fever facts. Miss adds zero and does not clear the gauge.
- Full gauge means pending eligibility. Entry waits until answer resolution/fall, terminal evaluation, safe-target verification, and presentation readiness are complete.
- Entry and exit presentation are noninteractive. Fever lasts 8 seconds counted only while Stage is exactly `FeverInput`.
- Fever Correct costs zero moves. Consecutive Fever Correct multipliers are 1, 2, 3, then 5 for the fourth and later. Miss keeps Fever active but resets its combo.
- Semantic Fever effects are expanded removal, obstacle damage x2, restoration x2, and score multiplied by the current Fever combo.
- Natural-end effect tiers by total Fever Correct count are 0 None, 1 RandomThreeBlocks, 2 SmallAreaExplosion, 3 CenterAreaExplosion, and 4+ LargeExplosionAndRestoration. Gauge resets after acknowledged end completion.
- Success/Failure always outranks pending/active Fever.

## Assemblies and ownership

Add Unity-free `MathGame.Fever` referencing Core, Stage, Answer, StageSession, and BoardResolution. BoardResolution is required only to pass authoritative resolution evidence into the atomic Fever-attempt command. It has no direct Board mutation, Targets, App, or Unity dependency; Unity asmdef compilation may also require direct references to model assemblies exposed by public dependency types.

- `FeverChargeTracker` owns live gauge and exactly-once normal-attempt charge consumption.
- `FeverSession` is an internal prospective-state calculator for one active cycle's total Correct count and current/max combo. It has no public commit API.
- `InteractiveFeverClock` owns the interaction-only duration and Stage subscription.
- `FeverController` owns one StageController, one StageSession, charge/session state, and the clock. It is the only public Fever-attempt command boundary, so StageSession accounting and Fever combo cannot be committed independently. It never mutates Board or presentation.
- StageSession remains authoritative for score, objectives, normal/Fever move cost, terminal ordering, and attempt sequence.

## Configuration and value contracts

`sealed FeverConfig(int maximumGauge, double durationSeconds)` exposes get-only `MaximumGauge` and `DurationSeconds`. `FeverConfig.Prototype` is 100 and 8.0. `public static FeverControllerCreateResult FeverController.TryCreate(FeverConfig config, StageController stage, StageSession.StageSession stageSession, ITimeProvider time, out FeverController controller)` returns `MissingConfig`, `InvalidMaximumGauge`, `InvalidDuration`, `MissingStage`, `MissingStageSession`, `MissingTimeProvider`, or `Succeeded` in that precedence; failure sets controller null.

`FeverState`: `Charging`, `PendingEntry`, `Entering`, `Active`, `Ending`, `Faulted`, `Aborted`, `Disposed`.

`FeverEndEffectTier`: `None`, `RandomThreeBlocks`, `SmallAreaExplosion`, `CenterAreaExplosion`, `LargeExplosionAndRestoration`.

`readonly struct FeverGameplayModifiers` has internal construction and exposes `int MoveCost` (0), `int ScoreMultiplier` (1/2/3/5), `int ObstacleDamageMultiplier` (2), `int RestorationMultiplier` (2), and `bool ExpandedRemovalRequested` (true). `None` is the all-zero/default value.

## StageSession integration

Extend `StageAttemptCommand` with non-null immutable `StageAttemptRules Rules`:

- `StageAttemptRules.Normal`: mode Normal, Correct move cost 1, score multiplier 1.
- `StageAttemptRules.CreateFever(int comboMultiplier)`: mode Fever, Correct move cost 0, multiplier restricted to 1,2,3,5; invalid value throws `ArgumentOutOfRangeException`. Rules expose get-only `StageAttemptMode Mode`, `int CorrectMoveCost`, and `int ScoreMultiplier`.
- Existing constructor overload without rules delegates to Normal for compatibility.
- Miss always costs/scores zero but accepts the current explicit mode so Fever orchestration remains traceable.

`StageAttemptResult` adds get-only `StageAttemptId AttemptId`, `StageAttemptMode Mode`, and `int ScoreMultiplier`. Applied results expose the command values; rejected results expose default invalid ID, Normal, and multiplier 1. Score calculation for Correct is checked `(base + grade bonus + length bonus) * ScoreMultiplier`. Objectives/correlation and final-move Success-before-Failure remain unchanged.

The mode/rules are a trusted domain-orchestration input, not a UI field. Only `FeverSession.PreviewAttempt` returns the approved Fever rules used by the gameplay coordinator. StageSession validates the allowed closed multiplier set and cannot accept arbitrary move cost or multiplier.

## Charge tracker

`FeverChargeTracker(int maximumGauge)` rejects nonpositive maximum. Public properties are `int Gauge`, `bool IsFull`, and `StageAttemptId LastConsumedNormalAttemptId`.

`FeverChargeApplyResult ApplyNormalAttempt(StageAttemptResult result)` returns:

- `Applied` or `ReachedMaximum` for an applied Normal Correct;
- `AppliedMiss` for applied Normal Miss (gauge unchanged);
- `MissingResult`, `NotApplied`, `WrongMode`, `StaleOrDuplicateAttempt`, or `NotCharging` otherwise.

It accepts only applied Normal statuses and an attempt ID strictly greater than the last consumed normal ID. Gaps are valid because intervening Fever attempts use the global StageSession sequence but are not charged. An ID less than or equal to the last is StaleOrDuplicateAttempt. Correct contribution saturates to Maximum using overflow-safe arithmetic. Miss advances the consumed normal ID but changes no gauge. The consumed baseline persists across cycles; reset clears gauge only.

When Gauge reaches Maximum, controller state becomes PendingEntry. Charge application is rejected outside Charging; no charge is banked during Pending/Entering/Active/Ending.

## Fever session, combo, and atomic attempt command

`FeverSession` begins empty at entry. `FeverSessionSnapshot` exposes get-only `long TotalCorrectAnswers`, `int CurrentCombo`, `int MaximumCombo`, `int CurrentMultiplier`, and `StageAttemptId LastCommittedAttemptId`.

Internally, `FeverAttemptPlan PreviewAttempt(StageAttemptId id, AnswerResult answer)`:

- validates positive strictly-next ID and a model-consistent Correct or Miss;
- Correct prospectively increments total/current combo and derives 1/2/3/5 multiplier, returning Fever rules and modifiers;
- Miss prospectively leaves total unchanged and resets current combo/multiplier to baseline 1;
- preview never mutates.

There is no public FeverSession commit. `public FeverAttemptResult FeverController.ApplyFeverAttempt(StageAttemptId id, AnswerResult answer, BoardResolutionResult resolution)` is the only command. It validates Active/ResolvingAnswer and creates a prospective internal plan without mutation; applies an owned StageSession command using the plan's closed Fever rules; leaves Fever unchanged if StageSession rejects; and on an applied result assigns the already validated prospective snapshot with no remaining failure branch.

`FeverAttemptApplyStatus` is `AppliedContinue`, `AppliedMiss`, `AppliedTerminal`, `InvalidState`, `InvalidAttempt`, `StageSessionRejected`, or `Disposed`. `FeverAttemptResult` exposes get-only `FeverAttemptApplyStatus Status`, nullable `StageAttemptResult StageResult`, nullable immutable `FeverSessionSnapshot Before`/`After`, and `FeverGameplayModifiers Modifiers`. StageResult and snapshots are null only when rejection occurs before an active FeverSession exists; once a session exists, rejection returns equal non-null Before/After snapshots. StageSessionRejected and all applied statuses retain the actual Stage result. Rejections and Miss expose Modifiers.None.

At CompleteEntry, the new FeverSession is initialized with `stageSession.CreateSnapshot().NextExpectedAttemptId`. Its first preview must equal that global ID, and every later preview must equal the StageSession snapshot's current NextExpectedAttemptId supplied/checked by FeverController. FeverSession does not maintain a competing independent sequence; its last committed ID is historical only.

Perfect/Fast/Normal all count equally toward Fever combo. The normal FAST streak remains separate and is not charged while Fever is active.

## Interactive Fever clock

`InteractiveFeverClock(StageController stage, ITimeProvider time, double durationSeconds) : IDisposable` throws `ArgumentNullException` for missing dependencies and `ArgumentOutOfRangeException` for invalid duration.

States: `Idle`, `Armed`, `Running`, `Suspended`, `Expired`, `Stopped`, `Faulted`, `Disposed`. `FeverClockResult` is `Succeeded`, `JustExpired`, `AlreadyExpired`, `AlreadyInRequestedState`, `InvalidFromCurrentState`, `InvalidTimeSource`, or `Disposed`. `FeverClockFault` is `None`, `NonFiniteSample`, or `TimeRegressed`. The clock exposes get-only State, Fault, DurationSeconds, ElapsedSeconds, and RemainingSeconds.

Commands/results:

- `Arm`: Idle -> Armed; if Stage already FeverInput, starts Running at a validated time sample.
- Stage transition to FeverInput: Armed/Suspended -> Running.
- Stage transition away from FeverInput: Running samples/accumulates then Suspended; terminal transitions stop.
- `Tick`: the only operation that samples for expiry while Running. Exact elapsed >= duration changes once to Expired and returns `JustExpired`; later ticks return `AlreadyExpired`.
- `Stop`: Running/Suspended/Armed -> Stopped, sampling first if Running.
- `Reset`: Stopped/Expired/Faulted -> Idle and zero; invalid while active.
- `RemainingSeconds` and `ElapsedSeconds` are cached, clamped, non-sampling observations and cannot consume expiry.
- nonfinite/backward samples produce Faulted/InvalidTimeSource without negative time or silent recovery.
- Dispose is idempotent and unsubscribes; commands after disposal return Disposed.

The clock invokes an internal same-assembly fault callback synchronously whenever any Arm, Tick, or Stage-transition sample faults. FeverController subscribes at construction and also observes Stage.StateChanged after the clock. It immediately enters Faulted; if Stage accepts FeverInput it calls BeginFeverEnding, while an already noninteractive state stays noninteractive. If a fault occurred while Paused, a later external Resume event that restores FeverInput is observed synchronously and immediately transitioned to EndingFever before control returns to the caller. ApplyFeverAttempt and later lifecycle commands reject while Faulted. Controller disposal removes both subscriptions.

Only exact StageState.FeverInput counts. PlayerInput, EnteringFever, ResolvingAnswer, PresentingTarget, RecoveringBoard, EndingFever, Paused, terminal states, focus/background/ad pauses, and all nested pauses are excluded.

## Stage lifecycle commands

Add explicit guarded commands and transition causes. Every command first returns StageAlreadyTerminated from Success/Failure/Exited, then rejects Paused or a wrong source as InvalidFromCurrentState, and only a successful transition emits one event:

- `BeginFeverEntry`: PresentingTarget -> EnteringFever.
- `CompleteFeverEntry`: EnteringFever -> FeverInput.
- `BeginAnswerResolution`: PlayerInput or FeverInput -> ResolvingAnswer; retain the originating input mode.
- `FinishMissResolution`: valid only for normal-origin resolution -> PlayerInput.
- `FinishFeverMissResolution`: valid only for Fever-origin resolution -> FeverInput.
- `EnableFeverInput`: PresentingTarget -> FeverInput for an already-active Fever cycle.
- `BeginFeverEnding`: FeverInput -> EndingFever.
- `FinishFeverEnding`: EndingFever -> ResolvingAnswer.

Resolution origin is `None`, `Normal`, or `Fever`. It is set only by successful BeginAnswerResolution, retained through ResolvingAnswer, and cleared by a successful mode-matching Miss return, BeginTargetPresentation, BeginFeverEnding/FinishFeverEnding, Complete/Fail, or Exit. Pause preserves it and restores the exact phase. FinishMissResolution requires Normal; FinishFeverMissResolution requires Fever. Wrong-mode, paused, repeated, and terminal commands do not mutate or emit events. `AcceptsPlayerInput` remains true only in PlayerInput/FeverInput.

## FeverController lifecycle

`FeverControllerCommandResult` is `Succeeded`, `AlreadyInRequestedState`, `InvalidFromCurrentState`, `UnsafeEntry`, `StageRejected`, `ClockFaulted`, `MissingAttempt`, `AttemptRejected`, or `Disposed`. Controller get-only properties expose State, Gauge, ClockState, SessionSnapshot (null outside Active/Ending), and PendingEndResult (null except Ending).

Exact controller signatures and mappings:

```text
FeverChargeApplyResult ApplyNormalAttempt(StageAttemptResult result)
FeverControllerCommandResult BeginEntry(bool safeTargetReady, bool stageSessionActive)
FeverControllerCommandResult CompleteEntry()
FeverAttemptResult ApplyFeverAttempt(StageAttemptId id, AnswerResult answer, BoardResolutionResult resolution)
FeverControllerTickResult Tick()
FeverControllerCommandResult CompleteEnding(bool effectsAcknowledged)
FeverControllerCommandResult Abort(FeverTerminationReason reason)
```

ApplyNormalAttempt returns the tracker result and updates Charging/PendingEntry/Aborted as described. BeginEntry returns UnsafeEntry when either boolean gate is false, InvalidFromCurrentState for state/Stage mismatch, StageRejected when the Stage command fails, otherwise Succeeded. CompleteEntry/Tick/CompleteEnding map clock or Stage failures to ClockFaulted/StageRejected and never partially advance. `FeverControllerTickResult` is `NoChange`, `EndingBegan`, `AlreadyEnding`, `ClockFaulted`, `InvalidFromCurrentState`, or `Disposed`.

- `ApplyNormalAttempt(result)` delegates charge and moves Charging -> PendingEntry exactly once at full gauge. If result is terminal, `Abort` wins and clears live Fever/gauge.
- `BeginEntry(bool safeTargetReady, bool stageSessionActive)` is valid only PendingEntry, both gates true, and Stage PresentingTarget. It calls Stage.BeginFeverEntry and enters Entering.
- `CompleteEntry()` arms the clock while Stage is EnteringFever; Arm performs no time sample there. It then calls Stage.CompleteFeverEntry. Stage rejection rolls Armed back to Idle and leaves controller Entering. On success, the Stage event starts the clock. After the Stage call returns, CompleteEntry first checks whether the synchronous fault callback changed controller state to Faulted; if so it returns ClockFaulted and never installs a session or overwrites the fault. Otherwise it installs the new FeverSession and becomes Active.
- `ApplyFeverAttempt()` is the single atomic attempt command described above and is valid only in Active while Stage is ResolvingAnswer.
- `Tick()` delegates clock. On JustExpired it requires Stage FeverInput, calls Stage.BeginFeverEnding, captures the end result, and enters Ending. Stage rejection is an invariant fault: controller enters Faulted and never remains Active with an Expired clock; expiry cannot be retried.
- `CompleteEnding(bool effectsAcknowledged)` requires acknowledgement (including tier None), calls Stage.FinishFeverEnding, resets clock/session/gauge, and returns Charging. False leaves Ending unchanged for retry/fatal handling.
- `Abort(FeverTerminationReason)` stops/disposes active timing, clears session/gauge, emits no gameplay end effect, and enters Aborted. Terminal/exit always uses Abort.

`FeverTerminationReason` is `NaturalExpiry`, `StageSucceeded`, `StageFailed`, `StageExited`, `ClockFault`, or `Cancelled`. Only NaturalExpiry produces a pending gameplay end tier; every Abort reason suppresses it.

`FeverEndResult` has internal construction and get-only `FeverTerminationReason TerminationReason`, `FeverEndEffectTier EffectTier`, `long TotalCorrectAnswers`, `int CurrentCombo`, `int MaximumCombo`, `int FinalMultiplier`, `double InteractiveElapsedSeconds`, `int ObstacleDamageMultiplier`, and `int RestorationMultiplier`. It is immutable and emitted once. Natural classification uses total correct count; Miss-reset combo never lowers the tier. Aborts expose no PendingEndResult.

## Ordering

Normal full-gauge answer: resolve Board -> apply StageSession -> charge -> terminal wins -> safe target search -> target presentation -> BeginEntry -> presentation acknowledgement -> CompleteEntry/FeverInput.

Fever answer: preview Fever plan -> BeginAnswerResolution (clock suspends) -> resolve current basic selected removal -> apply StageSession with plan rules -> commit Fever plan -> terminal wins/Abort or safe-target presentation -> EnableFeverInput if time remains.

Natural expiry: FeverInput Tick -> EndingFever -> emit intent -> STEPs 10/11 acknowledge effect -> FinishFeverEnding -> ResolvingAnswer -> safe normal target -> PresentingTarget -> PlayerInput.

## Expected files

Add `Assets/MathGame/Runtime/Fever/MathGame.Fever.asmdef`, config/state/result/charge/session/clock/controller/end-classifier files, and Edit/Play tests. Modify StageSession command/result/accounting and StageController transitions/causes. Update test asmdefs and documentation after verification.

## Acceptance matrix

- Gauge exact contributions, 99+1, overshoot, no carry, duplicate/out-of-order/mode failures, Miss preservation, pending/terminal behavior.
- Fever multipliers 1/2/3/5/5; Correct-Correct-Miss-Correct resets to 1 while total remains 3; preview/commit atomicity.
- Fever StageSession Correct costs zero and checked score multiplier applies; Miss costs zero; normal regressions unchanged; terminal precedence.
- Exact 8-second FeverInput timing across resolution, presentation, entry/end, nested pause/focus/background/ad exclusions; backward/nonfinite fault, expiry once, disposal.
- Full Stage graph, resolution-origin returns, wrong-phase/paused/terminal no-event behavior.
- End tiers 0/1/2/3/4+, immutable one-shot result, acknowledgement gate, reset only after completion, terminal abort without gameplay intent.
- No Board/obstacle/restoration/presentation implementation leaks into Fever.
- Full Edit/Play regression and independent review with no P0-P2.

## Deferred ambiguities

- PERFECT does not count as FAST for the normal FAST streak, preserving current literal behavior.
- Expanded removal geometry, obstacle execution, end explosion cells/radii: STEP 10.
- Restoration formula and composition of Fever x2 with combo: STEP 11; STEP 9 exposes both factors separately.
- Exact 0.5-second entry visuals and expiry-mid-gesture cancellation: STEP 12.
- Terminal Fever suppresses gameplay end effects; revise only with explicit product clarification.

## Disposition

STEP 9 is implementation-ready as Fever Core with semantic downstream effects. Full playable Fever effects remain intentionally incomplete until STEPs 10-12 consume the contracts.
