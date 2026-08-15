# MathGame Decisions

## Accepted

### ADR-001: Gameplay design document is mandatory

**Status:** Accepted; source now present
**Decision:** `Docs/GAME_DESIGN.md` will be the gameplay source of truth. The attached development brief is process guidance and a high-level product summary, not a substitute for the complete design.  
**Rationale:** Exact objectives, rewards, timing thresholds, Fever rules, obstacles, restoration, economy, analytics, and content cannot be safely inferred.

### ADR-002: Keep domain state independent of Unity objects

**Status:** Accepted  
**Decision:** Board, connection path, arithmetic validation, target availability, objectives, and Fever rules will be plain C# domain logic where practical. MonoBehaviours will adapt input, lifecycle, and presentation.  
**Rationale:** Deterministic Edit Mode tests and clear state ownership are essential for the puzzle rules.

### ADR-003: Control nondeterminism through seams

**Status:** Accepted  
**Decision:** Random generation uses `IRandomSource`. Timing-sensitive rules will use an explicit interactive-time responsibility rather than raw scene elapsed time.  
**Rationale:** Board/target/refill behavior and speed grading must be reproducible and must exclude non-interactive periods.

### ADR-004: Preserve SDK isolation

**Status:** Accepted  
**Decision:** Persistence, analytics, ads, and purchasing integrations remain behind adapters; domain logic emits semantic results/events and does not call vendor SDKs.  
**Rationale:** This prevents service concerns from owning gameplay behavior and keeps tests lightweight.

### ADR-005: Treat the current foundation as STEP 1

**Status:** Implemented and verified
**Decision:** The bootstrap, service seams, lifecycle controller, persistence seam, and tests form STEP 1. The implementation is reconciled with `GAME_DESIGN.md` v1.0 and verified by Edit and Play Mode tests.
**Rationale:** Reimplementing existing code would violate the inspect-before-change and scope rules.

### ADR-006: Blank initialization ends in non-interactive Ready

**Status:** Implemented in STEP 1
**Decision:** Append a stable explicit `Ready` stage state. Foundation initialization ends in `Ready`, not `PlayerInput`. Only later target/gameplay orchestration may enable input after a valid target is visible and manipulation is available.
**Rationale:** `GAME_DESIGN.md` §5.3 and §9.2 forbid input/timing during target transition and start timing only when the target is visible and playable.

### ADR-007: Retain and reconcile early Unity lifecycle facts

**Status:** Implemented in STEP 1
**Decision:** `ApplicationLifecycleRelay` retains nullable latest pause/focus facts. `MathGameBootstrap` defers them until successful stage initialization, then synchronizes independent pause reasons idempotently.
**Rationale:** Early callbacks previously reached a controller in `None` and were forgotten, which could expose a later interactive phase while the app was inactive. Nullable facts avoid guessing platform callback defaults.

### ADR-008: Use a Unity-free dense masked board domain

**Status:** Implemented and verified in STEP 2
**Decision:** Add an independent `MathGame.Board` assembly with `noEngineReferences`. Use positive rectangular extents, an immutable active-cell mask, dense row-major cell storage, lower-left coordinates, and deterministic `Up, Right, Down, Left` neighbor order.
**Rationale:** MVP boards are tiny, while masked topology must support holes and later special shapes. Dense storage keeps lookup and deterministic enumeration simple without binding state to GameObjects.

### ADR-009: Separate topology, number identity, occupancy, and access

**Status:** Implemented and verified in STEP 2
**Decision:** Active topology is immutable. Mutable cells contain an optional immutable number block and independent `Open`/`Blocked` access. Number blocks use stable positive logical IDs and positive values; blocked occupied cells are allowed. Named obstacle/special behavior is deferred.
**Rationale:** Holes, empty cells, number-bearing special blocks, locks/ice, fixed blockers, and underlying tiles cannot safely be represented by one mutually exclusive content enum. Minimal independent facts preserve later layering without implementing it prematurely.

### ADR-010: Board mutations are explicit, atomic primitives

**Status:** Implemented and verified in STEP 2
**Decision:** One mutable Board owns dense cell state and a unique live-ID index. Place, remove, relocate, and access changes return deterministic result values; failures leave cells, count, and identity index unchanged. No force flag or arbitrary mutable cell reference is exposed.
**Rationale:** Later generation, resolution, shuffle, search, and presentation need stable identity and safe state transitions, but their policies do not belong in STEP 2.

### ADR-011: Isolate deterministic initial board generation

**Status:** Implemented and verified in STEP 3
**Decision:** Add a Unity-free `MathGame.BoardGeneration` assembly depending only on `MathGame.Board` and `MathGame.Core`. It creates a fresh Board, consumes active positions in row-major order, and uses only an injected `IRandomSource`.
**Rationale:** Generation needs the random seam but the Board model should remain dependency-free and policy-neutral. A separate assembly keeps ownership and future consumers explicit.

### ADR-012: Use uniform prototype sampling and sequential board-local IDs

**Status:** Implemented and verified in STEP 3
**Decision:** Prototype population draws once per active cell from the inclusive configured range 1–9 and assigns sequential positive IDs in row-major order. Holes consume neither draws nor IDs. Success returns the next unused ID.
**Rationale:** The GDD specifies the initial range and allows duplicates but provides no weighting table. Uniform integer sampling is the smallest explicit prototype policy; difficulty weights and session-long ID ownership remain deferred.

### ADR-013: Population success is not a solvability claim

**Status:** Implemented and verified in STEP 3
**Decision:** STEP 3 reports only complete, invariant-safe population. It does not retry, search paths, select targets, shuffle, enable input, or label a board playable. STEP 7 must verify a legal target path before exposure.
**Rationale:** Deterministic random population cannot satisfy the GDD's verified-target requirement without the connection and search rules owned by STEPs 4 and 7.

### ADR-014: Connection paths are Unity-free ordered domain state

**Status:** Implemented and verified in STEP 4
**Decision:** Add `MathGame.Connection`, depending only on Board. One mutable ConnectionPath owns ordered captured entries, selected-position membership, and checked live sum while exposing immutable snapshots.
**Rationale:** Player path legality, identity, and addition must be deterministic and shared later by answer and search systems without depending on touch sampling or views.

### ADR-015: Backtrack only through the immediate predecessor

**Status:** Implemented and verified in STEP 4
**Decision:** Entering the immediate predecessor removes exactly the tail. Current-tail and other earlier duplicates are rejected; explicit Cancel clears the entire path. A one-entry path is valid working data, but its answer eligibility remains STEP 5.
**Rationale:** This is the narrow rule consistent with GDD §4.3, prevents loops and multi-cell teleport undo, and avoids pointer jitter clearing the start cell.

### ADR-016: Correct MVP answers require two connected blocks

**Status:** Implemented and verified in STEP 5
**Decision:** Exact equality is Correct only when a snapshot contains at least two entries. One-block equality is Miss/InsufficientConnectionLength; STEP 7 must share this rule.
**Rationale:** GDD solution, reward, and tutorial rules consistently begin at two blocks; permitting target-value taps would bypass the connection mechanic.

### ADR-017: Answer grades use Stage-gated interactive time

**Status:** Implemented and verified in STEP 5
**Decision:** A Stage-driven clock accumulates raw provider deltas only while effective player input is enabled. Misses do not reset the current target timer; correct completion freezes it. Prototype thresholds are inclusive 2/4 seconds.
**Rationale:** GDD excludes every noninteractive phase, pause, app deactivation, and ad interval and treats speed only as optional reward.

### ADR-018: Board resolution returns an atomic replacement Board

**Status:** Implemented and verified in STEP 6
**Decision:** Resolve correct answers by validating and planning off the source Board, then return a completely populated replacement Board plus immutable deltas. Never mutate and roll back the caller's Board.
**Rationale:** MVP boards are tiny, and copy-on-success prevents partial corruption when validation, randomness, or placement fails.

### ADR-019: Mask holes split downward gravity segments

**Status:** Implemented and verified provisionally in STEP 6
**Decision:** Gravity moves toward decreasing Row within vertically contiguous active segments. Inactive holes are hard barriers; refill follows deterministic column/segment/bottom-up order.
**Rationale:** The GDD permits special shapes but does not define fall-through gaps. Segment barriers avoid teleportation and produce reproducible outcomes.

### ADR-020: Reject generic blocked cells during basic resolution

**Status:** Implemented and verified in STEP 6
**Decision:** STEP 6 resolves only full Open numeric boards. Any active Blocked or empty source state returns an explicit unsupported-state failure.
**Rationale:** GDD obstacle types have incompatible layering and gravity behavior; STEP 10 must define them rather than treating one coarse flag as a universal rule.

### ADR-021: Targets require deterministic current-board witnesses

**Status:** Implemented and verified in STEP 7
**Decision:** Search simple Open/occupied orthogonal paths with bounded deterministic DFS and expose one canonical witness per eligible distinct target. Search-limit exhaustion is indeterminate, never deadlock proof.
**Rationale:** GDD forbids presenting an unproven target, while bounded search must fail safely on extreme content.

### ADR-022: Use explicit uniform target and repetition policy

**Status:** Implemented and verified in STEP 7
**Decision:** Select uniformly across distinct proven values. Content supplies a positive consecutive-repeat cap; exclude the capped prior value when alternatives exist and permit it only as a marked sole-candidate fallback.
**Rationale:** GDD requires limiting repetition but supplies neither weights nor a numeric cap. Explicit policy avoids hidden tuning and path-count bias.

### ADR-023: Recover deadlocks with bounded identity-preserving shuffle

**Status:** Implemented and verified in STEP 7
**Decision:** Only after no eligible target exists, perform configured bounded Fisher–Yates attempts over full Open boards, preserving NumberBlock identities/values and proving a witness after every attempt. Recovery has semantic move cost zero.
**Rationale:** Bounded attempts prevent hangs; copy-on-success and re-search prevent unsafe exposure. Obstacle-aware shuffle remains STEP 10.

### ADR-024: StageSession is the authoritative move/objective terminal owner

**Status:** Implemented and verified in STEP 8
**Decision:** Apply correlated answer/resolution attempts exactly once, consume normal Correct moves, update supported objective trackers atomically, and evaluate Success before zero-move Failure.
**Rationale:** Stage lifecycle alone cannot enforce GDD objective and final-move ordering; a Unity-free session makes the product caller deterministic.

### ADR-025: Implement only objectives with authoritative evidence

**Status:** Implemented and verified in STEP 8
**Decision:** Support number removal, specified target completion, and long connections. Reject obstacle, restoration, and special objectives until their owning systems emit typed evidence.
**Rationale:** Inferring those objectives from generic removals or path length would fabricate progress and conflict with later layered systems.

### ADR-026: Undefined score values require explicit configuration

**Status:** Implemented and verified in STEP 8
**Decision:** Stage definitions supply nonnegative base/grade/length score values; the domain has no guessed default. Exact GDD Fever contribution values and semantic special intents are calculated separately without applying a gauge or special.
**Rationale:** The GDD specifies relative rewards and Fever contributions but omits production score/restoration/economy formulas.

### ADR-027: Fever attempts use one atomic controller boundary

**Status:** Implemented and verified in STEP 9
**Decision:** FeverController prospectively derives closed Fever rules, applies the owned StageSession command, and only then commits the nonthrowing Fever snapshot update. Callers cannot commit the two domains separately.
**Rationale:** Separate public commits could diverge combo, score, objectives, and attempt sequence after a partial failure.

### ADR-028: Fever duration counts exact FeverInput time

**Status:** Implemented and verified in STEP 9
**Decision:** A dedicated injected-time clock accumulates only while Stage is exactly FeverInput. All resolution, presentation, pause, inactivity, entry/end, and terminal intervals are excluded; invalid time faults safely.
**Rationale:** GDD defines eight seconds of actual Fever manipulation, not wall-clock duration.

### ADR-029: STEP 9 emits downstream Fever effect intents

**Status:** Implemented and verified in STEP 9
**Decision:** Core Fever supplies zero-move/combo score rules, expanded-removal request, obstacle/restoration multipliers, and exact natural-end tiers without mutating Board, obstacles, restoration, or presentation.
**Rationale:** Those concrete systems do not yet exist and their spatial/formula rules belong to STEPs 10-12.

### ADR-030: Restoration uses fixed exact arithmetic and provisional stage ownership

**Status:** Approved for STEP 11 implementation
**Decision:** Each committed Correct awards `floor(10 × submitted-length factor × Fever factor)` with length factors 1.0/1.2/1.5/2.0, Fever factor 2, no combo factor, and one final floor using exact rational arithmetic. Large Fever-end awards fixed +50; other end tiers emit no restoration evidence. Stage progress is provisional and clamped; world progress alone emits typed 25/50/75/100 milestones. Failure enters a pending decision, Retry/Abandon discard, Continue preserves, and only Success commits additively to world progress.
**Rationale:** Product-approved values resolve the GDD's symbolic formula, provisional lifecycle, additive world application, and presentation boundary.

Use the acyclic direction `Restoration.Contracts <- StageSession` and `Restoration.Contracts <- Restoration -> StageSession`. A sole restoration transaction coordinator binds prospective evidence into the correlated StageSession attempt/system-effect terminal decision and permits a world commit only from the accepted Success snapshot. Concrete art bindings remain STEP 12.

Detected failure enters FailedPendingDecision and preserves restoration. Continue resumes the same run; Retry/Abandon discard it. Success applies `min(WorldCapacity, WorldCurrent + StageCommittedRestoration)` once per stable WorldCommitId. World milestones alone use 25/50/75/100 thresholds. Non-Large Fever-end tiers omit restoration evidence.

### ADR-033: Core implementation and Unity Production are separate conditional phases

**Status:** Accepted

**Decision:** Every STEP is classified as `DOMAIN_ONLY`, `UNITY_FACING`, or `MIXED`. The Unity Client Developer owns approved Domain/Application/code architecture and deterministic core behavior. A dedicated Unity Production Agent conditionally follows core implementation to integrate Unity-facing work into Scenes, Prefabs, serialized views, UI, input/EventSystem, Safe Area/layout, and Editor validation. The Lead/Manager chooses the exact review/test ordering based on whether Unity integration is required before meaningful Play Mode testing.

Stable presentation hierarchies are Prefab/Scene-owned and runtime-bound. Unity Production must not duplicate gameplay rules, recreate established BoardView/UI roots for convenience, infer ownership from names, or silently overwrite designer-authored assets. Player-facing work requires Unity integration verification and an exact manual checklist; unexecuted runtime behavior is reported as manual verification debt rather than a pass.

**Rationale:** Deterministic core tests establish gameplay correctness without Unity lifecycle noise, while an explicit production phase establishes that the same feature is actually composed and usable in Unity. The split preserves Domain independence, designer-friendly Prefabs, explicit Scene ownership, faster automated development, and clearer human Play Mode acceptance.

### ADR-034: Continuous Run is the primary Production gameplay mode

**Status:** Approved for implementation

**Decision:** Preserve legacy finite Stage behavior, but introduce an explicit Continuous Run mode for primary Production gameplay. Continuous Run uses Time as its sole terminal survival resource; normal correct answers cost no moves, objectives do not cause Success, and restoration/world progression is not composed into the primary Run. Time drains throughout every live non-paused Run phase, clamps to a configurable maximum, and recovers exactly once from an already-committed Correct according to configurable Normal/Fast/Perfect values. Expiry observed before commit wins; a prior atomic commit may recover before expiry is observed.

Difficulty advances monotonically by committed correct-answer cycles and initially changes only the proven target range at challenge boundaries. Fever has no additional Time modifier. Run End is exactly-once at Time zero and freezes score, active elapsed duration, run-wide maximum Fever combo, and highest difficulty for a Play Again result. Stable BoardView/UI roots remain serialized and reused.

**Temporary P03 playtest tuning:** initial 35 seconds, maximum 45 seconds, drain 1 second per active second, Normal +1.5 seconds, Fast +2.75 seconds, Perfect +4 seconds, one tier per six committed correct cycles, with target ranges 5-9, 7-11, 9-13, 11-15, and 13-16 capped at the last tier. These values are configuration data and are not final balance. `Docs/P03_SURVIVAL_DIFFICULTY_BALANCE.md` records the economy analysis and playtest risks.

**Rationale:** A distinct mode preserves tested Stage compatibility and transaction boundaries while preventing dummy moves/objectives from creating hidden terminal behavior. Explicit tuning and ordering make the survival clock deterministic and testable without transferring time authority to Unity UI.

## Tracked ambiguities for later STEP designs

### ADR-035: Run content is authored as CSV and consumed as validated JSON

**Status:** Accepted for P04-P06

**Decision:** Survival Time, grade recovery, and ordered difficulty tiers are separate immutable runtime configuration facts. Designers author one Run CSV and one Difficulty CSV. Editor tooling validates and emits schema-versioned JSON; runtime loads only JSON through a repository and resolves the same domain configuration. Missing or invalid content blocks composition. Maximum difficulty remains at the last configured tier. P06 progresses target range only and leaves obstacles, number range, Fever, and drain behavior unchanged.

**Rationale:** This centralizes balance without coupling gameplay to authoring formats, preserves deterministic target proof, and provides a small production pipeline without prematurely building a broad content framework.

- Precise touch tolerance for backtracking/cancellation.
- Target weighting and the numerical limit on consecutive identical targets.
- Weighted number-generation probabilities and stage-specific distribution data.
- Production seed persistence/replay guarantees and session-long block-ID allocation.
- Exact score values remain content-configured; the STEP 11 restoration formula is resolved by ADR-030.

### ADR-031: STEP 12 presentation remains blocked on explicit product contracts

**Status:** Open / implementation blocked

STEP 12 keeps Board, Connection, Answer, Target, Fever, StageSession, obstacle, and restoration state authoritative in their existing domain assemblies. A Unity presentation coordinator may serialize immutable results and acknowledge exact sequence tokens, but views never mutate gameplay or world state.

Implementation requires explicit decisions for Fever area-center selection, touch constants, animation/skip timing, approved assets, exact orientation/layout/accessibility defaults and settings-change timing, result/failure actions, pause behavior during animation, and the MVP disposition of the GDD-required special-block mechanics that have no domain implementation. STEP 10 orchestration must issue exact run/revision-correlated gameplay tokens, and Fever must expose a non-sampling read-only presentation snapshot; neither migration transfers gameplay authority to views. These product rules cannot be inferred safely. Until approved, no STEP 12 production scene/prefab/presenter work begins and STEP 13 remains out of scope.

**Resolution (2026-08-08):** Product approved deterministic footprint-derived Fever centers, 45% logical hit radius with ordered drag interpolation, fixed presentation timings, Reduced Motion at command/reconciliation boundaries, placeholder assets, portrait-only layout, non-color state indicators, exact failure/success fields, pause suspension with stale-token reconciliation, and explicit special-block deferral. STEP 12 implementation is authorized; STEP 13 is not.

**Implementation note (2026-08-13):** STEP 12 uses run/revision/source-correlated envelopes and exact acknowledgements. The Unity layer renders placeholder identity-keyed board objects, logical path/live-sum feedback, HUD and terminal/milestone cues, while all mutations continue through STEP 10/11 coordinators. Static compilation is verified; Unity Edit/Play execution remains unverified because licensing initialization blocks the runner.

### ADR-032: STEP 13 persistence is blocked on progression and recovery policy

**Status:** Open / implementation blocked

STEP 13 must persist successful progression/world restoration and settings without owning Board, StageSession, Fever, Restoration, or Presentation state. Successful world commits require a save-revision-bound prepare/commit plan and durable WorldCommitId/StageRunId uniqueness; active Board/Fever state is not assumed persistable.

The GDD provides themes and qualitative progression but no exact catalog/unlock graph, persisted stage result fields, settings defaults/ranges, interrupted-attempt behavior, durability point, corruption/future-version recovery, v1 migration meaning, or storage backend policy. `Docs/STEP_13_DESIGN.md` records the minimum decisions and a separate lowest-risk recommendation. No production persistence schema is authorized until product approves those rules. STEP 14 remains out of scope.

Exactly-once across restart additionally requires a product-approved durable prospective snapshot or write-ahead commit protocol before irreversible StageSession/world assignment. StageRunId reservation must also become durable before a new run handle is exposed. Primary/backup/WAL revisions require anti-rollback rules so recovery cannot lose applied commit IDs or reserved run IDs.
- Which two obstacle types form the first prototype set and their detailed gravity interaction.
- STEP 10 additionally requires the selected pair's spatial layers, hit aggregation, Fever expansion/end geometry, refill behavior, and objective-count policy; do not infer these from the current coarse Blocked state.
- STEP 10 product addendum now selects Dust and Box and resolves their layers, HP, damage aggregation, Fever potency, Box-to-refill conversion, orthogonal expansion, all end-tier Manhattan geometries, objective evidence, and shuffle preservation. Area centers are explicit resolver inputs; their gameplay selection policy remains an orchestration/presentation decision.
- Exact stage data/content, objective quantities, move limits, and progression gates.
- Save-on-background, interrupted-stage restoration, and corruption/migration policies.
- Analytics payload schemas and once-only delivery rules.
- Concrete ad spacing, consent flow, provider callback policy, and post-ad resume behavior.

### ADR-036: Continuous Run records use a narrow backend-neutral local repository

**Status:** Implemented for Production P07-P08

**Decision:** A finalized Continuous Run result carries a stable run identity and is the only input to `PlayerProgressService`. Persistent records are Best Score, best active Survival duration, highest reached difficulty tier, best Fever combo, total completed runs, and the applied run identities needed for exactly-once replay protection. Presentation does not calculate records.

`IPlayerProgressRepository` belongs to the Unity-independent progress boundary. The prototype implementation stores schema-version 1 JSON beneath `Application.persistentDataPath`, verifies a temporary write, preserves the previous valid primary as a backup, validates parsed invariants, and loads primary then backup then a new-player default. Missing data is normal; malformed data never blocks startup; write failures remain observable and prevent Play Again from silently discarding the in-memory update.

**Scope:** This is temporary/offline prototype storage for Run records only. It does not implement or authorize the broader STEP 13 stage/world/settings model, backend SDK integration, accounts, synchronization, conflict resolution, remote migration, leaderboards, achievements, economy, or meta progression. A future backend can replace the repository implementation without changing Run gameplay or record rules.

### ADR-037: P09 uses official Unity Localization with persisted en/ko selection

**Status:** Implemented; Unity asset migration and Play verification required

**Decision:** Primary Continuous Run HUD/result/common/settings text uses Unity Localization 1.5.12 String Tables. English and Korean are the only supported locales. A saved supported code wins at startup; otherwise Korean devices use Korean and all other devices fall back to English. Language changes update `LocalizationSettings.SelectedLocale` and save an optional locale code in the version-2 P08 DTO without modifying Run records.

Managed presentation contract v7 adds a serialized language button. Editor tooling owns creation/update of the four localization collections and validates missing values visibly at runtime. Domain gameplay remains localization-free.

## Deferred decisions

- Path rules, gravity, and obstacle layering remain deferred to their owning STEP designs.
- Scene/view architecture is deferred until domain rules are approved.
- A save backend, analytics provider, ad provider, and purchasing provider are deferred to their own STEPs.
- Post-MVP arithmetic modes, social systems, seasons, broad boosters, and multiple Fever variants are explicitly out of scope.
