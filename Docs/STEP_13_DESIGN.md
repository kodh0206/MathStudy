# STEP 13 Design — Progression and Persistence

**Status:** BLOCKED BY PRODUCT DECISION  
**Command:** Design STEP 13 only. Production implementation is prohibited until the decisions in “Product decisions required” are approved and an independent re-review has no P0/P1.

## Goal

Persist only the MVP progression and player settings explicitly approved by product design. Loading must produce a validated, internally consistent snapshot; supported older schemas migrate deterministically; corrupt or unsupported data fails safely without silently granting or losing progression. Successful world restoration commits remain exactly-once across restart. Application lifecycle saving must never become a second owner of gameplay state.

## Design basis and scope finding

The GDD defines themed worlds, stage success unlocking later play, world restoration, optional star evaluation, rewards, and accessibility controls. It does not define a concrete stage catalog, unlock graph, stage/world identifiers and capacities, star thresholds, currencies, setting defaults/ranges, interrupted-run policy, or corruption recovery policy. `DEVELOPMENT_PLAN.md` explicitly lists “progression/settings specification” as a STEP 13 dependency.

Existing production provides only `SaveData.SchemaVersion = 1` and synchronous `ISaveRepository.TryLoad/Save`; no progression payload or repository implementation exists. STEP 11 owns in-memory provisional stage restoration and successful idempotent world commits. STEP 10 Board state is explicitly not saved. STEP 12 presentation consumes settings but owns none.

Therefore storage architecture is designable, but writing a production schema now would freeze invented gameplay/content rules. STEP 13 remains blocked.

## STEP 13 owns

- Immutable validated persistent progression snapshot and settings snapshot.
- Save DTO schema versions and deterministic migrations between supported versions.
- Serialization/storage adapter, atomic replace strategy, validation, corruption classification, and recovery result.
- Progression transaction coordinator that prepares and commits persistence changes from authoritative successful domain results.
- Durable registries for applied `WorldCommitId` and globally unique `StageRunId` allocation.
- Dirty/version tracking, serialized save scheduling, lifecycle flush requests, retryable I/O failure state, and load-before-gameplay gate.
- Typed load/save/migration/recovery results and diagnostics without vendor SDK calls.

## STEP 13 consumes but must not own

- `StageSession`: active/FailedPendingDecision/Success lifecycle and provisional stage restoration.
- `WorldRestorationProgress`: authoritative in-memory world current/capacity and committed-ID behavior.
- `StageRunRegistry`: active/retired run identity semantics.
- STEP 10 Board/obstacle flow: runtime Board, block IDs, obstacle HP, pending target recovery. None is persisted in MVP.
- `FeverController`: active gauge, clock, combo, and pending end effect. None is persisted in MVP.
- STEP 12 Presentation: settings display and result acknowledgement. Views never load/save or confirm progression.
- App lifecycle relay: provides pause/focus/quit facts only; it does not choose save contents.

## Proposed assembly and dependency direction

- Extend Unity-free `MathGame.Save` into DTO/contracts and validation only; it references no gameplay assembly.
- Add Unity-free `MathGame.Progression`, referencing `MathGame.Save`, `MathGame.Restoration.Contracts`, and the minimum read-only StageSession result contracts required for successful-stage correlation.
- Add Unity adapter `MathGame.Persistence.Unity`, referencing `MathGame.Save`, `MathGame.Progression`, and `MathGame.App` lifecycle seams. It owns file I/O and Unity serialization/path access.
- Add composition adapter `MathGame.Progression.Composition`, referencing `MathGame.Progression`, `MathGame.Restoration`, and `MathGame.ObstacleFlow`. It is the only layer allowed to translate hydration records into the existing concrete `WorldRestorationProgress`, `StageRunRegistry`, and RestorationTransactionCoordinator calls.
- Gameplay composition depends on an `IProgressionCommitPort`; Progression never references Restoration implementation, ObstacleFlow, Fever, Presentation, or Unity.

No dependency may point from Board, StageSession, Fever, Restoration.Contracts, or Presentation back to persistence.

## Conditional persistent model

Exact fields depend on approved product decisions. The minimum non-speculative identities/facts are:

- installation/save-lineage ID;
- schema version and monotonic save revision;
- next durable StageRunId value plus reserved/retired IDs required to prevent reuse;
- per-world stable ID, capacity, current restoration, and applied `WorldCommitId` set;
- per-stage stable ID and only the approved completion/unlock facts;
- approved accessibility/audio settings and their validated values;
- optional integrity/last-write metadata used only for recovery, never gameplay rewards.

Do not persist Board layouts, RNG state, active target, selected path, moves, Fever state, provisional restoration, failed pending attempts, presentation sequence, or animation state unless product explicitly approves resumable stages. With the lowest-risk recommendation below, interrupted attempts are abandoned and restart from the last successful persistent snapshot.

## Transaction boundary and exactly-once behavior

### Composition owner

Add one application-level `StageProgressionTransactionCoordinator` in `MathGame.Progression`. It speaks only immutable `ProspectiveStageCommitRecord`, `WorldHydrationRecord`, `RunRegistryHydrationRecord`, and narrow interfaces declared by Progression. It does not name or construct `WorldRestorationProgress`, `StageRunRegistry`, RestorationTransactionCoordinator, ObstacleFlow results, or Unity objects.

`MathGame.Progression.Composition` owns concrete adapters implementing those ports. It translates the existing STEP 11 prospective result into the neutral record, delegates persistence planning, then invokes the existing restoration transaction through an adapter-local call. ObstacleFlow depends only on an interface declared by `MathGame.Progression`; the composition adapter implements that interface. ObstacleFlow never references the composition assembly, so no cycle or Progression-to-ObstacleFlow/Restoration implementation edge exists. Nonterminal STEP 10 adoption/retry ordering remains unchanged.

Progression exports validated immutable `WorldHydrationRecord` and `RunRegistryHydrationRecord` collections only. The composition adapter constructs the complete set of concrete `WorldRestorationProgress` owners and the one shared `StageRunRegistry` before gameplay. Import occurs only at bootstrap; neither concrete adapter exposes setters after composition.

### Successful stage

1. The existing STEP 11 coordinator prepares the restoration-aware successful attempt and its version-bound `WorldCommitPlan`.
2. Before assignment, the progression coordinator prepares a `ProgressionCommitPlan` bound to the exact save revision, StageRunId/WorldCommitId, StageDefinitionId, world identity, and prospective world snapshot.
3. Validate unlock/content mapping and all checked arithmetic without mutation or I/O.
4. Before gameplay assignment, satisfy one product-approved crash-safety mechanism: either durably write a validated pending commit/WAL record and flush it, or durably replace the complete prospective snapshot. A merely in-memory dirty flag is insufficient.
5. Existing StageSession/world assignment remains the gameplay authority and is invoked only after the durable prerequisite succeeds.
6. The coordinator performs the guaranteed in-memory persistent assignment, then completes/clears the pending record if the selected protocol requires it. On restart, a pending record is deterministically rolled forward or recognized as already contained in the snapshot by `WorldCommitId`; it is never applied twice.
7. Presentation/navigation acknowledgement follows the approved durability point. Repeating the same commit is idempotent and never reapplies restoration, unlocks, or rewards.

The repository must not be called inside StageSession or WorldRestorationProgress. A failure before the durable prerequisite leaves StageSession/world/profile unchanged and the success plan retryable. A failure after the durable prerequisite is recovered deterministically from the record/snapshot. This closes the process-kill window; the earlier “commit then eventually flush” protocol is explicitly prohibited.

### Durable StageRunId allocation

Allocating/reserving a StageRunId is itself a persistence transaction. Before a new StageRunHandle is returned or gameplay begins, the coordinator must durably advance the lineage counter/reservation using atomic replace or WAL. Failure returns an allocation/persistence error and creates no run. On restart, every durably reserved ID remains unavailable even if no stage result was committed. IDs are never reclaimed after uncertainty; gaps are valid. Retry follows the same rule before its fresh handle is published.

### WAL/prospective-snapshot state machine

The selected product protocol must have explicit states and first-applicable failure statuses:

1. `None` — no pending durable transaction.
2. `Prepared` — full canonical intent contains lineage, schema/catalog revision, base save revision/hash, prospective save revision, StageRunId/WorldCommitId, and complete before/after world/progression facts; it is flushed before gameplay assignment.
3. `GameplayAssigned` — optional marker only if the platform can durably advance it without creating another unsafe gap. Recovery correctness must not depend on observing this marker.
4. `ProfileCommitted` — the complete prospective profile has been atomically replaced and flushed. If prospective-snapshot replacement is the chosen protocol, this replacement itself is the authoritative persistent assignment; there is no later independent profile assignment.
5. `Cleared`/`None` — intent removal is cleanup only; failure to clear is safe because the committed profile contains the same commit ID/revision and replay becomes an idempotent no-op.

Startup precedence is: unsupported future schema -> lineage conflict -> malformed intent -> catalog/config incompatibility -> exact already-contained commit match -> stale base revision -> safe roll-forward -> no-intent normal load. “Already-contained” requires exact lineage, prospective save revision/hash, and WorldCommitId agreement between intent and current profile; it is checked before generic stale-base logic. A valid intent whose schema/catalog no longer validates is never guessed or discarded; bootstrap stays blocked and preserves all files for explicit recovery/update. A `Prepared` intent with matching base either rolls forward the complete prospective snapshot or is rejected according to the approved protocol; it never repeats gameplay callbacks. A committed profile plus uncleared exact matching intent loads the profile and clears the redundant intent when possible.

### Failure, Continue, Retry, Abandon

- FailedPendingDecision does not persist provisional restoration or a resumable Board under the recommended MVP policy.
- Continue stays in the same runtime attempt and creates no progression commit.
- Retry/Abandon discard provisional state through STEP 11 and create no world/progression reward.
- If the app terminates during FailedPendingDecision, the next launch restores only the last successful durable snapshot; this is equivalent to abandon only if product approves that policy.

## Repository and save coordination contract

Replace the current void/synchronous ambiguity with status-bearing contracts after approval:

- load statuses distinguish Missing, LoadedCurrent, Migrated, Corrupt, UnsupportedFutureVersion, I/O failure, and recovery-used;
- save statuses distinguish Saved, NoChanges, Busy/coalesced, stale snapshot, serialization failure, I/O failure, and disposed;
- one coordinator serializes writes; newer dirty revisions supersede queued older revisions but never an in-flight atomic replace;
- write a complete temporary payload, flush/close, then atomically replace the primary file; optionally retain one last-known-good backup according to product policy;
- never overwrite an unsupported future-version file;
- primary, backup, and WAL records carry the same lineage and monotonic save revision. Recovery may select only the highest valid non-conflicting revision after replaying any valid pending record. An older backup cannot replace newer durable run reservations or applied commit IDs; ambiguous lineage/revision combinations block for recovery rather than roll back;
- repository results contain no gameplay mutation callbacks.

## Schema migration and validation

- `SchemaVersion` is positive and exact. Version 1 currently contains no progression payload and needs an explicit migration/default policy.
- Migrations are sequential pure functions (`vN -> vN+1`), deterministic, checked, and source-preserving on failure.
- Validate identities, uniqueness, known catalog membership, ranges, monotonic run-ID handoff, world current within capacity, canonical world milestones, and applied commit IDs.
- Persisted world milestone flags are unnecessary because milestones derive canonically from current/capacity. If stored for diagnostics, they must exactly match derivation.
- Unknown enum values, duplicate IDs, negative counters, capacity mismatch, impossible unlocks, or invalid settings reject the payload; they are never silently clamped unless product explicitly approves field-level repair.

## Lifecycle and failure behavior

- Initial bootstrap remains noninteractive until load/migration/default creation completes.
- Background/focus-loss requests a save after existing gameplay/presentation clocks are suspended. Multiple callbacks coalesce by revision.
- Quit/teardown requests a bounded synchronous final flush only if the platform contract supports it; no assumption is made that mobile quit callbacks always run.
- Reconciliation after I/O failure reads the coordinator’s authoritative pending revision; it never recaptures partially changing gameplay state.
- Corrupt/future data does not enable gameplay until the approved recovery/user-choice policy completes.
- Save exceptions are translated to typed failures and never escape Unity lifecycle callbacks.

## Determinism

- Stable IDs and collections serialize in defined ascending identity order.
- Equivalent snapshots produce byte-equivalent canonical payloads, excluding explicitly noncanonical diagnostic timestamps.
- Migration and validation use no clock, Unity object, global randomness, locale formatting, or unordered dictionary enumeration.
- StageRunId allocation is monotonic/checked and cannot reuse active, retired, or applied world commit IDs across restart.

## Presentation boundary

STEP 12 may display load/recovery/save-pending states and apply loaded settings through a new immutable settings snapshot. It cannot edit DTOs, decide corruption recovery, acknowledge a successful progression result before the required durability point, or bind concrete persistence paths. Concrete accessibility controls may request validated setting commands; Progression owns the saved value.

## Migration from current code

- Preserve `ISaveRepository` as a compatibility shell only long enough to migrate callers atomically; do not retain both void and result-bearing save authorities.
- `SaveData` v1 is a raw DTO, never the validated runtime model.
- Bootstrap gains one injected persistence coordinator and a load gate; lifecycle relay forwards facts without directly constructing SaveData.
- STEP 11 `WorldRestorationProgress` gains export/import through validated snapshots or a composition factory, not public setters.
- STEP 11 shared StageRunRegistry is hydrated from the same validated save lineage and shares persisted collision knowledge.
- No STEP 10 Board/Obstacle or STEP 12 presentation behavior changes are required.

## Edit Mode test strategy

- missing save -> approved defaults;
- exact current-version round trip and canonical ordering;
- v1 and every supported migration boundary;
- invalid version, future version, truncation, malformed fields, duplicates, unknown IDs/enums, capacity mismatch;
- successful stage prepare/bind/commit, stale plans, duplicate WorldCommitId, crash/retry between in-memory assignment and flush;
- no persistence on Miss, nonterminal answer, Failure detection, Continue, Retry, or Abandon;
- StageRunId uniqueness across reload and overflow boundary;
- settings defaults, bounds, immutable snapshots, repeated identical command;
- atomic replace failures at serialize/write/flush/replace and last-known-good recovery;
- no partial mutation on validation/migration/repository failure.

## Play Mode test strategy

- bootstrap load gate on new/existing/migrated/corrupt data;
- application pause/focus nesting coalesces saves without resuming input;
- background during resolution/presentation and success commit;
- teardown/relaunch after successful commit proves world/unlock/commit-ID idempotence;
- interruption during temporary write and backup recovery;
- FailedPendingDecision + background/relaunch follows approved interrupted-attempt policy;
- settings load before presentation and changes only at approved command boundaries;
- no event/listener leak across scene changes or bootstrap destruction.

Unity licensing/project-process failures remain environment verification debt and are never reported as test passes.

## Risks

- Split-brain world state if persistence independently mutates restoration rather than consuming a bound world plan.
- Lost successful progress if presentation/navigation proceeds before the approved durability point.
- Duplicate restoration/unlocks if commit IDs are not durable.
- Silent grants/losses from permissive repair of corrupt or content-mismatched data.
- Reused StageRunId after reinstall/migration/backup restore.
- Main-thread stalls or overlapping writes during background callbacks.
- Future schemas overwritten by an older client.

## Product decisions required

1. **Stage/world catalog:** stable IDs, exact MVP worlds/stages, ordering or unlock graph, and mapping of each stage to restoration world/capacity.
2. **Unlock rule:** whether first stage only is initially unlocked; whether every successful stage unlocks the next; world transition requirements; replay behavior; and whether stars/restoration can gate anything.
3. **Persisted stage facts:** completion only, best stars, best score, best moves, tutorial completion, clear streak, currency/rewards. Numeric star/reward rules are currently undefined; lowest risk is completion only.
4. **Settings schema:** exact saved settings, defaults, and ranges. At minimum decide audio, haptics, reduced motion, screen shake, flash reduction, and color/accessibility preferences.
5. **Interrupted attempt policy:** abandon and restore last successful snapshot (recommended) versus full mid-stage resume. Full resume would require persisting Board/Fever/target/random/transaction state and is a major scope expansion.
6. **Durability/navigation and crash protocol:** choose durable prospective-snapshot replacement or a write-ahead pending-commit record, define its roll-forward rules, and decide whether success/result navigation waits for durable confirmation. Define the user-visible response to repeated save failure. An asynchronous post-commit dirty flush cannot satisfy exactly-once and is not allowed.
7. **Corruption/anti-rollback policy:** reset all, recover last-known-good backup, field-level repair, or user choice. Define how primary/backup/WAL revisions and lineages are compared so an older backup cannot lose run reservations or commit IDs. Recommended: replay a valid WAL, choose the highest valid same-lineage revision, otherwise preserve files and require an explicit recovery decision.
8. **Unsupported future version:** hard block with update-required messaging (recommended) versus separate profile/reset; never overwrite automatically.
9. **Schema-v1 migration:** whether the current empty v1 means a clean install/default profile and whether any shipped builds exist whose data must be preserved.
10. **Save backend/security:** local JSON/binary/PlayerPrefs/cloud, filename/profile count, atomic replace/backup requirements, and whether integrity hashing/encryption is required. Lowest-risk MVP is one local canonical JSON profile plus atomic replace and one backup; encryption is not security against a device owner.
11. **Installation identity and durable allocation:** how save-lineage uniqueness is seeded; whether restored backups may share a lineage across devices; and the atomic reservation protocol required before publishing every initial/retry StageRunHandle. Define behavior when reservation persistence fails.

## Out of scope

- Analytics delivery (STEP 14), ads/continue authorization (STEP 15), purchasing/cloud account merge, social/live ops, seasons, daily rewards, broad economy, multiple profiles, cross-device conflict resolution, mid-stage resume unless explicitly approved, and any STEP 14 implementation.

## Lowest-risk MVP recommendation

Use one local profile with canonical JSON, atomic temp-file replacement, and one validated backup. Persist completion/unlock, world current/capacity/applied commit IDs, monotonic run-ID handoff, tutorial completion only if content IDs are supplied, and approved settings. Initially unlock the first catalog stage; success unlocks the next catalog stage; stars/currency/rewards do not exist. Abandon interrupted attempts and restore the last successful snapshot. Block future schemas; recover primary from backup, otherwise require explicit reset confirmation.

This recommendation is separate from approved gameplay. Implementing it without product approval would invent progression and recovery behavior.

## Disposition

**BLOCKED BY PRODUCT DECISION.** No STEP 13 production code or tests may be added until decisions 1–11 are approved or explicitly narrowed, the exact public contracts are finalized, and independent design review has no P0/P1. STEP 14 must not begin.
