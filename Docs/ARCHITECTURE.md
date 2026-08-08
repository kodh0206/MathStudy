# MathGame Architecture (Current)

Last inspected: 2026-08-08

This file describes only code present in the repository. Proposed gameplay architecture belongs in the relevant STEP design until it is approved and implemented.

## Project baseline

- Unity 6000.3.6f1, Universal Render Pipeline 17.3.0, Input System 1.18.0, and Unity Test Framework 1.6.0.
- The only enabled build scene is `Assets/Scenes/SampleScene.unity`.
- Runtime code is under `Assets/MathGame/Runtime`; tests are under `Assets/MathGame/Tests`.
- The logical board, deterministic initial population, and orthogonal connection-path domains exist. No playable board view/input adapter, target, answer resolution, objective, Fever, obstacle behavior, restoration, gameplay UI, analytics adapter, ad adapter, or concrete save repository exists.

## Assembly boundaries

```text
MathGame.Core        (no custom assembly dependency)
MathGame.Save        (no custom assembly dependency)
MathGame.Board       (no custom assembly dependency; no UnityEngine reference)
MathGame.BoardGeneration ---> MathGame.Board, MathGame.Core (no UnityEngine reference)
MathGame.Connection  ---> MathGame.Board (no UnityEngine reference)
MathGame.Answer      ---> MathGame.Connection, MathGame.Core, MathGame.Stage (no UnityEngine reference)
MathGame.BoardResolution ---> MathGame.Board, MathGame.Answer, MathGame.Connection, MathGame.Core (no UnityEngine reference)
MathGame.Stage  ---> MathGame.Core
MathGame.App    ---> MathGame.Core, MathGame.Stage, MathGame.Save

EditModeTests   ---> MathGame.Core, MathGame.Stage, MathGame.Save, MathGame.Board, MathGame.BoardGeneration, MathGame.Connection
PlayModeTests   ---> MathGame.App, MathGame.Core, MathGame.Stage
```

The Board, BoardGeneration, Connection, and test assemblies are not auto-referenced. All three domain assemblies set `noEngineReferences` to enforce their Unity-free boundaries. Other runtime assemblies remain auto-referenced. Edit Mode tests are Editor-only.

## Existing systems

### Core services

- `IRandomSource` and `SystemRandomSource` provide injectable integer/float randomness. Seeded construction enables deterministic tests.
- `ITimeProvider` and `UnityTimeProvider` expose real-time seconds. No interactive-time accumulator exists yet.
- `IGameLogger` and `UnityGameLogger` isolate basic logging calls from domain classes.

### Stage lifecycle

- `StageController` is a plain C# state owner with transition results and `StateChanged` events.
- Foundation initialization ends in non-interactive `Ready`; a blank stage never advertises player input.
- It implements initialization, nested pause reasons, resume-to-previous-state, guarded terminal commands, and exit.
- `StageController` exposes the STEP 5 target-presentation, player-input, answer-resolution, and same-target miss-return transitions. Fever state names remain reserved and unreachable until their owning STEP.
- Player input eligibility is derived from `PlayerInput` or `FeverInput`.
- `Complete` and `Fail` are invalid from `None`, `Initializing`, and `Ready`. Later objective/session orchestration must become the authorized terminal caller.

### Application composition and lifecycle

- `MathGameBootstrapInstaller` creates a bootstrap before scene load if one is absent.
- `MathGameBootstrap` is a persistent MonoBehaviour and the current composition root. It constructs logger, time, randomness, and stage controller directly.
- A static instance field prevents duplicate bootstrap objects; the bootstrap clears the guard and exits its stage on destruction.
- `ApplicationLifecycleRelay` caches nullable latest pause/focus facts before publishing Unity lifecycle events. Nullable values distinguish an unreported fact from an active state.
- The bootstrap checks both initialization transitions, defers pre-initialization lifecycle mutation, and reconciles cached inactive facts after reaching `Ready`.
- Live and reconciled facts use the same idempotent pause-reason synchronization. Background and focus-loss reasons remain independent, so clearing one cannot resume while the other remains.

### Persistence seam

- `SaveData` contains only schema version 1.
- `ISaveRepository` defines load/save operations. No implementation, migration, validation, or progression model exists.

### Board domain

- `BoardTopology` owns positive rectangular extents and an immutable active-cell mask. It distinguishes out-of-bounds positions, inactive holes, and active cells.
- Coordinates use a lower-left origin and enumerate active cells row-major. Orthogonal neighbors enumerate deterministically as Up, Right, Down, Left while omitting bounds and holes.
- `BlockId` and `NumberBlock` are immutable positive value types. A block has stable logical identity independent of GameObjects; equal numeric values with different IDs are valid.
- `Board` owns dense mutable cell state plus a unique live-ID index. Each active cell has independent optional number occupancy and Open/Blocked access.
- Immutable cell snapshots expose state without leaking mutable arrays or cells.
- Place, remove, relocate, and access changes return explicit results. Failed mutations leave cells, block count, and ID index unchanged.
- The board contains no generation policy, selection, target, gravity, obstacle behavior, events, presentation, time, randomness, or persistence logic.

### Initial board generation

- `BoardGenerationConfig` is immutable request data containing topology, inclusive number bounds, and the first board-local block ID. Validation is centralized in the generator so expected invalid content produces stable failure results.
- `BoardGenerator` is a stateless plain C# service with an injected `IRandomSource`. It never creates or owns a seed and never uses Unity randomness.
- Generation validates the entire request before consuming randomness, creates a fresh Board, then fills active positions in topology row-major order.
- Each active position consumes exactly one integer draw and one sequential ID. Inactive holes consume neither. Prototype callers use 5×5 and values 1–9; the generator itself accepts other valid configured topology/ranges.
- `BoardGenerationResult` exposes either a complete Board plus the next unused ID or a stable failure with no partial Board. ID-capacity arithmetic and inclusive-to-exclusive range conversion are checked before generation.
- Population success is not a solution guarantee. No target, path search, retry, shuffle, refill, Stage transition, or input enabling exists in BoardGeneration; STEP 7 must verify a target path before gameplay exposure.

### Connection path

- `ConnectionPath` is a mutable plain C# owner bound to one Board. It reads cell snapshots but never mutates Board state.
- It captures ordered positions and immutable block identities/values, tracks selected membership, and maintains a checked `long` live sum.
- First selection requires an active Open occupied cell. Later additions require Manhattan distance one; diagonals, gaps, holes, empty/blocked cells, and duplicate positions are rejected atomically.
- Entering the immediate predecessor removes only the tail. Other selected positions are rejected. Explicit Cancel clears the complete path and is idempotent.
- `ConnectionPathSnapshot` copies entries into read-only historical data that is stable after later path or Board mutations.
- Connection contains no target comparison, answer submission, timing, Board resolution, Unity input, or presentation behavior.

### Answer validation and interactive timing

- `AnswerValidator` purely classifies immutable connection snapshots against positive targets. Correct requires exact sum and at least two blocks; under, over, and one-block equality are Miss outcomes. Correct grades use inclusive 2/4-second thresholds.
- `InteractiveAnswerClock` subscribes to Stage state, samples injected unscaled time, and accumulates only while Stage accepts player input. Presentation, resolution, every pause reason, and app inactivity are excluded.
- Clock state/command/fault results are explicit. Nonfinite or backward time faults rather than silently changing a grade; disposal unsubscribes safely.
- Stage now exposes the authoritative phase graph `Ready/ResolvingAnswer -> PresentingTarget -> PlayerInput -> ResolvingAnswer`, plus same-target miss return `ResolvingAnswer -> PlayerInput`. These commands add no Board resolution or target selection.

### Board resolution

- `BoardResolver` validates a Correct AnswerResult against exact current block positions, IDs, and values, then constructs a replacement Board without mutating the source.
- Gravity moves toward decreasing Row within vertically contiguous active segments. Inactive topology holes split segments; blocks never cross holes or columns.
- Survivors retain identity/value. Refills use injected randomness, configured inclusive bounds, sequential IDs, and deterministic column/segment/bottom-up traversal.
- Results contain copied read-only removed, moved, and spawned deltas plus the next unused ID. Expected failures expose no Board/deltas and consume no randomness during preflight.
- Basic resolution requires every active source cell to be Open and occupied. Generic blocked/obstacle states remain unsupported until STEP 10 defines their distinct semantics.

## Current runtime flow

```text
BeforeSceneLoad -> create bootstrap -> Awake composes services
               -> Start -> StageController.Start
                        -> FinishInitialization -> Ready (non-interactive)
                        -> reconcile cached pause/focus facts

Unity pause/focus -> lifecycle relay caches fact -> bootstrap
                  -> before initialization: defer
                  -> after initialization: synchronize matching pause reason
Bootstrap destroy -> unsubscribe -> StageController.Exit -> clear singleton guard
```

The stage is currently called “blank” because `Ready` has no board, target, input, or views. A later target/gameplay STEP must explicitly enter an interactive state.

## Verification already represented by tests

- Edit Mode (117 tests): foundation through BoardResolution coverage, including identity-safe atomic failure, rectangular/masked multi-segment gravity, deterministic refill/IDs/deltas, capacity and random-fault boundaries, source independence, delta/index coherence, stale repeat rejection, and all earlier regressions.
- Play Mode (19 tests): bootstrap/lifecycle regressions plus Stage-driven interactive timing, same-target miss continuation, nested pause exclusions, stopping, reset, faults, and disposal.

Verified with Unity 6000.3.6f1 on 2026-08-08 after STEP 6: Edit Mode 117/117 and Play Mode 19/19 passed with valid result XML. BoardResolution and affected assemblies compiled with no C# errors.

## Known architectural risks

- `StageState` anticipates future states without implemented transition commands; later STEP designs must confirm or revise these names rather than assuming them correct.
- `MathGameBootstrap` directly constructs concrete services. This is adequate for the current small foundation but will need a deliberate composition strategy as gameplay dependencies arrive.
- There is no explicit interactive-time owner; `UnityTimeProvider` alone must not be used to classify answer speed.
- There is no scene/content bootstrap beyond the persistent application object.
- Save-on-background, interrupted-stage restoration, and platform-specific lifecycle sequences still require their owning later STEP designs and device verification.
- Board access is intentionally a minimal Open/Blocked fact. STEP 10 must derive or replace it from approved layered obstacle state so independent flags do not become competing rule authorities.
- Exact gravity traversal through masked shapes and content connectivity rules remain deferred to their owning designs.
- Initial population is deliberately not a playability guarantee. Available-path search, target safety, and deadlock recovery remain mandatory in STEP 7 before input can be enabled.
