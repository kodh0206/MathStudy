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
        Color authoritativeBackground;

        public BoardPosition Position => new BoardPosition(column, row);
        public RectTransform RectTransform => (RectTransform)transform;
        public BlockId? DisplayedBlockId { get; private set; }
        public bool PointerIsOver { get; private set; }

        public void Apply(BoardCellSnapshot snapshot)
        {
            gameObject.SetActive(true);
            blockRoot.SetActive(snapshot.Block.HasValue);
            if (snapshot.Block.HasValue)
            {
                var value = snapshot.Block.Value.Value;
                valueText.text = value.ToString();
                valueText.color = ColorForValue(value);
            }
            else valueText.text = string.Empty;
            DisplayedBlockId = snapshot.Block?.Id;

            var obstacle = snapshot.HasBox ? "B" + snapshot.Box.Value.CurrentHitPoints : snapshot.HasDust ? "D" : string.Empty;
            obstacleRoot.SetActive(obstacle.Length > 0);
            obstacleText.text = obstacle;
            authoritativeBackground = snapshot.HasBox ? new Color(.30f,.18f,.08f,.96f) : new Color(.92f,.95f,1f,.98f);
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
            StartResponse(ScaleAndTint(value ? 1.06f : 1f,
                value ? new Color(.45f,.9f,1f,1f) : authoritativeBackground, .07f));
        }

        public void PlayRemoval(bool reducedMotion) => StartResponse(PunchBlock(.72f, reducedMotion ? .03f : .09f));
        public void PlayArrival(bool reducedMotion) => StartResponse(PunchBlock(.84f, reducedMotion ? .03f : .10f));
        public void PlayDamage(bool reducedMotion) => StartResponse(DamagePulse(reducedMotion ? .03f : .09f));
        public void ResetVisualState()
        {
            StopResponse();
            selected = false;
            RectTransform.localScale = Vector3.one;
            if (blockRoot != null) blockRoot.transform.localScale = Vector3.one;
            if (obstacleRoot != null) obstacleRoot.transform.localScale = Vector3.one;
            if (background != null) background.color = authoritativeBackground;
        }

        void StartResponse(IEnumerator routine)
        {
            StopResponse();
            if (blockRoot != null) blockRoot.transform.localScale = Vector3.one;
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

        void OnDisable() => ResetVisualState();
        public void OnPointerEnter(PointerEventData eventData)=>PointerIsOver=true;
        public void OnPointerDown(PointerEventData eventData)=>PointerIsOver=true;

        static Color ColorForValue(int value)
        {
            var palette=new[]{new Color(.10f,.32f,.72f),new Color(.72f,.18f,.22f),new Color(.12f,.52f,.28f),new Color(.55f,.22f,.72f),new Color(.85f,.38f,.08f),new Color(.04f,.52f,.58f),new Color(.72f,.12f,.48f),new Color(.38f,.30f,.18f),new Color(.18f,.42f,.62f)};
            return palette[Mathf.Abs(value-1)%palette.Length];
        }

#if UNITY_EDITOR
        public void Configure(int valueColumn,int valueRow,Image visualBackground,Text number,Text obstacle,GameObject numberRoot,GameObject obstacleVisualRoot)
        {column=valueColumn;row=valueRow;background=visualBackground;valueText=number;obstacleText=obstacle;blockRoot=numberRoot;obstacleRoot=obstacleVisualRoot;}

        public void ConfigureScenePreview(bool visible, int value, string obstacle)
        {
            gameObject.SetActive(visible);
            if (!visible) return;
            blockRoot.SetActive(string.IsNullOrEmpty(obstacle) || obstacle == "D");
            valueText.text = blockRoot.activeSelf ? value.ToString() : string.Empty;
            valueText.color = ColorForValue(value);
            obstacleRoot.SetActive(!string.IsNullOrEmpty(obstacle));
            obstacleText.text = obstacle ?? string.Empty;
            background.color = obstacle != null && obstacle.StartsWith("B")
                ? new Color(.30f,.18f,.08f,.96f)
                : new Color(.92f,.95f,1f,.98f);
        }
#endif
    }
}
