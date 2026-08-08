# MathGame Architecture (Current)

Last inspected: 2026-08-08

This file describes only code present in the repository. Proposed gameplay architecture belongs in the relevant STEP design until it is approved and implemented.

## Project baseline

- Unity 6000.3.6f1, Universal Render Pipeline 17.3.0, Input System 1.18.0, and Unity Test Framework 1.6.0.
- The only enabled build scene is `Assets/Scenes/SampleScene.unity`.
- Runtime code is under `Assets/MathGame/Runtime`; tests are under `Assets/MathGame/Tests`.
- No board, number block, selection, target, objective, Fever, obstacle, restoration, gameplay UI, analytics adapter, ad adapter, or concrete save repository exists.

## Assembly boundaries

```text
MathGame.Core        (no custom assembly dependency)
MathGame.Save        (no custom assembly dependency)
MathGame.Stage  ---> MathGame.Core
MathGame.App    ---> MathGame.Core, MathGame.Stage, MathGame.Save

EditModeTests   ---> MathGame.Core, MathGame.Stage, MathGame.Save
PlayModeTests   ---> MathGame.App, MathGame.Core, MathGame.Stage
```

All runtime assemblies are auto-referenced. Test assemblies are not auto-referenced; Edit Mode tests are Editor-only.

## Existing systems

### Core services

- `IRandomSource` and `SystemRandomSource` provide injectable integer/float randomness. Seeded construction enables deterministic tests.
- `ITimeProvider` and `UnityTimeProvider` expose real-time seconds. No interactive-time accumulator exists yet.
- `IGameLogger` and `UnityGameLogger` isolate basic logging calls from domain classes.

### Stage lifecycle

- `StageController` is a plain C# state owner with transition results and `StateChanged` events.
- It implements initialization, normal/terminal states, nested pause reasons, resume-to-previous-state, completion, failure, and exit.
- `StageState` names future concepts (`PresentingTarget`, `ResolvingAnswer`, and Fever states), but the controller currently exposes no public transitions into those states. Their presence is not gameplay implementation.
- Player input eligibility is derived from `PlayerInput` or `FeverInput`.

### Application composition and lifecycle

- `MathGameBootstrapInstaller` creates a bootstrap before scene load if one is absent.
- `MathGameBootstrap` is a persistent MonoBehaviour and the current composition root. It constructs logger, time, randomness, and stage controller directly.
- A static instance field prevents duplicate bootstrap objects; the bootstrap clears the guard and exits its stage on destruction.
- `ApplicationLifecycleRelay` translates Unity pause/focus callbacks into events. The bootstrap maps those events to independent nested pause reasons.

### Persistence seam

- `SaveData` contains only schema version 1.
- `ISaveRepository` defines load/save operations. No implementation, migration, validation, or progression model exists.

## Current runtime flow

```text
BeforeSceneLoad -> create bootstrap -> Awake composes services
               -> Start -> StageController.Start
                        -> FinishInitialization -> PlayerInput

Unity pause/focus -> lifecycle relay -> bootstrap -> StageController pause/resume
Bootstrap destroy -> unsubscribe -> StageController.Exit -> clear singleton guard
```

The stage is currently called “blank” because entering `PlayerInput` does not create gameplay state or views.

## Verification already represented by tests

- Edit Mode: seeded randomness, save schema default, stage initialization order, nested/duplicate/unknown pause handling, terminal transitions, exit cleanup, and invalid transitions.
- Play Mode: automatic blank-stage initialization, nested Unity pause/focus forwarding, and bootstrap destruction/recreation.

Test source exists, but a successful run in the current workspace has not yet been established. The existing ignored `Unity-EditMode.log` points at a different project path and exits with code 1, so it is not valid evidence for this checkout. A 2026-08-08 batch attempt for this checkout crashed while opening Unity's global `CurlRequestCache.db` and produced no test-result XML; this is an environment/runner failure, not a test pass or a test assertion failure.

## Known architectural risks

- The authoritative `Docs/GAME_DESIGN.md` is missing, so the existing lifecycle cannot be confirmed against final gameplay requirements.
- A pause/focus-loss callback between `Awake` and `Start` can move the controller from `Initializing` to `Paused`. `MathGameBootstrap.Start` then attempts initialization commands that fail, and the later resume can leave the stage stuck in `Initializing`. This is an unresolved P1 foundation finding.
- `StageState` anticipates future states without implemented transition commands; later STEP designs must confirm or revise these names rather than assuming them correct.
- `MathGameBootstrap` directly constructs concrete services. This is adequate for the current small foundation but will need a deliberate composition strategy as gameplay dependencies arrive.
- There is no explicit interactive-time owner; `UnityTimeProvider` alone must not be used to classify answer speed.
- There is no scene/content bootstrap beyond the persistent application object.
