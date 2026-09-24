using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using MathGame.Fever;

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
        bool feverWarning;
        bool warningReducedMotion;
        bool feverSequence;

        void Awake() { if (boardOutline != null) baselineOutline = boardOutline.effectColor; ResetImmediate(); }

        public void Begin(bool reducedMotion)
        {
            ResetImmediate();
            if (message != null) message.text = "SHUFFLE";
            if (message != null) message.color = new Color(.25f,.95f,1f,1f);
            if (overlay != null) overlay.SetActive(true);
            if (boardContent != null) boardContent.alpha = .7f;
            if (isActiveAndEnabled) playback = StartCoroutine(Scan(reducedMotion ? .05f : .26f));
        }

        public void SetFeverExpiryWarning(bool active, bool reducedMotion)
        {
            feverWarning = active;
            warningReducedMotion = reducedMotion;
            if (!active && !feverSequence && boardOutline != null) boardOutline.effectColor = baselineOutline;
        }

        public void BeginFeverEnd(FeverEndEffectTier tier, bool reducedMotion)
        {
            ResetImmediate();
            feverSequence = true;
            feverWarning = false;
            if (message != null)
            {
                message.text = "FEVER FINISH!\n" + Description(tier);
                message.color = new Color(1f,.68f,.16f,1f);
            }
            if (overlay != null) overlay.SetActive(true);
            if (boardContent != null) boardContent.alpha = reducedMotion ? .88f : .76f;
            if (boardOutline != null) boardOutline.effectColor = new Color(1f,.48f,.08f,1f);
        }

        public void PlayFeverWave(bool reducedMotion)
        {
            if (reducedMotion || !isActiveAndEnabled) return;
            if (playback != null) StopCoroutine(playback);
            playback = StartCoroutine(FeverWave(.18f));
        }

        public void PlayFeverFade(bool reducedMotion)
        {
            if (boardContent != null) boardContent.alpha = reducedMotion ? .92f : .82f;
        }

        public void CompleteFeverEnd()
        {
            if (!feverSequence) return;
            ResetImmediate();
        }

        static string Description(FeverEndEffectTier tier) => tier switch
        {
            FeverEndEffectTier.RandomThreeBlocks => "3 BLOCK BLAST",
            FeverEndEffectTier.SmallAreaExplosion => "SMALL BLAST",
            FeverEndEffectTier.CenterAreaExplosion => "CENTER BLAST",
            FeverEndEffectTier.LargeExplosionAndRestoration => "LARGE BLAST",
            _ => "NO BONUS"
        };

        IEnumerator FeverWave(float duration)
        {
            for(var elapsed=0f;elapsed<duration;elapsed+=Time.unscaledDeltaTime)
            {
                var wave=Mathf.Sin(Mathf.Clamp01(elapsed/duration)*Mathf.PI);
                if(boardOutline!=null)
                {
                    boardOutline.effectColor=Color.Lerp(new Color(1f,.42f,.05f,1f),new Color(1f,.9f,.28f,1f),wave);
                    boardOutline.effectDistance=Vector2.one*Mathf.Lerp(3f,8f,wave);
                }
                yield return null;
            }
            playback=null;
        }

        void Update()
        {
            if (!feverWarning || feverSequence || boardOutline == null) return;
            boardOutline.effectColor = new Color(1f,.55f,.10f,
                warningReducedMotion ? .9f : Mathf.Lerp(.45f,1f,.5f+.5f*Mathf.Sin(Time.unscaledTime*18f)));
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
            if(boardOutline!=null)boardOutline.effectDistance=new Vector2(3,-3);
            feverWarning=false;
            warningReducedMotion=false;
            feverSequence=false;
        }

        void OnDisable()=>ResetImmediate();

#if UNITY_EDITOR
        public void Configure(CanvasGroup content,GameObject overlayRoot,RectTransform line,Text label,Outline outline)
        {boardContent=content;overlay=overlayRoot;scanLine=line;message=label;boardOutline=outline;baselineOutline=outline!=null?outline.effectColor:Color.cyan;}
#endif
    }
}
