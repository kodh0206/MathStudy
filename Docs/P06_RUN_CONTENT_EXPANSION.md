# P06 - Run Content Expansion

## Difficulty progression and temporary playtest values

| Tier | Unlock correct cycles | Target range |
| ---: | ---: | ---: |
| 1 | 0 | 5-9 |
| 2 | 6 | 7-11 |
| 3 | 12 | 9-13 |
| 4 | 18 | 11-15 |
| 5 | 24 | 13-16 |

Survival remains 35 initial / 45 maximum / drain 1 with recovery 1.5 / 2.75 / 4. All values are **TEMPORARY PLAYTEST VALUES**.

## Target and survival pressure

Only target ranges progress. Every selected target still requires an orthogonal current-board witness. Tier 5 remains active indefinitely and never indexes beyond content. The upper bound 16 matches the useful limit of current 1-4 values and maximum four-block target search.

## Obstacle progression

No obstacle progression is introduced in P06. Dust and Box remain present under their existing deterministic setup and rules; data does not change their pressure.

## Runtime data and tests

The CSV content generates `Assets/MathGame/Resources/RunContent/run-config.json`. Composition now requires this validated runtime content. Tests validate tier ordering, maximum capping, stable JSON conversion/loading, and a proven target for each configured range on a deterministic representative board.

## Human playtest checklist

1. First 30 seconds are readable and Time/recovery are understandable.
2. Tier changes occur after 6/12/18/24 correct cycles.
3. Every target remains solvable and noticeably progresses without arbitrary escalation.
4. Normal/Fast/Perfect recovery differences are visible.
5. Fever/Combo retain their existing rhythm and have no direct Time modifier.
6. Dust/Box remain readable and mechanically unchanged.
7. Late-run Time pressure increases.
8. Tier 5 continues safely without easier wraparound or missing content.
9. Run End occurs once and Play Again resets to tier 1.
10. Addition-only play remains engaging across a multi-minute run.

## Known balance risks

Target range remains a proxy rather than a full solution-quality metric. Extremely fast maximum-tier players may still sustain for long periods. Obstacle progression and time-drain ramps require separate approval and evidence.

## Status

Complete - manual Unity Play Mode verification required.
