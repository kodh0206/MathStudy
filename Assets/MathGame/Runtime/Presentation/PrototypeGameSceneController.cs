using System;
using System.Collections;
using System.Collections.Generic;
using MathGame.Answer;
using MathGame.App;
using MathGame.Board;
using MathGame.BoardGeneration;
using MathGame.BoardResolution;
using MathGame.Core.Random;
using MathGame.Fever;
using MathGame.ObstacleFlow;
using MathGame.Obstacles;
using MathGame.Presentation;
using MathGame.Restoration;
using MathGame.Restoration.Contracts;
using MathGame.Stage;
using MathGame.StageSession;
using MathGame.SurvivalRun;
using MathGame.Targets;
using UnityEngine;
using UnityEngine.InputSystem;
using DomainBoard = MathGame.Board.Board;
using Session = MathGame.StageSession.StageSession;

namespace MathGame.Presentation.Unity
{
    [DefaultExecutionOrder(100)]
    public sealed class PrototypeGameSceneController : MonoBehaviour
    {
        [SerializeField] GamePresentationHost presentationHost;
        MathGameBootstrap bootstrap;
        StageController stage;
        Session session;
        FeverController fever;
        ObstacleResolutionCoordinator obstacleFlow;
        ObstacleGameplayPresentationPort commands;
        GameplayPresentationCoordinator presentation;
        GameplayPresentationRoot boardView;
        TargetRecoveryCoordinator targets;
        TargetRecoveryConfig targetConfig;
        TargetHistory history;
        TargetNumber target;
        RefillValueRange refill;
        WorldRestorationProgress world;
        LineRenderer selectionLine;
        PrototypeUILayout uiLayout;
        StageClearPopupView stageClearPopup;
        RunResultPopupView runResultPopup;
        SurvivalRunSession run;
        readonly List<BoardPosition> selected = new List<BoardPosition>();
        long commandId = 1;
        long presentationId = 1;
        float targetStarted;
        bool pointerDown;
        bool resolvingEnd;
        bool targetRecoveryPending;
        bool restarting;
        string status = "Starting prototype...";

        IEnumerator Start()
        {
            for (var i = 0; i < 120; i++)
            {
                bootstrap = FindFirstObjectByType<MathGameBootstrap>();
                if (bootstrap != null && bootstrap.StageController?.State == StageState.Ready) break;
                yield return null;
            }
            if (bootstrap == null || bootstrap.StageController?.State != StageState.Ready)
            {
                status = "Bootstrap did not reach Ready.";
                yield break;
            }
            Compose();
        }

        void Compose()
        {
            if (presentationHost == null || !presentationHost.HasValidContext)
            {
                status = "Serialized GamePresentationHost context is missing or incomplete.";
                return;
            }

            stage = bootstrap.StageController;
            var random = new SystemRandomSource(13012);
            var generated = new BoardGenerator(random).Generate(new BoardGenerationConfig(BoardTopology.CreateRectangular(5, 5), 1, 4));
            if (!generated.Succeeded) { status = "Board generation failed: " + generated.Failure; return; }

            var layout = new ObstacleLayout(new[]
            {
                ObstacleLayoutEntry.Dust(new BoardPosition(1, 1), new ObstacleId(1)),
                ObstacleLayoutEntry.Box(new BoardPosition(2, 2), new ObstacleId(2))
            });
            var built = new ObstacleBoardBuilder().Build(generated.Board, layout);
            if (!built.Succeeded) { status = "Obstacle setup failed: " + built.Status; return; }

            var definition = new StageDefinition(new StageDefinitionId(1), 1,
                Array.Empty<StageObjectiveDefinition>(),
                new ScoreRewardConfig(10, 25, 15, 5, new[]
                {
                    new ConnectionLengthScoreRule(3, 3), new ConnectionLengthScoreRule(4, 6), new ConnectionLengthScoreRule(5, 10)
                }), null, StageSessionMode.ContinuousRun);
            if (Session.TryCreate(definition, new StageRunId(1), out session) != StageSessionCreateStatus.Succeeded)
            { status = "StageSession creation failed."; return; }

            run = new SurvivalRunSession(SurvivalRunConfig.TemporaryPrototype);
            FeverController.TryCreate(new FeverConfig(50, 8), stage, session, bootstrap.TimeProvider, out fever);
            targets = new TargetRecoveryCoordinator(random);
            targetConfig = new TargetRecoveryConfig(new TargetSearchConfig(5, 10, 2, 4, 250000), new TargetSelectionPolicy(1), 5);
            history = new TargetHistory(null, 0);
            var initialTarget = targets.SelectNextTarget(built.Board, history, targetConfig);
            if (!initialTarget.Succeeded) { status = "Initial target search failed: " + initialTarget.Status; return; }
            history = initialTarget.UpdatedHistory;
            target = initialTarget.Solution.Target;
            refill = new RefillValueRange(1, 4);
            obstacleFlow = new ObstacleResolutionCoordinator(new ObstacleBoardResolver(random), stage, session, fever,
                targets, initialTarget.Board, generated.NextBlockIdValue, null);
            commands = new ObstacleGameplayPresentationPort(obstacleFlow, stage, fever, session,
                new AnswerValidator(AnswerTimingThresholds.Prototype), null);

            boardView = presentationHost.BoardView;
            if (boardView == null || !boardView.transform.IsChildOf(presentationHost.CreateContext().BoardSlot))
            {
                status = "Serialized BoardView must exist below GameplayRoot/BoardSlot before Play Mode.";
                return;
            }
            boardView.PlaybackCompleted += PlaybackCompleted;
            boardView.BeginSession();
            // Prototype board visuals are authored in the scene. Reconcile them immediately
            // instead of holding input while staging per-delta animations.
            boardView.Configure(new PresentationTiming(0, 0, 0, 0, 0));
            boardView.ConfigureRegistry(presentationHost.Registry);
            var context = presentationHost.CreateContext();
            stageClearPopup = context.OverlayRoot.GetComponentInChildren<StageClearPopupView>(true);
            if (stageClearPopup == null)
            {
                status = "Serialized StageClearPopup is missing from OverlaySlot.";
                return;
            }
            stageClearPopup.Bind(Restart, NextStageRequested, false);
            stageClearPopup.Hide();
            runResultPopup = context.OverlayRoot.GetComponentInChildren<RunResultPopupView>(true);
            if (runResultPopup == null)
            {
                status = "Serialized RunResultPopup is missing from OverlaySlot.";
                return;
            }
            runResultPopup.Bind(Restart);
            runResultPopup.Hide();
            boardView.ConfigureSlots(boardView.transform.Find("CellRoot"), boardView.transform.Find("BlockRoot"), context.EffectSlot);
            presentation = new GameplayPresentationCoordinator(commands, boardView);
            boardView.ApplyFinalState(SnapshotPlan(PresentationAcknowledgementKind.None, 0));

            var lineObject = new GameObject("PrototypeSelectionLine");
            selectionLine = lineObject.AddComponent<LineRenderer>();
            selectionLine.widthMultiplier = .09f;
            selectionLine.material = new Material(Shader.Find("Sprites/Default"));
            selectionLine.startColor = selectionLine.endColor = Color.cyan;

            var camera = Camera.main;
            if (camera != null)
            {
                camera.orthographic = true;
                camera.backgroundColor = new Color(.07f, .09f, .13f);
            }
            uiLayout = presentationHost.UILayout;
            uiLayout.Build(camera, boardView, Continue, TogglePause, Abandon, RetryTarget, presentationHost.Registry);
            uiLayout.SetRunMode(true);
            stage.BeginTargetPresentation();
            stage.EnablePlayerInput();
            targetStarted = Time.unscaledTime;
            status = "Drag across orthogonally adjacent cells, then release.";
        }

        void Update()
        {
            if (commands == null) return;
            // Expiry is sampled before input. All live Stage phases drain; only an actual
            // lifecycle pause/interruption stops the clock.
            if (run != null && run.Status == SurvivalRunStatus.Active &&
                run.Tick(Time.unscaledDeltaTime, stage.State == StageState.Paused))
            {
                pointerDown = false;
                selected.Clear();
                UpdateLine();
                stage.EndRun();
                status = "RUN OVER";
                runResultPopup.Show(run.Result);
            }
            var currentSnapshot=session.CreateSnapshot();
            var currentCombo = fever.SessionSnapshot?.CurrentCombo ?? 0;
            run?.RecordStatistics(currentSnapshot.Score, currentCombo);
            uiLayout?.RefreshRun(currentSnapshot, target.Value, fever.Gauge, 50, status,
                run?.RemainingTime ?? 0, run?.DifficultyTier ?? 0, currentCombo,
                run?.Status == SurvivalRunStatus.Ended, targetRecoveryPending);
            /*uiLayout?.Refresh(currentSnapshot,target.Value,fever.Gauge,50,status,
                stage.State==StageState.FailedPendingDecision,targetRecoveryPending,
                stage.State is StageState.Success or StageState.Failure);*/
            if (run?.Status == SurvivalRunStatus.Ended) return;
            if (stage.State == StageState.EnteringFever)
            {
                if (fever.CompleteEntry() == FeverControllerCommandResult.Succeeded)
                { status = "FEVER: answers cost no moves for 8 interactive seconds."; targetStarted = Time.unscaledTime; }
            }
            if (fever.State == FeverState.Active)
            {
                var tick = fever.Tick();
                if (tick == FeverControllerTickResult.EndingBegan && !resolvingEnd) ResolveFeverEnd();
            }
            if (!stage.AcceptsPlayerInput)
            {
                if(Mouse.current?.leftButton.wasPressedThisFrame==true||Touchscreen.current?.primaryTouch.press.wasPressedThisFrame==true)
                    status="Input locked while stage is "+stage.State+".";
                return;
            }
            HandlePointer();
        }

        void HandlePointer()
        {
            if (!TryReadPointer(out var screenPosition, out var down, out var held, out var up)) return;
            if (down)
            {
                selected.Clear();
                uiLayout?.SetSelectionSum(0,0);
                if (TryPointerCell(screenPosition, out var cell))
                {
                    var result = commands.BeginPath(new PathCommandRequest(new PresentationCommandId(commandId++), commands.CurrentToken, cell));
                    AcceptPathResult(result);
                    pointerDown = result?.Status == PresentationCommandStatus.Accepted;
                }
            }
            else if (held && pointerDown && TryPointerCell(screenPosition, out var cell) && (selected.Count == 0 || selected[selected.Count - 1] != cell))
            {
                AcceptPathResult(commands.ExtendPath(new PathCommandRequest(new PresentationCommandId(commandId++), commands.CurrentToken, cell)));
            }
            if (up && pointerDown)
            {
                pointerDown = false;
                // Apply the prospective tier only when the selected value is a correct answer.
                // The domain validator remains authoritative; this mirrors its sum fact solely
                // so target recovery for the threshold-crossing commit receives the new range.
                var requestTargetConfig = targetConfig;
                if (SelectedSum() == target.Value)
                {
                    var prospectiveRange = run.ProspectiveCorrectTargetRange;
                    requestTargetConfig = new TargetRecoveryConfig(new TargetSearchConfig(prospectiveRange.Minimum,
                        prospectiveRange.Maximum, 2, 4, 250000), new TargetSelectionPolicy(1), 5);
                }
                var request = new ReleasePathRequest(new PresentationCommandId(commandId++), commands.CurrentToken, target,
                    Math.Max(0, Time.unscaledTime - targetStarted), session.CreateSnapshot().NextExpectedAttemptId,
                    refill, history, requestTargetConfig, stage.ResolutionOrigin == AnswerResolutionOrigin.Fever);
                HandleRelease(commands.ReleasePath(request));
                selected.Clear();
                uiLayout?.SetSelectionSum(0,0);
                UpdateLine();
            }
        }

        void AcceptPathResult(GameplayCommandResult result)
        {
            if (result?.Status != PresentationCommandStatus.Accepted || result.Path == null) return;
            var previousCount=selected.Count;
            selected.Clear();
            foreach (var entry in result.Path.Entries) selected.Add(entry.Position);
            long sum=0;
            foreach(var position in selected)
                if(obstacleFlow.CurrentBoard.TryGetCell(position,out var cell)==CellLookupResult.Succeeded&&cell.Block.HasValue)
                    sum+=cell.Block.Value.Value;
            uiLayout?.SetSelectionSum(sum,selected.Count);
            if(selected.Count>previousCount)boardView?.PlaySelectionCue();
            UpdateLine();
        }

        long SelectedSum()
        {
            long sum = 0;
            foreach (var position in selected)
                if (obstacleFlow.CurrentBoard.TryGetCell(position, out var cell) == CellLookupResult.Succeeded && cell.Block.HasValue)
                    sum += cell.Block.Value.Value;
            return sum;
        }

        void HandleRelease(GameplayCommandResult result)
        {
            if (result == null || result.Status != PresentationCommandStatus.Accepted)
            { status = "Submission rejected: " + result?.Status; return; }
            if (!result.Answer.IsCorrect)
            {
                status = "MISS — no move spent.";
                PreparePlan(ObstaclePresentationPlanBuilder.ForMiss(Envelope(PresentationAcknowledgementKind.Answer,
                    session.CreateSnapshot().NextExpectedAttemptId.Value - 1), Settings()));
                return;
            }

            if (result.AnswerFlow?.StageResult != null && result.AnswerFlow.FeverResult == null)
                fever.ApplyNormalAttempt(result.AnswerFlow.StageResult);

            if (result.AnswerFlow?.AttemptCommitted == true)
            {
                var prepared = run.PrepareCorrectCycle(result.AnswerFlow.GameplayToken.SourceId, result.Answer.Grade, out var plan);
                if (prepared != CorrectCyclePrepareStatus.Prepared ||
                    run.CommitCorrectCycle(plan) != CorrectCycleCommitStatus.Committed)
                    throw new InvalidOperationException("Committed answer could not be correlated to Survival Time recovery.");
                run.RecordStatistics(session.CreateSnapshot().Score,
                    fever.SessionSnapshot?.CurrentCombo ?? 0);
                var committedRange = run.TargetRange;
                targetConfig = new TargetRecoveryConfig(new TargetSearchConfig(committedRange.Minimum,
                    committedRange.Maximum, 2, 4, 250000), new TargetSelectionPolicy(1), 5);
            }
            if (result.AnswerFlow?.History != null) history = result.AnswerFlow.History;
            if (result.AnswerFlow?.SelectedTarget != null) target = result.AnswerFlow.SelectedTarget.Target;
            boardView.ApplyFinalState(SnapshotPlan(PresentationAcknowledgementKind.None, 0));
            status = result.Answer.Grade + " — board resolved.";

            if (result.AnswerFlow?.Status == ObstacleAnswerFlowStatus.StageTerminal)
            {
                status = session.Status == StageSessionStatus.Success ? "SUCCESS" : "FAILED";
                PreparePlan(ObstaclePresentationPlanBuilder.ForTerminal(
                    Envelope(PresentationAcknowledgementKind.Terminal,result.AnswerFlow.GameplayToken.SourceId),Settings(),
                    session.Status==StageSessionStatus.Success));
                return;
            }
            if (result.AnswerFlow?.Status == ObstacleAnswerFlowStatus.FailedPendingDecision)
            {
                PreparePlan(new PresentationPlan(Envelope(PresentationAcknowledgementKind.FailedDecision,
                    result.AnswerFlow.GameplayToken.SourceId), Settings()));
                return;
            }
            if (!result.AnswerFlow.IsInputReady)
            { targetRecoveryPending = true; status = "Target recovery pending. Use Retry Target."; return; }
            targetRecoveryPending = false;
            PreparePlan(ObstaclePresentationPlanBuilder.ForAnswer(Envelope(PresentationAcknowledgementKind.Answer,
                result.AnswerFlow.GameplayToken.SourceId), Settings(), result.AnswerFlow));
        }

        void PlaybackCompleted()
        {
            var kind = presentation.ActiveEnvelope?.AcknowledgementKind ?? PresentationAcknowledgementKind.None;
            var ack = presentation.CompletePlayback();
            if (ack != PresentationAcknowledgementStatus.Accepted) { status = "Presentation acknowledgement: " + ack; return; }
            if (kind == PresentationAcknowledgementKind.Terminal && stage.State == StageState.Success)
            {
                stageClearPopup?.Show();
                status = "Stage clear. The next prototype stage is not available yet.";
                return;
            }
            if ((kind == PresentationAcknowledgementKind.Answer || kind == PresentationAcknowledgementKind.FeverEnd) &&
                stage.State == StageState.PresentingTarget)
                PreparePlan(new PresentationPlan(Envelope(PresentationAcknowledgementKind.TargetReady,
                    commands.CurrentToken.SourceId), Settings()));
            else if (stage.AcceptsPlayerInput) targetStarted = Time.unscaledTime;
        }

        void ResolveFeverEnd()
        {
            resolvingEnd = true;
            var result = commands.ResolveFeverEnd(new FeverEndCommandRequest(new PresentationCommandId(commandId++),
                commands.CurrentToken, refill, history, targetConfig));
            resolvingEnd = false;
            if (result?.Status != PresentationCommandStatus.Accepted) { status = "Fever end failed: " + result?.Status; return; }
            if (result.EndFlow?.History != null) history = result.EndFlow.History;
            if (result.EndFlow?.SelectedTarget != null) target = result.EndFlow.SelectedTarget.Target;
            boardView.ApplyFinalState(SnapshotPlan(PresentationAcknowledgementKind.None, 0));
            if(result.EndFlow.Status==ObstacleEndFlowStatus.StageTerminal)
                PreparePlan(ObstaclePresentationPlanBuilder.ForTerminal(Envelope(PresentationAcknowledgementKind.Terminal,
                    result.EndFlow.GameplayToken.SourceId),Settings(),true));
            else
                PreparePlan(new PresentationPlan(Envelope(PresentationAcknowledgementKind.FeverEnd,
                    result.EndFlow.GameplayToken.SourceId), Settings()));
        }

        void RetryTarget()
        {
            if(stage.State==StageState.RecoveringBoard)
            {
                var continued=obstacleFlow.RecoverAfterContinue(history,targetConfig);
                if(!continued.IsInputReady){status="Continue target retry failed: "+continued.Status;return;}
                history=continued.History;target=continued.SelectedTarget.Target;targetRecoveryPending=false;
                boardView.ApplyFinalState(SnapshotPlan(PresentationAcknowledgementKind.None,0));
                stage.BeginTargetPresentation();stage.EnablePlayerInput();targetStarted=Time.unscaledTime;
                return;
            }
            var result=commands.RetryTargetRecovery(new TargetRetryRequest(new PresentationCommandId(commandId++),commands.CurrentToken,history,targetConfig));
            if(result?.Status!=PresentationCommandStatus.Accepted||result.AnswerFlow?.IsInputReady!=true)
            {status="Target retry failed: "+result?.Status;return;}
            history=result.AnswerFlow.History;target=result.AnswerFlow.SelectedTarget.Target;targetRecoveryPending=false;
            PreparePlan(ObstaclePresentationPlanBuilder.ForTargetRetry(Envelope(PresentationAcknowledgementKind.TargetReady,
                result.AnswerFlow.GameplayToken.SourceId),Settings(),result.AnswerFlow));
        }

        void PreparePlan(IPresentationPlan plan)
        {
            var result = presentation.Prepare(plan);
            if (result != PresentationCommandStatus.Accepted) status = "Presentation prepare failed: " + result;
        }

        PresentationEnvelope Envelope(PresentationAcknowledgementKind kind, long source) =>
            new PresentationEnvelope(new PresentationSequenceId(presentationId++), obstacleFlow.CaptureGameplayState(),
                session.CreateSnapshot(), fever.CapturePresentationSnapshot(), kind, Math.Max(0, source));

        PresentationPlan SnapshotPlan(PresentationAcknowledgementKind kind, long source) =>
            new PresentationPlan(new PresentationEnvelope(new PresentationSequenceId(Math.Max(1, presentationId)),
                obstacleFlow.CaptureGameplayState(), session.CreateSnapshot(), fever.CapturePresentationSnapshot(), kind, source), Settings());

        static PresentationSettings Settings() => new PresentationSettings(false, true, true);

        static bool TryReadPointer(out Vector2 position, out bool down, out bool held, out bool up)
        {
            if (Touchscreen.current != null)
            {
                var touch = Touchscreen.current.primaryTouch;
                position = touch.position.ReadValue();
                down = touch.press.wasPressedThisFrame;
                held = touch.press.isPressed;
                up = touch.press.wasReleasedThisFrame;
                if (down || held || up) return true;
            }

            if (Mouse.current != null)
            {
                position = Mouse.current.position.ReadValue();
                down = Mouse.current.leftButton.wasPressedThisFrame;
                held = Mouse.current.leftButton.isPressed;
                up = Mouse.current.leftButton.wasReleasedThisFrame;
                return down || held || up;
            }

            position = default;
            down = held = up = false;
            return false;
        }

        bool TryPointerCell(Vector2 screenPosition, out BoardPosition position)
        {
            position = default;
            var camera = Camera.main;
            if (camera == null || boardView == null || !boardView.TryScreenPointToCell(camera, screenPosition, out var candidate)) return false;
            if (!obstacleFlow.CurrentBoard.IsActive(candidate)) return false;
            obstacleFlow.CurrentBoard.TryGetCell(candidate, out var cell);
            if (!cell.IsSelectable) return false;
            position = candidate;
            return true;
        }

        void UpdateLine()
        {
            boardView?.SetSelectedPositions(selected);
            if (selectionLine == null) return;
            selectionLine.positionCount = selected.Count;
            for (var i = 0; i < selected.Count; i++) selectionLine.SetPosition(i, boardView.GetCellWorldPosition(selected[i]));
        }

        void Abandon()
        {
            if(stage.State!=StageState.FailedPendingDecision)return;
            session.TryDiscardFailedAttempt();stage.Fail();status="Abandoned";
        }

        void Continue()
        {
            if (!session.TryContinueFailedAttempt(5) || stage.ResumeFromContinue() != TransitionResult.Succeeded)
            { status = "Continue rejected."; return; }
            var recovered = obstacleFlow.RecoverAfterContinue(history,targetConfig);
            if (!recovered.IsInputReady) { status = "Continue target recovery failed: "+recovered.Status;targetRecoveryPending=true; return; }
            history = recovered.History; target = recovered.SelectedTarget.Target;
            boardView.ApplyFinalState(SnapshotPlan(PresentationAcknowledgementKind.None,0));
            stage.BeginTargetPresentation(); stage.EnablePlayerInput(); targetStarted = Time.unscaledTime;
            status = "Continued with +5 moves; restoration preserved.";
        }

        void Restart()
        {
            stageClearPopup?.Hide();
            runResultPopup?.Hide();
            if(!restarting)StartCoroutine(RestartCleanly());
        }

        void TogglePause()
        {
            if (run == null || run.Status == SurvivalRunStatus.Ended) return;
            if (stage.State == StageState.Paused)
            {
                if (stage.Resume(PauseReason.User) != TransitionResult.Succeeded)
                {
                    uiLayout?.SetPauseState(true);
                    status = "Run remains paused by another interruption.";
                    return;
                }
                uiLayout?.SetPauseState(false);
                status = "Run resumed.";
            }
            else
            {
                if (stage.Pause(PauseReason.User) != TransitionResult.Succeeded)
                {
                    status = "Pause request rejected.";
                    return;
                }
                uiLayout?.SetPauseState(true);
                status = "Run paused.";
            }
        }

        void NextStageRequested()
        {
            status = "The next prototype stage is not available yet.";
        }

        IEnumerator RestartCleanly()
        {
            restarting = true;
            if (boardView != null) boardView.PlaybackCompleted -= PlaybackCompleted;
            presentation?.Dispose();
            fever?.Dispose();
            selected.Clear();
            pointerDown = false;
            targetRecoveryPending = false;
            resolvingEnd = false;
            if (selectionLine != null) Destroy(selectionLine.gameObject);
            yield return null;
            if (bootstrap == null || !bootstrap.RestartStage())
            {
                status = "Restart bootstrap did not reach Ready.";
                restarting = false;
                yield break;
            }

            commandId = 1;
            presentationId = 1;
            Compose();
            restarting = false;
        }

        void OnDestroy()
        {
            if (boardView != null) boardView.PlaybackCompleted -= PlaybackCompleted;
            presentation?.Dispose(); fever?.Dispose();
        }

#if UNITY_EDITOR
        public void ConfigurePresentationHost(GamePresentationHost host)=>presentationHost=host;
#endif
    }
}
