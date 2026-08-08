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

## Tracked ambiguities for later STEP designs

- Whether a one-block path is valid and the precise touch tolerance for backtracking/cancellation.
- Target weighting and the numerical limit on consecutive identical targets.
- Exact score and base restoration-energy formulas where the GDD specifies relative multipliers only.
- Which two obstacle types form the first prototype set and their detailed gravity interaction.
- Exact stage data/content, objective quantities, move limits, and progression gates.
- Save-on-background, interrupted-stage restoration, and corruption/migration policies.
- Analytics payload schemas and once-only delivery rules.
- Concrete ad spacing, consent flow, provider callback policy, and post-ad resume behavior.

## Deferred decisions

- Board generation policy, path rules, gravity, and obstacle layering remain deferred to their owning STEP designs.
- Scene/view architecture is deferred until domain rules are approved.
- A save backend, analytics provider, ad provider, and purchasing provider are deferred to their own STEPs.
- Post-MVP arithmetic modes, social systems, seasons, broad boosters, and multiple Fever variants are explicitly out of scope.
