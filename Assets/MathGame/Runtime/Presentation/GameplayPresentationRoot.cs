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
        Coroutine playback;
        bool paused;

        public event Action PlaybackCompleted;
        public PresentationTiming Timing { get; private set; } = PresentationTiming.Approved;

        public void Configure(PresentationTiming timing) => Timing = timing ?? throw new ArgumentNullException(nameof(timing));

        public void Prepare(IPresentationPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (playback != null) StopCoroutine(playback);
            playback = StartCoroutine(Play(plan));
        }

        public void ApplyFinalState(IPresentationPlan plan)
        {
            if (plan?.Envelope?.Gameplay?.Board == null) throw new ArgumentNullException(nameof(plan));
            var board = plan.Envelope.Gameplay.Board;
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
            }
            var removed = new List<BoardPosition>();
            foreach (var pair in cells) if (!seen.Contains(pair.Key)) { if (pair.Value != null) Destroy(pair.Value); removed.Add(pair.Key); }
            foreach (var position in removed) cells.Remove(position);
        }

        public void SetPaused(bool value) => paused = value;

        public void TearDown()
        {
            if (playback != null) StopCoroutine(playback);
            playback = null;
            foreach (var item in cells.Values) if (item != null) Destroy(item);
            cells.Clear();
        }

        IEnumerator Play(IPresentationPlan plan)
        {
            var milliseconds = plan.Settings.ReducedMotion ? Timing.ForReducedMotion(Timing.RemovalMilliseconds) : Timing.RemovalMilliseconds;
            var elapsed = 0f;
            while (elapsed < milliseconds / 1000f)
            {
                if (!paused) elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            playback = null;
            PlaybackCompleted?.Invoke();
        }

        void OnDestroy() => TearDown();
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
