# STEP 3 Design — Deterministic Initial Board Generation

Status: **APPROVED FOR IMPLEMENTATION**
Designed: 2026-08-08
Source of truth: `Docs/GAME_DESIGN.md` v1.0
Production code changed by this design pass: No

## Goal

Create a Unity-independent service that builds a fresh logical board and fills every active topology position with a valid number block. Given equivalent configuration and random-source state, generation must reproduce the same position-to-value and position-to-ID mapping.

STEP 3 produces a structurally valid population only. It does not select a target or claim that the board has a legal target path.

## Relevant game-design requirements

- **GDD §3.1–3.2:** The game loop begins from a number board and later performs target selection, connection, removal, falling, and refill.
- **GDD §4.1, §21.1, and §27:** The prototype board is 5×5. The generator must still accept an approved masked topology so later special shapes do not require a new board model.
- **GDD §4.2:** Initial number values are positive integers from 1 through 9 and duplicates are allowed.
- **GDD §5.2:** A displayed target must have at least one legal orthogonal path; early stages require an available 2–3-block solution.
- **GDD §5.4:** Deadlock handling regenerates a target and shuffles only when no possible target exists. These are STEP 7 responsibilities.
- **GDD §10.2:** Later obstacle placement must preserve a solution. STEP 3 creates no obstacles.
- **GDD §12.2:** Difficulty may change board shape and number-generation probabilities. No weighting table is specified for the prototype.
- **GDD §13.1:** Tutorial moments may use fixed boards. Fixed-layout content is not random generation and remains deferred.
- **GDD §24:** Impossible target states are a named risk, so population success cannot be treated as playability.
- **ADR-002, ADR-003, and ADR-008–010:** Generation remains plain C#, uses `IRandomSource`, consumes the immutable masked topology, and populates through invariant-safe Board operations.

## Approved decisions

1. Prototype generation samples the inclusive range 1–9 using one `IRandomSource.NextInt(minimum, maximum + 1)` call per active cell. This is the minimal uniform integer policy; weighted difficulty policies require a later design decision.
2. Active positions are processed in the topology's documented row-major order. Holes consume no random draw and no block ID.
3. Block IDs are sequential and board-local, beginning at a configured positive value (prototype default 1). A successful result also reports the next unused ID.
4. Every active cell is filled and remains `Open`. Inactive holes remain absent. Obstacles and access restrictions are not generated.
5. Generation returns a fresh Board and never mutates a caller-owned Board.
6. A success result means **population succeeded**, not **playable** or **solvable**.

## Architecture

### Assembly boundary

Create a separate Unity-free assembly:

```text
MathGame.BoardGeneration --> MathGame.Board
MathGame.BoardGeneration --> MathGame.Core
MathGame.EditModeTests  --> MathGame.BoardGeneration
```

`MathGame.BoardGeneration` uses `autoReferenced: false` and `noEngineReferences: true`. Keeping generation separate preserves the dependency-free Board model while reusing `MathGame.Core.Random.IRandomSource`. It must not reference Stage, App, Save, UnityEngine, views, input, targets, or objectives.

### Types and ownership

- **`BoardGenerationConfig`:** immutable request data containing a nullable topology reference, inclusive minimum/maximum value, and first block-ID value. Its constructor records raw input; `BoardGenerator` owns centralized validation and failure codes.
- **`BoardGenerator`:** stateless algorithm with an injected non-null `IRandomSource`. It owns neither seeds nor global random state.
- **`BoardGenerationFailure`:** stable failure codes: `None`, `MissingConfiguration`, `MissingTopology`, `InvalidValueRange`, `InvalidFirstBlockId`, `BlockIdRangeExhausted`, and defensive `BoardMutationRejected`.
- **`BoardGenerationResult`:** immutable success/failure value. Success contains the fresh Board and next unused ID; failure exposes no partial Board.

No additional generator interface, factory graph, ScriptableObject, singleton, retry policy, or event stream is justified in this STEP.

### Public API shape

```csharp
public sealed class BoardGenerationConfig
{
    public BoardTopology Topology { get; }
    public int MinimumValue { get; }
    public int MaximumValue { get; }
    public int FirstBlockIdValue { get; }

    public BoardGenerationConfig(
        BoardTopology topology,
        int minimumValue,
        int maximumValue,
        int firstBlockIdValue = 1);
}

public sealed class BoardGenerator
{
    public BoardGenerator(IRandomSource randomSource);
    public BoardGenerationResult Generate(BoardGenerationConfig config);
}

public sealed class BoardGenerationResult
{
    public bool Succeeded { get; }
    public BoardGenerationFailure Failure { get; }
    public Board Board { get; }
    public int NextBlockIdValue { get; }
}
```

Nullable reference annotations may express the validation inputs even if the Unity project's language settings do not enforce them. `Generate(null)` returns `MissingConfiguration`; a config whose topology is null returns `MissingTopology`. The config constructor intentionally does not throw for invalid range/ID data so generation can report the documented stable failure. On failure, `Succeeded` is false, `Board` is null, and `NextBlockIdValue` is 0. On success, `Failure` is `None`, `Board` is non-null, and `NextBlockIdValue` is a valid positive unused ID. Result construction uses private constructors/factories so contradictory combinations cannot be created publicly.

### Configuration contract

- Topology is required and remains immutable under STEP 2.
- Minimum and maximum are positive and ordered.
- The maximum cannot be `int.MaxValue`, because the existing random API requires an exclusive upper bound.
- The first block ID is positive.
- Checked preflight arithmetic must prove that every assigned ID and the returned next-unused ID remain positive and within `int` range.
- The prototype caller uses a rectangular 5×5 topology, values 1–9, and first ID 1. The generator itself does not hard-code those values.

### Generation algorithm

1. Validate the complete request before consuming randomness.
2. Construct a fresh empty Board from the configured topology.
3. Iterate active positions in row-major order.
4. For each active position, draw exactly once using the configured inclusive range, assign the next sequential ID, construct a NumberBlock, and require `TryPlaceBlock` to succeed.
5. Return success only after every active position is open and occupied and `BlockCount` equals active count.

If a Board mutation is unexpectedly rejected, discard the local Board and return `BoardMutationRejected`. This is an internal invariant guard: valid public inputs cannot trigger it with the approved fresh-Board architecture, so it is verified by code review rather than a test-only factory or mutation seam.

Exceptions from a faulty injected random source propagate; the incomplete local Board remains unreachable and random-source rollback is not promised. If a source violates `IRandomSource.NextInt` by returning a value outside the requested half-open range, `BoardGenerator` throws `InvalidOperationException` before placement. It must not clamp, retry, map the value, or publish a successful result.

### Determinism contract

Equivalent immutable topology, configuration, and fresh random sources in equivalent state produce identical position/ID/value mappings, call counts, and next-ID results. Reusing one stateful source across calls advances its sequence and is not required to reproduce the first result.

Different seeds are allowed, but not guaranteed, to produce different boards. Tests must not make probabilistic inequality assertions.

## Solvability boundary

**DESIGN:** GDD §5.2 requires every displayed target to have a verified path, including an early 2–3-block solution. GDD §5.4 and §24 require explicit deadlock recovery.

**CURRENT:** STEP 2 supplies board topology and neighbors, but path rules, available-sum search, target selection, and shuffle do not exist. The development plan assigns those responsibilities to STEPs 4 and 7.

**CONFLICT:** A deterministic random 1–9 population is reproducible but cannot prove that a suitable target exists.

**IMPACT:** STEP 3 output must not be shown as playable, paired with an arbitrary target, transition Stage into input, or start answer timing.

**RECOMMENDATION:** STEP 7 must enumerate legal paths/available sums, expose only a verified target, and apply the GDD deadlock policy. STEP 3 must not retry random boards until one appears solvable or hide search logic inside generation.

## Expected implementation files

Add:

- `Assets/MathGame/Runtime/BoardGeneration/MathGame.BoardGeneration.asmdef`
- `Assets/MathGame/Runtime/BoardGeneration/BoardGenerationConfig.cs`
- `Assets/MathGame/Runtime/BoardGeneration/BoardGenerationFailure.cs`
- `Assets/MathGame/Runtime/BoardGeneration/BoardGenerationResult.cs`
- `Assets/MathGame/Runtime/BoardGeneration/BoardGenerator.cs`
- `Assets/MathGame/Tests/EditMode/BoardGeneration/BoardGenerationConfigTests.cs`
- `Assets/MathGame/Tests/EditMode/BoardGeneration/BoardGeneratorTests.cs`

Modify during implementation:

- `Assets/MathGame/Tests/EditMode/MathGame.EditModeTests.asmdef`
- After verification only: `Docs/ARCHITECTURE.md`, `Docs/DECISIONS.md`, and `Docs/DEVELOPMENT_PLAN.md`.

No current Board, Core, Stage, App, Save, scene, or Play Mode production file requires modification.

## Acceptance criteria and Edit Mode tests

1. An explicit 5×5, 1–9 configuration produces 25 open occupied cells, IDs 1–25 in row-major order, and next ID 26.
2. Values remain inside the inclusive configured range; scripted draws prove the lower and upper boundaries map correctly to the exclusive random API.
3. Duplicate numeric values are accepted while every live ID remains unique and discoverable.
4. A masked, concave, or disconnected topology fills every and only active position. Holes consume neither a draw nor an ID.
5. Two equivalently seeded sources produce identical complete snapshots and identical next IDs.
6. Random call arguments, order, and count are exact: one integer draw per active cell and none during failed preflight.
7. Repeated generation returns independent Board instances. Mutating one result cannot alter another or the topology.
8. Null configuration/topology, non-positive or reversed ranges, `int.MaxValue` maximum, invalid first ID, and ID exhaustion return exact failures with a null Board and next ID 0.
9. A throwing or out-of-contract random source exposes no result or partial Board. The unreachable defensive Board-mutation guard is checked in code review and does not justify a production test seam.
10. Success satisfies Board public invariants: block count equals active count, each active snapshot is open and occupied, each ID resolves to its generated position, and holes remain inactive.
11. Near-ID-capacity valid and invalid boundaries are tested without overflow or wraparound.
12. Same minimum and maximum remain deterministic and follow the documented one-draw-per-cell rule.
13. No target, path, solution, retry, shuffle, refill, gravity, obstacle, stage/input, or view behavior appears in the implementation.
14. BoardGeneration, Board, Core, and Edit Mode test assemblies compile; all relevant Edit Mode tests pass; existing Play Mode tests remain a regression check; independent review reports no P0.

## Edge cases

- 1×1, one-row, one-column, sparse, concave, and disconnected valid topologies are generatable even if later content validation rejects them as unplayable.
- A topology with one active cell consumes one draw and one ID.
- All cells may receive the same valid value.
- `maximum + 1`, `firstId + activeCount`, and dense topology size arithmetic must not overflow.
- A random source that throws propagates its exception. A source returning outside the requested range causes `InvalidOperationException`; neither case publishes a result or partial board.
- Source collection order used to create a mask cannot affect row-major generation order.
- A second call using the same stateful source continues that source's sequence; reproducibility requires an equivalently initialized source.

## Deferred decisions

- Weighted value distributions and their stage/difficulty data.
- Fixed tutorial-board layouts.
- Production seed storage, replay compatibility, and save integration.
- Session-long block-ID allocation across removal/refill, retry, shuffle, and persistence.
- Topology connectivity and minimum-playability validation.
- Legal path rules, target weighting/repetition, solution search, deadlock recovery, and shuffle.

## Out of scope

- Connection paths, sums, and answer timing.
- Target generation, path search, solvability guarantees, retries, deadlock, and shuffle.
- Removal, gravity, refill, and refill distribution.
- Obstacles, special blocks, blocked-cell generation, Fever, objectives, and progression.
- Stage transitions, input enabling, scenes, views, animation, UI, audio, persistence, analytics, ads, and monetization.
- Weighted distributions, fixed tutorial content, or post-MVP arithmetic.

## Disposition

STEP 3 design is approved and implementation-ready. The next valid command is `Implement STEP 3`; implementation must stop after STEP 3 verification and independent review.
