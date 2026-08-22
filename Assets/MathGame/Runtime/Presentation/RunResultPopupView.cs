using System;
using System.Collections;
using MathGame.SurvivalRun;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace MathGame.Presentation.Unity
{
    /// <summary>Serialized Run result presentation. Gameplay authority remains in SurvivalRunSession.</summary>
    public sealed class RunResultPopupView : MonoBehaviour
    {
        [SerializeField] Text resultText;
        [SerializeField] Button playAgainButton;
        [SerializeField] Button homeButton;
        RunResult current;
        Coroutine transition;

        void OnEnable() => LocalizationSettings.SelectedLocaleChanged += LocaleChanged;
        void OnDisable() => LocalizationSettings.SelectedLocaleChanged -= LocaleChanged;
        void LocaleChanged(Locale _) { if (current != null) Render(); }

        public void Bind(Action playAgain, Action home = null)
        {
            playAgainButton.onClick.RemoveAllListeners();
            if (playAgain != null) playAgainButton.onClick.AddListener(() => playAgain());
            if (homeButton != null)
            {
                homeButton.onClick.RemoveAllListeners();
                if (home != null) homeButton.onClick.AddListener(() => home());
                homeButton.gameObject.SetActive(home != null);
            }
        }

        public void Show(RunResult result)
        {
            if (result == null) return;
            current = result;
            Render();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            if (transition != null) StopCoroutine(transition);
            transition = StartCoroutine(Enter());
        }

        void Render()
        {
            resultText.text = MathGameLocalization.Get("Result", "result.summary", current.Score,
                current.ActiveDuration, current.MaximumFeverCombo, current.HighestDifficultyTier + 1);
            var label = playAgainButton.GetComponentInChildren<Text>();
            if (label != null) label.text = MathGameLocalization.Get("Result", "result.play_again");
            var homeLabel = homeButton != null ? homeButton.GetComponentInChildren<Text>() : null;
            if (homeLabel != null) homeLabel.text = MathGameLocalization.Get("Result", "result.home");
        }

        public void Hide()
        {
            if (transition != null) StopCoroutine(transition);
            transition = null;
            transform.localScale = Vector3.one;
            current = null;
            gameObject.SetActive(false);
        }

        IEnumerator Enter()
        {
            const float duration = .18f;
            transform.localScale = Vector3.one * .94f;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                transform.localScale = Vector3.one * Mathf.Lerp(.94f, 1f, Mathf.SmoothStep(0, 1, elapsed / duration));
                yield return null;
            }
            transform.localScale = Vector3.one;
            transition = null;
        }

#if UNITY_EDITOR
        public void Configure(Text value, Button playAgain, Button home = null)
        { resultText = value; playAgainButton = playAgain; homeButton = home; }
#endif
    }

}
