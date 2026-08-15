using System;
using MathGame.SurvivalRun;
using UnityEngine;
using UnityEngine.UI;

namespace MathGame.Presentation.Unity
{
    /// <summary>Serialized Run result presentation. Gameplay authority remains in SurvivalRunSession.</summary>
    public sealed class RunResultPopupView : MonoBehaviour
    {
        [SerializeField] Text resultText;
        [SerializeField] Button playAgainButton;

        public void Bind(Action playAgain)
        {
            playAgainButton.onClick.RemoveAllListeners();
            if (playAgain != null) playAgainButton.onClick.AddListener(() => playAgain());
        }

        public void Show(RunResult result)
        {
            if (result == null) return;
            resultText.text = "RUN OVER\n\nSCORE  " + result.Score +
                "\nTIME  " + result.ActiveDuration.ToString("0.0") + "s" +
                "\nMAX COMBO  " + result.MaximumFeverCombo +
                "\nDIFFICULTY  " + (result.HighestDifficultyTier + 1);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide() => gameObject.SetActive(false);

#if UNITY_EDITOR
        public void Configure(Text value, Button playAgain)
        { resultText = value; playAgainButton = playAgain; }
#endif
    }
}
