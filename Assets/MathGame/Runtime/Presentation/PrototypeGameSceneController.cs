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
        readonly List<BoardPosition> selected = new List<BoardPosition>();
        long commandId = 1;
        long presentationId = 1;
        float targetStarted;
        bool pointerDown;
        bool resolvingEnd;
        bool targetRecoveryPending;
        bool restarting;
        string status = "Starting prototype...";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "GameScene") return;
            if (FindFirstObjectByType<PrototypeGameSceneController>() != null) return;
            new GameObject("PrototypeGameSceneComposition").AddComponent<PrototypeGameSceneController>();
        }

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

            var definition = new StageDefinition(new StageDefinitionId(1), 6,
                new[]
                {
                    new StageObjectiveDefinition(StageObjectiveKind.RemoveNumberBlocks, 12, default, 0),
                    new StageObjectiveDefinition(StageObjectiveKind.RemoveObstacle, 1, default, 0, ObstacleKind.Dust)
                },
                new ScoreRewardConfig(10, 25, 15, 5, new[]
                {
                    new ConnectionLengthScoreRule(3, 3), new ConnectionLengthScoreRule(4, 6), new ConnectionLengthScoreRule(5, 10)
                }), new StageRestorationConfig(new WorldRestorationId(1), 100));
            if (Session.TryCreate(definition, new StageRunId(1), out session) != StageSessionCreateStatus.Succeeded)
            { status = "StageSession creation failed."; return; }

            world = new WorldRestorationProgress(new WorldRestorationId(1), 100);
            var restoration = new RestorationTransactionCoordinator(session, world);
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
                targets, initialTarget.Board, generated.NextBlockIdValue, restoration);
            commands = new ObstacleGameplayPresentationPort(obstacleFlow, stage, fever, session,
                new AnswerValidator(AnswerTimingThresholds.Prototype), null);

            var rootObject = new GameObject("PrototypeBoardView");
            rootObject.AddComponent<PlaceholderPresentationFeedback>();
            boardView = rootObject.AddComponent<GameplayPresentationRoot>();
            boardView.PlaybackCompleted += PlaybackCompleted;
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
                camera.orthographicSize = 4.2f;
                camera.transform.position = new Vector3(2, 2.4f, -10);
                camera.backgroundColor = new Color(.07f, .09f, .13f);
            }
            stage.BeginTargetPresentation();
            stage.EnablePlayerInput();
            targetStarted = Time.unscaledTime;
            status = "Drag across orthogonally adjacent cells, then release.";
        }

        void Update()
        {
            if (commands == null) return;
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
            if (!stage.AcceptsPlayerInput) return;
            HandlePointer();
        }

        void HandlePointer()
        {
            if (!TryReadPointer(out var screenPosition, out var down, out var held, out var up)) return;
            if (down)
            {
                selected.Clear();
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
                var request = new ReleasePathRequest(new PresentationCommandId(commandId++), commands.CurrentToken, target,
                    Math.Max(0, Time.unscaledTime - targetStarted), session.CreateSnapshot().NextExpectedAttemptId,
                    refill, history, targetConfig, stage.ResolutionOrigin == AnswerResolutionOrigin.Fever);
                HandleRelease(commands.ReleasePath(request));
                selected.Clear();
                UpdateLine();
            }
        }

        void AcceptPathResult(GameplayCommandResult result)
        {
            if (result?.Status != PresentationCommandStatus.Accepted || result.Path == null) return;
            selected.Clear();
            foreach (var entry in result.Path.Entries) selected.Add(entry.Position);
            UpdateLine();
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
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            {
                var touch = Touchscreen.current.primaryTouch;
                position = touch.position.ReadValue();
                down = touch.press.wasPressedThisFrame;
                held = touch.press.isPressed;
                up = touch.press.wasReleasedThisFrame;
                return true;
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
            if (camera == null) return false;
            var worldPoint = camera.ScreenToWorldPoint(screenPosition);
            var column = Mathf.RoundToInt(worldPoint.x);
            var row = Mathf.RoundToInt(worldPoint.y);
            if (Mathf.Abs(worldPoint.x - column) > .45f || Mathf.Abs(worldPoint.y - row) > .45f) return false;
            var candidate = new BoardPosition(column, row);
            if (!obstacleFlow.CurrentBoard.IsActive(candidate)) return false;
            obstacleFlow.CurrentBoard.TryGetCell(candidate, out var cell);
            if (!cell.IsSelectable) return false;
            position = candidate;
            return true;
        }

        void UpdateLine()
        {
            if (selectionLine == null) return;
            selectionLine.positionCount = selected.Count;
            for (var i = 0; i < selected.Count; i++) selectionLine.SetPosition(i, new Vector3(selected[i].Column, selected[i].Row, -.5f));
        }

        void OnGUI()
        {
            if (session == null) { GUI.Label(new Rect(15, 15, 800, 30), status); return; }
            var snapshot = session.CreateSnapshot();
            GUI.Box(new Rect(10, 10, 430, 150), "MathGame Deterministic Prototype");
            GUI.Label(new Rect(25, 38, 400, 24), "Target: " + target.Value + "   Moves: " + snapshot.RemainingMoves + "   Score: " + snapshot.Score);
            GUI.Label(new Rect(25, 62, 400, 24), "Restoration: " + snapshot.ProvisionalRestoration + "/" + snapshot.StageRestorationCapacity + "   Fever: " + fever.Gauge + "/50");
            GUI.Label(new Rect(25, 86, 400, 24), "Stage: " + stage.State + "   " + status);
            GUI.Label(new Rect(25, 110, 400, 24), "Objectives: remove 12 numbers and destroy Dust");

            if (stage.State == StageState.FailedPendingDecision)
            {
                if (GUI.Button(new Rect(20, 175, 120, 40), "Continue +5")) Continue();
                if (GUI.Button(new Rect(150, 175, 120, 40), "Retry")) Restart();
                if (GUI.Button(new Rect(280, 175, 120, 40), "Abandon")) { session.TryDiscardFailedAttempt(); stage.Fail(); status = "Abandoned"; }
            }
            else if(targetRecoveryPending&&stage.State is StageState.ResolvingAnswer or StageState.RecoveringBoard)
            {
                if(GUI.Button(new Rect(20,175,180,40),"Retry Target Recovery"))RetryTarget();
            }
            else if (stage.State == StageState.Success || stage.State == StageState.Failure)
            {
                if (GUI.Button(new Rect(20, 175, 120, 40), "Retry Stage")) Restart();
            }
            if (GUI.Button(new Rect(Screen.width - 155, 15, 140, 35), "Restart Prototype")) Restart();
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
            if(!restarting)StartCoroutine(RestartCleanly());
        }

        IEnumerator RestartCleanly()
        {
            restarting=true;
            if (boardView != null) boardView.PlaybackCompleted -= PlaybackCompleted;
            presentation?.Dispose(); fever?.Dispose();
            if(bootstrap!=null)Destroy(bootstrap.gameObject);
            yield return null;
            UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
        }

        void OnDestroy()
        {
            if (boardView != null) boardView.PlaybackCompleted -= PlaybackCompleted;
            presentation?.Dispose(); fever?.Dispose();
        }
    }
}
