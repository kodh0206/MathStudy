using MathGame.Board;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MathGame.Presentation.Unity
{
    public sealed class PrototypeCellView : MonoBehaviour,IPointerEnterHandler,IPointerDownHandler
    {
        [SerializeField] int column;
        [SerializeField] int row;
        [SerializeField] Transform background;
        [SerializeField] TextMesh valueText;
        [SerializeField] TextMesh obstacleText;
        [SerializeField] GameObject blockRoot;
        [SerializeField] GameObject obstacleRoot;
        public BoardPosition Position=>new BoardPosition(column,row);
        public BlockId? DisplayedBlockId{get;private set;}
        public bool PointerIsOver{get;private set;}

        public void Apply(BoardCellSnapshot snapshot)
        {
            gameObject.SetActive(true);
            if(blockRoot!=null)blockRoot.SetActive(snapshot.Block.HasValue);
            if(valueText!=null)
            {
                valueText.text=snapshot.Block.HasValue?snapshot.Block.Value.Value.ToString():string.Empty;
                valueText.color=new Color(.035f,.055f,.09f,1f);
                valueText.transform.localPosition=new Vector3(0f,0f,-.08f);
                var renderer=valueText.GetComponent<MeshRenderer>();
                if(renderer!=null)renderer.sortingOrder=20;
            }
            DisplayedBlockId=snapshot.Block?.Id;
            var obstacle=snapshot.HasBox?"B"+snapshot.Box.Value.CurrentHitPoints:snapshot.HasDust?"D":string.Empty;
            if(obstacleRoot!=null)obstacleRoot.SetActive(obstacle.Length>0);
            if(obstacleText!=null)
            {
                obstacleText.text=obstacle;
                var renderer=obstacleText.GetComponent<MeshRenderer>();
                if(renderer!=null)renderer.sortingOrder=30;
            }
            if(background!=null)background.localScale=snapshot.HasBox?Vector3.one*.78f:Vector3.one*.88f;
        }

        public void SetUnused(){DisplayedBlockId=null;PointerIsOver=false;gameObject.SetActive(false);}
        public void SetBlockVisible(bool visible){if(blockRoot!=null)blockRoot.SetActive(visible);}
        public void SetObstacleVisible(bool visible){if(obstacleRoot!=null)obstacleRoot.SetActive(visible);}
        public void SetSelected(bool selected){if(background!=null)background.localScale*=selected?1.04f:1f;}
        public void OnPointerEnter(PointerEventData eventData)=>PointerIsOver=true;
        public void OnPointerDown(PointerEventData eventData)=>PointerIsOver=true;

#if UNITY_EDITOR
        public void Configure(int valueColumn,int valueRow,Transform visualBackground,TextMesh number,TextMesh obstacle,GameObject numberRoot,GameObject obstacleVisualRoot)
        {column=valueColumn;row=valueRow;background=visualBackground;valueText=number;obstacleText=obstacle;blockRoot=numberRoot;obstacleRoot=obstacleVisualRoot;}
#endif
    }
}
