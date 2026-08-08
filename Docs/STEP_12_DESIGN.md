# STEP 12 Design — Mobile Gameplay Presentation and Feedback

## Status

**APPROVED FOR IMPLEMENTATION.** Product decisions below are authoritative for the STEP 12 prototype.

This document is design-only. No STEP 12 production code, scene, prefab, asset binding, or STEP 13 persistence behavior is approved until the minimum decisions in “Product decisions required” are supplied and independently reviewed.

STEP 11 production remains structurally implemented with zero known P0/P1 findings. Its Unity Edit/Play verification remains blocked by licensing initialization and is inherited verification debt, not evidence that STEP 11 runtime behavior passed.

## Sources read

- Full `GAME_DESIGN.md`, especially §§3–5, 8–15, 21, 24, 26, and 27.
- `DEVELOPMENT_PLAN.md`, STEP 12 and completion gate.
- `DECISIONS.md` through ADR-030.
- `STEP_10_DESIGN.md`, especially obstacle resolution evidence, target-proof/adoption, Fever-end center seam, and answer/end retry ordering.
- `STEP_11_DESIGN.md`, especially provisional restoration, world milestones, FailedPendingDecision, Continue/Retry/Abandon, and presentation-asset separation.
- Existing Stage, Connection, Answer, BoardResolution, Targets, Fever, ObstacleFlow, StageSession, Restoration, App lifecycle, and test assemblies.

## Goal

Deliver the complete playable mobile presentation loop: display the authoritative board, target, path and live sum; translate touch gestures into existing Connection commands; serialize target, answer, obstacle, refill, Fever, restoration, pause, failed-decision, and terminal presentation without allowing views to own gameplay rules; and cleanly cancel/restore presentation across lifecycle and scene teardown.

## Ownership

STEP 12 owns:

- Unity scene composition and the gameplay presentation root.
- Board/cell/obstacle views and ID-keyed view lookup.
- Touch hit-testing and translation to Connection commands.
- Connection-line and live-sum rendering.
- Presentation sequencing and acknowledgement of domain results.
- Target, answer-grade, miss, resolution, obstacle, Fever, restoration-milestone, pause, failed-decision, and result presenters.
- Audio, haptic, reduced-motion, and color-independent presentation adapters.
- Cancellation on pause, focus loss, Stage termination, scene unload, and disposal.
- Presentation configuration/assets after product approval.

STEP 12 consumes but must not own:

- Board topology, blocks, Dust/Box state, gravity/refill, deltas, and IDs.
- Connection legality, answer validation, target safety, shuffle/recovery, moves/objectives/score, Fever state/combo/clock, restoration arithmetic/world state, run IDs, Continue authorization, or Stage lifecycle rules.
- Save/progression storage (STEP 13), analytics (STEP 14), ad authorization (STEP 15), and concrete economy/progression rules.

Views never mutate Board, StageSession, FeverSession, WorldRestorationProgress, or obstacle state. They request commands through a gameplay orchestration port and render immutable snapshots/results.

## Assembly boundaries

Add:

- `MathGame.Presentation.Contracts`, Unity-free: immutable presentation command/result tokens, typed asset identities, accessibility settings, and ports. It may reference domain result assemblies but no Unity types.
- `MathGame.Presentation`, Unity-dependent: MonoBehaviours, views, presenters, touch adapter, sequencing, audio/haptics, and scene composition.

The Unity assembly may depend on App, Stage, Board, Connection, Answer, BoardResolution, Targets, Fever, ObstacleFlow, StageSession, Restoration.Contracts, and Presentation.Contracts. Domain assemblies never reference Presentation.

Persistence remains outside both assemblies. STEP 12 receives session-local accessibility/audio settings; STEP 13 later persists them through an adapter.

## Gameplay presentation coordinator

`GameplayPresentationCoordinator` is the sole presenter-side serializer. It receives an injected `IGameplayCommandPort`, immutable configuration, view ports, and a cancellation source. It owns no gameplay state.

It tracks:

- monotonically increasing `PresentationSequenceId`;
- exact authoritative Board reference/version token and rendered BlockId/ObstacleId mapping;
- current presentation phase;
- optional active touch pointer;
- one pending domain acknowledgement token;
- disposed/cancelled state.

Every asynchronous sequence uses prepare/play/acknowledge:

1. Validate phase, sequence ID, domain result, and rendered source token.
2. Build an immutable presentation plan without changing domain or views.
3. Lock touch input before the first visual mutation.
4. Play deltas in documented order.
5. Reconcile every view against the final authoritative snapshot.
6. Acknowledge the exact domain token once.
7. Only the domain Stage transition may re-enable input.

Stale, duplicate, wrong-board, wrong-phase, cancelled, or disposed plans do not acknowledge domain progress. Cancellation forces a full snapshot reconciliation or teardown; it never rolls back committed domain state.

### Authoritative gameplay token and acknowledgement port

STEP 12 requires a narrow STEP 10 orchestration migration without changing Dust, Box, damage, refill, target, or restoration behavior. `ObstacleResolutionCoordinator`, as the sole logical Board/next-ID owner, also owns a monotonically increasing `GameplayStateRevision`. It issues an immutable `GameplayStateToken` containing `StageRunId`, revision, source kind, and the correlated `StageAttemptId` or `BoardSystemEffectId` when applicable. The initial state has revision 1. Every successful authoritative Board/next-ID adoption increments the revision exactly once, including an adopted answer before pending target recovery and a later recovered/shuffled replacement. Reads and failed operations do not increment it.

The coordinator validates tokens by exact run and revision equality. Board reference equality and hashes are diagnostics only: cloned/mutable Board objects are never presentation authority. Every current-state snapshot and answer/end/retry flow result exposes the token associated with its detached Board snapshot.

`IGameplayCommandPort` is the only presentation-to-gameplay command surface. Its command families are `BeginPath`, `ExtendPath`, `ReleasePath`, `CancelPath`, `RetryTargetRecovery`, `AcknowledgeAnswerPresentation`, `AcknowledgeTargetReady`, `AcknowledgeFeverEntry`, `AcknowledgeFeverEnd`, `ResolveFailedDecision`, and `AcknowledgeTerminal`. Every request carries a `PresentationCommandId` or `PresentationSequenceId`, the exact `GameplayStateToken`, phase/source identity, and the attempt/effect/result identity being acknowledged.

Command validation precedence is `Disposed`, `MissingRequest`, `StageTerminated`, `InvalidStageState`, `StaleGameplayToken`, `DuplicateCommand`, `OutOfOrderCommand`, `PresentationStillRunning`, `DomainRejected`, then `Accepted`. Acknowledgement precedence is `Disposed`, `MissingAcknowledgement`, `StageTerminated`, `StaleGameplayToken`, `WrongSourceIdentity`, `DuplicateAcknowledgement`, `OutOfOrderAcknowledgement`, `WrongPhase`, then `Accepted`. Rejections invoke no domain command. Duplicate acknowledgement is an idempotent rejection, never a replay. Accepted acknowledgement advances only the guarded Stage/presentation boundary; it never reapplies a committed answer, system effect, restoration award, world commit, or target recovery.

Each returned presentation envelope contains the exact token, source ID, immutable detached Board/session/Fever/restoration snapshot, legal next acknowledgement kind, and status. Cancellation cannot acknowledge a different or newer envelope.

## Touch and connection flow

- Touch begins only while Stage accepts the correct input mode and no presentation sequence is active.
- Hit-testing returns a BoardPosition only; ConnectionPath remains the selection authority.
- Drag movement submits positions in hit order. Re-entering the immediate predecessor delegates to the existing backtrack command. Repeated/invalid/Box/hole positions are rendered from the returned Connection result, not guessed by the view.
- Pointer release submits the immutable snapshot to the answer/orchestration port only if Stage still accepts input. Cancellation, pause, focus loss, expiry, scene unload, or terminal transition cancels the path and clears line/live sum.
- One active pointer is supported for MVP. Multi-touch does not create parallel paths.
- Connection line avoids obstacle visuals, shows selection order and live sum, and provides a color-independent invalid/over-target state.

Touch slop, cell hit padding, drag sampling/interpolation, and blocked-pointer behavior require approved values; they are configuration, not domain rules.

## Visual synchronization

- Cells are keyed by BoardPosition. Number views are keyed by BlockId; obstacle views by ObstacleId.
- Initial/full reconciliation creates or reuses views to exactly match the authoritative layered Board.
- Removed deltas resolve first: selected in submitted order, then collateral in result order. Obstacle damage/destruction follows its typed evidence. Moves animate from exact From to To. Spawns appear in result order. The final snapshot is authoritative even if animations are skipped.
- Destroyed Box conversion is presented before the newly opened cell’s move/refill animation. Dust remains cell-anchored.
- Shuffle uses BlockId mapping and preserves obstacle views. Target presentation cannot acknowledge input readiness without a verified TargetSolution.
- Pooling is optional and must preserve ID rebinding and clear subscriptions/state on release; correctness must not depend on pooling.

## Stage and transaction boundaries

- `PresentingTarget`, `ResolvingAnswer`, `RecoveringBoard`, `EnteringFever`, `EndingFever`, `FailedPendingDecision`, Pause, Success, Failure, and Exit are noninteractive.
- PlayerInput/FeverInput are enabled only by existing Stage commands after required presentation acknowledgement.
- A committed STEP 10 answer may leave target proof pending. Presentation shows the adopted Board but does not enable input; retry never replays removal/restoration animation as a new answer.
- A failed STEP 10 Fever-end prospective transaction has no committed Board/system effect and therefore receives no success animation or acknowledgement.
- STEP 11 prospective restoration/world plans remain invisible. Presentation consumes only committed results/snapshots and newly crossed world milestones.
- World milestone presentation is exactly-once by WorldCommitId plus milestone identity. Duplicate world-commit observation produces no reveal.
- FailedPendingDecision presents Continue/Retry/Abandon without discarding provisional restoration. Continue preserves the same board/run/progress and enters recovery; Retry tears down the old run and binds the returned fresh StageRunHandle; Abandon terminates and discards. Presentation cannot fabricate a ContinueGrant.

## Fever flow

- Entry presentation begins only after safe target readiness and Stage EnteringFever. Approximate 0.5-second spectacle is configuration-owned; clocks remain stopped until FeverInput.
- Fever answer resolution uses the same delta plan with stronger approved visual/audio/haptic treatment; multiplier does not change domain damage/restoration rules.
- Fever clock display uses `FeverPresentationSnapshot FeverController.CapturePresentationSnapshot()` and never drives expiry. The immutable snapshot contains Fever state, gauge/current maximum, clock state, configured duration, cached elapsed/remaining seconds, total Fever corrects, current/max combo, current multiplier, optional pending end tier, and a monotonically increasing snapshot revision. Capture is non-sampling: it does not call the time provider, tick the clock, expire Fever, or transition Stage. The controller updates cached time only during its authoritative tick/Stage-transition processing. Any presentation notification is emitted after that update, and subscriber failure cannot alter gameplay.
- EndingFever remains locked until the STEP 10 end effect and STEP 11 restoration transaction are committed and presented.
- RandomThree uses resolver-selected positions. Small/Center/Large require an explicit center in the end request. STEP 12 must supply that center through an approved center-selection interaction/policy; it may not assume board center, last cell, best cell, or random center.

## Restoration presentation

- Stage-local energy may be displayed as provisional progress but emits no 25/50/75/100 milestone reveal.
- Only a committed world result supplies world milestones, in ascending result order.
- Typed milestone identity plus world identity maps to presentation assets through configuration. No asset key enters StageSession/Restoration.
- FailedPendingDecision preserves provisional display. Retry/Abandon present discard once; Continue retains it. Success presents world progress from WorldBefore to WorldAfter and ignores discarded excess except optional non-gameplay feedback.

## Failure behavior

- Missing view/config/asset binding, invalid hierarchy, duplicate view identity, source-board mismatch, stale plan, cancelled plan, and domain acknowledgement rejection are explicit failures.
- Before play, failure changes nothing. Mid-animation failure locks input and performs full snapshot reconciliation; if reconciliation cannot complete, remain noninteractive and surface a fatal presentation/configuration error.
- Subscriber/animation/audio/haptic exceptions cannot advance domain acknowledgements.
- Pause/focus loss cancels active touch immediately. Animation policy on pause (freeze versus snap/reconcile) requires product approval; either policy must be deterministic.
- Scene unload/disposal unsubscribes all Stage/lifecycle events, cancels operations, clears pointer capture and pools, and never invokes delayed acknowledgements.

### Scheduling and cancellation

The presentation assembly owns `IPresentationScheduler`, all animation tasks, and one cancellation source per `PresentationSequenceId`, linked to a root scene-lifetime source. Domain assemblies own no scheduler or cancellation token. Pause/focus/terminal/unload cancellation may interrupt plan construction or animation playback.

Final reconciliation is a synchronous, idempotent, non-cancellable critical section. Once entered, it completes against the exact envelope token and immediately performs acknowledgement in the same synchronous continuation, with no `await`, frame callback, audio/haptic task, or cancellation observation between reconciliation and acknowledgement. Cancellation observed before that section causes no acknowledgement and leaves gameplay locked until a later full reconciliation/retry. Stage termination observed during the section wins and suppresses acknowledgement. A reconciliation exception also suppresses acknowledgement and leaves Stage noninteractive. Audio, particles, and haptics are cancellable consequences and never gate acknowledgement.

Accessibility/presentation settings are snapshotted into each immutable plan. Mid-sequence changes are queued for the next plan unless product explicitly approves a live-change policy; they never alter a running plan's timing or acknowledgement conditions.

## Determinism and idempotency

- Domain result order is never re-sorted except where the domain explicitly returns row-major animation order.
- Animation timing may vary by accessibility configuration, but final view state and acknowledgement order are deterministic.
- `PresentationSequenceId`, domain attempt/effect IDs, Board token, WorldCommitId, and milestone identity reject duplicate/stale delivery.
- Audio/haptic cues are consequences of committed typed events and fire at most once per presentation plan.
- Reconciliation is idempotent and produces the same hierarchy/state from the same authoritative snapshot.

## Scene/UI composition

Minimum screens/layers:

- gameplay HUD: target, moves, objectives, score if configured, Fever gauge/timer/combo, stage-local restoration;
- board: cells, number blocks, Dust/Box, connection line and live sum;
- overlays: pause, FailedPendingDecision, success/result, fatal presentation failure;
- feedback: PERFECT/FAST/NORMAL/Miss, obstacle damage, target transition, Fever entry/progress/end, world restoration milestones.

Result UI must not invent stars, currency, reward quantities, progression unlocks, or persistence claims. Continue authorization remains a port for STEP 15; without a valid grant, the button cannot mutate the run.

## Migrations

- App composition must construct the presentation root and explicit gameplay command port; existing bootstrap must not become a gameplay-rule singleton.
- ObstacleFlow gameplay rules do not change. Its coordinator adds the authoritative `GameplayStateRevision`/token and detached snapshot envelopes described above; Presentation consumes committed/adopted Board results and pending-target status distinctly.
- Fever adds only the non-sampling `FeverPresentationSnapshot` read contract; its clock remains the sole expiry authority.
- STEP 11 world-commit results must be delivered as immutable presentation input; Presentation must not call internal world mutation methods.
- Stage event subscriptions must include FailedPendingDecision and Fever states.
- Existing placeholder/empty scene work is replaced only after approved scene/prefab/asset contracts exist.

## Test strategy

Edit Mode:

- plan validation/precedence; source token and sequence staleness; duplicate acknowledgement; immutable copied inputs;
- exact removed/obstacle/move/spawn/shuffle ordering; destroyed-Box refill and Dust anchoring;
- connection touch translation, backtrack, invalid/Box/hole, multi-touch rejection, cancel/release races;
- target proof pending/retry without replay; Fever end center request mapping;
- restoration provisional/commit/discard mapping and duplicate milestone suppression;
- failure before/mid sequence, reconciliation idempotency, callback exception isolation, disposal.

Play Mode:

- scene composition and representative aspect/safe-area layouts;
- touch drag/path/live sum/release and focus/pause cancellation;
- full normal and Fever answer sequences with input locked throughout;
- obstacle damage/destruction, gravity/refill, shuffle and end-effect presentation synchronization;
- FailedPendingDecision Continue/Retry/Abandon lifecycle and old-run teardown;
- success world milestone reveal exactly once;
- scene unload/reload, pooled-view cleanup, no delayed callback or event leak;
- reduced-motion, vibration disabled, flash reduction, and color-independent readability.

Unity licensing failure remains an environment verification block and must never be reported as a passing test.

## Product decisions required

The previously blocking decisions are resolved as follows:

1. **Fever area center selection:** derive the arithmetic center of the committed answer-removal footprint and choose the nearest valid active cell; row-major order breaks equal-distance ties. Consume no gameplay RNG.
2. **Touch constants:** hit radius is 45% of rendered cell size. Dragging interpolates crossed logical cells in traversal order; re-entering an already selected cell never appends it again.
3. **Presentation timing:** selection 80ms, removal 120ms, gravity/fall 160ms, refill 160ms, restoration milestone 300ms. Timing never determines commit. Reduced Motion replaces translation/scale-heavy motion with immediate application or a fade no longer than 50ms.
4. **Asset contract:** placeholder visual/audio assets are approved where final assets do not exist. Domain assemblies never contain concrete asset identifiers.
5. **Layout/accessibility:** portrait-only MVP. State is never communicated by color alone; selected, blocked, damaged, completed, and unavailable states have additional indicators.
6. **Failure/result UI:** failure shows stage-local restoration, current/remaining objectives, eligible Continue only, Retry, and Abandon. Success shows attempt restoration, resulting world restoration, newly crossed milestones, and next/proceed.
7. **Pause behavior:** pause suspends presentation playback. Resume continues when its token remains current; otherwise it reconciles to the latest authoritative snapshot.
8. **MVP special-block scope:** special-block gameplay and presentation are explicitly deferred from STEP 12. Existing semantic intents do not create or display an invented special.
9. **Orientation and settings changes:** portrait only; runtime rotation is out of scope. Reduced Motion and other runtime setting changes take effect at the next command or reconciliation boundary and never rewrite committed state.

These decisions are final STEP 12 implementation requirements.

## Out of scope

- Save/schema/progression persistence (STEP 13).
- Analytics dispatch (STEP 14).
- Ad SDK and ContinueGrant issuance (STEP 15).
- New gameplay rules; special blocks remain outside STEP 12 unless decision 8 first schedules and completes their own domain slice. Deferred obstacle types, boosters, economy, stars, currency, and social/live operations remain out of scope.
- Post-MVP polish beyond approved assets and accessibility behavior.

## Disposition

**READY FOR IMPLEMENTATION.** STEP 12 may proceed under the approved decisions above. STEP 13 must not begin.
