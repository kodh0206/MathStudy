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
        MathGamePrefabRegistry prefabRegistry;
        Transform cellRoot,blockRoot,effectRoot;
        readonly Dictionary<BoardPosition,PrototypeCellView> prebuiltCells=new Dictionary<BoardPosition,PrototypeCellView>();

        public event Action PlaybackCompleted;
        public IReadOnlyList<PresentationEvent> AppliedEvents => appliedEvents.AsReadOnly();
        public int BlockViewCount => blocks.Count;
        public int ObstacleViewCount => obstacles.Count;
        public int SerializedCellViewCount => GetComponentsInChildren<PrototypeCellView>(true).Length;
        public PresentationTiming Timing { get; private set; } = PresentationTiming.Approved;
        public void PlaySelectionCue()=>feedback?.Play(PresentationFeedbackCue.Selection,true,false);
        public void SetSelectedPositions(IReadOnlyCollection<BoardPosition> positions)
        {
            var selectedPositions=positions==null?new HashSet<BoardPosition>():new HashSet<BoardPosition>(positions);
            foreach(var pair in prebuiltCells)if(pair.Value.gameObject.activeSelf)pair.Value.SetSelected(selectedPositions.Contains(pair.Key));
        }

        public void FrameCamera(Camera camera)
        {
            if(transform is RectTransform)return;
            if (camera == null || displayedBoard == null) return;
            if (prebuiltCells.Count == 0) IndexPrebuiltCells();

            var hasCell = false;
            var center = Vector3.zero;
            var count = 0;
            foreach (var position in displayedBoard.EnumerateActivePositions())
            {
                if (!prebuiltCells.TryGetValue(position, out var view)) continue;
                center += view.transform.position;
                count++;
                hasCell = true;
            }
            if (!hasCell) return;
            center /= count;

            camera.transform.rotation = transform.rotation;
            camera.transform.position = center - camera.transform.forward * 10f;
            var halfWidth = .5f;
            var halfHeight = .5f;
            foreach (var position in displayedBoard.EnumerateActivePositions())
            {
                if (!prebuiltCells.TryGetValue(position, out var view)) continue;
                var offset = view.transform.position - center;
                halfWidth = Mathf.Max(halfWidth, Mathf.Abs(Vector3.Dot(offset, camera.transform.right)) + .5f);
                halfHeight = Mathf.Max(halfHeight, Mathf.Abs(Vector3.Dot(offset, camera.transform.up)) + .5f);
            }
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(halfHeight, halfWidth / Mathf.Max(.1f, camera.aspect)) + .25f;
        }

        public bool TryScreenPointToCell(Camera camera, Vector2 screenPosition, out BoardPosition position)
        {
            position = default;
            if (camera == null || displayedBoard == null) return false;
            if(transform is RectTransform)
            {
                foreach(var pair in prebuiltCells)
                    if(pair.Value.gameObject.activeInHierarchy&&RectTransformUtility.RectangleContainsScreenPoint(pair.Value.RectTransform,screenPosition,null))
                    {position=pair.Key;return displayedBoard.IsActive(position);}
                return false;
            }
            var ray = camera.ScreenPointToRay(screenPosition);
            var hits=Physics.RaycastAll(ray,100f);
            Array.Sort(hits,(left,right)=>left.distance.CompareTo(right.distance));
            foreach(var hit in hits)
            {
                var view=hit.collider.GetComponentInParent<PrototypeCellView>();
                if(view==null||!view.transform.IsChildOf(transform))continue;
                var hitPosition=view.Position;
                if(!displayedBoard.IsActive(hitPosition))continue;
                position=hitPosition;
                return true;
            }
            var plane = new Plane(transform.forward, transform.position);
            if (!plane.Raycast(ray, out var distance)) return false;
            var local = transform.InverseTransformPoint(ray.GetPoint(distance));
            var column = Mathf.RoundToInt(local.x);
            var row = Mathf.RoundToInt(local.y);
            if (Mathf.Abs(local.x - column) > .5f || Mathf.Abs(local.y - row) > .5f) return false;
            var candidate = new BoardPosition(column, row);
            if (!displayedBoard.IsActive(candidate) || !prebuiltCells.ContainsKey(candidate)) return false;
            position = candidate;
            return true;
        }

        public Vector3 GetCellWorldPosition(BoardPosition position) =>
            prebuiltCells.TryGetValue(position, out var view)
                ? view.transform.position - transform.forward * .5f
                : transform.TransformPoint(new Vector3(position.Column, position.Row, -.5f));

        public void Configure(PresentationTiming timing) => Timing = timing ?? throw new ArgumentNullException(nameof(timing));
        public void ConfigureRegistry(MathGamePrefabRegistry registry)=>prefabRegistry=registry;
        public void ConfigureSlots(Transform cellsSlot,Transform blocksSlot,Transform effectsSlot){cellRoot=cellsSlot;blockRoot=blocksSlot;effectRoot=effectsSlot;}
        void IndexPrebuiltCells()
        {
            prebuiltCells.Clear();
            foreach(var view in GetComponentsInChildren<PrototypeCellView>(true))
                if(!prebuiltCells.TryAdd(view.Position,view))throw new InvalidOperationException("Duplicate prebuilt CellView position: "+view.Position);
        }

        void Awake()
        {
            BeginSession();
        }

        // The BoardView is serialized scene content and survives prototype stage restarts.
        // GameplayPresentationCoordinator.Dispose tears down session subscriptions, so a
        // new composition must explicitly restore them before binding the next Board.
        public void BeginSession()
        {
            IndexPrebuiltCells();
            overlay = GetComponent<GameplayOverlayView>();
            if (overlay == null) overlay = gameObject.AddComponent<GameplayOverlayView>();
            feedback = GetComponent<IPresentationFeedbackPort>();
            if (touch != null) touch.PathChanged -= PathChanged;
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
            if(prebuiltCells.Count==0)IndexPrebuiltCells();
            if (prebuiltCells.Count == 0)
                throw new InvalidOperationException("BoardView has no serialized PrototypeCellView children. Rebuild the versioned Board prefab before Play Mode.");
            ApplyPrebuiltBoard(board, plan);
        }

        void ApplyPrebuiltBoard(MathGame.Board.Board board,IPresentationPlan plan)
        {
            var active=new HashSet<BoardPosition>();blocks.Clear();obstacles.Clear();
            var minColumn=int.MaxValue;var maxColumn=int.MinValue;var minRow=int.MaxValue;var maxRow=int.MinValue;
            foreach(var position in board.EnumerateActivePositions()){minColumn=Math.Min(minColumn,position.Column);maxColumn=Math.Max(maxColumn,position.Column);minRow=Math.Min(minRow,position.Row);maxRow=Math.Max(maxRow,position.Row);}
            var columns=Math.Max(1,maxColumn-minColumn+1);var rows=Math.Max(1,maxRow-minRow+1);
            foreach(var position in board.EnumerateActivePositions())
            {
                active.Add(position);
                if(!prebuiltCells.TryGetValue(position,out var view))throw new InvalidOperationException("Board exceeds prebuilt visual capacity at "+position);
                board.TryGetCell(position,out var snapshot);view.SetGridLayout(minColumn,minRow,columns,rows,6f);view.Apply(snapshot);
                if(snapshot.Block.HasValue)blocks[snapshot.Block.Value.Id]=view.gameObject;
                if(snapshot.HasDust)obstacles[snapshot.Dust.Value.Id]=view.gameObject;
                if(snapshot.HasBox)obstacles[snapshot.Box.Value.Id]=view.gameObject;
            }
            foreach(var pair in prebuiltCells)if(!active.Contains(pair.Key))pair.Value.SetUnused();
            overlay?.ApplyEnvelope(plan.Envelope);appliedEvents.Clear();if(plan is ObstaclePresentationPlan obstaclePlan)appliedEvents.AddRange(obstaclePlan.Events);
        }

        public void SetPaused(bool value) => paused = value;

        public void TearDown()
        {
            if (touch != null) touch.PathChanged -= PathChanged;
            touch = null;
            if (playback != null) StopCoroutine(playback);
            playback = null;
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
                    // Prebuilt cells are never hidden or destroyed during playback. Their
                    // final number is rebound from the authoritative Board at reconciliation.
                    blocks.Remove(blockId);
                    feedback?.Play(value.Kind == PresentationEventKind.RemoveSelected ? PresentationFeedbackCue.Correct : PresentationFeedbackCue.Selection,
                        plan.Settings.AudioEnabled, plan.Settings.HapticsEnabled);
                    break;
                case PresentationEventKind.MoveBlock:
                case PresentationEventKind.ShuffleBlock:
                    var movedId = new BlockId((int)value.Identity);
                    if (blocks.TryGetValue(movedId, out var moved) && moved != null && cells.TryGetValue(value.Position, out var destination))
                    {if(moved.GetComponent<PrototypeCellView>()==null)moved.transform.SetParent(destination.transform, false);}
                    break;
                case PresentationEventKind.DamageObstacle:
                    var damageId = new ObstacleId((int)value.Identity);
                    if (obstacles.TryGetValue(damageId, out var damaged) && damaged != null&&damaged.GetComponent<PrototypeCellView>()==null) damaged.transform.localScale = Vector3.one * .8f;
                    feedback?.Play(PresentationFeedbackCue.ObstacleDamaged, plan.Settings.AudioEnabled, plan.Settings.HapticsEnabled);
                    break;
                case PresentationEventKind.DestroyObstacle:
                    var obstacleId = new ObstacleId((int)value.Identity);
                    // As with blocks, obstacle visuals are serialized children of each cell;
                    // ApplyFinalState updates their visibility and HP without recreating them.
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
