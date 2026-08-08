# STEP 10 Design - Dust, Box, and Fever Destruction

Status: **APPROVED FOR IMPLEMENTATION**  
Designed: 2026-08-08  
Source: GDD v1.0 plus approved STEP 10 product decisions

## Goal and scope

Implement exactly Dust and Box as layered logical obstacles; integrate them with selection, target search, shuffle, atomic normal/Fever answer resolution, all Fever end tiers, gravity/refill, typed objective evidence, and terminal ordering.

Deferred: Ice, Lock, Fixed Block, Pollution Tile, all special blocks/chains, restoration amounts, presentation, persistence, analytics, and post-MVP obstacle variants.

## Approved rules

- Dust is a stationary 1-HP overlay attached to a cell. It coexists with a number, is not directly selectable, and does not move with falling numbers. Removing the number in its own cell deals one damage and destroys it.
- Box is a stationary 2-HP occupying obstacle. Its cell has no number and is not selectable/refillable while Box survives. It receives one hit per resolver transaction when any orthogonally adjacent number is removed.
- Normal-origin evidence gives Box damage 1. Any Fever-origin evidence gives damage 2. Overlapping evidence uses the highest potency once; it never stacks.
- Destroyed Box is removed and its coordinate becomes an ordinary number slot before gravity/refill in the same transaction.
- Fever answer expansion removes selected numbers plus eligible number cells at Manhattan distance exactly one from selected cells. Diagonals and recursive expansion are excluded; positions are deduplicated.
- RandomThree selects up to three eligible number blocks deterministically without replacement.
- Small, Center, and Large remove eligible numbers within Manhattan radius 1, 2, and 3 respectively around an explicitly supplied center. Geometry clips to active cells; holes, Boxes, and cells without removable numbers are ignored.
- Obstacles are never directly selected by Fever footprints. Dust/Box damage derives only from the final removed-number evidence.
- Every Dust/Box destruction counts toward the matching obstacle objective, regardless of normal/Fever/end origin.
- Shuffle preserves obstacle identity, kind, position, HP/state and moves only numbers.

## Layered Board model

Every active coordinate has immutable topology and a current `CellRole`:

- `NumberSlot`: exactly one NumberBlock in every published stable Board, optional live Dust, no Box.
- `BoxSlot`: exactly one live Box, no number or Dust.

Box destruction converts BoxSlot to NumberSlot inside the copy-on-success resolver. No destroyed/empty BoxSlot or empty NumberSlot is published. Holes contain no layers.

Board values:

- `ObstacleId`: positive immutable identity, unique across Dust and Box.
- `ObstacleKind`: Dust and Box only.
- `DustState`: ID and fixed current/max HP 1.
- `BoxState`: ID, current HP 1-2, max HP 2.
- `BoardLayout`: immutable role mask aligned with BoardTopology.
- `BoardCellSnapshot`: Position, Role, optional Block/Dust/Box and derived `IsSelectable`, `IsRemovableNumber`, `IsMovableNumber`, `IsRefillable`, and `IsGravityBarrier`.

BlockIds remain unique independently. Dust is cell-attached: moving a number out of or into a dusty slot does not move/damage Dust. Only removing the number at that coordinate damages it.

Structural Board operations return explicit atomic results and enforce legal layers. `CellAccess` ceases to be mutable authority; remove `TrySetAccess` and migrate all callers to derived snapshot facts. An obsolete read-only compatibility projection may exist only during the single compile migration.

## Assembly direction

- Structural roles/IDs/states remain in `MathGame.Board`.
- Add Unity-free `MathGame.Obstacles -> MathGame.Board` for layouts, builder, damage planning, and evidence.
- `MathGame.BoardResolution -> MathGame.Board, MathGame.Obstacles, MathGame.Answer, MathGame.Core` plus exposed model dependencies. It must not depend on Fever.
- Connection and Targets continue depending on Board and consume derived eligibility.
- StageSession consumes typed BoardResolution evidence.
- Fever remains geometry-free; orchestration maps FeverEndResult to a closed resolution request.
- Add Unity-free `MathGame.ObstacleFlow -> MathGame.Fever, MathGame.BoardResolution, MathGame.StageSession, MathGame.Targets, MathGame.Stage`. It owns the cross-domain publishability transaction and contains no rule geometry or presentation policy.

## Initial layout

`ObstacleLayoutEntry` factories:

- `Dust(position, obstacleId)` preserves the generated number and adds HP1 overlay.
- `Box(position, obstacleId)` replaces the setup number, changes the coordinate to BoxSlot, and adds HP2 Box. Setup removal is diagnostic only, not gameplay/objective evidence, and its BlockId is never reused.

`ObstacleBoardBuilder` is copy-on-success. Validation precedence: missing inputs, duplicate obstacle IDs, duplicate positions, bounds/hole, unsupported kind, incompatible source/layer, final invariant failure. The result is invariant-safe but not input-ready; higher orchestration must obtain a STEP 7 witness before presentation.

## Closed resolution requests

Use separate immutable request types/factories so invalid mode combinations are unrepresentable:

1. `NormalAnswerResolutionRequest`: source Board, Correct AnswerResult, refill range, next BlockId.
2. `FeverAnswerResolutionRequest`: same inputs with fixed orthogonal expansion.
3. `FeverEndResolutionRequest`: source Board, neutral FeverEndPattern, positive BoardSystemEffectId, optional center, refill range, next BlockId.

`BoardSystemEffectId` is a positive value owned by BoardResolution models. StageSession consumes it through BoardResolutionResult and therefore introduces no reverse dependency. ObstacleFlow maps FeverEndResult.EffectTier to the neutral FeverEndPattern; BoardResolution never references Fever.

Center rules:

- None and RandomThree forbid a center.
- Small/Center/Large require a center that is an active topology coordinate on the exact source Board.
- The center need not itself contain a number; the radius is clipped to removable numbers. This avoids inventing a center-selection policy.
- Gameplay orchestration/presentation supplies the center. Resolver never chooses board center, random, last answer, or “best” position.

Invalid center presence/absence or inactive/out-of-bounds center fails before RNG/mutation.

## Removal footprints and ordering

Evidence records Position, NumberBlock, Cause, and Origin.

Causes: Selected, FeverExpanded, FeverEndRandom, FeverEndSmall, FeverEndCenter, FeverEndLarge. Origins: Normal or Fever.

- Normal: exact selected path positions only; origin Normal.
- Fever answer: selected path plus every active removable number at Manhattan distance one from any selected position; all origin Fever.
- RandomThree: pool every removable number row-major; choose `k=min(3,count)` by partial Fisher-Yates. For i=0..k-1 call `NextInt(i,count)`, validate/swap/select. Exactly k draws, then sort result positions row-major.
- Small/Center/Large: include every removable number within Manhattan distance <=1/2/3 of supplied center; origin Fever; no selection RNG.
- None: successful zero-removal system effect and zero RNG.

Answer results place selected deltas first in exact submitted-path order; Fever-expanded collateral follows row-major. End-effect removals are row-major regardless of random draw order. Every BlockId appears once.

## Evidence-driven obstacle damage

Compute from the complete deduplicated removed-number evidence against the source Board before mutation:

- Dust qualifies only when evidence exists at its own coordinate. Applied damage is 1; Dust is destroyed.
- Box qualifies when evidence exists at any active orthogonal neighbor. If any qualifying evidence has Fever origin, potency is 2; otherwise 1. Apply one hit only and clamp applied damage to remaining HP.
- Normal and Fever evidence overlapping one Box uses potency 2 once, never 1+2.
- Damage is simultaneous. Destroying an obstacle never exposes or chains another hit in the same transaction.

`ObstacleDamageDelta` contains ID/kind/position, HP before, potency origin, applied amount, HP after, and destroyed flag. `ObstacleDestroyedEvidence` contains stable ID/kind/position/origin and is the only obstacle-objective input.

## Gravity and refill

Transaction order:

1. validate request, stable layers, answer/effect identity, refill and ID capacity;
2. compute number-removal evidence;
3. compute/apply obstacle damage privately;
4. remove destroyed Dust; convert destroyed BoxSlot to NumberSlot;
5. derive barriers from topology holes and surviving Boxes;
6. compact surviving numbers downward within each contiguous non-Box segment, preserving bottom-to-top order;
7. refill every empty NumberSlot column/segment/bottom-up with one draw and sequential BlockId;
8. construct and validate a complete replacement Board and immutable result.

A destroyed Box may bridge formerly separate segments immediately and may receive a falling survivor or spawned number. Surviving Box never moves, refills, or permits fall-through. Dust remains fixed to its cell.

Expected validation failures consume zero RNG. For RandomThree, selection draws occur before refill draws. RNG exceptions/out-of-contract values expose no replacement Board; RNG rollback is not promised.

## Results and validation

Extend BoardResolutionResult with immutable selected/collateral/all removed-number deltas, moved/spawned numbers, obstacle damage, destroyed evidence, next BlockId, mode, and optional SystemEffectId.

Failure precedence: missing request/dependencies/source; invalid mode/effect/effect ID/center contract; refill/next-ID basic validity; invalid layered Board or duplicate IDs; answer correctness and exact current selected identities; invalid center; ID collision/capacity; plan/RNG; defensive final mutation/invariant failure.

Failure exposes no Board/deltas/evidence. Selected answer prefix remains exactly correlatable; authorized collateral is separate.

## Connection, target search, and shuffle

- Connection selects snapshot `IsSelectable`: Dust underlying number is selectable; Box is not.
- Target DFS traverses selectable numbers and skips Boxes rather than rejecting the entire mixed Board.
- Shuffler permutes NumberBlocks only among NumberSlots, including dusty slots; topology, roles, Dust/Box IDs, HP and positions remain unchanged.
- Every successful builder/resolver/shuffler result is a validated stable Board, but is not input-ready until TargetRecovery proves a witness. A target failure keeps Stage noninteractive. For an accepted answer, the stable replacement is nevertheless the authoritative logical Board and may be retried by target recovery without replaying the answer. For an uncommitted Fever-end system effect, the candidate remains private and is discarded if target proof fails.

Existing `BoardShuffler.Shuffle(Board)` and `BoardShuffleResult` signatures remain. Stable layered validation replaces the former full-Open requirement. Statuses remain MissingBoard, UnsupportedBoardState, InsufficientMovableBlocks, FinalBoardMutationRejected, and Succeeded. On success, Board is a fresh layered Board, deltas remain changed NumberBlocks ordered by destination row-major, and new read-only obstacle-preservation observations are available through Board snapshots rather than duplicate result lists. Failure has null Board/empty deltas and never consumes RNG before stable-state/count validation. Fisher-Yates call order is unchanged.

## Objective integration

`RemoveObstacle` requires one exact approved kind filter (Dust or Box) and positive count; no implicit Any filter. It advances only from unique matching destroyed evidence. Box damage 2->1 gives zero.

Number-removal objectives count every unique actual removed NumberBlock from selected, Fever-expanded, RandomThree, Small, Center, or Large evidence. They do not count moved/spawned numbers or obstacles.

Answer attempts retain StageAttemptId idempotence. Fever-end resolution uses the positive monotonic `BoardSystemEffectId`; duplicate/out-of-order system effects reject atomically, cost zero moves, apply number/obstacle evidence, and evaluate objectives before terminal state. Success remains checked before zero-move Failure.

Large additionally emits a typed restoration intent only; STEP 11 owns its amount/application.

## Stage/Fever flow

Answer flow remains noninteractive in ResolvingAnswer through obstacle resolution, attempt commit, logical Board adoption, target proof, and presentation. Fever damage uses Fever-origin evidence and is capped at potency 2 independent of combo. Once StageSession/FeverController accepts the attempt, the coordinator atomically adopts the resolver's replacement Board and next-ID handoff before target recovery. A target failure leaves that committed Board authoritative but noninteractive; retry runs only TargetRecovery and never reapplies the attempt.

End flow: Stage EndingFever -> resolve exact end request -> prepare StageSession system effect -> if predicted terminal, commit and terminate; otherwise prove/recover a safe target on the private candidate -> commit the still-current plan -> adopt Board/target -> acknowledge Fever end/reset -> FinishFeverEnding -> normal target presentation. Pause/exit or a stale plan cannot expose partial effects.

## API migration

This is one atomic breaking Board migration:

- Open+Number maps to NumberSlot+Number.
- Open+Empty remains private transient state only.
- legacy Blocked cells are rejected as ambiguous, never auto-converted.
- remove independent `TrySetAccess` authority.
- migrate BoardGeneration, Connection, BoardResolution, Target search/recovery, BoardShuffler, StageSession evidence, and all corresponding tests together.
- do not ship an intermediate dual-authority state.

No persistence migration is needed because Board runtime state is not saved.

## Exact public contracts

### Board and obstacle values

```text
enum CellRole { NumberSlot, BoxSlot }
readonly struct ObstacleId(long value) // throws for <=0; default invalid
enum ObstacleKind { Dust, Box }
readonly struct DustState(ObstacleId id) // HP is always 1
readonly struct BoxState(ObstacleId id, int currentHitPoints) // only 1 or 2

sealed BoardLayout
  static BoardLayout CreateAllNumberSlots(BoardTopology topology)
  static BoardLayout Create(BoardTopology topology, IEnumerable<BoardPosition> boxSlots)
  BoardTopology Topology { get; }
  CellRole GetRole(BoardPosition position) // throws only for out-of-bounds/inactive
  IEnumerable<BoardPosition> EnumerateNumberSlots()
  IEnumerable<BoardPosition> EnumerateBoxSlots()

sealed Board
  Board(BoardTopology topology) // compatibility: all NumberSlots
  Board(BoardLayout layout)
  BoardLayout Layout { get; }
  CellLookupResult TryGetCell(BoardPosition, out BoardCellSnapshot)
  BoardLayerMutationResult TryPlaceNumber(BoardPosition, NumberBlock)
  BoardLayerMutationResult TryRemoveNumber(BoardPosition, out NumberBlock removed)
  BoardLayerMutationResult TryRelocateNumber(BoardPosition source, BoardPosition destination)
  BoardLayerMutationResult TryPlaceDust(BoardPosition, DustState)
  BoardLayerMutationResult TryUpdateDust(BoardPosition, DustState)
  BoardLayerMutationResult TryRemoveDust(BoardPosition, out DustState removed)
  BoardLayerMutationResult TryPlaceBox(BoardPosition, BoxState)
  BoardLayerMutationResult TryUpdateBox(BoardPosition, BoxState)
  BoardLayerMutationResult TryRemoveBox(BoardPosition, out BoxState removed)
  BoardStabilityResult ValidateStable()
```

`Board` is a structural mutable construction container, not proof of playability or completeness. Its public constructors initially contain empty NumberSlots, and successful remove operations may intentionally create transient empty slots. `BoardStabilityResult` is immutable with `BoardStabilityStatus Status`, `bool IsStable`, and nullable first-invalid `BoardPosition`; statuses are `Stable`, `EmptyNumberSlot`, `MissingBox`, `NumberInBoxSlot`, `DustInBoxSlot`, `BoxInNumberSlot`, `DustWithoutNumber`, `InvalidNumber`, `InvalidObstacle`, `DuplicateBlockId`, and `DuplicateObstacleId`. Stable-consuming boundaries (layout builder input, resolver input, target search, shuffle, and any Board exposed as a successful replacement) call `ValidateStable`. Connection may observe an incomplete construction Board but simply rejects nonselectable cells. No target/input-ready result may expose an unstable Board.

`BoardLayerMutationResult` is `Succeeded`, `OutOfBounds`, `InactivePosition`, `WrongCellRole`, `Occupied`, `Empty`, `InvalidNumber`, `InvalidObstacle`, `DuplicateBlockId`, `DuplicateObstacleId`, `DustAlreadyPresent`, `DustMissing`, `BoxAlreadyPresent`, or `BoxMissing`. Failed operations are atomic. A successful operation may leave a transient Board unstable; stability is a separate explicit boundary validation. Role conversion is internal to the validated builder/resolver only.

`BoardCellSnapshot` exposes get-only `BoardPosition Position`, `CellRole Role`, `NumberBlock? Block`, `DustState? Dust`, `BoxState? Box`, and bool `HasBlock`, `HasDust`, `HasBox`, `IsSelectable`, `IsRemovableNumber`, `IsMovableNumber`, `IsRefillable`, `IsGravityBarrier`. Wrong-role nullable fields are always null. The former public Access setter is removed; compatibility Access, if retained, is get-only and derived.

### Layout builder

```text
sealed ObstacleLayoutEntry
  static Dust(BoardPosition, ObstacleId)
  static Box(BoardPosition, ObstacleId)

sealed ObstacleLayout // copied read-only Entries
  ObstacleLayout(IEnumerable<ObstacleLayoutEntry> entries)
  IReadOnlyList<ObstacleLayoutEntry> Entries { get; }
ObstacleBoardBuildResult ObstacleBoardBuilder.Build(Board source, ObstacleLayout layout)
```

`ObstacleLayoutEntry` exposes get-only Position, Kind, and Id. `ObstacleBoardBuildStatus` precedence is `MissingSource`, `MissingLayout`, `MissingEntry`, `InvalidObstacleId`, `DuplicateObstacleId`, `DuplicatePosition`, `OutOfBounds`, `InactivePosition`, `UnsupportedKind`, `IncompatibleSource`, `FinalMutationRejected`, then `Succeeded`. `ObstacleBoardBuildResult` exposes get-only Status, nullable Board, and copied `IReadOnlyList<NumberBlock> DiscardedSetupBlocks`; failure has null Board/empty list, success has a fresh stable Board sharing source topology.

### Resolution requests and IDs

```text
readonly struct BoardSystemEffectId(long value) // throws <=0; default invalid
enum ObstacleResolutionMode { NormalAnswer, FeverAnswer, FeverEnd }
enum FeverEndPattern { None, RandomThree, Small, Center, Large }

sealed ObstacleResolutionRequest
  static NormalAnswer(Board, AnswerResult, RefillValueRange, int nextBlockId)
  static FeverAnswer(Board, AnswerResult, RefillValueRange, int nextBlockId)
  static FeverEnd(Board, FeverEndPattern, BoardSystemEffectId,
                  BoardPosition? center, RefillValueRange, int nextBlockId)
```

Factories are nonthrowing raw holders except null enumerable copying; resolver owns expected validation. Pattern/center contract is exact as specified above.

`sealed ObstacleBoardResolver(IRandomSource randomSource)` throws `ArgumentNullException` for null. `BoardResolutionResult Resolve(ObstacleResolutionRequest request)` is synchronous and has the exact failure/result contracts below.

`ObstacleResolutionFailure` exact precedence/order:

1. `MissingRequest`
2. `MissingBoard`
3. `MissingAnswer` (answer modes)
4. `MissingRefillRange`
5. `InvalidMode`
6. `InvalidSystemEffectId`
7. `MissingCenter`
8. `UnexpectedCenter`
9. `InvalidRefillRange`
10. `InvalidNextBlockId`
11. `InvalidLayeredBoard`
12. `DuplicateBlockId`
13. `DuplicateObstacleId`
14. `InvalidObstacleState`
15. `AnswerNotCorrect`
16. `EmptySelection`
17. `DuplicateSelection`
18. `SelectedPositionMissing`
19. `SelectedBlockMismatch`
20. `InvalidCenter`
21. `NextBlockIdCollision`
22. `BlockIdRangeExhausted`
23. `RandomSourceContractViolation` (throws `InvalidOperationException`, no result)
24. `FinalBoardMutationRejected`

Answer-only statuses are skipped for end mode; effect/center statuses are skipped for answer modes. Expected failures consume zero RNG and return a failed result. Injected RNG exceptions propagate; out-of-contract returns throw `InvalidOperationException` as existing generators do.

### Resolution evidence/result

```text
enum RemovedNumberCause { Selected, FeverExpanded, FeverEndRandom,
                          FeverEndSmall, FeverEndCenter, FeverEndLarge }
enum RemovalOrigin { Normal, Fever }
enum ObstacleDamageOrigin { Normal, Fever }

readonly struct RemovedNumberDelta(Position, Block, Cause, Origin)
readonly struct ObstacleDamageDelta(Id, Kind, Position, HitPointsBefore,
    Potency, DamageApplied, HitPointsAfter, Origin, WasDestroyed)
readonly struct ObstacleDestroyedEvidence(Id, Kind, Position, Origin)

sealed BoardResolutionResult
  bool Succeeded
  ObstacleResolutionFailure Failure
  Board Board // success only
  ObstacleResolutionMode Mode
  BoardSystemEffectId SystemEffectId // valid only end success
  IReadOnlyList<RemovedNumberDelta> SelectedRemoved
  IReadOnlyList<RemovedNumberDelta> CollateralRemoved
  IReadOnlyList<RemovedNumberDelta> Removed
  IReadOnlyList<MovedBlockDelta> Moved
  IReadOnlyList<SpawnedBlockDelta> Spawned
  IReadOnlyList<ObstacleDamageDelta> ObstacleDamage
  IReadOnlyList<ObstacleDestroyedEvidence> DestroyedObstacles
  int NextBlockIdValue // success only
```

All lists are copied/read-only. Failure has null Board, default IDs/mode, next ID 0, and empty lists. SelectedRemoved is the exact submitted prefix; Removed is SelectedRemoved followed by CollateralRemoved.

### StageSession objective and system effects

`StageObjectiveDefinition` adds nullable `ObstacleKind? ObstacleKind`. It is required only for RemoveObstacle, forbidden for other kinds, and must be Dust or Box. Same-kind RemoveObstacle definitions are duplicate conditions even with different quantities.

`StageSessionSnapshot` adds `BoardSystemEffectId NextExpectedSystemEffectId` initialized to 1 and typed obstacle-destruction totals.

```text
StageSystemEffectPrepareResult StageSession.PrepareSystemEffect(BoardResolutionResult result)
StageSystemEffectCommitResult StageSession.CommitSystemEffect(StageSystemEffectPlan plan)
```

Prepare is pure and never mutates. `StageSystemEffectPrepareStatus` precedence: `MissingResult`, `SessionAlreadyTerminal`, `ResolutionNotSucceeded`, `NotSystemEffect`, `InvalidEffectId`, `DuplicateEffect` (ID lower), `OutOfOrderEffect` (ID higher), `InvalidEvidence`, `ArithmeticOverflow`, then `PreparedContinue` or `PreparedSuccess`. Success returns an immutable `StageSystemEffectPlan` containing the exact expected effect ID, Before and prospective After snapshots, objective/event deltas, and terminal prediction. Failure returns null Plan and identical snapshot observations.

Commit accepts only a plan prepared from this exact StageSession/version. `StageSystemEffectCommitStatus` is `CommittedContinue`, `CommittedSuccess`, `MissingPlan`, `SessionAlreadyTerminal`, or `StalePlan`. Stale means any attempt/effect changed the session since prepare. Commit performs only validated nonthrowing field assignment, increments expected effect ID, and returns the committed snapshot/events. System effects spend no move, add no answer score, and alter no answer/FAST counters. They count every unique removed BlockId and matching unique destroyed ObstacleId, apply progress, then evaluate Success; they cannot create Failure because no move was spent.

Exact immutable payloads:

```text
sealed StageSystemEffectPlan
  BoardSystemEffectId EffectId
  long PreparedSessionVersion
  StageSessionSnapshot Before
  StageSessionSnapshot ProspectiveAfter
  IReadOnlyList<StageSessionEvent> Events
  bool WouldSucceed

sealed StageSystemEffectPrepareResult
  StageSystemEffectPrepareStatus Status
  StageSystemEffectPlan Plan // non-null only PreparedContinue/PreparedSuccess
  StageSessionSnapshot Before
  StageSessionSnapshot ProspectiveAfter
  IReadOnlyList<StageSessionEvent> Events

sealed StageSystemEffectCommitResult
  StageSystemEffectCommitStatus Status
  BoardSystemEffectId EffectId // valid only committed
  StageSessionSnapshot Before
  StageSessionSnapshot After
  IReadOnlyList<StageSessionEvent> Events
```

Prepare failure exposes null Plan, equal Before/ProspectiveAfter snapshots, and an empty copied read-only Events list. Prepare success duplicates the plan's immutable observations for convenient callers. Commit failure exposes default EffectId, equal current Before/After snapshots, and empty Events. Commit success exposes the plan's Before/prospective snapshot and copied events exactly. Neither result uses null snapshots.

Answer `ApplyAttempt` validates selected evidence exactly, accepts authorized collateral, counts all unique removed numbers and destroyed obstacles, applies progress, consumes the mode's move cost, then preserves Success-before-Failure ordering.

### Publishability coordinator

Add `ObstacleResolutionCoordinator` in MathGame.ObstacleFlow. Its constructor is exact:

```text
ObstacleResolutionCoordinator(ObstacleBoardResolver resolver,
                              StageController stageController,
                              StageSession stageSession,
                              FeverController feverController,
                              TargetRecoveryCoordinator targetRecovery,
                              Board initialBoard,
                              int initialNextBlockId)
```

Null services/Board throw `ArgumentNullException`; an unstable initial Board or invalid/colliding next ID throws `ArgumentException`. FeverController adds public nonmutating `bool IsBoundTo(StageController stage, StageSession session)` solely for composition validation. The coordinator constructor throws `ArgumentException` unless the FeverController is bound to the exact injected StageController and StageSession references. The coordinator therefore has one lifecycle and one accounting authority across normal, Fever, and system-effect flows.

The coordinator is the sole mutable gameplay owner of private `currentBoard`, `nextBlockId`, and optional pending-target-proof state. It exposes only get-only CurrentBoard/NextBlockId snapshots; callers cannot swap them. Constructor dependencies are fixed references, and it owns no randomness beyond those injected services. It uses the injected StageController for every ResolvingAnswer/EndingFever/terminal precondition; it never infers lifecycle state through FeverController.

Normal attempts commit through the injected StageSession. Fever attempts delegate exactly once to `feverController.ApplyFeverAttempt(request.AttemptId, request.Answer, resolution)`, never directly to StageSession. `FeverAttemptApplyStatus.AppliedContinue` and `AppliedTerminal` are accepted; every other status maps to AttemptRejected and leaves coordinator state unchanged. This preserves STEP 9's FeverSession+StageSession atomic boundary and returns the exact FeverAttemptResult for diagnostics.

After either accepted commit, adoption consists only of assigning the already-validated non-null resolver Board and integer next-ID into the coordinator's private fields and setting a private pending-target flag. These field assignments are nonthrowing and occur synchronously in the same command before any callback/event or target call; no user-supplied owner port, setter, allocation, validation, or subscriber runs between commit and adoption. Thus accepted attempt + Board adoption cannot split. This ordering is deliberate: an answer has spent its move and changed objectives, so its resolved Board cannot be discarded merely because target recovery is indeterminate. Target recovery may then shuffle and replace `currentBoard` on success. On failure, the adopted resolved Board remains authoritative, Stage remains noninteractive, and retry never reapplies the attempt.

```text
sealed ObstacleAnswerFlowRequest(
  AnswerResult answer,
  StageAttemptId attemptId,
  RefillValueRange refill,
  TargetHistory history,
  TargetRecoveryConfig targetConfig)

ObstacleAnswerFlowResult ResolveNormalAnswer(ObstacleAnswerFlowRequest request)
ObstacleAnswerFlowResult ResolveFeverAnswer(ObstacleAnswerFlowRequest request)
ObstacleAnswerFlowResult RetryTargetRecovery(TargetHistory history,
                                             TargetRecoveryConfig targetConfig)
```

The request exposes only those get-only fields; Board and next BlockId are deliberately absent. Every resolver call uses the coordinator's private CurrentBoard and NextBlockId, so callers cannot fork or skip the identity handoff.

`ObstacleAnswerFlowStatus` precedence is `MissingRequest`, `InvalidStageState`, `NoPendingTargetProof` (retry only), `PendingTargetProofExists` (new answer only), `MissingAnswer`, `MissingRefillRange`, `MissingHistory`, `MissingTargetConfig`, `ResolutionFailed`, `AttemptRejected`, `StageTerminal`, followed by target mapping statuses `TargetMissingInput`, `TargetInvalidConfiguration`, `TargetInvalidBoard`, `TargetSearchLimitExceeded`, `TargetShuffleFailed`, `TargetUnrecoverable`, then `Succeeded`. New answer calls require ResolvingAnswer and no pending proof; retry requires a pending proof and a nonterminal ResolvingAnswer state. InvalidStageState precedes pending-state checks. All reject without mutation.

`ObstacleAnswerFlowResult` exposes get-only Status; nullable `Board LogicalBoard`; `int NextBlockIdValue`; nullable `BoardResolutionResult ResolutionResult`; nullable `StageAttemptResult StageResult`; nullable `FeverAttemptResult FeverResult`; nullable `TargetRecoveryResult TargetResult`; nullable `TargetSolution SelectedTarget`; nullable `TargetHistory History`; `bool AttemptCommitted`; and `bool IsInputReady`. Before attempt acceptance, failures expose no LogicalBoard and AttemptCommitted=false. StageTerminal and every target failure expose the adopted logical Board, adopted next ID, accepted attempt result, AttemptCommitted=true, and IsInputReady=false. Retry failures expose the same authoritative Board/ID but AttemptCommitted=false because retry performs no attempt. On successful target recovery, the coordinator adopts `TargetRecoveryResult.Board` (which may be the prior Board or a shuffled replacement), retains the already-adopted next-ID unchanged because shuffle creates no blocks, clears pending proof, and returns that exact final Board/ID/solution/history with IsInputReady=true. Resolver failure and attempt rejection leave source/session/coordinator unchanged.

```text
sealed ObstacleEndFlowRequest(
  FeverEndResult feverEnd,
  BoardPosition? center,
  RefillValueRange refill,
  TargetHistory history,
  TargetRecoveryConfig targetConfig)

ObstacleEndFlowResult ResolveAndCommitEnd(ObstacleEndFlowRequest request)
```

The request exposes those exact get-only properties and rejects nothing in its constructor; Board and next BlockId are absent, and the coordinator always resolves from its private authoritative state. Coordinator validation owns null/shape failures. It maps Fever tier to neutral pattern, resolves, and asks StageSession to PrepareSystemEffect. If preparation predicts Success, it commits immediately; after successful commit it nonthrowingly adopts the resolved Board/next ID, clears any pending-target flag, and returns StageTerminal. If preparation predicts Continue, it runs TargetRecovery on the private candidate before committing. Recovery failure discards the uncommitted plan/candidate and leaves StageSession/coordinator unchanged, so retrying the whole end effect remains legal. After successful recovery it commits the exact plan; StalePlan discards the candidate and returns StageSessionRejected. After successful commit it nonthrowingly adopts the final TargetRecovery Board plus resolver next-ID, clears pending proof, and returns Succeeded. Only a committed plan makes an end-effect replacement authoritative.

`ObstacleEndFlowStatus` precedence is `MissingRequest`, `InvalidStageState` (must be EndingFever and nonterminal), `PendingTargetProofExists`, `MissingFeverEndResult`, `MissingRefillRange`, `MissingHistory`, `MissingTargetConfig`, `MissingCenter`, `UnexpectedCenter`, `ResolutionFailed`, `StageSessionRejected`, followed by the exact target mapping statuses `TargetMissingInput`, `TargetInvalidConfiguration`, `TargetInvalidBoard`, `TargetSearchLimitExceeded`, `TargetShuffleFailed`, `TargetUnrecoverable`, then `StageTerminal` and `Succeeded`.

`ObstacleEndFlowResult` exposes get-only `ObstacleEndFlowStatus Status`; nullable `Board LogicalBoard`; `int NextBlockIdValue`; nullable `BoardResolutionResult ResolutionResult`; nullable `StageSystemEffectPrepareResult PrepareResult`; nullable `StageSystemEffectCommitResult SystemEffectResult`; nullable `TargetRecoveryResult TargetResult`; nullable `TargetSolution SelectedTarget`; nullable `TargetHistory History`; `bool EffectCommitted`; and `bool IsInputReady`. Missing/validation failures expose only the available diagnostic input result, null Board/commit/target payloads, EffectCommitted=false, and false. ResolutionFailed retains its failed ResolutionResult. StageSessionRejected retains ResolutionResult plus prepare/commit rejection as applicable but no Board. Each target failure retains successful ResolutionResult, successful PrepareResult, exact failed TargetResult, no Board/commit, false/false. StageTerminal exposes committed logical Board, next ID, prepare/commit results, no target, EffectCommitted=true, IsInputReady=false. Succeeded exposes all success payloads, EffectCommitted=true, and IsInputReady=true. All exposed lists/snapshots are immutable. Source Board and StageSession remain unchanged on every precommit failure.

Exact `TargetRecoveryStatus` mapping for both flows is normative: `MissingInput -> TargetMissingInput`; `InvalidConfiguration -> TargetInvalidConfiguration`; `InvalidBoardState -> TargetInvalidBoard`; `SearchLimitExceeded -> TargetSearchLimitExceeded`; `ShuffleFailed -> TargetShuffleFailed`; `UnrecoverableDeadlock -> TargetUnrecoverable`; and any of `CurrentTargetStillValid`, `SelectedOnCurrentBoard`, or `RecoveredByShuffle` is success. The original TargetRecoveryResult is always retained, so indeterminate, invalid, shuffle-fault, and bounded-deadlock diagnostics are never collapsed.

The explicit center supplied in the request is the approved selection seam. STEP 10 does not implement a center chooser. Tests and later presentation/orchestration supply it; therefore domain completion is not blocked, while autonomous UI selection remains STEP 12 integration work.

## Expected files

Board: add CellRole, BoardLayout, ObstacleId/Kind, DustState, BoxState; modify Board/snapshot/mutation/access models.

Add `Runtime/Obstacles/MathGame.Obstacles.asmdef`, layout/builder/result, damage planner and evidence models.

Modify BoardResolution models/resolver/asmdef; Connection eligibility; Targets search/shuffle; StageSession objective/system-effect models; test asmdefs and docs. Fever geometry remains outside Fever assembly.

## Acceptance matrix

- Layer/role/HP/ID invariants and invalid combinations.
- Dust number selectable/searchable; Dust fixed while numbers move; own-cell removal destroys it.
- Box 2->1 Normal, later Normal destroys; any qualifying Fever evidence destroys; multi-cell overlap produces one highest-potency hit.
- Exact orthogonal Fever expansion, no diagonal/recursive expansion, deduplication and deterministic ordering.
- None and every end tier: center contract, Manhattan clipping, RandomThree N=0/1/2/3+, exact RNG calls, source atomicity.
- Surviving Box barriers and destroyed-Box segment bridge/refill with holes/multiple Boxes; Dust anchoring.
- Immutable coherent deltas/evidence, selected-prefix correlation, unique removals, deterministic RNG/IDs.
- Connection/search agreement; shuffle numbers only and preserve all obstacle layers.
- Exact-kind obstacle objectives, all-number removal objectives, effect/attempt idempotence, zero end move cost, final-objective terminal ordering.
- Input and answer/Fever clocks excluded throughout resolution/end/target recovery; pause/exit cleanup.
- Full Edit/Play regressions and independent review with no P0-P2.

## Remaining integration dependency

Area-center **selection policy** is intentionally outside Board effect resolution. The request requires an explicit active center supplied by gameplay orchestration/presentation. STEP 10 domain behavior is implementation-ready and testable with explicit centers; a fully autonomous playable end loop still requires its caller to choose/provide that center without hiding a policy in the resolver.

## Disposition

STEP 10 domain design is implementation-ready for Dust, Box, normal/Fever answer effects, all Fever end-effect geometries, gravity/refill, target safety, shuffle, and objectives. No deferred obstacle or special behavior is included.
