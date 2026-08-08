# STEP 11 Design — Environment Restoration Progress

## Status

**READY FOR IMPLEMENTATION — reward, lifecycle, and world-ledger rules approved; design review required before code.**

This is a design-only artifact. It changes no production behavior. STEP 10 remains structurally accepted with no P0/P1 findings; its remaining P2 Fever-coordinator coverage and Unity licensing verification debt remain open.

## Sources

- `GAME_DESIGN.md`, especially §§3.1–3.2, 8.2, 9.7–9.9, 11.1–11.5, 12.1, and 17.
- `DEVELOPMENT_PLAN.md`, STEP 11.
- `STEP_8_DESIGN.md`, `STEP_9_DESIGN.md`, and `STEP_10_DESIGN.md`.
- Current `ARCHITECTURE.md`, `DECISIONS.md`, StageSession, Fever, obstacle resolution, and ObstacleFlow contracts.
- Approved STEP 11 product addendum supplied after the initial blocked design.

## Goal

Convert committed gameplay outcomes into deterministic integer restoration energy, advance stage-local restoration through typed 25/50/75/100-percent milestones, update restoration objectives in the same StageSession transaction, discard or preserve provisional state according to lifecycle policy, and publish an atomic world-progression commit only on stage Success.

STEP 11 owns arithmetic and semantic progress. It does not own art, animation, scene objects, persistence storage, currency, stars, analytics, or ads.

## Approved rules

### Answer energy

Every successfully committed Correct answer resolution awards base energy `10`. Miss, NoSelection, invalid/rejected/duplicate attempts, failed resolution, and uncommitted plans award zero.

The connection factor uses `AnswerResult.SelectedBlockCount`, never Fever collateral removals or total resolver removals:

| Submitted length | Factor |
|---|---:|
| 1–2 | 1.0 |
| 3 | 1.2 |
| 4 | 1.5 |
| 5+ | 2.0 |

Although a valid Correct currently requires at least two blocks, length 1 remains a defensive factor-table entry and is not a legal committed answer.

Normal answers use Fever factor `1.0`. Fever answers use `2.0`. Fever combo does not multiply restoration in STEP 11.

The exact formula is:

```text
floor(10 × ConnectionFactor × FeverFactor)
```

Composition occurs before one final floor. Implementation uses exact integer/rational arithmetic, not binary floating point: factors are represented as tenths (`10`, `12`, `15`, `20`) and Fever as integer `1` or `2`; calculate checked numerator then integer-divide once by `10`. Approved outputs are therefore deterministic: normal `10/12/15/20`, Fever `20/24/30/40`.

### Fever-end energy

A successfully committed `LargeExplosionAndRestoration` system effect awards exactly `50` additional restoration energy. `None`, `RandomThreeBlocks`, `SmallAreaExplosion`, and `CenterAreaExplosion` award zero direct restoration energy.

Fever-end removed numbers still participate in STEP 10 number/obstacle objectives, but they are not submitted answers and do not each generate the base-10 answer award. Combo, removed count, radius, and damage do not modify the fixed `50`.

### Stage capacity and world milestones

Each StageDefinition supplies a positive integral `StageRestorationCapacity`. Stage-local applied progress is clamped to that capacity. Excess is discarded permanently and is neither stored, converted, nor carried forward.

Each associated world target separately owns positive integral `WorldRestorationCapacity` and `WorldCurrent` constrained to `0..WorldRestorationCapacity`. Stage capacity does not redefine or replace world capacity.

Milestone identities are fixed domain values:

- `QuarterRestored` — 25%
- `HalfRestored` — 50%
- `ThreeQuartersRestored` — 75%
- `FullyRestored` — 100%

No concrete asset key belongs to gameplay/domain code. STEP 12 maps these typed identities plus stage/world identity to presentation assets.

Milestones belong only to persistent world restoration, not stage-local progress. To avoid inventing rounding for world capacities not divisible by four, crossing uses exact rational comparison:

```text
worldAfter × 100 >= worldCapacity × milestonePercent
```

Use checked/widened arithmetic. A successful world commit emits every newly crossed world milestone exactly once in ascending percentage order. One commit may emit multiple milestones. Stage-local accumulation emits no milestone. World progress zero has none; world capacity emits `FullyRestored` once.

### Lifecycle and ownership

- StageSession owns active stage-local restoration state and cumulative energy earned during the run.
- Exhausting moves with incomplete objectives enters `FailedPendingDecision`; it does not discard provisional restoration yet.
- Abandon/Exit discards provisional restoration.
- Retry terminates the failed attempt, discards its provisional restoration, and creates a new StageSession/stage-run identity at zero restoration.
- Continue is valid only from `FailedPendingDecision`; it resumes the same StageSession/stage-run identity, grants the separately approved continue moves through the existing continue policy, and preserves provisional restoration unchanged.
- Leaving the failed flow without Continue is Abandon and discards restoration.
- Success atomically produces and commits the final restoration result to world progression.
- World-space restoration state is never modified by a Correct answer, Fever answer, end effect, failure, abandon, retry, or continue independently; only successful stage completion may modify it.

The world commit equation is exact:

```text
WorldAfter = min(WorldCapacity, WorldBefore + StageCommittedRestoration)
```

World-boundary excess is discarded. A previously unseen WorldCommitId applies once; a repeated committed ID is an idempotent no-op and emits no milestones. Persistence remains STEP 13; STEP 11 owns this in-memory ledger behavior and immutable commit result.

## Additional approved lifecycle/world decisions

- StageSession status adds `FailedPendingDecision` between detected failure and the player's decision.
- Small, Center, RandomThree, and None Fever-end effects produce no restoration-bonus evidence. They do not consume a restoration source sequence or emit a zero award.
- Every stage run receives a stable unique `StageRunId` at StageSession creation from the composition/session identity owner. `WorldCommitId` is deterministically the corresponding successful StageRunId wrapped as a distinct type. One run can therefore produce at most one world commit ID; retry creates a new run ID, while Continue retains the same ID.
- World commit IDs are checked against an in-memory applied-ID set owned by `WorldRestorationProgress`. Duplicate IDs return `AlreadyCommitted`, preserve WorldCurrent, and emit no milestone or new-success fact. Unseen IDs atomically update current value and record the ID.

## Assemblies and dependency direction

Add Unity-free `MathGame.Restoration.Contracts` and `MathGame.Restoration`, both non-auto-referenced and without UnityEngine references.

- Contracts owns immutable restoration configuration values, milestone identity, award breakdown/evidence, and world-commit result types; it depends only on Core primitive contracts.
- StageSession references Restoration.Contracts and owns stage-local progress/objective mutation.
- Restoration references Contracts and StageSession; it owns the pure calculator plus the exclusive world-progression commit coordinator.
- Fever and BoardResolution do not reference Restoration.
- ObstacleFlow/gameplay composition consumes the Restoration coordinator/port. Restoration never references ObstacleFlow.

```text
Core <- Restoration.Contracts <- StageSession <- Fever/ObstacleFlow
             ^                    ^
             |                    |
             +---- Restoration ---+
ObstacleFlow/composition -> Restoration public port
```

This remains acyclic: StageSession never references the concrete Restoration assembly.

## Public domain contracts

### Configuration

`StageRestorationConfig` is immutable and contains:

- positive `long Capacity`;
- fixed prototype rules/version identity;
- stable stage/world restoration identity needed for the success commit.

The prototype formula is closed by the approved addendum; callers cannot supply arbitrary base/factors/Fever multipliers. A later balance change requires a new explicit rules version, not runtime flags.

Validation rejects missing/invalid identity, nonpositive capacity, and capacity/arithmetic combinations that cannot be safely compared using the approved milestone calculation. Input construction is immutable and nonthrowing where repository conventions require status-bearing creation.

### Award evidence

`RestorationAwardEvidence` is immutable and contains:

- source kind: `NormalAnswer`, `FeverAnswer`, or `LargeFeverEnd`;
- exact source ID: StageAttemptId for answers or BoardSystemEffectId for the end effect;
- submitted connection length for answers, zero for system effects;
- base amount, connection factor tenths, Fever multiplier, gross award;
- restoration rules version;
- exact StageSession/restoration owner/version binding token.

The evidence is created only by the pure approved calculator after successful answer/resolution correlation or successful STEP 10 Large end resolution. Public UI callers cannot fabricate arbitrary energy amounts.

### Snapshots and milestone facts

StageSessionSnapshot adds:

- stage restoration capacity;
- current provisional applied restoration;
- cumulative gross restoration earned;
- discarded excess total;
- stable StageRunId;
- current restoration lifecycle state (`Provisional`, `FailedPendingDecision`, `CommittedSuccess`, or `Discarded`).

`RestorationAwardResult` exposes stage-local Before/After facts, gross/applied/discarded amounts, and source correlation. It exposes no milestone collection because milestones belong to world commits. Rejections expose equal Before/After.

`WorldRestorationSnapshot` exposes World identity, WorldCurrent, WorldCapacity, immutable reached world milestones, and applied commit count. `WorldRestorationCommitResult` exposes status, stable stage/world identity, WorldBefore/WorldAfter snapshots, stage committed amount, applied amount, discarded world excess, newly crossed milestones, source StageSession version, and exactly-once WorldCommitId. It contains no asset paths, sprites, scene references, or localized text.

`WorldRestorationCommitStatus` precedence is `MissingInput`, `InvalidWorldIdentity`, `WorldIdentityMismatch`, `InvalidStageSuccess`, `InvalidCommitId`, `AlreadyCommitted`, `ArithmeticOverflow`, then `Committed`. `AlreadyCommitted` is an idempotent observation, not a new success: Before equals After and newly crossed milestones are empty.

WorldRestorationProgress construction requires stable World identity, positive capacity, and initial current in `0..capacity`. Its reached-milestone set is not caller supplied: it is derived canonically from initial current using the same rational 25/50/75/100 comparisons. A supplied persisted milestone set, when STEP 13 adds one, must exactly equal this derivation or loading rejects it.

Before any StageSession transition to Success, the coordinator creates an immutable version-bound `WorldCommitPlan` containing exact World owner/reference identity, world version, WorldCommitId, WorldBefore, prospective WorldAfter, applied amount, discarded excess, and crossed milestones. Missing/mismatched identity, invalid/already-applied ID, and all arithmetic failures are resolved during preparation. StageSession binds this exact plan token into its prospective Success result. After preparation, success consists only of StageSession assignment followed immediately by the already-validated WorldRestorationProgress snapshot assignment; both are nonthrowing and no callback or validation runs between them. A stale world version makes the StageSession plan stale before either assignment.

`AlreadyCommitted` is handled before a new StageSession can start or succeed with that StageRunId. A duplicate direct commit call returns the prior idempotent observation, but it cannot authorize a new StageSession Success. Thus there is no accepted new stage result without a matching world mutation/no-op that was validated in advance.

## Atomic transaction model

### Answer flow

1. STEP 10 produces a successful correlated normal/Fever answer resolution.
2. Restoration calculator derives prospective evidence from the submitted answer length and trusted StageAttemptMode.
3. The sole restoration transaction coordinator supplies that evidence to the same StageSession `ApplyAttempt` command.
4. StageSession validates source ID/mode/rules version, calculates prospective clamped stage restoration, advances `EarnRestorationEnergy`, and evaluates Success-before-Failure in that same commit. It emits no milestone yet.
5. Accepted result contains the immutable restoration award result. Rejected attempt changes nothing.
6. If the accepted attempt is nonterminal, existing STEP 10 Board adoption and target-proof behavior continues unchanged.
7. If it prospectively succeeds, the coordinator first prepares/binds the WorldCommitPlan, then performs the StageSession and world assignments exactly once.

### Fever-end flow

1. STEP 10 resolves the end effect without committing StageSession.
2. Only Large derives prospective `+50` evidence. None/Random/Small/Center omit restoration-bonus evidence entirely and never mutate or advance restoration sequencing.
3. Restoration evidence is included before StageSession `PrepareSystemEffect` and before target proof.
4. The restoration-aware plan predicts terminal state. If it predicts Continue, commit is withheld until target proof succeeds. If it predicts Success, no next-target proof is required.
5. StageSession system-effect commit and its restoration state assignment are one nonthrowing commit.
6. A prospective terminal result prepares/binds WorldCommitPlan before either assignment; a failed proof/rejected/stale plan changes neither StageSession restoration nor world progression.

### Exclusive ownership and staleness

Only the Restoration transaction coordinator may create binding tokens or request a world commit. StageSession validates exact owner ID, rules version, source ID, and session version. A duplicate/out-of-order source or stale plan rejects atomically. There is no public setter for stage restoration or world progress and no callback between validated StageSession assignment and nonthrowing world commit assignment.

Continue preserves the same owner/run identity and restoration amount while incrementing the StageSession version for its move/status transition. Retry constructs new owner/run identities, so stale evidence from the prior run cannot apply.

## Failed-decision commands

The exclusive restoration/gameplay coordinator exposes:

```text
StageFailedDecisionResult ContinueFailedStage(ContinueGrant grant)
StageFailedDecisionResult RetryFailedStage(StageDefinition definition)
StageFailedDecisionResult AbandonFailedStage()
```

`ContinueGrant` is an immutable trusted contract with `ContinueGrantId`, exact `StageRunId`, and fixed `AdditionalMoves=5`. Construction is internal to the approved monetization/reward authorization adapter; gameplay/UI cannot construct arbitrary grants. STEP 11 validates positive unique grant ID, exact active run correlation, fixed move value 5, and unused status. It consumes one grant at most once and permits at most one Continue per StageRunId, matching GDD §8.4. Provider/ad execution remains STEP 15; tests use an internal friend factory/fake issuer.

`StageFailedDecisionStatus` precedence is `InvalidStageState`, `SessionNotFailedPendingDecision`, `MissingOrInvalidGrant` (Continue only), `GrantRunMismatch`, `DuplicateGrant`, `ContinueAlreadyUsed`, `AlreadyResolvedDecision`, `RunIdAllocationFailed` (Retry only), `ArithmeticOverflow`, then `Continued`, `Retried`, or `Abandoned`.

- Append StageState `FailedPendingDecision` without renumbering existing states. StageController `EnterFailedPendingDecision()` transitions `ResolvingAnswer -> FailedPendingDecision`; it is noninteractive/pausable. It replaces direct Failure for ordinary move exhaustion while a Continue decision is available. `ResumeFromContinue()` transitions `FailedPendingDecision -> RecoveringBoard`; target proof is required before presentation/input. Terminal `Fail()` is used only after Retry/Abandon/leave resolves the pending attempt for failure presentation/teardown.
- Continue requires StageController and StageSession both FailedPendingDecision, checked-adds exactly 5 moves once, transitions the same StageSession back to Active and Stage to RecoveringBoard, increments version, and preserves restoration/run ID.
- Retry and Abandon require the same pending state, atomically mark old restoration Discarded, make later Success/world commit impossible for that run, and resolve the decision once. Retry obtains a fresh run ID from the coordinator's ID source and returns a fresh zeroed StageSession; Abandon returns no replacement session.
- Exit or leaving the failed flow delegates to AbandonFailedStage before teardown. Calls from Success, Active, already discarded, or exited states reject without mutation.
- Results expose immutable old Before/After snapshots, optional replacement snapshot, applied move grant, and restoration disposition. No UI/ad SDK enters these commands; STEP 15 later supplies the approved ContinueGrant.

### Stage-run identity ownership

`IStageRunIdSource` is an injected domain port owned by the gameplay composition root. It returns globally unique nondefault IDs for this installation/save lineage. The Restoration coordinator, not callers, requests IDs for initial StageSession creation and Retry. It maintains active and retired IDs and rejects collisions with either set or with any WorldCommitId already recorded in any WorldRestorationProgress it owns. Continue retains the existing ID. Success, Retry, and Abandon retire it. `WorldCommitId` is deterministically constructed from the successful StageRunId, so one run has at most one commit.

The source's durable uniqueness across restart becomes STEP 13 persistence responsibility; STEP 11 tests deterministic collision rejection and exactly-once behavior in memory.

## StageSession migration

- Require valid StageRestorationConfig when an `EarnRestorationEnergy` objective is configured.
- Enable that objective and advance it only from accepted `RestorationAwardEvidence.GrossAward` (the approved earned amount), display-clamped to objective requirement.
- Track provisional applied progress separately from cumulative gross earned and discarded excess.
- Include restoration in attempt/system-effect prospective snapshots, versioning, events, and terminal prediction.
- On final-move Correct, apply restoration objective progress before Success/Failure; Success wins.
- Miss, rejected, duplicate, stale, failed-resolution, or failed-end-effect inputs produce no restoration mutation.
- Detected failure sets FailedPendingDecision and preserves provisional progress. Abandon/Retry mark it Discarded and expose zero committable world amount while preserving immutable historical gross totals.
- Continue preserves restoration and run identity; its move grant/status transition increments the session version exactly once.

## Semantic presentation contract

Emit ordered immutable semantic events only:

- `RestorationEnergyAwarded` with gross/applied/discarded amounts;
- `RestorationMilestoneReached` with typed world milestone identity, emitted only by a new world commit;
- `RestorationProvisionalDiscarded` on Retry/Abandon, not initial failure detection;
- `RestorationCommittedToWorld` on Success.

STEP 12 chooses concrete dust, lighting, wall, furniture, decoration, color, animation, audio, and asset bindings. No STEP 11 production assembly may reference those assets or presentation keys.

## Required migrations

1. StageDefinition/StageSession: optional required restoration config, enable restoration objective, evidence-aware attempt/system-effect plans, provisional lifecycle, snapshots/events.
2. STEP 9 integration: use trusted Normal/Fever mode only; ignore combo for restoration.
3. STEP 10 end flow: attach fixed Large +50 evidence before system-effect preparation without changing obstacle/end geometry.
4. ObstacleFlow/gameplay coordinator: route calculator evidence through existing atomic answer/end transactions and target-proof ordering.
5. Stage summary: include gross/applied/discarded stage restoration, lifecycle, and optional world commit result; world milestones appear only inside that commit result.
6. STEP 12: consume typed milestone identities; no reverse asset dependency.
7. STEP 13 later persists successful world commits and ensures commit-ID idempotence across restart.

## Risks

- Split StageSession/world progress if success commit is not exclusively coordinated.
- Duplicate awards from answer callbacks, Fever-end retry, target-proof retry, continue, or persistence replay.
- Mistaking total resolver removals for submitted connection length.
- Applying Fever combo despite its explicit exclusion.
- Floating-point or intermediate-rounding drift; exact rational arithmetic is mandatory.
- Overflow in percentage comparison or cumulative totals.
- Duplicate/out-of-order world milestone emission when one commit crosses several thresholds.
- Accidentally discarding progress at failure detection, retaining it after Retry/Abandon, or clearing it on Continue.
- Concrete art leaking into domain configuration.
- STEP 10 P2 Fever coordinator coverage and Unity licensing verification debt hiding integrated regressions; both remain required regression work.

## Test strategy

### Edit Mode

- Exact normal outputs `10/12/15/20` and Fever outputs `20/24/30/40` at lengths 2/3/4/5 and larger.
- Defensive length 1 cannot bypass Answer validation; invalid/zero/negative length evidence rejects.
- One final floor only and no floating-point behavior.
- Combo values 1/2/3/5 produce identical restoration for the same Fever answer.
- Large committed end awards exactly 50; None/Random/Small/Center omit bonus evidence; failed/stale end plan awards zero.
- Capacity below/equal/above awards; applied clamp and exact discarded excess.
- Non-quarter-divisible capacities use rational percentage comparison correctly.
- Exact world threshold-below/at/above cases for 25/50/75/100; stage accumulation emits none.
- One award crossing multiple milestones emits each once in ascending order; duplicate evidence emits none.
- EarnRestorationEnergy objective advances from gross approved award exactly once and final-move completion succeeds before Failure.
- Normal/Fever answer, Fever-end target failure/retry, stale plan, duplicate IDs, arithmetic overflow, and rollback preserve atomicity.
- Failure enters pending and preserves; Abandon/Retry discard; Retry starts zero/new run identity; Continue preserves same run; Success emits one world commit.
- Additive world commits clamp/discard excess; duplicate commit ID is an idempotent no-op; retry IDs are distinct; no world mutation occurs before Success.
- Snapshots, events, milestone collections, and historical results are immutable.
- Same configuration/evidence always produces identical outputs.

### Play Mode

- Restoration reveal remains noninteractive; answer/Fever clocks remain suspended.
- Pause/focus/background nesting does not duplicate awards or milestones.
- Continue preserves provisional progress through lifecycle transitions.
- Failure preserves through pending UI; Continue resumes it; Retry/exit discard once; Success commits once.
- STEP 10 normal/Fever/end coordinator regressions rerun, including its preserved P2 debt.

Unity verification remains blocked while licensing initialization cannot complete and must not be reported as a pass or failure.

## Out of scope

- Concrete restoration assets, keys, GameObjects, animation, sound, particles, haptics, and localization.
- Player-selectable decor, furniture variants, wallpapers, seasons, screenshots, or revisit mode.
- Persistence repository/schema (STEP 13), analytics (STEP 14), ads/IAP, coins, stars, and economy.
- Changes to Dust, Box, Fever geometry, target recovery, or special blocks.

## Disposition

The approved product addenda resolve answer/Fever arithmetic, Large-end energy, stage-local clamping/excess, Failure/Continue/Retry/Abandon lifecycle, additive idempotent world commits, world milestone ownership/order, and the presentation-asset boundary. STEP 11 is **READY FOR IMPLEMENTATION**, subject to independent design review. No production implementation begins until that review passes.
