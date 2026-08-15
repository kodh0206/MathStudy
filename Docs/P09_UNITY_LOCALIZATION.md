# Production P09: Unity Localization

## Scope

P09 uses Unity Localization 1.5.12 for the primary Continuous Run HUD, Run Result, common controls, and language selection. Supported locales are English (`en`) and Korean (`ko`). Gameplay assemblies remain independent of Localization APIs.

## Tables and keys

- `Gameplay`: time, target, score, combo, Fever, difficulty tier, selected sum.
- `Result`: summary, Play Again, Best Score, New Best.
- `Common`: Pause, Resume, Restart, Confirm, Cancel, Back.
- `Settings`: Language, language selector, language-changed feedback, Korean, English.

The Editor command `MathGame/Localization/Build Korean and English Tables` creates or updates official Locale assets and String Table Collections. `MathGame/Build Prototype Scene` invokes it before migrating the managed presentation prefab contract to version 7.

## Runtime policy

Startup waits for `LocalizationSettings` initialization. A saved supported locale wins; otherwise Korean devices select `ko`, and every other/unsupported device locale falls back to `en`. The serialized Language button toggles `LocalizationSettings.SelectedLocale` without restarting and saves the selected code through the existing P08 repository.

Primary HUD and result strings are retrieved from Unity's String Database with stable semantic keys. Raw values remain gameplay data. Missing entries produce an explicit diagnostic and visible bracketed key rather than a blank label.

## Save compatibility

Local progress schema version 2 adds optional settings with `localeCode`. Version 1 records remain valid and load with an unspecified locale, after which startup applies device/default selection. Run records and applied Run IDs are preserved unchanged when language changes.

## Manual migration

1. Allow Unity to compile scripts.
2. Run `MathGame/Build Prototype Scene`.
3. Approve the managed prefab migration from contract v6 to v7.
4. Confirm `Assets/MathGame/Localization` contains `en`, `ko`, and four String Table Collections.
5. Save `GameScene`.

Unity Play Mode and portrait layout verification in both languages remain required.
