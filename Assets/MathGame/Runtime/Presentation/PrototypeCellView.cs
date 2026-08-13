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
            background.color = snapshot.HasBox ? new Color(.30f,.18f,.08f,.96f) : new Color(.92f,.95f,1f,.98f);
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

        public void SetUnused(){DisplayedBlockId=null;PointerIsOver=false;gameObject.SetActive(false);}
        public void SetBlockVisible(bool visible)=>blockRoot.SetActive(visible);
        public void SetObstacleVisible(bool visible)=>obstacleRoot.SetActive(visible);
        public void SetSelected(bool selected)=>background.color=selected?new Color(.45f,.9f,1f,1f):new Color(.92f,.95f,1f,.98f);
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
