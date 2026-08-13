using System;
using System.Collections;
using System.Collections.Generic;
using MathGame.Board;
using UnityEngine;

namespace MathGame.Presentation.Unity
{
    public sealed class GameplayPresentationRoot : MonoBehaviour, IPresentationViewPort
    {
        readonly Dictionary<BoardPosition, GameObject> cells = new Dictionary<BoardPosition, GameObject>();
        readonly Dictionary<BlockId, GameObject> blocks = new Dictionary<BlockId, GameObject>();
        readonly Dictionary<ObstacleId, GameObject> obstacles = new Dictionary<ObstacleId, GameObject>();
        readonly List<PresentationEvent> appliedEvents = new List<PresentationEvent>();
        Coroutine playback;
        bool paused;
        GameplayOverlayView overlay;
        IPresentationFeedbackPort feedback;
        MathGame.Board.Board displayedBoard;
        LogicalBoardTouchAdapter touch;

        public event Action PlaybackCompleted;
        public IReadOnlyList<PresentationEvent> AppliedEvents => appliedEvents.AsReadOnly();
        public int BlockViewCount => blocks.Count;
        public int ObstacleViewCount => obstacles.Count;
        public PresentationTiming Timing { get; private set; } = PresentationTiming.Approved;

        public void Configure(PresentationTiming timing) => Timing = timing ?? throw new ArgumentNullException(nameof(timing));

        void Awake()
        {
            overlay = GetComponent<GameplayOverlayView>();
            if (overlay == null) overlay = gameObject.AddComponent<GameplayOverlayView>();
            feedback = GetComponent<IPresentationFeedbackPort>();
            touch = GetComponentInChildren<LogicalBoardTouchAdapter>();
            if (touch != null) touch.PathChanged += PathChanged;
        }

        public void Prepare(IPresentationPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (playback != null) StopCoroutine(playback);
            playback = StartCoroutine(Play(plan));
        }

        public void CancelPlayback()
        {
            if (playback != null) StopCoroutine(playback);
            playback = null;
        }

        public void ApplyFinalState(IPresentationPlan plan)
        {
            if (plan?.Envelope?.Gameplay?.Board == null) throw new ArgumentNullException(nameof(plan));
            var board = plan.Envelope.Gameplay.Board;
            displayedBoard = board;
            var seen = new HashSet<BoardPosition>();
            foreach (var position in board.EnumerateActivePositions())
            {
                seen.Add(position);
                if (!cells.TryGetValue(position, out var cell) || cell == null)
                {
                    cell = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    cell.name = "Cell_" + position.Column + "_" + position.Row;
                    cell.transform.SetParent(transform, false);
                    cells[position] = cell;
                }
                cell.transform.localPosition = new Vector3(position.Column, position.Row, 0);
                board.TryGetCell(position, out var snapshot);
                cell.transform.localScale = snapshot.HasBox ? new Vector3(.85f, .85f, 1) : Vector3.one;
                // Shape/scale is an additional non-colour indicator for Box/unavailable state.
                if (snapshot.Block.HasValue)
                {
                    var block = snapshot.Block.Value;
                    if (!blocks.TryGetValue(block.Id, out var blockView) || blockView == null)
                    {
                        blockView = new GameObject();
                        var label = blockView.AddComponent<TextMesh>();
                        label.anchor = TextAnchor.MiddleCenter;
                        label.alignment = TextAlignment.Center;
                        label.characterSize = .35f;
                        label.fontSize = 64;
                        label.color = Color.white;
                        blocks[block.Id] = blockView;
                    }
                    blockView.name = "Block_" + block.Id.Value + "_Value_" + block.Value;
                    var numberLabel = blockView.GetComponent<TextMesh>();
                    if (numberLabel != null) numberLabel.text = block.Value.ToString();
                    blockView.transform.SetParent(cell.transform, false);
                    blockView.transform.localPosition = Vector3.back * .01f;
                }
                if (snapshot.HasDust)
                {
                    var dust = snapshot.Dust.Value;
                    if (!obstacles.TryGetValue(dust.Id,out var dustView)||dustView==null)
                    {
                        dustView=new GameObject();
                        var label=dustView.AddComponent<TextMesh>();
                        label.anchor=TextAnchor.UpperLeft;label.characterSize=.18f;label.fontSize=48;label.color=new Color(.75f,.65f,.45f);
                        obstacles[dust.Id]=dustView;
                    }
                    dustView.name="Dust_"+dust.Id.Value+"_HP_"+dust.CurrentHitPoints+"_DamagedIndicator";
                    dustView.transform.SetParent(cell.transform,false);
                    dustView.transform.localPosition=new Vector3(-.38f,.4f,-.03f);
                    dustView.GetComponent<TextMesh>().text="D";
                }
                if (snapshot.HasBox)
                {
                    var box=snapshot.Box.Value;
                    if(!obstacles.TryGetValue(box.Id,out var boxView)||boxView==null)
                    {
                        boxView=new GameObject();
                        var label=boxView.AddComponent<TextMesh>();
                        label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.characterSize=.3f;label.fontSize=56;label.color=new Color(1f,.55f,.2f);
                        obstacles[box.Id]=boxView;
                    }
                    boxView.name="Box_"+box.Id.Value+"_HP_"+box.CurrentHitPoints+"_BlockedIndicator";
                    boxView.transform.SetParent(cell.transform,false);
                    boxView.transform.localPosition=new Vector3(0,0,-.03f);
                    boxView.GetComponent<TextMesh>().text="B"+box.CurrentHitPoints;
                }
            }
            var removed = new List<BoardPosition>();
            foreach (var pair in cells) if (!seen.Contains(pair.Key)) { if (pair.Value != null) Destroy(pair.Value); removed.Add(pair.Key); }
            foreach (var position in removed) cells.Remove(position);
            ReconcileIdentityViews(board);
            overlay?.ApplyEnvelope(plan.Envelope);
            appliedEvents.Clear();
            if(plan is ObstaclePresentationPlan obstaclePlan)appliedEvents.AddRange(obstaclePlan.Events);
        }

        public void SetPaused(bool value) => paused = value;

        public void TearDown()
        {
            if (touch != null) touch.PathChanged -= PathChanged;
            touch = null;
            if (playback != null) StopCoroutine(playback);
            playback = null;
            foreach (var item in cells.Values) if (item != null) Destroy(item);
            cells.Clear(); blocks.Clear(); obstacles.Clear(); appliedEvents.Clear();
        }

        IEnumerator Play(IPresentationPlan plan)
        {
            var events=plan is ObstaclePresentationPlan op?op.Events:null;
            var count=events?.Count??1;
            for(var index=0;index<count;index++)
            {
                if(events!=null)ApplyEvent(events[index],plan);
                var normal=DurationFor(events==null?PresentationEventKind.Reconcile:events[index].Kind);
                var milliseconds=plan.Settings.ReducedMotion?Timing.ForReducedMotion(normal):normal;
                var elapsed=0f;while(elapsed<milliseconds/1000f){if(!paused)elapsed+=Time.unscaledDeltaTime;yield return null;}
            }
            playback = null;
            PlaybackCompleted?.Invoke();
        }

        void ApplyEvent(PresentationEvent value, IPresentationPlan plan)
        {
            appliedEvents.Add(value);
            switch (value.Kind)
            {
                case PresentationEventKind.RemoveSelected:
                case PresentationEventKind.RemoveCollateral:
                    var blockId = new BlockId((int)value.Identity);
                    if (blocks.TryGetValue(blockId, out var removed) && removed != null) Destroy(removed);
                    blocks.Remove(blockId);
                    feedback?.Play(value.Kind == PresentationEventKind.RemoveSelected ? PresentationFeedbackCue.Correct : PresentationFeedbackCue.Selection,
                        plan.Settings.AudioEnabled, plan.Settings.HapticsEnabled);
                    break;
                case PresentationEventKind.MoveBlock:
                case PresentationEventKind.ShuffleBlock:
                    var movedId = new BlockId((int)value.Identity);
                    if (blocks.TryGetValue(movedId, out var moved) && moved != null && cells.TryGetValue(value.Position, out var destination))
                        moved.transform.SetParent(destination.transform, false);
                    break;
                case PresentationEventKind.DamageObstacle:
                    var damageId = new ObstacleId((int)value.Identity);
                    if (obstacles.TryGetValue(damageId, out var damaged) && damaged != null) damaged.transform.localScale = Vector3.one * .8f;
                    feedback?.Play(PresentationFeedbackCue.ObstacleDamaged, plan.Settings.AudioEnabled, plan.Settings.HapticsEnabled);
                    break;
                case PresentationEventKind.DestroyObstacle:
                    var obstacleId = new ObstacleId((int)value.Identity);
                    if (obstacles.TryGetValue(obstacleId, out var destroyed) && destroyed != null) Destroy(destroyed);
                    obstacles.Remove(obstacleId);
                    feedback?.Play(PresentationFeedbackCue.ObstacleDestroyed, plan.Settings.AudioEnabled, plan.Settings.HapticsEnabled);
                    break;
                case PresentationEventKind.PresentTarget:
                    overlay?.SetTarget((int)value.Identity);
                    break;
                case PresentationEventKind.RestorationMilestone:
                    overlay?.ShowMilestone(value.Identity);
                    feedback?.Play(PresentationFeedbackCue.Milestone, plan.Settings.AudioEnabled, plan.Settings.HapticsEnabled);
                    break;
                case PresentationEventKind.Miss:
                    overlay?.ShowStatus("MISS");
                    feedback?.Play(PresentationFeedbackCue.Miss, plan.Settings.AudioEnabled, plan.Settings.HapticsEnabled);
                    break;
                case PresentationEventKind.FeverEntry:
                    overlay?.ShowStatus("FEVER");
                    feedback?.Play(PresentationFeedbackCue.FeverEntry, plan.Settings.AudioEnabled, plan.Settings.HapticsEnabled);
                    break;
                case PresentationEventKind.FeverEnd:
                    overlay?.ShowStatus("FEVER END");
                    feedback?.Play(PresentationFeedbackCue.FeverEnd, plan.Settings.AudioEnabled, plan.Settings.HapticsEnabled);
                    break;
                case PresentationEventKind.StageSuccess:
                    var success = plan.Envelope.Success;
                    var successDetail = success == null
                        ? "Restoration " + (plan.Envelope.Session?.ProvisionalRestoration ?? 0) + "  Proceed"
                        : "Stage restoration " + success.RestorationEarned +
                          "  World " + success.ResultingWorld.Current + "/" + success.ResultingWorld.Capacity +
                          "  Milestones " + string.Join(",", success.NewlyCrossedMilestones) + "  Proceed";
                    overlay?.ShowResult(true, successDetail);
                    feedback?.Play(PresentationFeedbackCue.Success, plan.Settings.AudioEnabled, plan.Settings.HapticsEnabled);
                    break;
                case PresentationEventKind.StageFailure:
                    var failure = plan.Envelope.Failure;
                    var failureDetail = new System.Text.StringBuilder();
                    failureDetail.Append("Restoration ").Append(failure?.StageLocalRestoration ?? plan.Envelope.Session?.ProvisionalRestoration ?? 0);
                    var objectives = failure?.Objectives ?? plan.Envelope.Session?.Objectives;
                    if (objectives != null)
                        foreach (var objective in objectives)
                            if (!objective.IsComplete) failureDetail.Append("  Remaining ").Append(objective.Remaining);
                    if (failure?.ContinueEligible == true) failureDetail.Append("  Continue");
                    failureDetail.Append("  Retry  Abandon");
                    overlay?.ShowResult(false, failureDetail.ToString());
                    feedback?.Play(PresentationFeedbackCue.Failure, plan.Settings.AudioEnabled, plan.Settings.HapticsEnabled);
                    break;
            }
        }

        void OnDestroy() => TearDown();
        void PathChanged(IReadOnlyList<BoardPosition> positions)
        {
            long sum = 0;
            if (displayedBoard != null)
                foreach (var position in positions)
                    if (displayedBoard.TryGetCell(position, out var cell) == CellLookupResult.Succeeded && cell.Block.HasValue)
                        sum += cell.Block.Value.Value;
            overlay?.ShowPositions(positions, sum);
        }
        int DurationFor(PresentationEventKind kind)=>kind switch
        {PresentationEventKind.RemoveSelected or PresentationEventKind.RemoveCollateral or PresentationEventKind.DamageObstacle or PresentationEventKind.DestroyObstacle=>Timing.RemovalMilliseconds,
         PresentationEventKind.MoveBlock or PresentationEventKind.ShuffleBlock=>Timing.GravityMilliseconds,
         PresentationEventKind.SpawnBlock=>Timing.RefillMilliseconds,
         PresentationEventKind.RestorationMilestone=>Timing.RestorationMilestoneMilliseconds,_=>Timing.SelectionMilliseconds};
        void ReconcileIdentityViews(MathGame.Board.Board board)
        {
            var liveBlocks=new HashSet<BlockId>();var liveObstacles=new HashSet<ObstacleId>();
            foreach(var p in board.EnumerateActivePositions()){board.TryGetCell(p,out var c);if(c.Block.HasValue)liveBlocks.Add(c.Block.Value.Id);if(c.HasDust)liveObstacles.Add(c.Dust.Value.Id);if(c.HasBox)liveObstacles.Add(c.Box.Value.Id);}
            foreach(var id in new List<BlockId>(blocks.Keys))if(!liveBlocks.Contains(id)){if(blocks[id]!=null)Destroy(blocks[id]);blocks.Remove(id);}
            foreach(var id in new List<ObstacleId>(obstacles.Keys))if(!liveObstacles.Contains(id)){if(obstacles[id]!=null)Destroy(obstacles[id]);obstacles.Remove(id);}
        }
    }

    public sealed class PortraitOnlyPolicy : MonoBehaviour
    {
        void Awake()
        {
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
        }
    }
}
