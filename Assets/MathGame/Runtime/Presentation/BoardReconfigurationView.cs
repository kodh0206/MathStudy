using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MathGame.Presentation.Unity
{
    /// <summary>Presentation-only feedback for an authoritative deadlock recovery.</summary>
    public sealed class BoardReconfigurationView : MonoBehaviour
    {
        [SerializeField] CanvasGroup boardContent;
        [SerializeField] GameObject overlay;
        [SerializeField] RectTransform scanLine;
        [SerializeField] Text message;
        [SerializeField] Outline boardOutline;
        Coroutine playback;
        Color baselineOutline;

        void Awake() { if (boardOutline != null) baselineOutline = boardOutline.effectColor; ResetImmediate(); }

        public void Begin(bool reducedMotion)
        {
            ResetImmediate();
            if (message != null) message.text = MathGameLocalization.Get("Gameplay", "gameplay.reconfiguring");
            if (overlay != null) overlay.SetActive(true);
            if (boardContent != null) boardContent.alpha = .7f;
            if (isActiveAndEnabled) playback = StartCoroutine(Scan(reducedMotion ? .05f : .26f));
        }

        public void Complete(bool reducedMotion)
        {
            if (playback != null) StopCoroutine(playback);
            playback = isActiveAndEnabled ? StartCoroutine(Completion(reducedMotion ? .05f : .18f)) : null;
            if (playback == null) ResetImmediate();
        }

        IEnumerator Scan(float duration)
        {
            if (scanLine != null)
                for (var elapsed=0f;elapsed<duration;elapsed+=Time.unscaledDeltaTime)
                {
                    var t=Mathf.Clamp01(elapsed/duration);
                    scanLine.anchorMin=new Vector2(0,1-t);
                    scanLine.anchorMax=new Vector2(1,1-t);
                    scanLine.anchoredPosition=Vector2.zero;
                    yield return null;
                }
            playback=null;
        }

        IEnumerator Completion(float duration)
        {
            if (boardContent != null) boardContent.alpha=1f;
            for(var elapsed=0f;elapsed<duration;elapsed+=Time.unscaledDeltaTime)
            {
                var wave=Mathf.Sin(Mathf.Clamp01(elapsed/duration)*Mathf.PI);
                if(boardOutline!=null)boardOutline.effectColor=Color.Lerp(baselineOutline,new Color(.5f,1f,1f,1f),wave);
                yield return null;
            }
            playback=null;
            ResetImmediate();
        }

        public void ResetImmediate()
        {
            if(playback!=null)StopCoroutine(playback);playback=null;
            if(boardContent!=null)boardContent.alpha=1f;
            if(overlay!=null)overlay.SetActive(false);
            if(boardOutline!=null)boardOutline.effectColor=baselineOutline;
        }

        void OnDisable()=>ResetImmediate();

#if UNITY_EDITOR
        public void Configure(CanvasGroup content,GameObject overlayRoot,RectTransform line,Text label,Outline outline)
        {boardContent=content;overlay=overlayRoot;scanLine=line;message=label;boardOutline=outline;baselineOutline=outline!=null?outline.effectColor:Color.cyan;}
#endif
    }
}
