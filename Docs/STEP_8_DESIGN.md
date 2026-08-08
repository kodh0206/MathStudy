# STEP 8 Design - Stage Session, Objectives, Rewards, and Completion

Status: **APPROVED FOR IMPLEMENTATION WITH EXPLICIT DEFERRED OBJECTIVES**  
Designed: 2026-08-08  
Source: `Docs/GAME_DESIGN.md` v1.0

## Goal and scope

Create a deterministic Unity-free stage session that commits answer cycles exactly once, owns normal-mode moves and supported objective progress, records configured score and exact GDD reward facts, and decides Success before zero-move Failure. STEP 8 supports only objectives backed by current authoritative data: number removal, specified-target completion, and long connections. Obstacle, restoration-energy, and special create/use objectives return an explicit unsupported-configuration result until their owning systems exist.

GDD rules retained:

- Sections 4.6 and 8.1: normal Correct costs one move; Miss costs zero and resets the FAST streak.
- Sections 8.2-8.3: a stage has one or two objectives; all must complete; final-move completion is Success; Failure occurs only when zero moves remain and an objective is incomplete.
- Sections 6 and 27: length tiers are 2 standard, 3 extra Fever, 4 basic-special intent, and 5+ enhanced/area intent.
- Sections 9.2, 9.4, and 9.10: Correct grade contributions are 25/15/5; length contributions are 0/3/6/10; consecutive FAST contributes 5 from the second FAST. Speed never gates completion.
- The GDD provides no base score, restoration formula, currency, star thresholds, or stage quantities. Score values are explicit content configuration with no default; undefined rewards are not invented.

Fever move exemption, obstacle/restoration/special objectives, actual specials, progression, persistence, UI, analytics, and monetization remain later STEPs.

## Assembly and ownership

Add `MathGame.StageSession`, a Unity-free, non-auto-referenced assembly depending only on `MathGame.Answer` and `MathGame.BoardResolution` (and their transitive model dependencies). It does not reference StageController, Targets, Board, Unity, UI, SDKs, or persistence.

`StageSession` is the only mutable owner of a run's status, moves, objective progress, attempt sequence, counters, FAST streak, score, removed count, and accumulated semantic Fever contribution. Mutation occurs only through `ApplyAttempt`; it emits immutable returned semantic events rather than callbacks.

## Public configuration contracts

- `readonly struct StageDefinitionId`: `StageDefinitionId(int value)` throws `ArgumentOutOfRangeException` for nonpositive values; `int Value { get; }`, `bool IsValid { get; }`, value equality. Default remains invalid defensively.
- `StageObjectiveKind`: `RemoveNumberBlocks`, `CompleteTarget`, `CompleteLongConnection`, `RemoveObstacle`, `EarnRestorationEnergy`, `CreateSpecial`, `UseSpecial`.
- `sealed StageObjectiveDefinition`: `StageObjectiveDefinition(StageObjectiveKind kind, int requiredCount, TargetNumber target, int minimumConnectionLength)` is a nonvalidating immutable holder with matching get-only properties. Static factories are conveniences only.
- `readonly struct ConnectionLengthScoreRule`: `ConnectionLengthScoreRule(int minimumLength, long bonus)` is a nonvalidating holder with get-only properties.
- `sealed ScoreRewardConfig`: `ScoreRewardConfig(long baseCorrectScore, long perfectBonus, long fastBonus, long normalBonus, IEnumerable<ConnectionLengthScoreRule> lengthRules)` copies `lengthRules` when non-null and stores null distinctly for central validation. It exposes the four get-only numeric properties and `IReadOnlyList<ConnectionLengthScoreRule> LengthRules` (null only for invalid raw input). Rules require minimum length >=2, nonnegative bonus, and unique thresholds. The highest applicable rule is used once.
- `sealed StageDefinition`: `StageDefinition(StageDefinitionId id, int initialMoves, IEnumerable<StageObjectiveDefinition> objectives, ScoreRewardConfig scoreConfig)` is nonvalidating raw input. It exposes get-only values and a copied `IReadOnlyList<StageObjectiveDefinition>`; null input remains distinguishable as null.

Supported objective validity:

- the four named deferred kinds are rejected as `UnsupportedObjective` before their fields or duplicate condition are inspected; any undefined enum value is `InvalidObjective`;
- for supported kinds, all required counts are positive;
- RemoveNumberBlocks has no target and length 0;
- CompleteTarget has a valid target and length 0;
- CompleteLongConnection has no target and minimum length >=3;
- identical conditions are duplicates; different targets or thresholds may coexist;
- the four deferred kinds are recognized but unsupported.

Creation API:

```text
public static StageSessionCreateStatus StageSession.TryCreate(
    StageDefinition definition,
    out StageSession session)
```

Normative validation precedence is: missing definition, invalid ID, invalid moves, missing objectives, objective count outside 1-2, first null objective in definition order, first unsupported objective kind in definition order, first invalid supported objective in definition order, duplicate supported condition, missing score config, invalid score config, then success. The corresponding statuses are `MissingDefinition`, `InvalidDefinitionId`, `InvalidMoves`, `MissingObjectives`, `InvalidObjectiveCount`, `MissingObjective`, `UnsupportedObjective`, `InvalidObjective`, `DuplicateObjective`, `MissingScoreConfig`, `InvalidScoreConfig`, and `Succeeded`. Failure sets `session` to null and exposes no partial run.

## Attempts and correlation

- `readonly struct StageAttemptId`: `StageAttemptId(long value)` throws `ArgumentOutOfRangeException` for nonpositive values; `long Value { get; }`, `bool IsValid { get; }`, value equality. Default remains invalid defensively.
- `sealed StageAttemptCommand`: `StageAttemptCommand(StageAttemptId id, AnswerResult answer, BoardResolutionResult resolution)` with get-only `Id`, `Answer`, and nullable `Resolution`.
- A session initially expects ID 1.
- `NoSelection` is `InvalidAnswer` and does not advance, preserving STEP 5's no-gameplay-submission rule.
- Miss requires a null resolution. It advances the sequence, increments miss count, spends no move, advances no objective/score/Fever contribution, and resets FAST streak. A supplied resolution is `UnexpectedResolution`.
- Correct requires a nonempty snapshot, grade Perfect/Fast/Normal, and a non-null successful resolution. Removed count must equal snapshot count, and every removed delta in order must equal the submitted position, BlockId, and value. Any mismatch is `AnswerResolutionMismatch` and is atomic.
- Only normal Correct context exists now. STEP 9 introduces an explicit Fever context/move policy; STEP 8 does not accept an unowned boolean bypass.

An answer is model-consistent only when its target is valid, snapshot is non-null, and elapsed seconds are finite and nonnegative, plus exactly one of:

- NoSelection: empty snapshot, Relation None, MissReason None, Grade None;
- Miss below: nonempty snapshot, submitted sum below target, Relation BelowTarget, MissReason UnderTarget, Grade Miss;
- Miss above: nonempty snapshot, submitted sum above target, Relation AboveTarget, MissReason OverTarget, Grade Miss;
- Miss insufficient: one-entry snapshot whose sum equals target, Relation MatchesTarget, MissReason InsufficientConnectionLength, Grade Miss;
- Correct: at least two entries, submitted sum equals target, Relation MatchesTarget, MissReason None, and Grade Perfect, Fast, or Normal.

Any other combination is `InvalidAnswer`. These guards are defensive because public AnswerResult construction is already constrained by AnswerValidator; they require no test-only construction seam.

`public StageAttemptResult ApplyAttempt(StageAttemptCommand command)` never throws for expected command/state failures. Its normative precedence is: null command -> `MissingCommand`; terminal session -> `SessionAlreadyTerminal`; invalid/default ID -> `InvalidAttempt`; ID lower than expected -> `DuplicateAttempt`; ID higher -> `OutOfOrderAttempt`; null answer -> `InvalidAttempt`; NoSelection or inconsistent answer model -> `InvalidAnswer`; Miss with resolution -> `UnexpectedResolution`; valid Miss commit; Correct with missing/failed/mismatched resolution -> `AnswerResolutionMismatch`; defensive zero moves -> `NoMovesRemaining`; checked prospective arithmetic including next expected ID -> `ArithmeticOverflow`; otherwise commit. Status values are `AppliedContinue`, `AppliedMiss`, `AppliedSuccess`, `AppliedFailure`, and the failures above.

An applied attempt must advance `NextExpectedAttemptId` by one. If the current expected ID is `long.MaxValue`, the entire attempt returns `ArithmeticOverflow`; it does not commit even if every other fact is valid. No wrapped or sentinel ID is published.

## Objective, score, reward, and streak rules

On a correlated Correct:

- RemoveNumberBlocks advances by `Resolution.Removed.Count`.
- CompleteTarget advances once when the answer target value matches.
- CompleteLongConnection advances once when selected count meets its threshold.
- Progress is monotonic and clamped to Required; objective events follow definition order.
- Exactly one move is spent.
- Score is checked: configured base + grade bonus + highest applicable configured length bonus.

`ConnectionLengthRewardClassifier.Classify(int)` returns `None` below 2, `StandardRemoval` at 2, `ExtraFeverRequested` at 3, `BasicSpecialRequested` at 4, and `EnhancedAreaSpecialRequested` at 5+. These are semantic intents only.

`StageRewardBreakdown` exposes grade contribution (25/15/5), length contribution (0/3/6/10), FAST-streak contribution (5 when the newly committed streak is >=2), checked total contribution, configured score award, and length tier. FAST Correct increments the streak; Perfect, Normal, and Miss reset it. Reward facts do not mutate a Fever gauge or Board.

Exact immutable value contract:

```text
int GradeFeverContribution { get; }
int LengthFeverContribution { get; }
int FastStreakFeverContribution { get; }
int TotalFeverContribution { get; }
long ScoreAwarded { get; }
ConnectionLengthRewardTier LengthRewardTier { get; }
```

`StageRewardBreakdown.None` has all numeric values zero and tier None. Results always expose a non-null/value reward; failures and Miss use None.

## Atomic terminal ordering

The session computes a full prospective state before mutation:

1. validate session, command, sequence, answer, and correlation;
2. calculate checked counters, score, reward, objective progress, and event payloads;
3. consume one move for normal Correct;
4. apply all objective effects;
5. if every objective is complete, set Success;
6. otherwise, if remaining moves is zero, set Failure;
7. otherwise remain Active.

Any failure/overflow leaves all observable state unchanged. A terminal session rejects every later attempt with `SessionAlreadyTerminal`. An Active session cannot legitimately have zero moves; `NoMovesRemaining` is a defensive guard.

## Snapshots, results, and events

- `ObjectiveProgressSnapshot` has internal construction and get-only `Index`, `Definition`, `long Current`, `long Required`, `long Remaining`, and `bool IsComplete`.
- `StageSessionSnapshot` has internal construction and get-only `StageDefinitionId DefinitionId`, `StageSessionStatus Status`, `int InitialMoves`, `int RemainingMoves`, `int SpentMoves`, `long Score`, `StageAttemptId NextExpectedAttemptId`, `IReadOnlyList<ObjectiveProgressSnapshot> Objectives`, `long CorrectCount`, `long MissCount`, `long PerfectCount`, `long FastCount`, `long NormalCount`, `int CurrentFastStreak`, `int MaximumFastStreak`, `long TotalRemovedNumberBlocks`, `long TotalLongConnections`, and `long TotalFeverContribution`. The objective list is a copied read-only historical collection.
- `StageAttemptResult` has internal construction and get-only `StageAttemptApplyStatus Status`, non-null immutable historical `StageSessionSnapshot Before` and `After`, `int MoveCost`, non-null/value `StageRewardBreakdown Reward`, and copied `IReadOnlyList<StageSessionEvent> Events`. Rejections have observationally equal Before/After, zero move, Reward.None, and an empty event list. Applied Miss also has Reward.None.
- `StageSessionEventKind`: `AnswerAccepted`, `MissRecorded`, `ScoreAwarded`, `ObjectiveProgressed`, `MoveConsumed`, `StageSucceeded`, `StageFailed`.
- `readonly struct StageSessionEvent` exposes `Kind`, `int ObjectiveIndex`, and `long Amount`. `ObjectiveIndex` is the changed definition index only for ObjectiveProgressed and `-1` otherwise. `Amount` is score awarded, objective increment actually applied after clamping, or move cost for those three event kinds, and `0` otherwise.

Correct event order is AnswerAccepted; ScoreAwarded only when its amount is greater than zero; ObjectiveProgressed only for each objective whose clamped Current increases, in definition order; MoveConsumed (always amount 1); then an optional terminal event. An already-complete or unchanged objective emits no progress event. Miss returns only MissRecorded.

`TotalRemovedNumberBlocks` is the checked cumulative count of all `Resolution.Removed` deltas from committed Correct attempts, regardless of whether a removal objective is configured. `TotalLongConnections` is the checked cumulative count of committed Correct answers with selected count at least 3, independent of configured objective thresholds. These counters, all grade/correct/miss counters, cumulative Fever contribution, score, objective progress, and next expected ID are included in the same prospective checked transaction; overflow returns `ArithmeticOverflow` without mutation.

## Stage boundary

No StageController production change is required. Orchestration applies Correct while Stage is `ResolvingAnswer`. `AppliedContinue` runs STEP 7 then begins target presentation; `AppliedSuccess` calls `Complete`; `AppliedFailure` calls `Fail`. Miss records zero-cost semantics before the existing same-target `FinishMissResolution`. StageSession stays independent of lifecycle state.

## Acceptance matrix

- Exact creation precedence, input copying, supported/unsupported objectives, duplicate conditions, and score-rule validation.
- All supported objective kinds, clamping, unrelated facts, two-objective AND semantics, and definition-order events.
- Miss/NoSelection/Correct handling; successful exact correlation; count/order/position/ID/value mismatch; duplicate/out-of-order IDs; rejection atomicity.
- Normal move cost, early and final-move Success, final-move Failure, terminal exclusivity, and slow Normal completion.
- Grade/length boundaries, highest score tier, FAST sequences and Perfect/Normal/Miss reset, arithmetic-overflow atomicity, and special intent without Board mutation.
- Immutable historical snapshots/results/event collections and reconciled summary totals/shortfalls.
- Stage integration from ResolvingAnswer to Continue/Success/Failure with input remaining disabled; full regressions and independent review.

## Disposition

STEP 8 is implementation-ready for the evidence-backed objective slice. Unsupported objectives fail explicitly and are extended by STEPs 10-11; no claim is made that those later gameplay systems are implemented.
