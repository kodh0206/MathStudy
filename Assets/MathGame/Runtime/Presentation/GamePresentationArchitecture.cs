using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MathGame.Presentation.Unity
{
    /// <summary>Prefab-owned, non-interactive line used only to render an authoritative selection path.</summary>
    public sealed partial class SelectionLineGraphic : Graphic
    {
        [SerializeField, Min(2f)] float lineWidth = 10f;
        readonly List<Vector2> points = new List<Vector2>();
        Color normalColor = new Color(.18f, .88f, 1f, .86f);
        Color matchColor = new Color(.72f, 1f, 1f, 1f);
        public IReadOnlyList<Vector2> Points => points;

        public void SetPoints(IReadOnlyList<Vector2> value)
        {
            points.Clear();
            if (value != null)
                for (var i = 0; i < value.Count; i++) points.Add(value[i]);
            SetVerticesDirty();
        }

        public void Clear() => SetPoints(null);

        public void SetMatched(bool value)
        {
            color = value ? matchColor : normalColor;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (points.Count < 2) return;
            for (var i = 1; i < points.Count; i++)
            {
                var from = points[i - 1];
                var to = points[i];
                var delta = to - from;
                if (delta.sqrMagnitude < .001f) continue;
                var perpendicular = new Vector2(-delta.y, delta.x).normalized * (lineWidth * .5f);
                var start = helper.currentVertCount;
                Add(helper, from - perpendicular);
                Add(helper, from + perpendicular);
                Add(helper, to + perpendicular);
                Add(helper, to - perpendicular);
                helper.AddTriangle(start, start + 1, start + 2);
                helper.AddTriangle(start, start + 2, start + 3);
            }
        }

        void Add(VertexHelper helper, Vector2 position)
        {
            var vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = position;
            helper.AddVert(vertex);
        }

#if UNITY_EDITOR
        public void Configure(float width, Color value)
        {
            lineWidth = width;
            normalColor = value;
            color = normalColor;
            raycastTarget = false;
        }
#endif
    }

    /// <summary>Reusable theme-neutral UI burst. Visual children and timing are prefab-owned.</summary>
    public sealed partial class BlockRemovalEffectView : MonoBehaviour
    {
        [SerializeField] Graphic[] particles = Array.Empty<Graphic>();
        [SerializeField, Min(.03f)] float duration = 1f;
        [SerializeField, Min(1f)] float travelDistance = 34f;
        Coroutine playback;
        Vector2[] origins = Array.Empty<Vector2>();

        public bool IsPlaying => playback != null;

        public void Play(Action<BlockRemovalEffectView> completed)
        {
            ResetEffect();
            gameObject.SetActive(true);
            playback = StartCoroutine(Animate(completed));
        }

        public void ResetEffect()
        {
            if (playback != null) StopCoroutine(playback);
            playback = null;
            EnsureOrigins();
            for (var i = 0; i < particles.Length; i++)
            {
                if (particles[i] == null) continue;
                particles[i].rectTransform.anchoredPosition = origins[i];
                particles[i].rectTransform.localScale = Vector3.one;
                particles[i].rectTransform.localRotation = Quaternion.identity;
                var color = particles[i].color;
                color.a = 1f;
                particles[i].color = color;
            }
        }

        IEnumerator Animate(Action<BlockRemovalEffectView> completed)
        {
            EnsureOrigins();
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                for (var i = 0; i < particles.Length; i++)
                {
                    if (particles[i] == null) continue;
                    var angle = particles.Length == 0 ? 0f : i * Mathf.PI * 2f / particles.Length;
                    angle += (i % 2 == 0 ? 1f : -1f) * t * .28f;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    var distance = travelDistance * (i % 3 == 0 ? 1.18f : i % 3 == 1 ? .88f : 1f);
                    var eased = 1f - (1f - t) * (1f - t);
                    particles[i].rectTransform.anchoredPosition = origins[i] + direction * distance * eased;
                    particles[i].rectTransform.localRotation = Quaternion.Euler(0, 0, (i % 2 == 0 ? 1 : -1) * 120f * t);
                    particles[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(1.15f, .18f, t);
                    var color = particles[i].color;
                    color.a = 1f - t;
                    particles[i].color = color;
                }
                yield return null;
            }
            playback = null;
            completed?.Invoke(this);
        }

        void EnsureOrigins()
        {
            if (origins.Length == particles.Length) return;
            origins = new Vector2[particles.Length];
            for (var i = 0; i < particles.Length; i++)
                if (particles[i] != null) origins[i] = particles[i].rectTransform.anchoredPosition;
        }

        void OnDisable() => ResetEffect();

#if UNITY_EDITOR
        public void Configure(Graphic[] values, float seconds, float distance)
        {
            particles = values ?? Array.Empty<Graphic>();
            duration = seconds;
            travelDistance = distance;
            origins = Array.Empty<Vector2>();
        }
#endif
    }

    /// <summary>Small bounded pool; removal gameplay never waits for or depends on it.</summary>
    public sealed partial class BlockRemovalEffectPool : MonoBehaviour
    {
        [SerializeField, Min(1)] int maximumInstances = 16;
        readonly List<BlockRemovalEffectView> instances = new List<BlockRemovalEffectView>();
        GameObject effectPrefab;
        Transform effectRoot;

        public int InstanceCount => instances.Count;

        public void Configure(GameObject prefab, Transform root)
        {
            effectPrefab = prefab;
            effectRoot = root;
        }

        public void PlayAt(Vector3 worldPosition)
        {
            var view = Acquire();
            if (view == null) return;
            view.transform.position = worldPosition;
            view.transform.SetAsLastSibling();
            view.Play(Release);
        }

        public void ResetAll()
        {
            foreach (var view in instances)
            {
                if (view == null) continue;
                view.ResetEffect();
                view.gameObject.SetActive(false);
            }
        }

        BlockRemovalEffectView Acquire()
        {
            foreach (var view in instances)
                if (view != null && !view.gameObject.activeSelf) return view;
            if (effectPrefab == null || effectRoot == null || instances.Count >= maximumInstances) return null;
            var instance = Instantiate(effectPrefab, effectRoot);
            var effect = instance.GetComponent<BlockRemovalEffectView>();
            if (effect == null)
            {
                Destroy(instance);
                return null;
            }
            instances.Add(effect);
            return effect;
        }

        static void Release(BlockRemovalEffectView view)
        {
            if (view != null) view.gameObject.SetActive(false);
        }

        void OnDisable() => ResetAll();
    }

    public sealed class GamePresentationContext
    {
        public GamePresentationContext(Transform gameplayRoot,Transform boardSlot,Transform effectSlot,
            Transform topUiRoot,Transform centerUiRoot,Transform bottomUiRoot,Transform overlayRoot,Transform presentationRoot,
            MathGamePrefabRegistry registry)
        {GameplayRoot=gameplayRoot;BoardSlot=boardSlot;EffectSlot=effectSlot;TopUIRoot=topUiRoot;CenterUIRoot=centerUiRoot;BottomUIRoot=bottomUiRoot;OverlayRoot=overlayRoot;PresentationRoot=presentationRoot;Registry=registry;}
        public Transform GameplayRoot{get;}public Transform BoardSlot{get;}public Transform EffectSlot{get;}
        public Transform TopUIRoot{get;}public Transform CenterUIRoot{get;}public Transform BottomUIRoot{get;}public Transform OverlayRoot{get;}
        public Transform PresentationRoot{get;}public MathGamePrefabRegistry Registry{get;}
    }

    public interface IGamePresentationModule : IDisposable
    {
        void Initialize(GamePresentationContext context);
    }

}
