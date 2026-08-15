using System;
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
        public IReadOnlyList<Vector2> Points => points;

        public void SetPoints(IReadOnlyList<Vector2> value)
        {
            points.Clear();
            if (value != null)
                for (var i = 0; i < value.Count; i++) points.Add(value[i]);
            SetVerticesDirty();
        }

        public void Clear() => SetPoints(null);

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
            color = value;
            raycastTarget = false;
        }
#endif
    }

    public sealed class PresentationPrefabContract : MonoBehaviour
    {
        [SerializeField] string contractId;
        [SerializeField] int version;
        public string ContractId=>contractId;public int Version=>version;
#if UNITY_EDITOR
        public void Configure(string id,int value){contractId=id;version=value;}
#endif
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
