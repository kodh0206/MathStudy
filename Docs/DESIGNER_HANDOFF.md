# MathGame Designer Handoff

This guide describes the presentation assets that exist now. Gameplay remains owned by the Domain/Application assemblies; visual assets must not become gameplay authorities.

## 1. Gameplay Screen Structure

`Assets/Scenes/GameScene.unity` is the entry scene. Its managed presentation is an instance of `Assets/MathGame/Prefabs/Core/GameRoot.prefab`:

```text
GameScene
├── GameController
├── PrototypeGameSceneComposition
├── GameRoot
│   ├── GameplayRoot (Canvas, CanvasScaler, GraphicRaycaster)
│   │   ├── BoardSlot
│   │   │   └── BoardView
│   │   │       ├── CellRoot
│   │   │       │   └── Cell_0_0 ... Cell_7_7
│   │   │       ├── BlockRoot
│   │   │       ├── EffectRoot
│   │   │       └── SelectionLine
│   │   └── EffectSlot
│   ├── UIRoot
│   │   └── PrototypeCanvas (Canvas, CanvasScaler, PrototypeUILayout)
│   │       └── SafeArea
│   │           ├── TopSlot/HUD
│   │           │   ├── MainStats/Target, Moves, Score
│   │           │   ├── RunStats/Time, Fever, Combo, Tier
│   │           │   ├── Resources
│   │           │   └── Objectives
│   │           ├── CenterSlot/BoardArea
│   │           ├── BottomSlot/BottomHUD
│   │           │   ├── Status
│   │           │   ├── SelectionSum
│   │           │   └── Actions
│   │           └── OverlaySlot
│   │               ├── StageClearPopup
│   │               └── RunResultPopup
│   └── PresentationRoot
└── Main Camera
```

The Stage-only objects are retained for compatibility but hidden in Continuous Run mode.

## 2. Main Prefabs

| Visual area | Asset / prefab | Purpose | Safe to edit? |
|---|---|---|---|
| Composition | `Assets/MathGame/Prefabs/Core/GameRoot.prefab` | Serialized roots, slots, HUD bottom area and required references | Limited |
| HUD | `Assets/MathGame/Prefabs/UI/HUD.prefab` | Target, Score, Time, Fever, Combo and Tier | Yes, preserve named bindings |
| Board | `Assets/MathGame/Prefabs/Board/Board.prefab` | Serialized 8×8 visual capacity and selection line | Limited |
| Cell/block | `Assets/MathGame/Prefabs/Board/Cell.prefab` | Actual active number block, cell and obstacle visuals | Yes, preserve component references |
| Legacy block | `Assets/MathGame/Prefabs/Board/Block.prefab` | Registered legacy world-space block asset; not the active prebuilt-cell number visual | Do not use as the primary redesign target |
| Result | `Assets/MathGame/Prefabs/UI/RunResultPopup.prefab` | Run summary and Play Again; New Best is not currently implemented | Yes, preserve required references |
| Removal effect | `Assets/MathGame/Prefabs/Effects/BlockRemovalEffect.prefab` | Pooled correct-answer removal feedback | Yes |
| Registry | `Assets/MathGame/Prefabs/MathGamePrefabRegistry.asset` | Serialized prefab lookup | References only |

## 3. What Designers Can Change

- Image colors and sprites, fonts, font sizes and text alignment.
- Panel backgrounds, spacing, padding and visual hierarchy inside the existing bound roots.
- `Cell.prefab` background and number/obstacle appearance.
- Selection-line color and width through `SelectionLineGraphic`.
- Removal-effect dot colors, sprites, UI materials and sizes, plus the exposed duration and travel distance.
- Run Result panel appearance and button styling.
- Script-exposed presentation durations and visual intensity values.

Use Prefab Mode and keep changes as prefab overrides/assets. Do not edit generated YAML manually.

## 4. What Designers Should Not Change

- Do not rename or remove paths used by `PrototypeUILayout`: `MainStats/Target/Value`, `MainStats/Score/Value`, `RunStats/*/Value`, `BottomHUD/SelectionSum`, or the named action buttons.
- Do not remove `GamePresentationHost`, `PrototypeUILayout`, `GameplayPresentationRoot`, `PrototypeCellView`, `RunResultPopupView`, `SelectionLineGraphic`, or `BlockRemovalEffectView`.
- Do not change the serialized row/column identity on a cell or create duplicate positions.
- Do not move `BoardView` out of `GameplayRoot/BoardSlot`, create another BoardView, or delete prebuilt cells.
- Do not detach `RunResultPopup` from `OverlaySlot` or the effect slot from `GameplayRoot`.
- Do not replace button objects without restoring their serialized `Button` and view references.
- Do not put gameplay state or rules into Animator events, particles, or UI scripts.

The normal builder preserves current-contract designer assets. The explicit **Recreate Prototype Prefabs** command is destructive and should not be used after art production begins without source-control review.

## 5. Block Design

Edit `Assets/MathGame/Prefabs/Board/Cell.prefab`:

- Cell surface: root `Image`.
- Number container: `BlockRoot`.
- Number font, color and size: `BlockRoot/ValueText` (`UnityEngine.UI.Text`). Runtime assigns the number and value palette color.
- Obstacle label: `ObstacleRoot/ObstacleText`.
- Selected state: `PrototypeCellView` script animates root scale and background tint.
- Removal/arrival/damage feedback: `PrototypeCellView` animates scale by coroutine.
- Removal particles: edit `Assets/MathGame/Prefabs/Effects/BlockRemovalEffect.prefab`.

Keep `PrototypeCellView`'s background, value text, obstacle text, block root and obstacle root references assigned.

## 6. Board Design

`GameRoot/GameplayRoot/BoardSlot` controls the board viewport. `GameplayPresentationRoot` fits the active logical rectangle into the serialized cells; `PrototypeCellView.SetGridLayout` controls cell anchors and currently applies 6 pixels of cell padding.

The Board prefab has no dedicated art background today; visible board color comes from each Cell root Image. Add decorative content only if it does not intercept raycasts or alter Cell rects. `BoardView/SelectionLine` uses `SelectionLineGraphic`, with script-driven points. Width/color are designer-editable; its RectTransform and component must remain.

Gravity and refill visuals are script-owned reactions on the same prebuilt CellViews. Never rearrange cell identity to simulate movement.

## 7. HUD Design

- Target and Score: `Assets/MathGame/Prefabs/UI/HUD.prefab` → `MainStats`.
- Survival Time, Fever, Combo and Difficulty: the same prefab → `RunStats`.
- Current Sum: `Assets/MathGame/Prefabs/Core/GameRoot.prefab` → `UIRoot/PrototypeCanvas/SafeArea/BottomSlot/BottomHUD/SelectionSum`.
- Pause/Resume: the same BottomHUD → `Actions/Restart`; Continuous Run relabels and binds this serialized button as Pause/Resume.

`PrototypeUILayout` updates values, selection-sum target-match color/pulse, low-time warning, combo/target/Fever pulses, and pause labels. Preserve its bound hierarchy and avoid an Animator that also drives the same text scale, color, or anchored position.

## 8. Result UI

Edit `Assets/MathGame/Prefabs/UI/RunResultPopup.prefab`. `RunResultPopupView` owns the localized summary binding, Play Again button binding and a short script-driven scale entrance. Preserve `Result` and `PlayAgainButton` references/hierarchy required by the component. A **New Best** label/presentation is not currently implemented in this popup; do not design against a nonexistent binding. Do not add an Animator that drives the popup root scale unless the script transition is first migrated.

## 9. Particles / Effects

`Assets/MathGame/Prefabs/Effects/BlockRemovalEffect.prefab` is the theme-neutral removal effect. The registry reference is `MathGamePrefabRegistry.BlockRemovalEffectPrefab`. Runtime pools instances under `GameplayRoot/EffectSlot`, places them at removed cell positions, and releases them without gating board mutation or presentation acknowledgement.

The current placeholder is eight UI `Image` dots, not a Unity `ParticleSystem`. Designers may change each Image sprite, UI material, color and size, and tune `BlockRemovalEffectView` duration/travel distance in the Inspector. Preserve `BlockRemovalEffectView`, keep the effect self-contained, and do not add gameplay callbacks. A later visual redesign may replace this prefab with another self-contained implementation while keeping the same view contract. Fever, answer, timing and obstacle cues currently use script-driven transforms/colors plus synthesized placeholder audio rather than separate designer effect prefabs.

## 10. Localization Constraints

The project uses Unity Localization through `MathGameLocalization` and `LocalizationSettings`. It does **not** currently use `LocalizeStringEvent` or TMP; visible text is legacy `UnityEngine.UI.Text` updated by scripts. English and Korean string tables are generated by the localization builder.

Do not replace runtime-bound labels with hard-coded text. Allow additional width for Korean and English, keep best-fit/wrapping settings, and validate both locales after font changes.

## 11. Responsive Layout

Both presentation Canvases use **Scale With Screen Size**, reference resolution **1080×1920**, Match Width Or Height at 0.5. `PrototypeUILayout` applies `Screen.safeArea`. Preserve the SafeArea stretch anchors, Top/Center/Bottom/Overlay slots, BoardSlot bounds and layout groups. Validate 1080×1920, 720×1280 and 1440×2560 portrait, including notched devices.

## 12. Animation Ownership

- Current Sum, HUD warnings and stat feedback: `PrototypeUILayout` coroutines.
- Cell selection/removal/arrival/obstacle damage: `PrototypeCellView` coroutines.
- Selection path: `SelectionLineGraphic` script.
- Run Result entrance: `RunResultPopupView` coroutine.
- Removal effect: `BlockRemovalEffectView` drives the prefab's UI `Image` particles. There is no `ParticleSystem` dependency in the current mobile-friendly placeholder.
- Audio/haptics: `PlaceholderPresentationFeedback`.
- DOTween: not used in the current Presentation implementation.
- Animator: not used by current managed presentation prefabs.

Avoid animating the same scale, color, or anchored-position properties with a new Animator until the corresponding script ownership is deliberately migrated.

## Design Modification Map

Change number block design  
→ Edit `Assets/MathGame/Prefabs/Board/Cell.prefab` (`BlockRoot` and `ValueText`)

Change cell/board surface  
→ Edit `Assets/MathGame/Prefabs/Board/Cell.prefab`; keep `Board.prefab` cell identities intact

Change selection line  
→ Edit `Assets/MathGame/Prefabs/Board/Board.prefab` → `SelectionLine`

Change Target or Score panel  
→ Edit `Assets/MathGame/Prefabs/UI/HUD.prefab` → `MainStats`

Change Time, Fever, Combo or Tier  
→ Edit `Assets/MathGame/Prefabs/UI/HUD.prefab` → `RunStats`

Change Current Sum  
→ Edit `Assets/MathGame/Prefabs/Core/GameRoot.prefab` → `BottomHUD/SelectionSum`

Change Pause button  
→ Edit `Assets/MathGame/Prefabs/Core/GameRoot.prefab` → `BottomHUD/Actions/Restart`

Change removal particle  
→ Edit `Assets/MathGame/Prefabs/Effects/BlockRemovalEffect.prefab`

Change Run Result or Play Again  
→ Edit `Assets/MathGame/Prefabs/UI/RunResultPopup.prefab`
