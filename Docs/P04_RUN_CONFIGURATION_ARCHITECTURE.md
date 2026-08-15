# P04 - Run Configuration Architecture

## Existing configuration

P03 used one validated `SurvivalRunConfig` constructor containing Time, recovery, uniform tier cadence, and target ranges. Composition selected a static temporary instance.

## Hard-coded values found

The balance values existed only in `SurvivalRunConfig.TemporaryPrototype`; target search path length/cap and board generation range remain separate approved system configuration. No additional Survival recovery constants were found in gameplay systems.

## New runtime configuration model

- `SurvivalTimeSettings`: initial, maximum, and drain.
- `TimingRecoverySettings`: Normal, Fast, and Perfect recovery.
- `DifficultyTierConfig`: contiguous tier ID, explicit correct-cycle unlock threshold, and target range.
- `SurvivalRunConfig`: validated aggregate and deterministic capped tier lookup.

The legacy uniform-tier constructor remains for compatibility. Gameplay consumes only resolved immutable configuration and knows nothing about CSV or JSON.

## Validation rules

- Initial and maximum Time are finite and positive; maximum is not below initial.
- Drain and every recovery are finite and nonnegative.
- At least one tier exists.
- Tier IDs are contiguous and ordered from 1.
- First threshold is zero; later thresholds strictly increase.
- Target minimum is positive and maximum is not below minimum.
- Counts and threshold multiplication use checked arithmetic.

## Tests and compatibility

Tests cover invalid tier identity/ordering, legacy uniform construction, threshold boundaries, maximum-tier capping, and all prior P03 timing behavior. P03 values and rules remain unchanged by P04.

## Status

Complete. Static compilation passed; Unity test execution remains manual.
