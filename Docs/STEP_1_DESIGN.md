# STEP 1 Design — Project Foundation and Stage Lifecycle

Status: **APPROVED FOR IMPLEMENTATION**
Designed: 2026-08-08
Source of truth: `Docs/GAME_DESIGN.md` v1.0
Production code changed by this design pass: No

## Goal

Provide the mobile application and stage-lifecycle foundation required by later gameplay STEPs without implementing gameplay:

- one stable application composition root;
- explicit and observable stage state;
- a non-interactive initialized state;
- nested pause/deactivation reasons, including events received before Unity `Start`;
- deterministic time/random seams;
- persistence isolated behind a repository interface;
- safe teardown and recreation.

## Relevant game-design requirements

- **GDD §3.1–3.2:** The stage loop has ordered target, input, resolution, removal, gravity, Fever, objective, and restoration phases. The lifecycle must distinguish interactive from non-interactive work.
- **GDD §5.3:** Input is rejected during target transition. Timing begins only after the target is visible and manipulation is available.
- **GDD §8.2–8.3:** Stages eventually terminate in success or failure, with failure based on exhausted moves and incomplete objectives.
- **GDD §9.2–9.3:** Answer timing starts only while a visible target is playable. Removal, gravity, obstacle destruction, target transition, chain explosions, Fever transitions, pause, app deactivation, and ads are excluded.
- **GDD §9.5–9.6:** Fever begins only after resolution, before terminal outcome, when normal manipulation is possible; Fever time decreases only during actual player-control time.
- **GDD §14.1:** Gameplay has a user pause action.
- **GDD §18.2:** Ads can interrupt gameplay and must not accidentally resume another active interruption.
- **GDD §20.1:** Stage start, success, failure, and abandon are semantic lifecycle events. Provider integration is later scope.
- **GDD §21.1 and §26:** A complete stage, timing, Fever, failure/retry, and analytics are MVP requirements, but not STEP 1 implementation scope.

## Requirements

### Ownership and services

1. Exactly one active persistent `MathGameBootstrap` owns foundation services.
2. A duplicate bootstrap cannot replace or duplicate the owner. Destroying the owner permits clean recreation.
3. The bootstrap exposes `StageController`, `ITimeProvider`, and `IRandomSource` and composes logging internally.
4. Randomness remains seed-controllable, raw realtime remains behind `ITimeProvider`, and persistence remains behind `ISaveRepository` with schema-versioned data.
5. Raw realtime is not the answer/Fever clock. Later timing systems accumulate time only while their owning interaction state is eligible.

### State and input

6. State and transition outcomes are explicit and observable. Each successful transition reports previous state, current state, and cause.
7. Blank foundation initialization is `None -> Initializing -> Ready`.
8. `Ready` is non-interactive. `AcceptsPlayerInput` remains true only for `PlayerInput` and `FeverInput`.
9. `PlayerInput` cannot be reached in STEP 1 because no board or valid visible target exists.
10. Target, resolution, and Fever states remain reserved vocabulary. Their transition commands are introduced only by their owning later STEPs.
11. `Complete` and `Fail` are invalid from `None`, `Initializing`, and `Ready`. Later objective/session orchestration will become the authorized terminal caller.
12. Rejected, idempotent, or blocked commands publish no successful transition event.

### Pause and deactivation

13. User pause, application background, focus loss, advertisement, and system interruption remain independent reasons.
14. Repeating a reason is idempotent. Clearing one reason cannot resume while another remains. Clearing the final reason restores the exact underlying state.
15. The latest pause/focus facts received after relay creation but before successful stage initialization are retained independently.
16. After successful initialization, all retained inactive reasons are reconciled before the next interaction frame.
17. An early inactive fact cleared before initialization is not applied later; latest known state wins per reason.
18. Normal and early lifecycle synchronization compares desired reason presence with actual presence so duplicate/active callbacks do not produce invalid resume calls or warnings.
19. Initialization results are checked in sequence. `FinishInitialization` is attempted only after `Start` succeeds; reconciliation occurs only after both succeed.

### Teardown and scope

20. Teardown unsubscribes lifecycle callbacks before exiting the owned stage, clears controller pause bookkeeping through exit, and releases bootstrap ownership.
21. STEP 1 adds no board, target, answer, move, timing grade, Fever rule, obstacle, restoration, UI, analytics provider, advertising, monetization, progression, or concrete save backend.

## Architecture

### State model

Append `Ready` with a new explicit numeric value; do not renumber existing enum members. Declaration order is not the transition graph.

```text
None --Start--> Initializing --FinishInitialization--> Ready

Initializing/Ready --Pause(reason)--> Paused
Paused --clear final reason--> exact StateBeforePause

Any non-Exited state --Exit--> Exited
```

`Ready` may pause because the app can deactivate after foundation initialization and before later gameplay orchestration starts. `Initializing` remains pausable as a domain capability, although the bootstrap initializes synchronously.

Reserved future lifecycle, not implemented in STEP 1:

```text
Ready -> PresentingTarget -> PlayerInput -> ResolvingAnswer
ResolvingAnswer -> PresentingTarget | EnteringFever | Success | Failure
EnteringFever -> FeverInput -> ResolvingAnswer | EndingFever
EndingFever -> PresentingTarget | Success | Failure
```

This is an architectural boundary, not a complete mandatory graph. Later STEPs may group removal/gravity/effects inside resolution states, but must preserve every GDD input/timing exclusion interval.

### System ownership

- **`ApplicationLifecycleRelay`:** sole Unity pause/focus callback adapter. Stores latest raw facts, then publishes events. It makes no stage decision.
- **`MathGameBootstrap`:** composition root. Sequences initialization, gates early lifecycle events, translates lifecycle facts to pause reasons, and owns teardown.
- **`StageController`:** sole plain-C# stage state and pause-reason owner. It has no Unity dependency.
- **Core services:** raw time, controlled randomness, and logging adapters only.
- **Save shell:** unchanged in STEP 1.

### Lifecycle snapshots

The relay exposes read-only nullable facts:

```csharp
bool? IsApplicationPaused { get; }
bool? HasApplicationFocus { get; }
```

Each Unity callback caches its new value before raising its event. `null` means Unity has not reported that fact. Do not guess an initial focus value or introduce a second focus authority.

### Initialization and synchronization

```text
Awake
  -> compose services
  -> subscribe to relay

Lifecycle callback before initialization
  -> relay caches latest fact
  -> bootstrap defers StageController mutation

Start
  -> StageController.Start
  -> require Succeeded
  -> StageController.FinishInitialization
  -> require Succeeded
  -> mark initialized
  -> reconcile known lifecycle facts

Lifecycle callback after initialization
  -> synchronize desired pause-reason presence immediately
```

The bootstrap uses one private synchronization path conceptually equivalent to:

```csharp
SynchronizePauseReason(PauseReason reason, bool shouldBePaused)
```

It calls `Pause` only when the reason should exist and is absent; it calls `Resume` only when the reason should be absent and is present. Both reconciliation and live events use this path.

With two inactive reasons, the first creates the single `Ready -> Paused` transition and the second only augments the reason set. Clearing one leaves the stage paused; clearing the final reason restores `Ready`.

### Assembly boundaries

No new assembly or dependency is required:

```text
MathGame.Core        independent
MathGame.Save        independent
MathGame.Stage  ---> MathGame.Core
MathGame.App    ---> MathGame.Core, MathGame.Stage, MathGame.Save
```

No dependency-injection framework, public service locator, new singleton, ScriptableObject runtime state, or async workflow is introduced.

## Design conflicts resolved

### Conflict 1 — blank startup advertises input

**DESIGN:** GDD §5.3 and §9.2 require a visible valid target and actual manipulation availability before input and timing begin.

**CURRENT:** `FinishInitialization` enters `PlayerInput` despite having no board or target.

**CONFLICT:** `AcceptsPlayerInput` is true when GDD-valid interaction is impossible.

**IMPACT:** Future input/timing consumers could activate early and tests would encode an invalid contract.

**RECOMMENDATION:** Initialize into non-interactive `Ready`; only later target orchestration can enter `PlayerInput`.

### Conflict 2 — permissive terminal commands

**DESIGN:** GDD §8 makes success objective-driven and failure dependent on exhausted moves with objectives incomplete.

**CURRENT:** `Complete` and `Fail` accept `Initializing` and all declared active phase states.

**CONFLICT:** Foundation callers can terminate without objective/move evaluation.

**IMPACT:** Later integrations could bypass stage rules.

**RECOMMENDATION:** Make terminal commands invalid from `Initializing` and `Ready`. Successful terminal entry remains unreachable through the STEP 1 public flow until objective/session orchestration introduces approved transitions.

### Conflict 3 — early lifecycle intent is lost

**DESIGN:** GDD §9.3 requires pause and app deactivation to exclude interaction time and therefore block effective interaction.

**CURRENT:** A pause/focus callback after `Awake` but before Unity `Start` is sent to `StageController` in `None`, rejected, and forgotten.

**CONFLICT:** Initialization can finish active while the application remains inactive.

**IMPACT:** Later interaction/timing could begin in the background.

**RECOMMENDATION:** Retain nullable latest lifecycle facts and reconcile them immediately after successful initialization.

## Files expected to change during `Implement STEP 1`

### Production

- `Assets/MathGame/Runtime/App/ApplicationLifecycleRelay.cs`
  - retain nullable pause/focus facts and cache before event publication.
- `Assets/MathGame/Runtime/App/MathGameBootstrap.cs`
  - check initialization results;
  - gate pre-initialization events;
  - reconcile cached facts;
  - synchronize reason presence idempotently.
- `Assets/MathGame/Runtime/Stage/StageState.cs`
  - append explicit `Ready` value without renumbering existing members.
- `Assets/MathGame/Runtime/Stage/StageController.cs`
  - finish initialization in `Ready`;
  - allow pausing/restoring `Ready`;
  - disallow completion/failure from `Initializing` and `Ready`.

No new production file or assembly definition is expected.

### Tests

- `Assets/MathGame/Tests/EditMode/StageControllerTests.cs`
- `Assets/MathGame/Tests/PlayMode/MathGameBootstrapTests.cs`

### Documentation after implementation

- `Docs/ARCHITECTURE.md`
- `Docs/DECISIONS.md`
- STEP 1 status in `Docs/DEVELOPMENT_PLAN.md`

## Acceptance criteria

### Edit Mode

1. Startup is exactly `None -> Initializing -> Ready` with correct causes and `AcceptsPlayerInput == false` throughout.
2. Pause/resume from `Ready` restores `Ready`.
3. All five reasons remain independent, nested, and idempotent.
4. Unknown resume does not mutate state; clearing one of multiple reasons cannot resume.
5. `Complete` and `Fail` are invalid from `None`, `Initializing`, and `Ready` and publish no transition.
6. Exit clears pause bookkeeping, enters `Exited`, and later commands are rejected.
7. Successful transitions publish exactly one correct event; unsuccessful commands publish none.
8. Seeded randomness and save-schema tests remain passing.

### Play Mode

Create a bootstrap, send relay callbacks after `Awake` but before the first yielded frame, then verify:

1. early background pause results in `Paused`, background reason active, `StateBeforePause == Ready`, and no accepted input;
2. early focus loss produces the equivalent focus reason;
3. both inactive facts in either order produce two reasons and one transition to `Paused`;
4. inactive then active before initialization leaves `Ready` with no stale reason;
5. clearing only one of two reasons remains paused; clearing the final reason restores `Ready`;
6. duplicate and initial-active callbacks produce no spurious transition or warning;
7. unknown lifecycle facts leave the stage in `Ready`;
8. normal post-initialization callbacks preserve nested behavior;
9. destruction while paused exits cleanly and recreation is independent;
10. a duplicate bootstrap does not steal lifecycle ownership.

Compilation and valid Edit/Play Mode result artifacts are required. Runner/environment failure is `NOT VERIFIED`, never `PASS`.

## Ambiguities deferred

- The GDD says “app deactivation” but does not define Unity callback ordering. Independent pause/focus reasons and nullable latest facts are the conservative mobile interpretation.
- User pause menu behavior, resume countdown, animation pausing, audio, and `Time.timeScale` policy are unspecified.
- Full phase granularity is not specified; later resolution/timing/Fever designs must preserve the GDD exclusion boundaries.
- The exact source phase and ordering for success/failure relative to final resolution are deferred to STEP 8.
- Ad callback mapping and duplicate provider behavior are deferred to STEP 15; `Advertisement` remains a foundation reason.
- Save-on-background, crash restoration, analytics once-only policy, and reentrant `StateChanged` subscriber behavior remain deferred.

## Edge cases

- Pause/focus callbacks arrive before `Start`, repeat, reverse order, or clear before initialization.
- Only one lifecycle fact is ever reported; unknown remains distinct from active.
- User pause, background, focus loss, ad, and system interruption overlap.
- An ad ends while the application remains backgrounded.
- Destruction occurs before `Start`, while paused, or after exit.
- A duplicate bootstrap is created while the owner is inactive.
- Initialization unexpectedly returns a non-success result.
- Domain reload is disabled in the Editor.

## Out of scope

- Board/cells, numbers, paths, backtracking, sums, answers, target search, deadlock, removal, gravity, refill, moves, objectives, rewards, timing grades, Fever, special blocks, obstacles, restoration, presentation, audio, accessibility, progression, concrete persistence, analytics providers, ads, IAP, or content.
- Full gameplay transition APIs and complete scene/navigation flow.
- Assembly/package cleanup, broad refactors, or a DI framework.

## Disposition

STEP 1 design is approved and implementation-ready. It resolves the GDD conflicts without adding later gameplay. The next valid command is `Implement STEP 1`; implementation must stop after STEP 1 verification and review.
