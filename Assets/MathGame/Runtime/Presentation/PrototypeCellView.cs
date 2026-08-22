using System.Collections;
using MathGame.Board;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MathGame.Presentation.Unity
{
    public sealed class PrototypeCellView : MonoBehaviour, IPointerEnterHandler, IPointerDownHandler
    {
        [SerializeField] int column;
        [SerializeField] int row;
        [SerializeField] Image background;
        [SerializeField] Text valueText;
        [SerializeField] Text obstacleText;
        [SerializeField] GameObject blockRoot;
        [SerializeField] GameObject obstacleRoot;
        Coroutine response;
        bool selected;
        bool matched;
        Color authoritativeBackground;
        Outline border;
        static readonly Color NumberColor = new Color(.90f,.97f,1f,1f);
        static readonly Color NodeColor = new Color(.035f,.09f,.16f,.98f);
        static readonly Color SelectedColor = new Color(.08f,.38f,.55f,1f);
        static readonly Color MatchedColor = new Color(.22f,.82f,.92f,1f);

        public BoardPosition Position => new BoardPosition(column, row);
        public RectTransform RectTransform => (RectTransform)transform;
        public BlockId? DisplayedBlockId { get; private set; }
        public bool PointerIsOver { get; private set; }

        public void Apply(BoardCellSnapshot snapshot)
        {
            border ??= GetComponent<Outline>();
            gameObject.SetActive(true);
            blockRoot.SetActive(snapshot.Block.HasValue);
            if (snapshot.Block.HasValue)
            {
                var value = snapshot.Block.Value.Value;
                valueText.text = value.ToString();
                valueText.color = NumberColor;
            }
            else valueText.text = string.Empty;
            DisplayedBlockId = snapshot.Block?.Id;

            var obstacle = snapshot.HasBox
                ? "X"
                : snapshot.HasDust ? "✕" : string.Empty;
            obstacleRoot.SetActive(obstacle.Length > 0);
            obstacleText.text = obstacle;
            obstacleText.alignment = TextAnchor.MiddleCenter;
            obstacleText.color = snapshot.HasBox ? new Color(1f,.20f,.18f,1f) : new Color(1f,.36f,.28f,.82f);
            authoritativeBackground = snapshot.HasBox ? new Color(.10f,.035f,.05f,.99f) : NodeColor;
            if (border != null) border.effectColor = snapshot.HasBox
                ? new Color(.72f,.10f,.14f,.95f) : new Color(.12f,.48f,.64f,.85f);
            ResetVisualState();
        }

        public void SetGridLayout(int minColumn,int minRow,int columns,int rows,float padding)
        {
            var x=(column-minColumn)/(float)columns;var y=(row-minRow)/(float)rows;
            RectTransform.anchorMin=new Vector2(x,y);
            RectTransform.anchorMax=new Vector2(x+1f/columns,y+1f/rows);
            RectTransform.offsetMin=new Vector2(padding,padding);
            RectTransform.offsetMax=new Vector2(-padding,-padding);
            RectTransform.localScale=Vector3.one;
        }

        public void SetUnused(){DisplayedBlockId=null;PointerIsOver=false;StopResponse();gameObject.SetActive(false);}
        public void SetBlockVisible(bool visible)=>blockRoot.SetActive(visible);
        public void SetObstacleVisible(bool visible)=>obstacleRoot.SetActive(visible);
        public void SetSelected(bool value)
        {
            if (selected == value) return;
            selected = value;
            if (border != null) border.effectColor = value
                ? (matched ? new Color(.75f,1f,1f,1f) : new Color(.18f,.88f,1f,1f))
                : (authoritativeBackground == NodeColor ? new Color(.12f,.48f,.64f,.85f) : new Color(.72f,.10f,.14f,.95f));
            StartResponse(ScaleAndTint(value ? 1.05f : 1f,
                value ? (matched ? MatchedColor : SelectedColor) : authoritativeBackground, .10f));
        }

        public void SetMatched(bool value)
        {
            if (matched == value) return;
            matched = value;
            if (border != null && selected) border.effectColor = value
                ? new Color(.75f,1f,1f,1f) : new Color(.18f,.88f,1f,1f);
            if (selected) StartResponse(ScaleAndTint(value ? 1.07f : 1.05f,
                value ? MatchedColor : SelectedColor, .08f));
        }

        public void PlayRemoval(bool reducedMotion) => StartResponse(PunchBlock(.72f, reducedMotion ? .03f : .09f));
        public void PlayArrival(bool reducedMotion) => StartResponse(PunchBlock(.84f, reducedMotion ? .03f : .10f));
        public void PlayMoveTo(Vector3 destinationWorld, bool reducedMotion) =>
            StartResponse(MoveBlock(destinationWorld, reducedMotion ? .03f : .16f));
        public void PlaySpawn(bool reducedMotion) => StartResponse(SpawnBlock(reducedMotion ? .03f : .15f));
        public void PlayDamage(bool reducedMotion) => StartResponse(DamagePulse(reducedMotion ? .03f : .09f));
        public void PlayReconfigurationFlicker(bool reducedMotion) => StartResponse(FlickerNumber(reducedMotion ? .04f : .14f));
        public void ResetVisualState()
        {
            StopResponse();
            selected = false;
            matched = false;
            RectTransform.localScale = Vector3.one;
            if (blockRoot != null) blockRoot.transform.localScale = Vector3.one;
            if (blockRoot?.transform is RectTransform blockRect) blockRect.anchoredPosition = Vector2.zero;
            if (obstacleRoot != null) obstacleRoot.transform.localScale = Vector3.one;
            if (background != null) background.color = authoritativeBackground;
            if (border != null) border.effectColor = authoritativeBackground == NodeColor
                ? new Color(.12f,.48f,.64f,.85f) : new Color(.72f,.10f,.14f,.95f);
        }

        void StartResponse(IEnumerator routine)
        {
            StopResponse();
            if (blockRoot != null) blockRoot.transform.localScale = Vector3.one;
            if (blockRoot?.transform is RectTransform blockRect) blockRect.anchoredPosition = Vector2.zero;
            if (obstacleRoot != null) obstacleRoot.transform.localScale = Vector3.one;
            if (isActiveAndEnabled) response = StartCoroutine(routine);
        }

        void StopResponse()
        {
            if (response != null) StopCoroutine(response);
            response = null;
        }

        IEnumerator ScaleAndTint(float targetScale, Color targetColor, float duration)
        {
            var startScale = RectTransform.localScale;
            var startColor = background.color;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                RectTransform.localScale = Vector3.Lerp(startScale, Vector3.one * targetScale, t);
                background.color = Color.Lerp(startColor, targetColor, t);
                yield return null;
            }
            RectTransform.localScale = Vector3.one * targetScale;
            background.color = targetColor;
            response = null;
        }

        IEnumerator PunchBlock(float minimumScale, float duration)
        {
            if (blockRoot == null) yield break;
            var half = duration * .5f;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = elapsed < half ? elapsed / half : 1f - (elapsed - half) / half;
                blockRoot.transform.localScale = Vector3.one * Mathf.Lerp(1f, minimumScale, Mathf.Clamp01(t));
                yield return null;
            }
            blockRoot.transform.localScale = Vector3.one;
            response = null;
        }

        IEnumerator DamagePulse(float duration)
        {
            if (obstacleRoot == null) yield break;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var wave = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI);
                obstacleRoot.transform.localScale = Vector3.one * (1f + .12f * wave);
                yield return null;
            }
            obstacleRoot.transform.localScale = Vector3.one;
            response = null;
        }

        IEnumerator FlickerNumber(float duration)
        {
            if(valueText==null)yield break;var original=valueText.color;
            for(var elapsed=0f;elapsed<duration;elapsed+=Time.unscaledDeltaTime)
            {
                var color=original;color.a=Mathf.Lerp(.2f,1f,Mathf.PingPong(elapsed/(duration*.25f),1f));valueText.color=color;yield return null;
            }
            valueText.color=original;response=null;
        }

        IEnumerator MoveBlock(Vector3 destinationWorld, float duration)
        {
            if (blockRoot?.transform is not RectTransform blockRect) yield break;
            var parent = blockRect.parent as RectTransform;
            var destination = parent != null ? (Vector2)parent.InverseTransformPoint(destinationWorld) : Vector2.zero;
            var origin = blockRect.anchoredPosition;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                t = 1f - (1f - t) * (1f - t);
                blockRect.anchoredPosition = Vector2.LerpUnclamped(origin, destination, t);
                yield return null;
            }
            blockRect.anchoredPosition = destination;
            response = null;
        }

        IEnumerator SpawnBlock(float duration)
        {
            if (blockRoot?.transform is not RectTransform blockRect) yield break;
            var distance = Mathf.Max(40, RectTransform.rect.height * 1.25f);
            blockRect.anchoredPosition = new Vector2(0, distance);
            blockRoot.transform.localScale = Vector3.one * .82f;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                var t = Mathf.Clamp01(elapsed / duration);
                t = 1f - (1f - t) * (1f - t);
                blockRect.anchoredPosition = Vector2.Lerp(new Vector2(0, distance), Vector2.zero, t);
                blockRoot.transform.localScale = Vector3.one * Mathf.Lerp(.82f, 1f, t);
                yield return null;
            }
            blockRect.anchoredPosition = Vector2.zero;
            blockRoot.transform.localScale = Vector3.one;
            response = null;
        }

        void OnDisable() => ResetVisualState();
        public void OnPointerEnter(PointerEventData eventData)=>PointerIsOver=true;
        public void OnPointerDown(PointerEventData eventData)=>PointerIsOver=true;

#if UNITY_EDITOR
        public void Configure(int valueColumn,int valueRow,Image visualBackground,Text number,Text obstacle,GameObject numberRoot,GameObject obstacleVisualRoot)
        {column=valueColumn;row=valueRow;background=visualBackground;valueText=number;obstacleText=obstacle;blockRoot=numberRoot;obstacleRoot=obstacleVisualRoot;}

        public void ConfigureScenePreview(bool visible, int value, string obstacle)
        {
            gameObject.SetActive(visible);
            if (!visible) return;
            blockRoot.SetActive(string.IsNullOrEmpty(obstacle) || obstacle == "D");
            valueText.text = blockRoot.activeSelf ? value.ToString() : string.Empty;
            valueText.color = NumberColor;
            obstacleRoot.SetActive(!string.IsNullOrEmpty(obstacle));
            obstacleText.text = obstacle != null && obstacle.StartsWith("B") ? "X" : string.IsNullOrEmpty(obstacle) ? string.Empty : "✕";
            obstacleText.alignment = TextAnchor.MiddleCenter;
            background.color = obstacle != null && obstacle.StartsWith("B")
                ? new Color(.10f,.035f,.05f,.99f)
                : NodeColor;
        }
#endif
    }
}
