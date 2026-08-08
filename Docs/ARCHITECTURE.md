# MathGame Architecture (Current)

Last inspected: 2026-08-08

This file describes only code present in the repository. Proposed gameplay architecture belongs in the relevant STEP design until it is approved and implemented.

## Project baseline

- Unity 6000.3.6f1, Universal Render Pipeline 17.3.0, Input System 1.18.0, and Unity Test Framework 1.6.0.
- The only enabled build scene is `Assets/Scenes/SampleScene.unity`.
- Runtime code is under `Assets/MathGame/Runtime`; tests are under `Assets/MathGame/Tests`.
- The logical board and number-block domain exists. No board generation, playable board instance/view, selection, target, objective, Fever, obstacle behavior, restoration, gameplay UI, analytics adapter, ad adapter, or concrete save repository exists.

## Assembly boundaries

```text
MathGame.Core        (no custom assembly dependency)
MathGame.Save        (no custom assembly dependency)
MathGame.Board       (no custom assembly dependency; no UnityEngine reference)
MathGame.Stage  ---> MathGame.Core
MathGame.App    ---> MathGame.Core, MathGame.Stage, MathGame.Save

EditModeTests   ---> MathGame.Core, MathGame.Stage, MathGame.Save, MathGame.Board
PlayModeTests   ---> MathGame.App, MathGame.Core, MathGame.Stage
```

The Board and test assemblies are not auto-referenced; Board also sets `noEngineReferences` to enforce its Unity-free boundary. Other runtime assemblies remain auto-referenced. Edit Mode tests are Editor-only.

## Existing systems

### Core services

- `IRandomSource` and `SystemRandomSource` provide injectable integer/float randomness. Seeded construction enables deterministic tests.
- `ITimeProvider` and `UnityTimeProvider` expose real-time seconds. No interactive-time accumulator exists yet.
- `IGameLogger` and `UnityGameLogger` isolate basic logging calls from domain classes.

### Stage lifecycle

- `StageController` is a plain C# state owner with transition results and `StateChanged` events.
- Foundation initialization ends in non-interactive `Ready`; a blank stage never advertises player input.
- It implements initialization, nested pause reasons, resume-to-previous-state, guarded terminal commands, and exit.
- `StageState` names future concepts (`PresentingTarget`, `ResolvingAnswer`, and Fever states), but the controller currently exposes no public transitions into those states. Their presence is not gameplay implementation.
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
- The board contains no generation, selection, target, gravity, obstacle behavior, events, presentation, time, randomness, or persistence logic.

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

- Edit Mode (48 tests): the 16 STEP 1 foundation tests plus coordinate/value invariants, rectangular/masked topology, boundary shapes, deterministic enumeration/neighbors, hole/empty distinction, block identity/indexing, access state, and atomic placement/removal/relocation result contracts.
- Play Mode (10 tests): automatic `Ready` initialization, early background/focus reconciliation, both callback orders, stale-fact clearing, duplicate callback idempotence, nested reason clearing, duplicate ownership, and destruction/recreation.

Verified with Unity 6000.3.6f1 on 2026-08-08 after STEP 2: Edit Mode 48/48 passed and Play Mode regression 10/10 passed with valid result XML. The Unity-free Board and affected test assemblies compiled with no C# errors.

## Known architectural risks

- `StageState` anticipates future states without implemented transition commands; later STEP designs must confirm or revise these names rather than assuming them correct.
- `MathGameBootstrap` directly constructs concrete services. This is adequate for the current small foundation but will need a deliberate composition strategy as gameplay dependencies arrive.
- There is no explicit interactive-time owner; `UnityTimeProvider` alone must not be used to classify answer speed.
- There is no scene/content bootstrap beyond the persistent application object.
- Save-on-background, interrupted-stage restoration, and platform-specific lifecycle sequences still require their owning later STEP designs and device verification.
- Board access is intentionally a minimal Open/Blocked fact. STEP 10 must derive or replace it from approved layered obstacle state so independent flags do not become competing rule authorities.
- Exact gravity traversal through masked shapes and content connectivity rules remain deferred to their owning designs.
