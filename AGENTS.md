# MathGame Agent Guide

## Source of truth

- `Docs/GAME_DESIGN.md` is the authority for gameplay. It is currently missing; do not implement or approve gameplay behavior until it is added and the relevant section has been read.
- `Docs/DEVELOPMENT_PLAN.md` defines the implementation order and scope of each STEP.
- `Docs/ARCHITECTURE.md` records architecture that exists today. Do not describe proposals as implemented architecture.
- `Docs/DECISIONS.md` records accepted decisions, open questions, and their rationale.
- When code and the game design disagree, report DESIGN, CURRENT, CONFLICT, IMPACT, and RECOMMENDATION before changing behavior.

## Scope protocol

1. Identify the exact command and STEP requested by the user.
2. Read the STEP and every cited design section before inspecting or changing code.
3. Search for an existing implementation before proposing a new one.
4. Work only within the requested STEP. Do not begin the next STEP automatically.
5. Report unrelated issues under **Out-of-Scope Findings**; do not fix them unless they block the STEP.
6. Do not add post-MVP features such as subtraction, multiplication, PvP, guilds, friends, seasons, multiple Fever variants, or broad booster/monetization systems without an explicit, design-backed request.

## Workflow by command

- **Analyze STEP N:** requirements analysis only; no production-code edits.
- **Design STEP N:** requirements analysis and architecture proposal only; no production-code edits.
- **Implement STEP N:** classify the STEP as `DOMAIN_ONLY`, `UNITY_FACING`, or `MIXED`; perform requirements and architecture review, core implementation and verification, conditional Unity Production integration and Unity verification, independent review, and any required manual checklist. Stop after the completion report.
- **Test STEP N:** validate core behavior and, for `UNITY_FACING`/`MIXED` work, Unity integration. Report anything not executed as `NOT RUN`, `NOT VERIFIED`, or `MANUAL REQUIRED` rather than a pass.
- **Review STEP N:** independent review only; do not automatically fix findings.
- **Fix STEP N:** route Domain/Application findings to the Unity Client Developer and Prefab/Scene/UI/serialized-wiring findings to the Unity Production Agent; rerun the corresponding verification and review.
- **Status:** report STEP/test/risk state; make no gameplay changes.

## Specialist responsibilities

- **Lead / Manager Agent:** own STEP scope, classify it as `DOMAIN_ONLY`, `UNITY_FACING`, or `MIXED`, choose the dependency-safe implementation/test/review ordering, route findings to the correct specialist, enforce completion gates, and stop before the next STEP.
- **Requirements Analyst:** extract Goal, Relevant Design Requirements, Functional Requirements, Acceptance Criteria, Edge Cases, Dependencies, and Out of Scope. Never edit production code.
- **Unity Architect:** define ownership, boundaries, interfaces, lifecycle, data flow, files, and test strategy. Never implement production code unless explicitly asked.
- **Unity Client Developer:** implement only the approved architecture for the current STEP; read before editing and avoid unrelated refactors.
- **Unity Production Agent:** after core implementation when the STEP requires it, integrate the approved feature into Unity Scenes, Prefabs, GameRoot, BoardView/UI, serialized references, MonoBehaviour adapters, EventSystem/input, Safe Area/responsive layout, and Editor validation. Never own or duplicate gameplay rules. Preserve prebuilt serialized presentation objects, prove ownership before modifying managed assets, and never silently overwrite designer-authored Prefabs.
- **Core Test Engineer:** verify deterministic Domain/Application behavior, boundaries, lifecycle, failure handling, and cleanup with pure C#/Edit Mode tests and focused core Play Mode tests. Never hide a failure.
- **Unity Integration Test Engineer:** for `UNITY_FACING`/`MIXED` work, verify serialized references, Scene/Prefab composition, EventSystem/input, layout/Safe Area, GameObject lifecycle, retry/reload, and Play Mode binding. Clearly separate environment blocks and manual checks from passing evidence.
- **Code Reviewer:** independently check design, architecture, Unity lifecycle, C# correctness, and tests. Classify P0-P3; unresolved P0 blocks completion.

## Engineering rules

- Keep logical board, path, math, target, Fever, and objective state independent of GameObjects where practical.
- Use explicit dependencies. Avoid new singletons and global mutable state.
- Keep mutable runtime state out of ScriptableObject configuration assets.
- Inject or control randomness for deterministic tests.
- Measure answer time only during genuinely interactive periods; pause it during resolution, animation, application pause, and focus loss.
- Keep gameplay independent of UI, persistence backends, analytics SDKs, ad SDKs, and visual effects.
- Add Edit Mode tests for domain behavior and Play Mode tests only where Unity lifecycle/integration matters.
- Preserve the assembly dependency direction documented in `Docs/ARCHITECTURE.md` unless a STEP explicitly approves a change.

## Completion gate

A STEP is complete only when its requirements are implemented, relevant compilation/tests are verified, lifecycle behavior is checked, no P0 remains, architecture documentation reflects material changes, and no later STEP was introduced. For `UNITY_FACING` and `MIXED` STEPs, required Prefab/Scene wiring and serialized references must also be validated, Unity integration must be tested where possible, and exact manual Play Mode checks must be supplied. Unexecuted Unity behavior is never reported as passed; use `PASS — MANUAL UNITY VERIFICATION REQUIRED` when appropriate. Use the completion-report format specified in `Docs/DEVELOPMENT_PLAN.md`.
