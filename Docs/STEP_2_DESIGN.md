# STEP 2 Design — Board Domain Model

Status: **APPROVED FOR IMPLEMENTATION**
Designed: 2026-08-08
Source of truth: `Docs/GAME_DESIGN.md` v1.0
Production code changed by this design pass: No

## Goal

Create a deterministic, Unity-independent logical board that can represent the MVP 5×5 grid and later masked grid shapes while keeping topology, active cells, empty cells, number occupancy, identity, and normal access distinct.

STEP 2 supplies invariant-safe queries and primitive mutations. It does not generate a board, select paths, validate sums, remove answers, apply gravity/refill, search solutions, or implement obstacle/special-block behavior.

## Relevant game-design requirements

- **GDD §3.1–3.2:** The same logical board participates in target, connection, validation, removal, fall, Fever, obstacle, and restoration phases.
- **GDD §4.1 and §27:** Prototype/MVP default is 5×5, but the domain must not hard-code one size.
- **GDD §4.2:** Number blocks display explicit positive integer values; prototype generation uses 1–9 and duplicate values are allowed.
- **GDD §4.3:** Only orthogonally adjacent cells connect; diagonals, empty cells, and blocked cells cannot be traversed.
- **GDD §5.2 and §5.4:** Later solution search and deadlock recovery require deterministic board inspection and safe mutation.
- **GDD §7:** Special blocks retain a displayed number value. STEP 2 must not tie numeric identity to a plain-only view type.
- **GDD §10:** Obstacles may block a cell, coexist with a number, stay fixed, or occupy a tile layer. The core cannot flatten topology, number, and obstacle into one mutually exclusive enum.
- **GDD §12.2:** Later difficulty may use special board shapes.
- **GDD §21.1:** Board, values, orthogonal connection, removal/fall, solution checks, and two obstacle types are prototype requirements; STEP 2 provides only their data substrate.
- **GDD §24:** Impossible-solution boards are a known risk, so full board enumeration must be deterministic.
- **ADR-002:** Board state remains independent of GameObjects and presentation.

## Functional requirements

### Coordinate and topology

1. `BoardPosition` is an immutable value with integer `Column` and `Row`, value equality, stable hashing, and no Unity type dependency.
2. A topology has positive `Width` and `Height` and a non-empty explicit set of active positions inside those extents.
3. Positions inside extents may be inactive holes. Out-of-bounds, inactive, active-empty, and active-occupied are distinct states.
4. The model accepts valid sizes other than 5×5. The MVP default belongs to STEP 3 configuration/generation.
5. A rectangular factory activates every position; a masked factory copies and validates its input.
6. Active positions enumerate deterministically in row-major order: increasing `Row`, then increasing `Column`.
7. Orthogonal neighbors enumerate in fixed `Up, Right, Down, Left` order, omitting bounds and holes. Diagonals never appear.
8. Domain origin is lower-left and rows increase upward. Presentation may map coordinates differently. Gravity behavior remains STEP 6.
9. Disconnected and concave masks are representable. Connectivity validation is deferred until a stage design requires it.

### Cells and number blocks

10. Every active position owns one logical cell; inactive positions own none.
11. An active cell contains zero or one `NumberBlock`.
12. `BlockId` is a stable positive logical identity independent of GameObjects.
13. `NumberBlock` is immutable and contains a valid `BlockId` plus a positive integer `Value`.
14. Equal numeric values with different IDs are valid. One live ID cannot occupy multiple cells.
15. Values above 9 remain representable because 1–9 is generation policy, not a permanent value-type invariant. STEP 3 enforces the prototype range.
16. Default struct values can bypass constructors, so placement boundaries reject an invalid ID or value without mutation.
17. Clearing a block leaves an active empty cell; it does not create a hole or “removed” marker.

### Access and future layering

18. Each active cell has minimal `CellAccess.Open` or `CellAccess.Blocked` state.
19. An open occupied cell is structurally eligible for later normal selection/removal. Empty, inactive, out-of-bounds, and blocked cells are not.
20. Access is independent of occupancy: an occupied number may become blocked so future locked/iced-number states are representable. STEP 2 does not explain why it is blocked.
21. Normal place, remove, and relocate operations reject blocked endpoints. Access can be set before or after occupancy.
22. Do not add named obstacle types, durability, damage, tile overlays, special-block kinds, or a generic component framework. STEP 10 replaces or derives access from the approved layered obstacle model.

### Board ownership and mutations

23. One `Board` instance exclusively owns mutable cell state and a live block-ID index. Topology, positions, IDs, blocks, and snapshots are immutable values.
24. Public snapshots and enumerations cannot mutate board internals.
25. Board queries distinguish out-of-bounds and inactive positions explicitly.
26. `TryFindBlock` locates a live ID deterministically without scanning presentation objects.
27. `TryPlaceBlock` succeeds only at an active, open, empty cell with a valid unique block.
28. `TryRemoveBlock` succeeds only at an active, open, occupied cell and returns the exact removed block.
29. `TryRelocateBlock` moves an existing block between distinct active, open cells when the destination is empty. It defines no gravity order or rule.
30. `TrySetAccess` changes access on any active cell, including occupied cells, and is idempotent when already in the requested state.
31. Expected state conflicts return explicit results. Invalid constructor arguments throw documented argument exceptions.
32. Every failed mutation is atomic: cell state, block count, and ID index remain unchanged.

### Independence

33. Board types contain no `UnityEngine`, GameObject, scene, input, UI, rendering, animation, analytics, ads, persistence backend, time, or randomness dependency.
34. All STEP 2 behavior is verifiable in Edit Mode without scenes.

## Architecture

### Assembly

Create `MathGame.Board` with:

- no custom assembly references;
- `noEngineReferences: true` to enforce Unity independence;
- `autoReferenced: false` so future consumers opt into the dependency.

`MathGame.EditModeTests` adds an explicit reference to `MathGame.Board`. No Stage, App, Core, Save, or Play Mode assembly changes are required.

```text
MathGame.Board         independent, Unity-free
MathGame.EditModeTests ---> MathGame.Board
```

### Dense masked representation

The board uses dense internal arrays indexed by:

```text
index = Row * Width + Column
```

An immutable active mask distinguishes cells from holes. This gives constant-time lookup and deterministic traversal while remaining simple for the tiny 5×5/6×6 domain. Sparse dictionaries and arbitrary graph topology are unnecessary.

### Types and responsibilities

- **`BoardPosition`:** coordinate value, equality, hash, diagnostic string.
- **`BlockId`:** validated stable positive identity with `IsValid` protection for default structs.
- **`NumberBlock`:** immutable ID/value pair with `IsValid` protection.
- **`BoardTopology`:** immutable extents and active mask; bounds/active queries; deterministic active and neighbor enumeration.
- **`CellAccess`:** `Open` or `Blocked` structural fact only.
- **`BoardCellSnapshot`:** immutable position, access, nullable block, and derived `HasBlock`/normal-eligibility facts.
- **`CellLookupResult`:** `Succeeded`, `OutOfBounds`, or `InactivePosition`.
- **`BoardMutationResult`:** explicit mutation outcome.
- **`Board`:** mutable owner of cell states, unique live-ID index, block count, queries, and primitive atomic mutations.

### Public API shape

Queries:

```csharp
BoardTopology Topology { get; }
int BlockCount { get; }
bool IsWithinBounds(BoardPosition position)
bool IsActive(BoardPosition position)
CellLookupResult TryGetCell(BoardPosition position, out BoardCellSnapshot cell)
bool TryFindBlock(BlockId id, out BoardPosition position)
IEnumerable<BoardPosition> EnumerateActivePositions()
IEnumerable<BoardPosition> EnumerateOrthogonalNeighbors(BoardPosition position)
```

Neighbor enumeration returns an empty sequence for a non-active source; callers use bounds/active queries when they need the reason. This keeps geometric iteration simple while cell lookup retains explicit failure distinctions.

Mutations:

```csharp
BoardMutationResult TryPlaceBlock(BoardPosition position, NumberBlock block)
BoardMutationResult TryRemoveBlock(BoardPosition position, out NumberBlock removed)
BoardMutationResult TryRelocateBlock(BoardPosition source, BoardPosition destination)
BoardMutationResult TrySetAccess(BoardPosition position, CellAccess access)
```

`BoardMutationResult` contains at least:

```text
Succeeded
AlreadyInRequestedState
OutOfBounds
InactivePosition
InvalidBlock
InvalidAccess
Blocked
Occupied
Empty
DuplicateBlockId
SourceEqualsDestination
```

Validation order must be documented and consistent so an operation with multiple faults returns a deterministic result. Recommended order: argument/value validity, coordinate bounds/activity, same-source check where relevant, access, occupancy, identity conflict.

### Invariants

- Dimensions are positive; topology is non-empty; active positions are unique and in bounds.
- Inactive positions never contain cell state or appear as neighbors.
- Every occupied cell has one valid block; every live ID maps to exactly one occupied cell.
- `BlockCount` equals occupied active cells and live ID-index entries.
- Failed operations do not change cells, count, or ID index.
- Caller-owned topology inputs and returned data cannot mutate internal state.
- No public arbitrary cell setter, force flag, or mutable cell reference exists.

## Data flow to later STEPs

```text
STEP 3 configuration/generator -> BoardTopology -> Board -> validated placements
STEP 4 path selection          -> snapshots + orthogonal neighbors (read-only)
STEP 6 resolution              -> remove/relocate/place primitives -> resolution deltas
STEP 7 solution search         -> deterministic full enumeration
STEP 10 obstacle rules         -> layered state derived onto preserved topology/identity
STEP 12 presentation           -> snapshots/results; never owns logical state
```

## Files expected during `Implement STEP 2`

### Production files to add

- `Assets/MathGame/Runtime/Board/MathGame.Board.asmdef`
- `Assets/MathGame/Runtime/Board/BoardPosition.cs`
- `Assets/MathGame/Runtime/Board/BlockId.cs`
- `Assets/MathGame/Runtime/Board/NumberBlock.cs`
- `Assets/MathGame/Runtime/Board/CellAccess.cs`
- `Assets/MathGame/Runtime/Board/BoardCellSnapshot.cs`
- `Assets/MathGame/Runtime/Board/CellLookupResult.cs`
- `Assets/MathGame/Runtime/Board/BoardMutationResult.cs`
- `Assets/MathGame/Runtime/Board/BoardTopology.cs`
- `Assets/MathGame/Runtime/Board/Board.cs`

Unity `.meta` files are generated alongside new assets.

### Test files to add

- `Assets/MathGame/Tests/EditMode/Board/BoardPositionTests.cs`
- `Assets/MathGame/Tests/EditMode/Board/BoardTopologyTests.cs`
- `Assets/MathGame/Tests/EditMode/Board/NumberBlockTests.cs`
- `Assets/MathGame/Tests/EditMode/Board/BoardTests.cs`

### Files to modify

- `Assets/MathGame/Tests/EditMode/MathGame.EditModeTests.asmdef`
- After implementation: `Docs/ARCHITECTURE.md`, `Docs/DECISIONS.md`, and `Docs/DEVELOPMENT_PLAN.md`.

## Acceptance criteria and Edit Mode tests

### Value types

1. Position equality, inequality, hashes, and diagnostics are consistent.
2. Non-positive IDs and values are rejected by constructors.
3. Default/otherwise invalid blocks are rejected at board boundaries without mutation.
4. Duplicate numeric values with different IDs are valid.

### Topology

5. A rectangular 5×5 topology has 25 active positions in documented row-major order.
6. 1×1, 1×N, N×1, arbitrary positive extents, concave masks, holes, and disconnected islands are representable.
7. Invalid dimensions, empty masks, duplicate positions, and out-of-bounds active positions are rejected deterministically.
8. Mutating the caller’s source collection after construction cannot change topology.
9. Bounds, inactive holes, and active positions return distinct query facts.
10. Center, edge, corner, and hole-adjacent neighbor queries return only active orthogonal positions in `Up, Right, Down, Left` order.
11. Non-active neighbor sources return no positions and never fabricate cells.

### Board state and mutations

12. Every active cell begins open and empty; holes return no cell snapshot.
13. Placement updates lookup, block count, and ID lookup consistently.
14. Duplicate live IDs, occupied cells, blocked cells, holes, bounds errors, and invalid blocks return exact failure results with no state change.
15. Removal returns the exact block, empties the cell, removes its ID mapping, and decrements count.
16. Removing empty/blocked/inactive/out-of-bounds cells fails atomically.
17. Relocation preserves ID/value/count and updates the index exactly once.
18. Same-source, empty-source, occupied/blocked destination, inactive, and out-of-bounds relocation failures preserve both endpoints and the index.
19. Access can transition open/blocked/open for empty and occupied cells. Repeating the same state is idempotent.
20. A blocked occupied cell remains fully queryable but normal removal/relocation is rejected.
21. Enumeration and snapshots do not expose mutable internal collections or cell references.
22. Public observations always satisfy unique-ID and block-count invariants after arbitrary valid/failed mutation sequences.

### Verification gate

23. The Unity-free Board assembly compiles with `noEngineReferences` and no custom dependency.
24. All new and existing Edit Mode tests pass.
25. Existing STEP 1 Play Mode tests remain passing if rerun; no new Play Mode test is required for pure STEP 2 behavior.
26. Independent review reports no P0 and no later gameplay appears in the diff.

## Ambiguities resolved for STEP 2

- **Topology connectivity:** disconnected masks are representable; validation is later content policy.
- **Coordinate convention:** lower-left origin, increasing rows upward; visual mapping and gravity rules remain later.
- **Neighbor order:** deterministic `Up, Right, Down, Left`.
- **Values outside 1–9:** model accepts all positive values; STEP 3 enforces 1–9 generation.
- **Blocked occupied cells:** allowed as a minimal structural fact because GDD locked/iced number states require coexistence. No obstacle semantics are inferred.
- **Special metadata:** not represented yet; stable block identity/value remain extensible.
- **Mutation model:** mutable Board owner with explicit atomic results; no immutable board copies or event stream.

## Deferred ambiguities

- Whether one-block paths are valid.
- Exact gravity traversal through irregular holes.
- Which two obstacle types form the prototype and how obstacle/tile/number layers compose.
- Whether special-block orientation comes from path direction and the exact area radius.
- Whether future path/search systems treat a blocked geometric neighbor as visible-but-unusable or filter it themselves. STEP 2 returns geometric active neighbors and exposes access separately.

## Out of scope

- Random generation, number distribution, and 5×5 stage defaults.
- Touch/drag path state, duplicate prevention, backtracking, cancellation, and live sums.
- Answer validation, timing, targets, solution search, deadlock, and shuffle.
- Answer removal transactions, gravity, falling, refill, and resolution deltas.
- Named obstacle/special-block types, layers, durability, damage, effects, or chains.
- Moves, objectives, success/failure, Fever, restoration, UI, scenes, animation, audio, persistence, analytics, ads, or monetization.
- Events, undo/history, replay, serialization, pooling, arbitrary graph topology, or speculative ECS/rules frameworks.

## Disposition

STEP 2 design is approved and implementation-ready. The next valid command is `Implement STEP 2`; implementation must stop after STEP 2 verification and review.
