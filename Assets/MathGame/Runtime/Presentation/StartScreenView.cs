using System;
using System.Collections;
using MathGame.PlayerProgress;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace MathGame.Presentation.Unity
{
    /// <summary>Serialized home presentation. It requests actions but never owns run state.</summary>
    public sealed class StartScreenView : MonoBehaviour
    {
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] Text titleText, subtitleText, statusText, bestTimeText, bestScoreText;
        [SerializeField] Button startButton, languageButton;
        [SerializeField] RectTransform coreVisual;
        Action startRequested, languageRequested;
        RunRecords records = RunRecords.Empty;
        Coroutine transition;
        float pulseTime;

        void OnEnable() { LocalizationSettings.SelectedLocaleChanged += LocaleChanged; Render(); }
        void OnDisable() { LocalizationSettings.SelectedLocaleChanged -= LocaleChanged; if (transition != null) StopCoroutine(transition); transition = null; }
        void Update() { if (coreVisual == null) return; pulseTime += Time.unscaledDeltaTime; coreVisual.localScale = Vector3.one * (1f + Mathf.Sin(pulseTime * 1.8f) * .015f); coreVisual.Rotate(0, 0, Time.unscaledDeltaTime * 8f); }
        public void Bind(Action onStart, Action onLanguage) { startRequested=onStart; languageRequested=onLanguage; startButton.onClick.RemoveAllListeners(); languageButton.onClick.RemoveAllListeners(); startButton.onClick.AddListener(BeginStartTransition); languageButton.onClick.AddListener(()=>languageRequested?.Invoke()); }
        public void Show(RunRecords value) { records=value??RunRecords.Empty; gameObject.SetActive(true); transform.SetAsLastSibling(); canvasGroup.alpha=1; canvasGroup.blocksRaycasts=true; canvasGroup.interactable=true; startButton.interactable=true; Render(); }
        public void HideImmediate() { if(transition!=null)StopCoroutine(transition); transition=null; gameObject.SetActive(false); }
        void BeginStartTransition() { if(!startButton.interactable||transition!=null)return; startButton.interactable=false; canvasGroup.interactable=false; transition=StartCoroutine(StartTransition()); }
        IEnumerator StartTransition() { statusText.text=MathGameLocalization.Get("Start","start.system_online"); const float duration=.28f; for(var elapsed=0f;elapsed<duration;elapsed+=Time.unscaledDeltaTime){canvasGroup.alpha=1-Mathf.Clamp01(elapsed/duration);yield return null;} canvasGroup.blocksRaycasts=false;transition=null;startRequested?.Invoke(); }
        void LocaleChanged(Locale _) => Render();
        void Render() { if(titleText==null)return; titleText.text=MathGameLocalization.Get("Start","start.title");subtitleText.text=MathGameLocalization.Get("Start","start.subtitle");statusText.text=MathGameLocalization.Get("Start","start.core_online");bestTimeText.text=MathGameLocalization.Get("Start","start.best_time",records.BestSurvivalDuration);bestScoreText.text=MathGameLocalization.Get("Start","start.best_score",records.BestScore);startButton.GetComponentInChildren<Text>().text=MathGameLocalization.Get("Start","start.run");languageButton.GetComponentInChildren<Text>().text=MathGameLocalization.Get("Settings","settings.language_button"); }
#if UNITY_EDITOR
        public void Configure(CanvasGroup group,Text title,Text subtitle,Text state,Text bestTime,Text bestScore,Button start,Button language,RectTransform core){canvasGroup=group;titleText=title;subtitleText=subtitle;statusText=state;bestTimeText=bestTime;bestScoreText=bestScore;startButton=start;languageButton=language;coreVisual=core;}
#endif
    }
}
