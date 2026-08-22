# MathGame Architecture (Current)

Last inspected: 2026-08-15

This file describes only code present in the repository. Proposed gameplay architecture belongs in the relevant STEP design until it is approved and implemented.

## Project baseline

- Unity 6000.3.6f1, Universal Render Pipeline 17.3.0, Input System 1.18.0, and Unity Test Framework 1.6.0.
- The only enabled build scene is `Assets/Scenes/SampleScene.unity`.
- Runtime code is under `Assets/MathGame/Runtime`; tests are under `Assets/MathGame/Tests`.
- Logical board, generation, connection, answer timing, board resolution, safe-target recovery, stage-session, and Fever-core domains exist. No playable board view/input adapter, composed gameplay loop, concrete Fever board effects, obstacle behavior, restoration, gameplay UI, analytics adapter, ad adapter, or concrete save repository exists.

## Development responsibility boundary

Core gameplay implementation and Unity production integration are separate delivery responsibilities:

```text
Domain / Application / StageSession
-> Presentation contracts and runtime adapters
-> Unity Production integration
-> Serialized Prefabs / Scene
```

The Unity Client Developer owns approved code architecture, deterministic gameplay/application behavior, presenters/contracts, and required runtime adapters. The Unity Production Agent consumes those APIs to make the feature usable in Unity through Scene composition, Prefabs, serialized references, BoardView/UI binding, input/EventSystem wiring, responsive Safe Area layout, and Unity-specific validation.

Unity Production is conditional: `DOMAIN_ONLY` work does not acquire artificial Scene/Prefab scope; `UNITY_FACING` and `MIXED` work do. Stable presentation objects such as GameRoot, BoardView, HUD roots, and overlay roots should be serialized where the feature design establishes that model. Runtime binds state to those views rather than recreating their hierarchy. Dynamic content such as variable objective items may instantiate controlled Prefabs.

This workflow boundary does not change runtime assembly ownership or transfer gameplay authority to MonoBehaviours.

## Assembly boundaries

```text
MathGame.Core        (no custom assembly dependency)
MathGame.Save        (legacy schema shell; no custom assembly dependency)
MathGame.PlayerProgress ---> MathGame.SurvivalRun (no UnityEngine reference)
MathGame.LocalSave   ---> MathGame.PlayerProgress (Unity-facing path/JSON/file infrastructure)
MathGame.Board       (no custom assembly dependency; no UnityEngine reference)
MathGame.BoardGeneration ---> MathGame.Board, MathGame.Core (no UnityEngine reference)
MathGame.Connection  ---> MathGame.Board (no UnityEngine reference)
MathGame.Answer      ---> MathGame.Connection, MathGame.Core, MathGame.Stage (no UnityEngine reference)
MathGame.BoardResolution ---> MathGame.Board, MathGame.Answer, MathGame.Connection, MathGame.Core (no UnityEngine reference)
MathGame.Targets     ---> MathGame.Board, MathGame.Answer, MathGame.Core (no UnityEngine reference)
MathGame.StageSession ---> MathGame.Answer, MathGame.BoardResolution, MathGame.Connection, MathGame.Board (no UnityEngine reference)
MathGame.Fever       ---> MathGame.Core, MathGame.Stage, MathGame.Answer, MathGame.StageSession, MathGame.BoardResolution and exposed model dependencies (no UnityEngine reference)
MathGame.Stage  ---> MathGame.Core
MathGame.App    ---> MathGame.Core, MathGame.Stage, MathGame.Save

EditModeTests   ---> MathGame.Core, MathGame.Stage, MathGame.Save, MathGame.Board, MathGame.BoardGeneration, MathGame.Connection
PlayModeTests   ---> MathGame.App, MathGame.Core, MathGame.Stage
```

The domain feature assemblies and test assemblies are not auto-referenced. Unity-free domain assemblies set `noEngineReferences` to enforce their boundaries. Edit Mode tests are Editor-only.

## Existing systems

### Core services

- `IRandomSource` and `SystemRandomSource` provide injectable integer/float randomness. Seeded construction enables deterministic tests.
- `ITimeProvider` and `UnityTimeProvider` expose real-time seconds. No interactive-time accumulator exists yet.
- `IGameLogger` and `UnityGameLogger` isolate basic logging calls from domain classes.

### Stage lifecycle

- `StageController` is a plain C# state owner with transition results and `StateChanged` events.
- Foundation initialization ends in non-interactive `Ready`; a blank stage never advertises player input.
- It implements initialization, nested pause reasons, resume-to-previous-state, guarded terminal commands, and exit.
- `StageController` exposes the STEP 5 target-presentation, player-input, answer-resolution, and same-target miss-return transitions. Fever state names remain reserved and unreachable until their owning STEP.
- Player input eligibility is derived from `PlayerInput` or `FeverInput`.
- `Complete` and `Fail` are invalid from `None`, `Initializing`, and `Ready`. Later objective/session orchestration must become the authorized terminal caller.

### Application composition and lifecycle

- `MathGameBootstrapInstaller` creates a bootstrap before scene load if one is absent.
- `MathGameBootstrap` is a persistent MonoBehaviour and the current composition root. It constructs logger, time, randomness, and stage controller directly.
- A static instance field prevents duplicate bootstrap objects; the bootstrap clears the guard and exits its stage on destruction.
- `ApplicationLifecycleRelay` caches nullable latest pause/focus facts before publishing Unity lifecycle events. Nullable values distinguish an unreported fact from an active state.
- The bootstrap checks both initialization transitions, defers pre-initialization lifecycle mutation, and reconciles cached inactive facts after reaching `Ready`.
- Live and reconciled facts use the same idempotent pause-reason synchronization. Background and focus-loss reasons remain independent, so clearing one cannot resume while the other remains.

### Persistence seam

- The older `SaveData`/`ISaveRepository` shell remains for compatibility and is not used by Continuous Run records.
- `MathGame.PlayerProgress` owns immutable `RunRecords`, applied-run identity history, deterministic personal-best comparison, and `IPlayerProgressRepository`. It is Unity-independent.
- A finalized `RunResult` carries a stable run identity. `PlayerProgressService` applies each identity at most once and returns truthful personal-best flags without Presentation comparisons.
- `MathGame.LocalSave` maps progress to schema-version 1 JSON under `Application.persistentDataPath`. It writes a verified temporary file, preserves the previous valid primary as backup, and recovers primary -> backup -> new-player default.
- `PrototypeGameSceneController` is the outer composition point. It loads progress when composing a run and applies/saves the terminal `RunResult` before Play Again. Save failure is observable and blocks restart until a retry succeeds.
- Backend/account synchronization, conflict resolution, cloud migration, currencies, inventory, achievements, and meta progression remain outside this local prototype repository.
- `MathGame.Presentation` references the official Unity Localization and Resource Manager assemblies. It selects only supported `en`/`ko` Locale assets, reads semantic String Table keys, and persists the selected locale through P08 settings. No gameplay/domain assembly references Localization.

### Board domain

- `BoardTopology` owns positive rectangular extents and an immutable active-cell mask. It distinguishes out-of-bounds positions, inactive holes, and active cells.
- Coordinates use a lower-left origin and enumerate active cells row-major. Orthogonal neighbors enumerate deterministically as Up, Right, Down, Left while omitting bounds and holes.
- `BlockId` and `NumberBlock` are immutable positive value types. A block has stable logical identity independent of GameObjects; equal numeric values with different IDs are valid.
- `Board` owns dense mutable cell state plus a unique live-ID index. Each active cell has independent optional number occupancy and Open/Blocked access.
- Immutable cell snapshots expose state without leaking mutable arrays or cells.
- Place, remove, relocate, and access changes return explicit results. Failed mutations leave cells, block count, and ID index unchanged.
- The board contains no generation policy, selection, target, gravity, obstacle behavior, events, presentation, time, randomness, or persistence logic.

### Initial board generation

- `BoardGenerationConfig` is immutable request data containing topology, inclusive number bounds, and the first board-local block ID. Validation is centralized in the generator so expected invalid content produces stable failure results.
- `BoardGenerator` is a stateless plain C# service with an injected `IRandomSource`. It never creates or owns a seed and never uses Unity randomness.
- Generation validates the entire request before consuming randomness, creates a fresh Board, then fills active positions in topology row-major order.
- Each active position consumes exactly one integer draw and one sequential ID. Inactive holes consume neither. Prototype callers use 5×5 and values 1–9; the generator itself accepts other valid configured topology/ranges.
- `BoardGenerationResult` exposes either a complete Board plus the next unused ID or a stable failure with no partial Board. ID-capacity arithmetic and inclusive-to-exclusive range conversion are checked before generation.
- Population success is not a solution guarantee. No target, path search, retry, shuffle, refill, Stage transition, or input enabling exists in BoardGeneration; STEP 7 must verify a target path before gameplay exposure.

### Connection path

- `ConnectionPath` is a mutable plain C# owner bound to one Board. It reads cell snapshots but never mutates Board state.
- It captures ordered positions and immutable block identities/values, tracks selected membership, and maintains a checked `long` live sum.
- First selection requires an active Open occupied cell. Later additions require Manhattan distance one; diagonals, gaps, holes, empty/blocked cells, and duplicate positions are rejected atomically.
- Entering the immediate predecessor removes only the tail. Other selected positions are rejected. Explicit Cancel clears the complete path and is idempotent.
- `ConnectionPathSnapshot` copies entries into read-only historical data that is stable after later path or Board mutations.
- Connection contains no target comparison, answer submission, timing, Board resolution, Unity input, or presentation behavior.

### Answer validation and interactive timing

- `AnswerValidator` purely classifies immutable connection snapshots against positive targets. Correct requires exact sum and at least two blocks; under, over, and one-block equality are Miss outcomes. Correct grades use inclusive 2/4-second thresholds.
- `InteractiveAnswerClock` subscribes to Stage state, samples injected unscaled time, and accumulates only while Stage accepts player input. Presentation, resolution, every pause reason, and app inactivity are excluded.
- Clock state/command/fault results are explicit. Nonfinite or backward time faults rather than silently changing a grade; disposal unsubscribes safely.
- Stage now exposes the authoritative phase graph `Ready/ResolvingAnswer -> PresentingTarget -> PlayerInput -> ResolvingAnswer`, plus same-target miss return `ResolvingAnswer -> PlayerInput`. These commands add no Board resolution or target selection.

### Board resolution

- `BoardResolver` validates a Correct AnswerResult against exact current block positions, IDs, and values, then constructs a replacement Board without mutating the source.
- Gravity moves toward decreasing Row within vertically contiguous active segments. Inactive topology holes split segments; blocks never cross holes or columns.
- Survivors retain identity/value. Refills use injected randomness, configured inclusive bounds, sequential IDs, and deterministic column/segment/bottom-up traversal.
- Results contain copied read-only removed, moved, and spawned deltas plus the next unused ID. Expected failures expose no Board/deltas and consume no randomness during preflight.
- Basic resolution requires every active source cell to be Open and occupied. Generic blocked/obstacle states remain unsupported until STEP 10 defines their distinct semantics.

### Verified targets and recovery

- `TargetPathSearcher` performs bounded deterministic DFS over Open occupied cells, using row-major starts and fixed orthogonal neighbor order. It returns sorted distinct targets with canonical current-Board witnesses; a search-limit result discards partial candidates.
- `SafeTargetSelector` chooses uniformly across proven distinct values with explicit immutable repetition policy/history. A capped previous value is excluded when alternatives exist and marked as a fallback when it is the sole safe value.
- `BoardShuffler` performs identity-preserving Fisher–Yates on a fresh Board. `TargetRecoveryCoordinator` shares one injected random stream across shuffle and selection, regenerates a target before shuffling, bounds attempts, and re-searches after every attempt.
- Successful recovery returns an exact witness, Board/history, zero move cost, and immutable original-to-final shuffle deltas. Failure never advertises a safe target or enables input.
- Stage includes noninteractive pausable `RecoveringBoard`; deadlock recovery enters it from PlayerInput and only verified success may proceed to PresentingTarget.

### Stage session, objectives, and completion

- `StageSession` is the Unity-free authoritative owner of normal-mode moves, supported objective progress, score, answer counters, FAST streak, semantic Fever contributions, and terminal ordering.
- Attempts use positive monotonic IDs. Duplicate, out-of-order, malformed, and mismatched answer/resolution inputs are rejected without mutation.
- Correct attempts require a successful resolution whose removed entries exactly match the submitted positions, IDs, values, and order. Misses cost no move and reset the FAST streak.
- Implemented objectives are number-block removal, specified-target completion, and long-connection completion. Obstacle, restoration-energy, and special create/use objectives fail explicitly until their owning systems provide authoritative evidence.
- Normal Correct consumes one move. Objective effects are applied before terminal evaluation, so completing the final objective on the last move succeeds; zero moves with an incomplete objective fails.
- Score values are explicit stage configuration because the GDD supplies no formula. Exact grade/length/FAST-streak Fever contributions and 4/5+ special intents are immutable semantic facts only; STEP 8 does not apply Fever or create specials.
- Results expose immutable historical snapshots and ordered semantic events. StageController remains a lifecycle state machine; orchestration maps session Success/Failure from ResolvingAnswer to its existing terminal commands.

### Fever core

- `FeverChargeTracker` consumes applied normal StageSession attempts exactly once, permits global attempt-ID gaps across Fever attempts, caps the live gauge at configured maximum, and does not bank excess charge.
- `FeverController` owns the cross-domain Fever attempt command. It prospectively derives combo/rules, applies the owned StageSession attempt, and commits Fever state only after StageSession accepts, preventing split accounting.
- Fever-aware StageSession rules form a closed Normal/Fever policy. Fever Correct costs zero moves and multiplies checked configured score by the approved 1/2/3/5 combo multiplier; normal accounting remains unchanged.
- `InteractiveFeverClock` counts only exact `FeverInput` intervals using injected monotonic time. Resolution, target presentation, entry/end phases, nested pause reasons, focus/background loss, and ads are excluded. Faults force the controller into a safe noninteractive state.
- Stage exposes explicit EnteringFever, FeverInput, Fever-origin resolution/miss return, and EndingFever transitions while retaining resolution origin across pause.
- Natural expiry emits immutable end-effect tiers based on total Fever Correct answers and resets only after effect acknowledgement. Terminal Stage outcomes abort Fever and suppress gameplay end effects.
- Expanded removal, obstacle damage execution, restoration calculation, random/area end effects, and spectacle remain semantic requests for STEPs 10-12.

### Restoration progress

- `MathGame.Restoration.Contracts` contains Unity-free run/world identities, stage restoration configuration, typed award evidence, lifecycle facts, milestones, world-plan binding, and the two-phase Continue grant port.
- `MathGame.StageSession` owns provisional per-run restoration. Approved answer and Large Fever-end awards enter the same prospective attempt/system-effect plan as objective and terminal evaluation; clamping and discarded excess are atomic with that plan.
- `MathGame.Restoration` calculates exact integer awards, owns additive clamped world progress, derives canonical 25/50/75/100 milestones, and applies stable `WorldCommitId` values exactly once.
- A prospective Success binds a versioned world plan before StageSession mutation. The composition coordinator in `MathGame.ObstacleFlow` then performs the already-validated StageSession and world assignments without callbacks between them; `MathGame.Restoration` has no Fever, Stage, or ObstacleFlow dependency.
- Move exhaustion in restoration-enabled runs enters noninteractive `FailedPendingDecision`. Continue preserves the same run and provisional energy while adding the approved five moves through a two-phase grant reservation; Abandon discards provisional energy and terminates the run.
- Obstacle answer/Fever/end orchestration supplies restoration evidence without changing STEP 10 board adoption or target-recovery ordering. Concrete restoration visuals and asset bindings remain STEP 12.

## Current runtime flow

```text
BeforeSceneLoad -> create bootstrap -> Awake composes services
               -> Start -> StageController.Start
                        -> FinishInitialization -> Ready (non-interactive)
                        -> reconcile cached pause/focus facts

Unity pause/focus -> lifecycle relay caches fact -> bootstrap
                  -> before initialization: defer
                  -> after initialization: synchronize matching pause reason
Bootstrap destroy -> unsubscribe -> StageController.Exit -> clear singleton guard
```

The stage is currently called “blank” because `Ready` has no board, target, input, or views. A later target/gameplay STEP must explicitly enter an interactive state.

## Verification already represented by tests

- Edit Mode (208 tests): foundation through Fever Core coverage, including charge/idempotence, atomic Fever attempts, zero-move score multipliers, combo/reset, exact-input clock exclusions/faults, Stage Fever graph, end tiers/acknowledgement, terminal abort, and all earlier regressions.
- Play Mode (22 tests): bootstrap/lifecycle and answer-clock regressions plus deterministic Fever resolution, focus/background pause nesting, expiry exclusion, and disposal behavior.

Verified with Unity 6000.3.6f1 on 2026-08-08 after STEP 9: Edit Mode 208/208 and Play Mode 22/22 passed with valid result XML. Fever, StageSession, Stage, and affected assemblies compiled with no C# errors.

## Known architectural risks

- `StageState` anticipates future states without implemented transition commands; later STEP designs must confirm or revise these names rather than assuming them correct.
- `MathGameBootstrap` directly constructs concrete services. This is adequate for the current small foundation but will need a deliberate composition strategy as gameplay dependencies arrive.
- There is no scene/content bootstrap beyond the persistent application object.
- Save-on-background, interrupted-stage restoration, and platform-specific lifecycle sequences still require their owning later STEP designs and device verification.
- Board access is intentionally a minimal Open/Blocked fact. STEP 10 must derive or replace it from approved layered obstacle state so independent flags do not become competing rule authorities.
- Exact gravity traversal through masked shapes and content connectivity rules remain deferred to their owning designs.
- Initial population remains unverified until the implemented STEP 7 search supplies a current-board witness. Bounded `SearchLimitExceeded` or `UnrecoverableDeadlock` results intentionally keep input disabled and require content/configuration handling rather than unsafe target exposure.
- Restoration behavior is implemented as a logical domain transaction, but Unity Edit/Play verification is currently blocked by licensing initialization. Presentation remains STEP 12.

### STEP 12 presentation boundary

- `MathGame.Presentation.Contracts` owns immutable presentation envelopes, ordered plans, gameplay command/acknowledgement contracts, deterministic touch/center policies, and cancellation/reconciliation sequencing.
- `MathGame.Presentation` owns Unity-only placeholder views, logical touch input, identity-keyed block/obstacle views, HUD/connection/result feedback, and a playback driver. Views never mutate Board, StageSession, Fever, restoration, or world state directly.
- `ObstacleGameplayPresentationPort` delegates normal/Fever answers, target retries, Fever end effects, and failed decisions to the existing STEP 10/11 authorities. Miss and committed-answer presentation remain noninteractive until their exact token/source acknowledgement.
- Animation cancellation stops the active coroutine, reconciles the authoritative snapshot once, and requires explicit acknowledgement. Stale tokens never replay gameplay commands.
- Static Production/Edit/Play assembly compilation is verified. Unity Edit/Play execution remains environment-blocked by licensing initialization and is not recorded as a runtime pass.

### Continuous Run production mode

- `MathGame.StageSession` supports explicit `LegacyStage` and `ContinuousRun` modes. Legacy stages retain move/objective terminal behavior. Continuous runs accept zero objectives, spend no moves, and cannot succeed or fail from objectives or move exhaustion; score, attempt correlation, obstacle evidence, and Fever facts still use the established transaction.
- `MathGame.SurvivalRun` owns configurable time capacity, all-live-phase drain, active duration, grade recovery, committed-correct-cycle difficulty, run-wide statistics, and the immutable exactly-once Run result. Recovery plans correlate to increasing committed Stage attempt IDs, allowing gaps created by misses.
- `StageController.RunEnded` is the dedicated continuous-run terminal state. Unity ticks Survival Time before accepting pointer input, so observed expiry wins before a new answer. An answer already committed may apply its prepared recovery once.
- Difficulty changes only the proven target range. The prospective range is supplied to target recovery for the threshold-crossing correct cycle; number generation, obstacles, and Fever remain unchanged.
- The Production composition reuses the serialized `GameplayRoot/BoardSlot/BoardView`. Run HUD and Run Result are presentation-owned; no BoardView is instantiated during a run or Play Again.
- Production P10A keeps interaction polish entirely inside `MathGame.Presentation`: the serialized Board prefab owns its UI selection line, prebuilt cell views animate authoritative resolution events, and HUD/result views own short resettable transitions. Consecutive removal, movement, and refill events share one presentation phase delay so effect count does not create a long gameplay lock. Gameplay, Survival Time, target, Fever, score, and obstacle assemblies remain the only authorities for their facts.
- Pre-design removal feedback is also presentation-owned: `GameplayPresentationRoot` consumes committed removal deltas and requests a capped `BlockRemovalEffectPool` under the existing serialized `EffectSlot`. The pool obtains its replaceable UI-effect prefab from `MathGamePrefabRegistry`, reuses completed instances, and is reset on run teardown; missing effects never block or alter Board mutation.
- Legacy Stage Clear, moves, objectives, restoration, Continue, and stage progression code remains available but is not composed into the primary Continuous Run UI.
- P04 separates immutable Survival Time, timing recovery, and ordered explicit difficulty-tier configuration. Tier lookup caps at the final configured tier.
- P05 adds a one-way authoring boundary: Editor CSV conversion -> validated generated JSON -> runtime `IRunConfigRepository` -> `SurvivalRunConfig`. Survival domain code has no CSV, file, AssetDatabase, or Unity dependency.
- P06 runtime composition requires the generated JSON resource. Invalid/missing content fails composition explicitly rather than silently selecting fallback balance.
