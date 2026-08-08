# MathGame Development Plan

Last updated: 2026-08-08  
Status: **GDD-backed.** STEPs 1-6 are complete; later STEPs require their own GDD reconciliation.

## Planning basis

The repository contains an application/lifecycle foundation but no gameplay. `Docs/GAME_DESIGN.md` v1.0 now defines the product and MVP. Consequently:

- STEP 1 reflects verified existing code.
- STEPs 2-16 are dependency-safe planning slices and require a dedicated design pass before implementation.
- Before any `Design STEP N` or `Implement STEP N`, read the relevant `Docs/GAME_DESIGN.md` sections, map them into that STEP, and revise acceptance criteria where necessary.
- Ambiguous rules must remain open; they must not be invented during implementation.

## Current status

| STEP | Slice | State |
|---:|---|---|
| 1 | Project Foundation and Stage Lifecycle | Complete — Edit Mode 16/16, Play Mode 10/10, no P0 |
| 2 | Board Domain Model | Complete — Edit Mode 48/48 total, Play Mode regression 10/10, no P0/P1/P2 |
| 3 | Deterministic Initial Board Generation | Complete — Edit Mode 66/66 total, Play Mode regression 10/10, no P0/P1/P2 |
| 4 | Orthogonal Connection Path | Complete — Edit Mode 82/82 total, Play Mode regression 10/10, no P0/P1/P2 |
| 5 | Addition Validation and Interactive Timing | Complete — Edit Mode 99/99, Play Mode 19/19, no P0/P1/P2 |
| 6 | Board Resolution: Removal, Gravity, Refill | Complete — Edit Mode 117/117, Play Mode 19/19, no P0/P1/P2 |
| 7-16 | Prototype/MVP development | Planned; design required per STEP |

## Dependency path

```text
1 Foundation
  -> 2 Board Model -> 3 Board Generation
  -> 4 Connection -> 5 Math/Answer Timing
  -> 6 Removal/Gravity/Refill -> 7 Solvable Targets/Deadlock
  -> 8 Stage Objectives -> 9 Fever Core -> 10 Obstacles
  -> 11 Restoration -> 12 Gameplay Presentation
  -> 13 Progression/Persistence -> 14 Semantic Analytics
  -> 15 Approved Monetization -> 16 MVP Integration/Release
```

Presentation work may be prototyped alongside domain STEPs only when explicitly included in the requested STEP; it must not pull later gameplay rules forward.

---

## STEP 1 — Project Foundation and Stage Lifecycle

**Goal:** Establish a testable application composition root, deterministic service seams, and lifecycle state/pause handling.  
**Requirements:** Initialize a blank stage into non-interactive `Ready`; expose controllable time/randomness seams; reject invalid state transitions; disable input outside input states; preserve nested pause reasons and early lifecycle intent; exit and clean up safely.
**Systems:** Core time/random/logging abstractions, `StageController`, application bootstrap, Unity lifecycle relay, persistence interface/schema shell.  
**Expected files:** Existing `Assets/MathGame/Runtime/{Core,Stage,App,Save}` assemblies and their Edit/Play Mode tests; architecture docs.  
**Dependencies:** None.  
**Implementation scope:** Reconcile existing lifecycle with the design; fill only confirmed foundation gaps. Do not add board/gameplay.  
**Tests:** `None -> Initializing -> Ready`; no blank-stage input; repeated/invalid commands; nested/unknown/duplicate pause reasons; early focus/background callback ordering and clearing; teardown/recreation; assembly compilation.
**Completion criteria:** Early lifecycle intent is retained/reconciled, blank startup is non-interactive, all foundation tests pass in this checkout, no P0 review findings remain, and architecture reflects actual behavior.

## STEP 2 — Board Domain Model

**Goal:** Represent board topology and cell contents without GameObjects.  
**Requirements:** Grid coordinates, number cells, blocked/obstacle-ready cells, selectable/removable state, bounds/neighbors, and support for design-approved non-rectangular shapes.  
**Systems:** Board, coordinate/value types, cell/content model, topology queries, board invariants.  
**Expected files:** New domain assembly/folder under `Assets/MathGame/Runtime/Board`; Edit Mode tests.  
**Dependencies:** STEP 1; design board rules.  
**Implementation scope:** Logical representation and mutations only; no random generation, input, gravity, or views.  
**Tests:** Bounds, orthogonal neighbors, holes/blocked cells, occupancy, removal markers, invalid construction/mutation.  
**Completion criteria:** All specified board states can be represented and validated deterministically without Unity scene objects.

## STEP 3 — Board Generation

**Goal:** Create valid initial number boards from stage configuration.  
**Requirements:** Design-approved dimensions/shapes/value ranges/distribution; deterministic seeded generation; explicit failure for invalid configuration.  
**Systems:** Board configuration, generator, number policy, validation result.  
**Expected files:** Board generation classes/configuration plus Edit Mode tests.  
**Dependencies:** STEP 2; approved generation rules.  
**Implementation scope:** Initial logical population only; no target guarantee, shuffle, refill, or presentation.  
**Tests:** Fixed seeds, boundary values, excluded cells, invalid configs, repeated generation, invariant preservation.  
**Completion criteria:** Generated boards match every approved constraint and tests reproduce exact outcomes.

## STEP 4 — Orthogonal Connection Path

**Goal:** Build and edit a legal player selection path with a live sum.  
**Requirements:** Orthogonal adjacency; no diagonal steps; no duplicate cells; blocked/non-selectable rejection; approved backtracking and cancellation; live addition sum.  
**Systems:** Connection path state, selection commands/results, path validator, input-to-domain adapter contract.  
**Expected files:** New selection/domain classes and Edit Mode tests; minimal input adapter only if explicitly approved.  
**Dependencies:** STEP 2; exact gesture/path rules.  
**Implementation scope:** Path construction and feedback data; no answer resolution or block removal.  
**Tests:** First/add/backtrack/cancel, diagonal/gap/duplicate/blocked rejection, boundaries, live sum, repeated commands.  
**Completion criteria:** Every accepted path is legal and every rejected operation leaves consistent state.

## STEP 5 — Addition Validation and Interactive Timing

**Goal:** Resolve a submitted path against the target and grade only interactive response time.  
**Requirements:** Addition only; exact target comparison; design-approved valid/invalid submission rules and speed bands; speed is a bonus, never a failure condition; animation/resolution/pause/deactivation excluded from timing.  
**Systems:** Answer validator/result, interactive clock/session, stage input gating integration, semantic answer event.  
**Expected files:** Math/rules and timing classes, Stage integration changes, Edit and targeted Play Mode tests.  
**Dependencies:** STEPs 1 and 4; target value contract; exact timing/reward rules.  
**Implementation scope:** Produce answer/grade results; no board mutation, score economy, or visual effects.  
**Tests:** Equal/under/over sums, threshold boundaries, slow correct answer, pause/focus, resolution exclusion, repeated submissions.  
**Completion criteria:** Validation is deterministic and timing advances only while input is genuinely available.

## STEP 6 — Board Resolution: Removal, Gravity, and Refill

**Goal:** Resolve a correct path into the next stable board.  
**Requirements:** Remove selected number blocks; apply design-approved gravity around holes/obstacles; refill using controlled randomness; lock input for the entire resolution.  
**Systems:** Resolution transaction/result, removal, gravity, refill policy, stage resolution states.  
**Expected files:** Board resolver and policies, Stage integration, Edit Mode tests; minimal Play Mode lifecycle test.  
**Dependencies:** STEPs 3 and 5; obstacle interaction deferred unless required for correct topology behavior.  
**Implementation scope:** Logical stable-state transition; presentation consumes results but animations are STEP 12.  
**Tests:** Columns/holes/irregular shapes, simultaneous removal, refill order, deterministic values, no-op/invalid requests, input gating.  
**Completion criteria:** Resolution preserves board invariants and cannot accept overlapping player input.

## STEP 7 — Solvable Target Selection and Deadlock Recovery

**Goal:** Ensure the displayed target always has at least one legal current-board path.  
**Requirements:** Enumerate/search legal paths or equivalent safe strategy; select only available sums; detect target deadlock; regenerate target, then shuffle only if necessary; consume no move for recovery.  
**Systems:** Path search, available-sum/target selector, deadlock coordinator, deterministic shuffle, target transition state.  
**Expected files:** Target/search domain assembly or Board extensions, Stage integration, Edit Mode tests.  
**Dependencies:** STEPs 3, 4, and 6; target range/weight/repetition rules.  
**Implementation scope:** One MVP target strategy; no speculative difficulty variants.  
**Tests:** Known solution boards, no-solution target, regeneration, shuffle fallback, impossible board/config failure, deterministic selection, move-count invariance.  
**Completion criteria:** A target is never exposed without a verified path, or the system returns an explicit unrecoverable configuration error.

## STEP 8 — Stage Objectives, Rewards, and Completion

**Goal:** Turn repeated answer cycles into a completable, non-speed-gated stage.  
**Requirements:** Implement only design-defined objectives, move/progress accounting, long-path rewards, scoring, success/failure rules, and stage configuration. Slower correct play must still permit completion.  
**Systems:** Stage definition, objective trackers, reward calculation, run summary, completion transitions.  
**Expected files:** Stage/objective domain classes, configuration assets/adapters, Edit and Play Mode tests.  
**Dependencies:** STEPs 5-7; complete objective/reward design.  
**Implementation scope:** MVP objective types only; no metagame progression or economy persistence.  
**Tests:** Objective boundaries, long-path reward ordering, slow correct completion, invalid answer effects, success/failure exclusivity, repeated terminal commands.  
**Completion criteria:** Every MVP stage type reaches the correct terminal state under deterministic scenarios.

## STEP 9 — Fever Core

**Goal:** Add the main skill reward as independently testable gameplay state.  
**Requirements:** Design-defined gauge gain/threshold, entry, duration, Fever combo/modifiers, end behavior, and non-interactive timing; one MVP Fever variant only.  
**Systems:** Gauge, Fever state/session clock, combo/modifier policy, end result, Stage orchestration; separate presentation contract.  
**Expected files:** New Fever domain assembly/folder, Stage/answer integration, Edit and Play Mode tests.  
**Dependencies:** STEPs 5 and 8; complete Fever rules.  
**Implementation scope:** Gameplay rules and semantic effects; no spectacle implementation beyond event/result contracts.  
**Tests:** Gain/threshold boundaries, repeated triggers, duration pause/resume, answer interactions, combo reset, end transition, stage termination during Fever.  
**Completion criteria:** Fever lifecycle and modifiers are deterministic, non-monolithic, and cannot leave Stage/input in an invalid state.

## STEP 10 — Obstacles and Fever Destruction

**Goal:** Add design-approved obstacles and their interaction with board resolution/Fever.  
**Requirements:** Exact obstacle types, selectability, hit/destruction rules, gravity/refill behavior, objective contribution, and Fever destruction effects.  
**Systems:** Obstacle content/state, damage/removal rules, board resolver integration, objective/Fever result integration.  
**Expected files:** Board/obstacle domain classes, tests, configuration updates.  
**Dependencies:** STEPs 6, 8, and 9; full obstacle specification.  
**Implementation scope:** MVP obstacle set only; no future variants.  
**Tests:** Blocked selection/pathing, adjacency effects if specified, multiple hits, simultaneous resolution, gravity edges, Fever destruction, objective accounting.  
**Completion criteria:** Obstacles behave consistently across normal and Fever resolution without corrupting topology.

## STEP 11 — Environment Restoration Progress

**Goal:** Convert gameplay progress into the design-defined environment restoration reward.  
**Requirements:** Restoration source, milestones, stage/end behavior, and semantic presentation data exactly as designed.  
**Systems:** Restoration progress model, milestone events/results, stage summary integration, presentation contract.  
**Expected files:** Restoration domain/configuration classes and Edit Mode tests.  
**Dependencies:** STEPs 8-10; restoration design and asset plan.  
**Implementation scope:** Progress/rules first; final art and metagame persistence remain later unless explicitly part of the design slice.  
**Tests:** Progress boundaries, multiple milestones, overflow/clamping, duplicate results, success/failure behavior, deterministic mapping.  
**Completion criteria:** Identical gameplay results always produce identical restoration progress and milestones.

## STEP 12 — Mobile Gameplay Presentation and Feedback

**Goal:** Deliver the complete playable mobile board loop and its visual/audio feedback without moving rule ownership into views.  
**Requirements:** Board/target/path/live sum, invalid/correct feedback, resolution motion, Fever spectacle, obstacle feedback, restoration reveal, pause and stage result UI, touch cancellation, readability, approved audio/haptics/accessibility.  
**Systems:** Scene composition, board/cell views, touch adapter, presenters, animation sequencing, pools if justified, audio/haptic adapters.  
**Expected files:** Gameplay scene/prefabs/assets, Presentation assembly, Play Mode tests.  
**Dependencies:** STEPs 4-11; approved UX/art requirements.  
**Implementation scope:** MVP screens and feedback only; domain remains authoritative.  
**Tests:** View synchronization, input locked during sequences, destroyed/reloaded scene cleanup, pause/focus, touch cancel, representative device layouts; manual feel pass.  
**Completion criteria:** A player can complete the full loop on target devices with no view/domain divergence or lifecycle leaks.

## STEP 13 — Progression and Persistence

**Goal:** Persist only the MVP progression and settings defined by design.  
**Requirements:** Save/load, defaults, schema migration/validation, stage unlock/progress, restoration/settings data, corruption recovery, app lifecycle saves.  
**Systems:** Progression model, save DTO evolution, repository implementation, migration, coordinator.  
**Expected files:** Save/progression classes and Edit/Play Mode tests.  
**Dependencies:** STEPs 8 and 11; progression/settings specification.  
**Implementation scope:** Local backend unless design explicitly requires another; no social/cloud system by assumption.  
**Tests:** New/existing/corrupt/future-version data, migration, repeated save, interruption, stage unlock boundaries, round trip.  
**Completion criteria:** Supported data survives restart and failure paths recover without silently granting or losing progress.

## STEP 14 — Semantic Analytics

**Goal:** Measure the approved MVP funnel without coupling gameplay to an SDK.  
**Requirements:** Design-defined semantic events/properties, consent/privacy behavior, session/stage/answer/Fever/obstacle/restoration coverage, and failure handling.  
**Systems:** Event contracts, analytics port, provider adapter, dispatcher/buffering policy.  
**Expected files:** Analytics assembly/folder, integration points, contract tests.  
**Dependencies:** Stable event results from STEPs 5-13; analytics specification.  
**Implementation scope:** Required events only; no speculative telemetry.  
**Tests:** Exact event emission, no duplicates, property validation, disabled/failed provider behavior, lifecycle flushing if required.  
**Completion criteria:** Required events are verifiable through a fake provider and SDK failure cannot alter gameplay.

## STEP 15 — Approved Ads and Monetization

**Goal:** Integrate only MVP monetization placements explicitly authorized by the design.  
**Requirements:** Exact placements, eligibility, rewards, failure/cancel behavior, pause/input handling, consent, and purchase rules if any.  
**Systems:** Ad/purchase ports and adapters, reward transaction, lifecycle integration, UI entry points.  
**Expected files:** Monetization assembly/folder, provider configuration, Play Mode/adapter tests.  
**Dependencies:** STEPs 1, 12-14; complete monetization design.  
**Implementation scope:** No complex booster/store architecture or undesigned placements. Skip this STEP if the MVP design excludes monetization.  
**Tests:** Success/failure/cancel, duplicate callbacks, reward-once guarantee, pause nesting, scene exit, offline/provider unavailable.  
**Completion criteria:** Monetization cannot corrupt progress, grant duplicate rewards, or resume input while another pause reason remains.

## STEP 16 — MVP Integration, Content Validation, and Release Readiness

**Goal:** Verify the complete approved MVP across content, lifecycle, performance, and target devices.  
**Requirements:** Required stages/content, tutorial/onboarding if specified, full regression, build configuration, performance/memory budgets, accessibility/privacy/store compliance, and release analytics.  
**Systems:** Content validators, smoke/regression suites, build profiles, release checklist; fixes only within confirmed MVP behavior.  
**Expected files:** Validation/editor tools if justified, test scenarios, build configuration, release documentation.  
**Dependencies:** All included prior STEPs.  
**Implementation scope:** Integration and confirmed fixes; no new feature expansion.  
**Tests:** Clean install/upgrade, full stage loop, pause/background/scene changes, low-memory/relaunch paths as practical, device/resolution matrix, performance soak, save and analytics regression.  
**Completion criteria:** Clean player builds pass the approved release matrix with no P0/P1 release blocker and all MVP requirements trace to verification evidence.

---

## Required completion report

After `Implement STEP N`, report: Goal; Requirements Implemented; Architecture; Files Added/Modified; Edit Mode/Play Mode tests; Review P0/P1/P2; Verification (Compilation, Edit Mode, Play Mode, Manual); Out-of-Scope Findings; Remaining Risks; and final Status (`PASS` or `BLOCKED`). Then stop.

## Immediate next action

Stop and wait for the user's next command. The next dependency-safe command is `Design STEP 7`; do not begin it automatically.
