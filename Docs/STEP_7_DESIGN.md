# STEP 7 Design — Verified Targets and Deadlock Recovery

Status: **APPROVED FOR IMPLEMENTATION**
Designed: 2026-08-08
Source: `Docs/GAME_DESIGN.md` v1.0

## Goal

Search a stable board for legal addition paths, select only a target with a current witness, and recover deadlocks by choosing another verified target before attempting bounded identity-preserving shuffles. Recovery never spends a move and never enables input without proof.

## GDD rules

- §§4.3–4.5: witnesses are simple orthogonal Open/occupied paths with exact addition; no diagonal, reuse, holes, or blocked cells.
- §§5.1–5.2: target values are configured by stage range; early targets require a 2–3-block witness.
- §5.3: input remains disabled during target transition and immediate target repetition is limited.
- §5.4: retain a still-valid target, otherwise regenerate from current-board solutions, and shuffle only when none exist; recovery costs no move.
- §§10.2, 21, and 24: no target may be exposed without a verified path. Generic obstacle rules remain unavailable until STEP 10.

## Approved MVP policies

1. Search uses deterministic DFS: row-major starts and Board's Up, Right, Down, Left neighbors.
2. Paths never repeat a position or BlockId. Minimum correctness length is 2; early configuration uses length 2–3 and targets 5–10.
3. Candidate selection is uniform across distinct proven target values, not weighted by path multiplicity.
4. `MaxConsecutiveIdenticalTargets` is explicit configuration. When the cap is reached, exclude the previous target if another proven value exists; allow it as a marked safety fallback when it is the sole candidate.
5. `MaxShuffleAttempts` and `MaxNodeExpansions` are explicit positive configuration. No hidden retry/default is embedded in the domain.
6. Shuffle preserves complete NumberBlocks (ID/value), topology, access, count, and next-ID ownership. It generates no values or IDs.
7. STEP 7 supports full Open/occupied boards only. Blocked/incomplete boards fail until obstacle semantics exist.

## Assembly and ownership

Add Unity-free `MathGame.Targets`, referencing Board, Answer, and Core. It has no Connection dependency, but its witness legality exactly matches the final Connection/Answer rules. It does not reference Stage, BoardResolution, App, Unity, moves, or views.

- `TargetPathSearcher`: deterministic bounded DFS, no randomness or mutation.
- `SafeTargetSelector`: injected-random selection from sorted proven distinct targets plus explicit history.
- `BoardShuffler`: injected-random Fisher–Yates, copy-on-success replacement and immutable movement deltas.
- `TargetRecoveryCoordinator`: composes search → select → bounded shuffle/re-search.
- A later session owner owns current Board/target/history and invokes Stage transitions.

## Public contracts

`TargetSearchConfig(minTarget, maxTarget, minPathLength, maxPathLength, maxNodeExpansions)` is immutable raw input. Search validates positive ordered bounds, minimum length at least 2, ordered lengths, and a positive expansion cap.

`TargetSolutionStep` contains Position and NumberBlock. `TargetSolution` contains SourceBoard, TargetNumber, copied read-only Steps, Count, and Sum. `TargetSolutionValidation Validate(Board board)` returns `Valid`, `MissingBoard`, `DifferentBoard`, `InvalidCell`, `BlockMismatch`, `DuplicatePosition`, `DuplicateBlockId`, `NotOrthogonallyAdjacent`, or `SumMismatch`. Presentation requires the exact SourceBoard reference; an equivalent replacement is rejected until searched itself.

`TargetSearchStatus`: `Succeeded`, `NoAvailableTarget`, `SearchLimitExceeded`, `InvalidConfiguration`, `MissingBoard`, `UnsupportedBoardState`. `TargetSearchResult` contains `Status`, `IReadOnlyList<TargetSolution> Solutions`, and `NodeExpansions`. Succeeded always has at least one sorted solution; every other status has an empty immutable list. Missing Board precedes config validation; then invalid config; then unsupported Board. Search constructors/results are internal/private; config is a raw immutable holder.

`TargetPathSearcher.Search(Board, TargetSearchConfig)` returns all distinct eligible values with one canonical witness each. Positive values prune after sum reaches/exceeds maximum or maximum path length. Appending a start/root is expansion 1; the Nth append is allowed when cap=N, while attempting N+1 returns SearchLimitExceeded and discards all candidates. Checked sum overflow prunes that branch. “All target values found” uses checked/long range cardinality without range-sized allocation.

`TargetSelectionPolicy(maxConsecutiveIdenticalTargets)` and `TargetHistory(TargetNumber? lastTarget, int consecutiveCount)` are immutable raw inputs. Valid history is `(null,0)` or a valid target with count>=1; negative/zero-with-target, positive-without-target, invalid target, and increment overflow are invalid.

`SafeTargetSelector(IRandomSource)` rejects null. `TargetSelectionStatus` is `Succeeded`, `MissingSearchResult`, `SearchNotSuccessful`, `NoCandidates`, `MissingPolicy`, `InvalidPolicy`, `InvalidHistory`, or `HistoryOverflow`. `TargetSelectionResult` exposes Status, SelectedSolution, UpdatedHistory, and UsedRepetitionFallback. Failure has null solution/history and false fallback. Validation precedence follows the enum list; failures consume zero RNG. Success makes exactly one `NextInt(0,candidateCount)` call, even for one candidate. A different selected value resets history count to 1; the same value increments checked. Out-of-contract random values throw; random exceptions propagate.

`BoardShuffler(IRandomSource)` rejects null. `BoardShuffleStatus` is `Succeeded`, `MissingBoard`, `UnsupportedBoardState`, `InsufficientMovableBlocks`, or defensive `FinalBoardMutationRejected`. `BoardShuffleResult` exposes Status, replacement Board, and immutable `IReadOnlyList<ShuffledBlockDelta> Deltas`; failure has null Board/empty deltas. Validation follows enum order and expected failures consume zero RNG. Shuffle validates a full Open/occupied board with at least two cells, enumerates positions row-major, and performs Fisher–Yates from `n-1` to `1`, one `NextInt(0,i+1)` per iteration. Deltas contain Block/From/To only when changed and are ordered by destination row-major. Identity permutations succeed with zero deltas. Source is never mutated. Final placement rejection is unreachable with valid input and review-only; no test factory is added.

`TargetRecoveryConfig` is a raw immutable holder combining search config, selection policy, and max attempts. `TargetRecoveryCoordinator(IRandomSource)` rejects null and constructs one searcher plus selector and shuffler sharing that exact random instance; therefore shuffle and subsequent selection consume one reproducible stream in algorithm order. It exposes:

```csharp
TargetRecoveryResult SelectNextTarget(Board board, TargetHistory history, TargetRecoveryConfig config);
TargetRecoveryResult RecoverCurrentTarget(Board board, TargetNumber current, TargetHistory history, TargetRecoveryConfig config);
```

Statuses: `CurrentTargetStillValid`, `SelectedOnCurrentBoard`, `RecoveredByShuffle`, `MissingInput`, `InvalidConfiguration`, `InvalidBoardState`, `SearchLimitExceeded`, `ShuffleFailed`, `UnrecoverableDeadlock`. `TargetRecoveryResult` exposes Status, Board, Solution, UpdatedHistory, immutable Deltas, ShuffleAttemptCount, BoardChanged, and constant MoveCost=0. The first three are success. CurrentTargetStillValid returns original Board/current witness, unchanged history, zero attempts/deltas. SelectedOnCurrentBoard returns original Board/selected solution/updated history, zero attempts/deltas. RecoveredByShuffle returns final replacement/solution/history, attempts completed, and original-to-final deltas. Every failure returns null Board/solution/history, empty deltas, BoardChanged false, MoveCost 0; attempt count records completed shuffle attempts.

Missing board/config/history/current-target (for RecoverCurrentTarget) maps to MissingInput first. Invalid nested config/policy/history maps InvalidConfiguration. Search Missing/unsupported maps InvalidBoardState; limit maps SearchLimitExceeded. Shuffle Unsupported maps InvalidBoardState, InsufficientMovableBlocks maps UnrecoverableDeadlock, and defensive placement maps ShuffleFailed. Random out-of-contract values throw `InvalidOperationException`; random exceptions propagate without publishing partial results.

Recovery order:

1. Search unchanged board.
2. For current-target recovery, keep current without random calls when its witness remains valid.
3. Otherwise choose another proven target on the unchanged Board.
4. Only after `NoAvailableTarget`, perform up to `MaxShuffleAttempts`; each attempt shuffles the prior candidate, runs a complete search, and selects only after proof.
5. Search-limit is indeterminate and aborts immediately. Exhaustion returns UnrecoverableDeadlock. Original Board remains unchanged on overall failure.
6. Successful final shuffle deltas map each changed BlockId From its original Board position To its final Board position, omit unchanged identities, and are ordered by final destination row-major, independent of intermediate attempts.

## Stage boundary

Append stable `StageState.RecoveringBoard`. Add `BeginDeadlockRecovery(): PlayerInput -> RecoveringBoard` and allow `BeginTargetPresentation()` from RecoveringBoard. RecoveringBoard is pausable, rejects input, and suspends the interactive clock. Wrong-phase/paused/terminal calls reject without events. After STEP 6, Stage already remains ResolvingAnswer; successful STEP 7 selection then permits existing BeginTargetPresentation. Failure remains noninteractive.

## Acceptance matrix

- Exact canonical witnesses, sorted distinct sums, four directions, no diagonal/reuse/reverse weighting, holes/disconnected shapes, blocked/empty rejection, 2–3 early filtering, positive pruning, checked arithmetic, deterministic expansion counts, exact limit boundary, and no partial result on limit.
- Selector exact random calls/bounds, one/many candidates, history updates, cap exclusion, sole-candidate fallback, invalid policy/history, and source solution immutability.
- Shuffler exact Fisher–Yates calls/permutation, identity attempt, topology/access/ID/value/count preservation, immutable coherent deltas, insufficient/unsupported boards, random faults, and source independence.
- Recovery current-valid no RNG, alternate target without shuffle, shuffle success after one/multiple attempts, identity retry, limit abort, bounded exhaustion, deterministic seeded result, final original-to-final deltas, and zero move cost.
- Witness revalidation rejects stale Board/identity before presentation.
- Stage RecoveringBoard input/pause/focus/clock behavior and transition legality.
- Full Edit/Play regressions pass; no STEP 8+ behavior; independent review has no P0.

## Expected files

Add production/test folders under `Runtime/Targets` and `Tests/EditMode/Targets` for search, selection, shuffle, and recovery. Modify Edit test asmdef. Add RecoveringBoard state/cause/commands and focused Stage/Edit/Play tests. Update architecture/decisions/plan only after verification.

## Deferred/out of scope

Nonuniform difficulty weights, numeric default repetition/attempt limits, constructive permutation solving, moves/objectives, obstacle-aware search/shuffle, special blocks/Fever, target UI/animation/hints, persistence, analytics, and monetization.

## Disposition

STEP 7 is implementation-ready. Search-limit exhaustion or failed bounded recovery can never be treated as permission to expose an unsafe target.
