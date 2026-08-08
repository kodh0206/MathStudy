# STEP 4 Design — Orthogonal Connection Path

Status: **APPROVED FOR IMPLEMENTATION**
Designed: 2026-08-08
Source of truth: `Docs/GAME_DESIGN.md` v1.0
Production code changed by this design pass: No

## Goal

Implement a deterministic, Unity-independent in-progress number connection. It preserves selected block order and identity, accepts only accessible orthogonally adjacent cells, supports immediate-predecessor backtracking and explicit cancellation, and exposes an immutable snapshot with a live addition sum.

It does not interpret touch geometry, submit an answer, compare a target, mutate the Board, or render feedback.

## Design requirements

- **GDD §4.3:** connections are orthogonal only; diagonal reuse, empty cells, and blocked cells are forbidden; reversing through the immediately previous block removes the last selection.
- **GDD §4.4:** MVP arithmetic is addition and selected values/order must support a live expression and sum.
- **GDD §4.5:** reaching or exceeding a target never auto-finishes the connection. Target comparison belongs to STEP 5.
- **GDD §5.2:** later solution search must use the same accessible orthogonal path rules.
- **GDD §14.3:** presentation needs ordered path feedback, supplied here as domain data only.
- **ADR-002 and ADR-008–010:** path logic remains plain C# and consumes Board topology, occupancy, access, and stable block identity without mutating them.

## Resolved rules

1. The first selection may be any active, Open, occupied number cell.
2. Later selections require Manhattan distance exactly one from the current tail.
3. Selecting the immediate predecessor when at least two entries exist removes only the tail and returns `Backtracked`.
4. Selecting the current tail or any other previously selected position returns `AlreadySelected` without mutation.
5. A two-entry path returning to its start backtracks to one entry. A later non-predecessor jump to the start is rejected.
6. Repeated sampling of the current tail is a no-op rejection, never cancellation.
7. `Cancel` clears the complete working path and is idempotent.
8. A one-entry path is valid working data. Whether it can be a correct submitted answer is deferred to STEP 5.
9. Finger release/submit is not a ConnectionPath command. A later adapter obtains an immutable snapshot and passes it to STEP 5.
10. The Board must remain stable during an active interaction. Entries capture BlockId/value so later consumers can detect stale identity; Board locking/versioning is not added here.

## Architecture

Create `MathGame.Connection`, a Unity-free assembly with `autoReferenced: false`, `noEngineReferences: true`, and one dependency on `MathGame.Board`.

```text
MathGame.Connection --> MathGame.Board
MathGame.EditModeTests --> MathGame.Connection
```

### Types

- `ConnectionEntry`: immutable `BoardPosition Position` and `NumberBlock Block`.
- `ConnectionStepResult`: `Added`, `Backtracked`, `OutOfBounds`, `InactivePosition`, `Empty`, `Blocked`, `NotOrthogonallyAdjacent`, `AlreadySelected`, and defensive `SumOverflow`.
- `ConnectionCancelResult`: `Cleared` or `AlreadyEmpty`.
- `ConnectionPathSnapshot`: immutable copied ordered entries, `Count`, `Sum`, and `IsEmpty`. It cannot change after later path commands.
- `ConnectionPath`: sole mutable owner of ordered entries, selected-position membership, and live sum; bound to one Board for its lifetime.

### Public API

```csharp
public sealed class ConnectionPath
{
    public ConnectionPath(Board board);
    public int Count { get; }
    public long Sum { get; }
    public bool IsEmpty { get; }
    public bool Contains(BoardPosition position);
    public ConnectionStepResult TrySelect(BoardPosition position);
    public ConnectionCancelResult Cancel();
    public ConnectionPathSnapshot CreateSnapshot();
}

public sealed class ConnectionPathSnapshot
{
    public IReadOnlyList<ConnectionEntry> Entries { get; }
    public int Count { get; }
    public long Sum { get; }
    public bool IsEmpty { get; }
}
```

`ConnectionPath(null)` throws `ArgumentNullException`. Snapshots are created only by ConnectionPath through an internal/private constructor that copies entries into a private array exposed as `IReadOnlyList<ConnectionEntry>`; callers can enumerate and index it but cannot mutate either the snapshot or live path. No internal List, HashSet, Board mutation method, target, event, input type, or mutable cell reference is exposed.

### Deterministic validation precedence

`TrySelect` applies this order:

1. If the candidate is the immediate predecessor, remove the tail using its captured value and return `Backtracked`. This unwind remains possible even if the Board changed unexpectedly.
2. If the position is otherwise already selected, return `AlreadySelected`.
3. Query Board and distinguish out-of-bounds, inactive, blocked, and empty.
4. If a tail exists, require Manhattan distance exactly one; otherwise return `NotOrthogonallyAdjacent`.
5. Add the positive block value using checked `long` arithmetic. Overflow returns `SumOverflow` atomically.
6. Append the captured entry, membership, and sum; return `Added`.

Structural cell reasons precede adjacency so a touched hole/empty/blocked cell reports its actual state. Backtracking precedes current Board validation so a valid working path can always unwind.

## Acceptance criteria

1. A valid first cell creates one ordered entry with exact position, ID/value, and sum.
2. Out-of-bounds, inactive, empty, and blocked first selections return exact results and preserve empty state.
3. Up, Right, Down, and Left neighbors append; diagonals, gaps, wraparound, and holes do not.
4. Duplicate numeric values with distinct IDs/positions are legal and summed.
5. Immediate-predecessor selection removes exactly one tail; repeated reverse moves unwind one entry at a time.
6. Current-tail and non-predecessor duplicates never mutate the path.
7. A removed tail can be selected again later.
8. Equal/over-target knowledge does not exist and cannot stop extension.
9. Cancel clears entries, membership, and sum; repeating it returns `AlreadyEmpty`; the path is reusable.
10. Snapshots retain historical entries/order/sum after add, backtrack, and cancel.
11. Every rejection preserves order, membership, sum, and Board state.
12. After selecting A→B, removing/replacing/blocking those Board cells cannot alter captured snapshot IDs/values/sum, and selecting predecessor A can still backtrack B using captured state.
13. A null Board constructor argument throws `ArgumentNullException`.
14. Long sums beyond `int.MaxValue` work without wrapping; checked overflow remains a reviewed defensive guard because current dense Board limits make public overflow impractical to construct.
15. All behavior is covered in Edit Mode; no Play Mode test is required.
16. Assemblies compile, all regressions pass, independent review has no P0, and no STEP 5+ behavior appears.

## Expected files

Add production files under `Assets/MathGame/Runtime/Connection`:

- `MathGame.Connection.asmdef`
- `ConnectionEntry.cs`
- `ConnectionStepResult.cs`
- `ConnectionCancelResult.cs`
- `ConnectionPathSnapshot.cs`
- `ConnectionPath.cs`

Add Edit Mode tests under `Assets/MathGame/Tests/EditMode/Connection` and add the Connection reference to `MathGame.EditModeTests.asmdef`. After verification, update current architecture, decisions, and development status.

## Deferred and out of scope

- Minimum valid submitted length and target/correctness comparison (STEP 5).
- Release submission, pointer/touch IDs, screen hit testing, drag interpolation, tolerance, multitouch, and cancellation event mapping (STEP 12).
- Timing, removal, gravity/refill, target search, obstacles, Stage orchestration, rewards, Fever, presentation, persistence, analytics, and monetization.
- Named obstacle rules; Connection consumes only the Board's current authoritative access fact.

## Disposition

STEP 4 is implementation-ready. A generated path is structural input for STEP 5 and never a correctness or solvability claim.
