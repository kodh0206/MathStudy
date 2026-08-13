# Unity Prototype Playtest

The prototype is composed at runtime when `Assets/Scenes/GameScene.unity` loads. The installer deliberately does not modify the authored scene or require prefab/Inspector setup.

## Launch

1. Open the project with Unity `6000.3.6f1`.
2. Select `MathGame > Build Prototype Scene`. The builder is idempotent and preserves existing camera/light objects and Build Settings entries.
3. Open `Assets/Scenes/GameScene.unity` if it is not already open.
4. Optionally select `MathGame > Validate Prototype Scene` and confirm the Console reports success.
5. Wait for script compilation to finish with no Console errors, then enter Play Mode.
6. Drag with the left mouse button across orthogonally adjacent number cells and release to submit. A touchscreen uses the primary touch in the same way.

## Scene entry objects

- `PrototypeGameSceneComposition` — authored by the builder; hosts the runtime composition controller.

## Runtime-created objects

- `PrototypeBoardView` — placeholder board cells, number labels, Dust/Box labels, overlay, and feedback.
- `PrototypeSelectionLine` — live connection line.

Authored scene entry objects after running the builder:

- `GameController` — `MathGameBootstrap` and `ApplicationLifecycleRelay`.
- `PrototypeGameSceneComposition` — `PrototypeGameSceneController` and `PortraitOnlyPolicy`.
- A tagged Main Camera.

No prefab, texture, audio clip, save asset, backend, or manually assigned Inspector reference is required.

## Prefab and extension workflow

`MathGame > Build Prototype Prefabs` creates missing prototype assets under `Assets/MathGame/Prefabs` and never overwrites an existing prefab. `Build Prototype Scene` calls the same non-destructive check before instantiating `GameRoot.prefab`.

`MathGamePrefabRegistry.asset` is the single presentation-asset registry. `GameRoot` provides stable `GameplayRoot/BoardSlot/EffectSlot`, `UIRoot` slots, and `PresentationRoot` extension points through `GamePresentationHost`. Objective rows are instantiated from `ObjectiveItem.prefab` based on the session snapshot rather than being fixed to two entries.

To add a later presentation feature, create its view and prefab, add it to the registry, and initialize it against `GamePresentationContext`/the appropriate stable slot. Domain assemblies and the scene hierarchy do not need to change. Designers can edit existing prefab layout, fonts, colors, sprites, spacing, and animations; normal builders reuse those assets unchanged.

## Deterministic stage

- 5x5 board, values 1–4, seed 13012.
- Six moves.
- Objective: remove 12 number blocks and destroy the Dust overlay.
- Dust at column 1, row 1; Box at column 2, row 2.
- Fever threshold 50 and eight interactive seconds.
- Restoration capacity 100, using the STEP 11 rules.

## Manual checks

- Drag/backtrack over adjacent cells; confirm the cyan line and live path update.
- Submit a wrong sum; confirm MISS and no move loss.
- Submit the displayed target; confirm removal, gravity, refill, and a new target.
- Remove the number beneath `D`; confirm Dust disappears and its objective advances.
- Remove numbers beside `B2`; confirm normal damage changes it to `B1`; use Fever adjacency to destroy it in one hit.
- Fill the Fever gauge; confirm entry occurs after target presentation and Fever answers cost no moves.
- Let Fever expire; confirm its end effect resolves before normal input returns.
- Confirm restoration and objective HUD values advance.
- Exhaust moves before finishing; confirm Continue, Retry, and Abandon controls. Continue preserves restoration and grants five moves once; Retry reloads the deterministic stage; Abandon ends the attempt.
- Complete both objectives; confirm Success and world-restoration commit, then restart if desired.

## Verification status

The C# production, Edit Mode test, and Play Mode test assemblies compile outside Unity. Unity behavioral verification must still be run when no other Unity process owns the project and licensing initialization succeeds.
