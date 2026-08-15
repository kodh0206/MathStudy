# Production P07-P08: Run Records and Local Save

## Scope

P07 consumes finalized Continuous Run results and updates player records. P08 persists only those records locally for prototype/offline use. Backend SDKs, accounts, cloud synchronization, conflict resolution, stage/world progression, settings, economy, and meta progression are excluded.

## P07 ownership

`RunResult` remains the authoritative completed-run input and now includes a stable `RunId`. `MathGame.PlayerProgress` is Unity-independent and owns:

- `RunRecords`: Best Score, best active Survival duration, highest difficulty tier, best Fever combo, and total runs.
- `PlayerProgress`: records plus the immutable set of applied run identities.
- `PlayerProgressService`: deterministic max comparisons, checked total-run increment, duplicate suppression, and personal-best flags.
- `IPlayerProgressRepository`: the storage abstraction consumed by outer composition.

Presentation never recalculates records. Applying an already-seen `RunId` is an unchanged duplicate result.

## P08 ownership

`MathGame.LocalSave` is Unity-facing infrastructure. `LocalPlayerProgressRepository` maps runtime progress to schema-version 1 JSON at `Application.persistentDataPath/player_progress.json`.

Save order:

1. Map and validate a detached DTO.
2. Write `player_progress.json.tmp`.
3. Read the temporary file back, deserialize it, and validate that payload.
4. Copy the previous primary to `player_progress.backup.json` only when that primary is valid.
5. Replace the primary with the verified temporary file.

Load order is valid primary, valid backup, then new-player defaults. A missing file is normal. Invalid data and IO failures return typed results and diagnostics rather than throwing into gameplay.

## Runtime flow

`PrototypeGameSceneController` constructs the repository at composition, loads progress once, and creates `PlayerProgressService`. On the exactly-once Run end, it applies `RunResult`, saves the updated snapshot, and then shows the existing result UI. Play Again does not apply the result again. If saving fails, the failure is logged and restart is withheld so the same in-memory snapshot can be retried.

## Verification

Static builds cover SurvivalRun, PlayerProgress, LocalSave, Presentation, and Edit Mode test assemblies. Tests cover defaults, first/higher/lower records, multiple personal-best flags, duplicate suppression, overflow rollback, versioned round trip, repository recreation, missing data, malformed primary backup recovery, and invalid-data fallback.

Unity Edit/Play execution and device/WebGL persistence behavior remain manual verification requirements.
