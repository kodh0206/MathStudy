# P05 - CSV to JSON Run Content Pipeline

## Authoring files and schema

`Assets/MathGame/Content/Authoring/RunConfig.csv` contains one Run row:

`Id,InitialTime,MaximumTime,DrainPerSecond,NormalRecovery,FastRecovery,PerfectRecovery`

`Assets/MathGame/Content/Authoring/RunDifficulty.csv` contains ordered tiers:

`TierId,UnlockCorrectCycles,TargetMin,TargetMax`

## Conversion flow

The Editor-only menu `MathGame/Content/Build Run Content JSON` parses both CSV files with invariant numeric rules, validates headers/rows/duplicates, serializes deterministic schema-v1 JSON, runs the runtime repository validation, and only then replaces the generated JSON.

Gameplay never parses CSV. Runtime loads `Resources/RunContent/run-config.json` through `IRunConfigRepository` / `RunConfigJsonRepository` and receives a validated `SurvivalRunConfig`.

## Validation and failures

Missing fields, malformed numbers, duplicate IDs, invalid target ranges, invalid thresholds, invalid JSON/schema, and invalid aggregate configuration return explicit failures. Content is not silently repaired and composition does not fall back to hidden defaults.

## Tests

Tests cover stable conversion, valid repository resolution, malformed values, duplicate IDs, invalid ranges, missing JSON, and invalid JSON structure. Static runtime/editor/test assembly compilation passed.

## Status

Complete. Editor menu execution and generated-asset import require Unity verification.
