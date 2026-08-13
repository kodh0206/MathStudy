using System;
using UnityEngine;
using UnityEngine.UI;

namespace MathGame.Presentation.Unity
{
    /// <summary>Serialized presentation view for a completed stage. It emits intent only.</summary>
    public sealed class StageClearPopupView : MonoBehaviour
    {
        [SerializeField] Text title;
        [SerializeField] Text message;
        [SerializeField] Button retryButton;
        [SerializeField] Button nextStageButton;

        public void Bind(Action retryRequested, Action nextStageRequested, bool hasNextStage)
        {
            retryButton.onClick.RemoveAllListeners();
            nextStageButton.onClick.RemoveAllListeners();
            if (retryRequested != null) retryButton.onClick.AddListener(() => retryRequested());
            if (nextStageRequested != null) nextStageButton.onClick.AddListener(() => nextStageRequested());
            nextStageButton.interactable = hasNextStage;
            message.text = hasNextStage
                ? "All objectives complete. Continue to the next stage?"
                : "All objectives complete. The next prototype stage is not available yet.";
        }

        public void Show()
        {
            title.text = "STAGE CLEAR!";
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide() => gameObject.SetActive(false);

#if UNITY_EDITOR
        public void Configure(Text titleText, Text messageText, Button retry, Button next)
        {
            title = titleText;
            message = messageText;
            retryButton = retry;
            nextStageButton = next;
        }
#endif
    }
}
