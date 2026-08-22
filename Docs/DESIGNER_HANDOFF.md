# MathGame Production Presentation — Designer Handoff

## Current production hierarchy

```text
GameScene
├── GameController
│   ├── MathGameBootstrap
│   └── ApplicationLifecycleRelay
├── GameSceneComposition
│   ├── PrototypeGameSceneController (serialization-safe retained name)
│   └── PortraitOnlyPolicy
└── GameRoot (prefab instance)
    ├── GameplayRoot
    │   ├── Backdrop
    │   ├── BoardSlot
    │   │   └── BoardView
    │   │       ├── CellRoot / 64 serialized CellViews
    │   │       ├── BlockRoot
    │   │       ├── EffectRoot
    │   │       └── SelectionLine
    │   └── EffectSlot
    ├── UIRoot
    │   └── PrototypeCanvas (serialization-safe retained name)
    │       └── SafeArea
    │           ├── TopSlot/HUD/RunHUD
    │           │   ├── SurvivalPanel
    │           │   ├── ScorePanel
    │           │   ├── TargetPanel
    │           │   ├── SecondaryStats
    │           │   └── FeverPanel
    │           ├── CenterSlot
    │           ├── BottomSlot/BottomHUD
    │           │   ├── Status
    │           │   ├── SelectionSum
    │           │   └── Actions
    │           │       ├── RetryTarget
    │           │       └── Language
    │           └── OverlaySlot/RunResultPopup
    └── PresentationRoot
```

There is intentionally no manual Pause control. Background, focus-loss, and platform interruption pausing remains application-owned.

## Production visual source of truth

| Area | Asset | Designer may edit |
|---|---|---|
| Run HUD | `Assets/MathGame/Prefabs/UI/HUD.prefab` | Fonts, colors, spacing, anchors, gauge appearance |
| Board | `Assets/MathGame/Prefabs/Board/Board.prefab` | Board frame/background and selection-line appearance |
| Cell | `Assets/MathGame/Prefabs/Board/Cell.prefab` | Cell background, border, number and obstacle typography |
| Block | `Assets/MathGame/Prefabs/Board/Block.prefab` | Optional block visual styling |
| Run result | `Assets/MathGame/Prefabs/UI/RunResultPopup.prefab` | Result layout, typography and Play Again appearance |
| Composition | `Assets/MathGame/Prefabs/Core/GameRoot.prefab` | Slot layout and prefab composition; preserve host references |

Runtime binds state into these serialized assets. It does not instantiate another BoardView or rebuild permanent cells.

## Binding contracts — do not break

- Keep `GamePresentationHost` references assigned.
- Keep `BoardView` below `GameplayRoot/BoardSlot`.
- Keep exactly one serialized `GameplayPresentationRoot` and its 64 coordinate-stable `PrototypeCellView` children.
- Keep `RunHUDView`, `RunResultPopupView`, and `SelectionLineGraphic` references assigned.
- Keep `SelectionSum` as Current Sum. It uses authoritative `ConnectionPathSnapshot.Sum`; do not calculate it in UI.
- Decorative graphics above the board must not intercept raycasts.

## Responsive layout

- Canvas Scaler: Scale With Screen Size.
- Reference resolution: 1080 × 1920.
- Match Width Or Height: 0.5.
- `PrototypeUILayout` applies `Screen.safeArea` and fits the board into BoardSlot.
- Validate 720 × 1280, 1080 × 1920, and 1440 × 2560 portrait.

## Animation ownership

- HUD feedback and Current Sum pulses: `PrototypeUILayout`.
- Cell selection/removal/arrival/damage: `PrototypeCellView`.
- Connection line: `SelectionLineGraphic`.
- Run result entrance: `RunResultPopupView`.

Avoid adding an Animator that drives the same scale, color, or anchored position unless the script-owned effect is migrated first.

## Localization

Runtime labels use Unity Localization tables for English and Korean. Do not replace bound labels with permanent hard-coded player-facing text. Verify both locales after hierarchy changes.

## Editor workflow

Use:

- `MathGame/Production/Build Game Scene`
- `MathGame/Production/Validate Game Scene`
- `MathGame/Production/Validate Production Prefabs`

The older Prototype-named commands remain compatibility aliases. Do not use `MathGame/Development/Recreate Prototype Prefabs` after designer work begins; it is explicitly destructive.

## Legacy / Do Not Edit

Domain Stage mode remains for compatibility and tests, but Moves/Objectives/Restoration/Stage Clear UI is not part of the primary Continuous Run presentation.

These active production components retain `Prototype` names because an ad-hoc rename risks Unity script GUID and serialized-reference breakage:

- `PrototypeGameSceneController`
- `PrototypeUILayout`
- `PrototypeCellView`
- `PrototypeGeneratedRoot`

Do not remove them. A future scripted serialization migration may rename them safely.

## Manual acceptance checklist

1. Open GameScene and confirm one GameRoot and one BoardView.
2. Confirm Time, Target, Score, Combo, Fever, Current Sum, and Difficulty update.
3. Drag an orthogonal path; Current Sum updates immediately and resets after release.
4. Confirm removal effects, gravity, refill, Fever, and target recovery.
5. Let Time reach zero; only RunResultPopup appears.
6. Press Play Again; the same BoardView is reused and transient effects reset.
7. Confirm no Moves, Objectives, Restoration, Stage Clear, Next Stage, Continue, Retry-stage, Abandon, or manual Pause UI appears.
8. Verify English and Korean, all three portrait resolutions, and focus/background resume behavior.
