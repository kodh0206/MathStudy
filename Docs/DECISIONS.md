# MathGame Decisions

## Accepted

### ADR-001: Gameplay design document is mandatory

**Status:** Accepted, implementation blocked pending document  
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

**Status:** Accepted for planning  
**Decision:** The already-present bootstrap, service seams, lifecycle controller, persistence seam, and tests form STEP 1. It requires verification and design reconciliation before it can be marked complete.  
**Rationale:** Reimplementing existing code would violate the inspect-before-change and scope rules.

## Open questions requiring GAME_DESIGN.md

- Board dimensions, allowed values, initial distribution, board shapes, and refill constraints.
- Minimum/maximum path length, whether a single block is valid, exact backtracking gesture behavior, and cancellation rules.
- Target selection weighting, repetition policy, and target/path difficulty controls.
- Wrong-answer consequence, move definition, score formula, combo rules, and exact PERFECT/FAST/NORMAL thresholds.
- Stage objectives, failure conditions (if any), progression gates, stage data, and tutorial rules.
- Fever gain formula, threshold, duration, combo, modifiers, end behavior, and obstacle interaction.
- Obstacle types, hit/removal rules, spawn/layout rules, and whether obstacles participate in gravity.
- Restoration milestones, mapping from gameplay progress, persistence, and visual acceptance criteria.
- Required MVP screens, accessibility, audio/haptics, analytics event schema, ad placements, and monetization rules.

## Deferred decisions

- Concrete board data structures and assembly boundaries are deferred to Design STEP 2.
- Scene/view architecture is deferred until domain rules are approved.
- A save backend, analytics provider, ad provider, and purchasing provider are deferred to their own STEPs.
- Post-MVP arithmetic modes, social systems, seasons, broad boosters, and multiple Fever variants are explicitly out of scope.
