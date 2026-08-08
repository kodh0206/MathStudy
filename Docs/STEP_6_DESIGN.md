# STEP 6 Design — Atomic Board Resolution

Status: **APPROVED FOR IMPLEMENTATION**
Designed: 2026-08-08
Source: `Docs/GAME_DESIGN.md` v1.0

## Goal

Resolve one accepted Correct answer into a new stable logical board through an atomic deterministic transaction: validate exact selected identities, remove them simultaneously, compact survivors downward, refill vacancies, and return immutable ordered deltas plus the next unused block ID.

The source Board is never mutated. Stage remains in `ResolvingAnswer`; target selection and presentation are later responsibilities.

## GDD requirements and explicit boundaries

- §§3.1–3.2 and 21.1 require correct selected blocks to be removed and new blocks to fall.
- §4.6 requires Miss/NoSelection to remove nothing.
- §5.3 permits the next target only after resolution/fall completes and keeps input disabled.
- §9.3 excludes removal/fall from answer timing.
- §10 defines incompatible obstacle gravity semantics. STEP 6 therefore rejects any active Blocked cell instead of inventing a generic obstacle rule.
- §§7 and 9 special-block/Fever effects remain outside this basic numeric transaction.

## Approved policies

1. Gravity is downward toward decreasing Row.
2. Inactive topology holes are hard separators. Each maximal vertically contiguous active segment compacts independently; blocks never cross holes or columns.
3. Columns process left-to-right; segments and their cells process bottom-to-top.
4. Refill destinations use the same deterministic order. Each spawn consumes one integer random draw and one sequential ID.
5. Removal validates exact position, BlockId, and value against the Correct AnswerResult snapshot before any random draw.
6. Every active source cell must be Open and occupied. Blocked or already-empty boards return UnsupportedBoardState until STEP 10.
7. Resolution uses copy-on-success: plan privately, construct a replacement Board, and expose it only on complete success.
8. Submitted ConnectionPath belongs to the old Board and must be discarded after the owner swaps to the replacement.

## Architecture

Add Unity-free `MathGame.BoardResolution`, referencing Board, Answer, Connection where required by public types, and Core randomness. It does not reference Stage, App, Unity, presentation, BoardGeneration, targets, or objectives.

### Request and configuration

- `RefillValueRange`: immutable inclusive positive minimum/maximum; prototype 1–9; maximum must permit exclusive-bound conversion.
- `BoardResolutionRequest`: source Board, Correct AnswerResult, refill range, and positive next unused ID.
- `BoardResolver(IRandomSource)`: stateless synchronous resolver; null dependency throws.

```csharp
public sealed class RefillValueRange
{
    public RefillValueRange(int minimumValue, int maximumValue);
    public int MinimumValue { get; }
    public int MaximumValue { get; }
}

public sealed class BoardResolutionRequest
{
    public BoardResolutionRequest(
        Board board,
        AnswerResult answer,
        RefillValueRange refillValues,
        int nextBlockIdValue);
    public Board Board { get; }
    public AnswerResult Answer { get; }
    public RefillValueRange RefillValues { get; }
    public int NextBlockIdValue { get; }
}

public sealed class BoardResolver
{
    public BoardResolver(IRandomSource randomSource);
    public BoardResolutionResult Resolve(BoardResolutionRequest request);
}
```

Request/range constructors are immutable raw data holders and deliberately retain invalid/null input so Resolve can return stable content failures. Only a null random dependency throws `ArgumentNullException`.

### Results

- `BoardResolutionFailure`: explicit validation/configuration/identity/capacity/unsupported-state/internal failure.
- `RemovedBlockDelta`: original position and exact block, ordered by submitted path.
- `MovedBlockDelta`: source, destination, exact survivor block; only actual moves; ordered column/segment/bottom-up.
- `SpawnedBlockDelta`: destination and new block, ordered refill traversal.
- `BoardResolutionResult`: success/failure, replacement Board only on success, next unused ID only on success, and immutable copied delta lists.

```csharp
public enum BoardResolutionFailure
{
    None,
    MissingRequest,
    MissingBoard,
    MissingAnswer,
    MissingRefillRange,
    AnswerNotCorrect,
    EmptySelection,
    InvalidRefillRange,
    InvalidNextBlockId,
    UnsupportedBoardState,
    DuplicateSelectionPosition,
    DuplicateSelectionBlockId,
    SelectedPositionMissing,
    SelectedBlockMismatch,
    NextBlockIdCollision,
    BlockIdRangeExhausted,
    FinalBoardMutationRejected
}
```

Delta objects expose their documented `Position`/`From`/`To` and immutable `NumberBlock Block`. `BoardResolutionResult` exposes `Succeeded`, `Failure`, `Board`, `NextBlockIdValue`, `IReadOnlyList<RemovedBlockDelta> Removed`, `IReadOnlyList<MovedBlockDelta> Moved`, and `IReadOnlyList<SpawnedBlockDelta> Spawned`. Construction is internal/private. Failure always has null Board, next ID 0, and shared/read-only empty delta lists. Success has Failure.None, non-null replacement, valid next ID, and copied read-only delta arrays.

### Validation before randomness

Validate request, source, answer Correct/nonempty, refill range, positive/capacity-safe next ID, full Open/occupied source, unique submitted positions/IDs, exact current identity/value/access, and a next ID greater than every live source ID. Expected failures consume zero draws and expose no Board.

Normative failure precedence is exactly: missing request; Board; Answer; refill range; Answer not Correct; empty selection; invalid refill bounds; invalid next ID; unsupported source cell; duplicate submitted position; duplicate submitted ID; selected position missing/inactive; exact captured block mismatch (ID or value); next-ID collision/not greater than every live ID; ID range exhaustion. The resolver trusts immutable `AnswerResult.IsCorrect` and does not recompute target arithmetic. `EmptySelection` after a Correct result, duplicate submitted position/ID, and final placement rejection are defensive guards unreachable through valid public AnswerResult/ConnectionPathSnapshot/Board construction; they are verified by code review without weakening immutable boundaries or adding test-only factories. Public Miss and NoSelection results return `AnswerNotCorrect` by precedence.

### Transaction

Snapshot source cells; mark selected identities removed; compact each vertical segment while preserving survivor bottom-to-top order; fill remaining highest vacancies bottom-to-top with injected values and sequential IDs; construct a fresh Board and require every placement to succeed. A faulty random value throws `InvalidOperationException`; random exceptions propagate. The source Board remains unchanged, though random-source rollback is not promised.

Traversal is exact: columns ascending; within each column contiguous segments ordered by lowest Row ascending; within each segment source survivors are read by Row ascending and assigned to active destinations by Row ascending. Move deltas are emitted in assigned destination-row order only when source differs. Remaining refill destinations are the higher rows of that segment and are visited by Row ascending; draw, sequential-ID assignment, and Spawned delta order follow that list. Removed deltas alone follow submitted-path order.

`FinalBoardMutationRejected` is a defensive invariant guard unreachable through valid public inputs and a fresh Board. It is verified by code review and does not justify a test-only Board factory or mutation seam.

Success invariants:

- same topology instance and Open access;
- every active cell occupied;
- final count equals source count;
- survivors retain identity/value;
- removed IDs are absent and spawned IDs unique;
- deltas exactly describe source-to-final transformation.

## Stage boundary

The caller enters `ResolvingAnswer` before invoking the resolver. No Stage production change is needed. Input and answer timing remain disabled throughout and after logical resolution. STEP 7 validates/selects a safe target before `BeginTargetPresentation`.

## Acceptance coverage

- Miss/NoSelection (`AnswerNotCorrect`), nulls, invalid range/ID/collision/capacity, unsupported blocked/empty board, and stale/moved/replaced selected identities fail atomically with zero random draws where preflight applies. Unreachable empty-selection, duplicate-evidence, and final-placement guards are reviewed rather than fabricated through test-only seams.
- Bottom/middle/top, adjacent/nonadjacent, multi-column, and entire-segment removals produce exact gravity and deltas.
- Mask holes split segments; blocks/refills never cross them; concave/disconnected masks remain deterministic.
- Refill proves inclusive bounds, exact draw arguments/count/order, duplicate values, sequential IDs, and returned handoff.
- Faulting/out-of-contract randomness never exposes a partial Board.
- Result collections are immutable; replacement and source Boards are independent.
- Final invariants/delta coherence and repeat/stale resolution are tested.
- Stage remains noninteractive in ResolvingAnswer and pause/resume restores that phase.
- Full Edit Mode and lifecycle regression pass with no P0 and no STEP 7+ logic.

## Expected files

Add `Assets/MathGame/Runtime/BoardResolution` with asmdef, request/range/failure/delta/result/resolver types; add focused Edit Mode tests and the test assembly reference. A targeted existing Stage lifecycle test may be extended, but no BoardResolution Play Mode dependency is required.

## Deferred/out of scope

Target search/deadlock/shuffle, special blocks/chains, obstacles and obstacle damage, moves/objectives/rewards, Fever, animation/view cancellation, presentation, analytics, persistence, and monetization.

## Disposition

STEP 6 is implementation-ready for ordinary Open numeric boards. Obstacle boards remain intentionally unsupported until STEP 10.
