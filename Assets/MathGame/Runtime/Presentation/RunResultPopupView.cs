using System;
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
        RunResult current;

        void OnEnable() => LocalizationSettings.SelectedLocaleChanged += LocaleChanged;
        void OnDisable() => LocalizationSettings.SelectedLocaleChanged -= LocaleChanged;
        void LocaleChanged(Locale _) { if (current != null) Render(); }

        public void Bind(Action playAgain)
        {
            playAgainButton.onClick.RemoveAllListeners();
            if (playAgain != null) playAgainButton.onClick.AddListener(() => playAgain());
        }

        public void Show(RunResult result)
        {
            if (result == null) return;
            current = result;
            Render();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        void Render()
        {
            resultText.text = MathGameLocalization.Get("Result", "result.summary", current.Score,
                current.ActiveDuration, current.MaximumFeverCombo, current.HighestDifficultyTier + 1);
            var label = playAgainButton.GetComponentInChildren<Text>();
            if (label != null) label.text = MathGameLocalization.Get("Result", "result.play_again");
        }

        public void Hide() { current = null; gameObject.SetActive(false); }

#if UNITY_EDITOR
        public void Configure(Text value, Button playAgain)
        { resultText = value; playAgainButton = playAgain; }
#endif
    }
}
