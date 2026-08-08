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

## Tracked ambiguities for later STEP designs

- Precise touch tolerance for backtracking/cancellation.
- Target weighting and the numerical limit on consecutive identical targets.
- Weighted number-generation probabilities and stage-specific distribution data.
- Production seed persistence/replay guarantees and session-long block-ID allocation.
- Exact score and base restoration-energy formulas where the GDD specifies relative multipliers only.
- Which two obstacle types form the first prototype set and their detailed gravity interaction.
- Exact stage data/content, objective quantities, move limits, and progression gates.
- Save-on-background, interrupted-stage restoration, and corruption/migration policies.
- Analytics payload schemas and once-only delivery rules.
- Concrete ad spacing, consent flow, provider callback policy, and post-ad resume behavior.

## Deferred decisions

- Path rules, gravity, and obstacle layering remain deferred to their owning STEP designs.
- Scene/view architecture is deferred until domain rules are approved.
- A save backend, analytics provider, ad provider, and purchasing provider are deferred to their own STEPs.
- Post-MVP arithmetic modes, social systems, seasons, broad boosters, and multiple Fever variants are explicitly out of scope.
