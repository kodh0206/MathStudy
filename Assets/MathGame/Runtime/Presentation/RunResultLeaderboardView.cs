using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MathGame.Presentation.Unity
{
    /// <summary>Game-over-only leaderboard UI. The supplied RunResult score remains authoritative.</summary>
    public sealed class RunResultLeaderboardView : MonoBehaviour
    {
        const string NicknamePreferenceKey = "SumVive.Leaderboard.Nickname";

        [Header("LootLocker")]
        [SerializeField] string leaderboardKey = "s_10001";
        [SerializeField, Min(1)] int visibleEntryCount = 10;
        [Header("Serialized UI")]
        [SerializeField] TMP_InputField nicknameInput;
        [SerializeField] Button submitButton;
        [SerializeField] TMP_Text statusText;
        [SerializeField] RectTransform rowsRoot;
        [SerializeField] LeaderboardRowView rowTemplate;

        readonly List<LeaderboardRowView> rows = new();
        IRunLeaderboardClient client;
        string runId;
        long finalScore;
        bool requestInFlight;
        bool submittedForCurrentRun;
        int viewGeneration;

        public bool IsConfigured => nicknameInput != null && submitButton != null && statusText != null &&
                                    rowsRoot != null && rowTemplate != null && !string.IsNullOrWhiteSpace(leaderboardKey);

        void Awake()
        {
            client ??= new LootLockerLeaderboardClient();
            if (!IsConfigured) return;
            ConfigureNicknamePresentation();
            submitButton.onClick.RemoveListener(Submit);
            submitButton.onClick.AddListener(Submit);
            nicknameInput.onValueChanged.RemoveListener(RefreshNicknameText);
            nicknameInput.onValueChanged.AddListener(RefreshNicknameText);
            nicknameInput.text = PlayerPrefs.GetString(NicknamePreferenceKey, string.Empty);
            rowTemplate.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (submitButton != null) submitButton.onClick.RemoveListener(Submit);
            if (nicknameInput != null) nicknameInput.onValueChanged.RemoveListener(RefreshNicknameText);
        }

        void ConfigureNicknamePresentation()
        {
            if (nicknameInput == null) return;
            nicknameInput.customCaretColor = true;
            nicknameInput.caretColor = new Color(.35f, .9f, 1f, 1f);
            nicknameInput.selectionColor = new Color(.18f, .55f, .72f, .65f);
            if (nicknameInput.textComponent != null)
            {
                nicknameInput.textComponent.gameObject.SetActive(true);
                nicknameInput.textComponent.enabled = true;
                if (nicknameInput.textComponent.font == null)
                    nicknameInput.textComponent.font = TMP_Settings.defaultFontAsset;
                nicknameInput.textComponent.color = new Color(.9f, .98f, 1f, 1f);
                nicknameInput.textComponent.alpha = 1f;
                nicknameInput.textComponent.textWrappingMode = TextWrappingModes.NoWrap;
                nicknameInput.textComponent.overflowMode = TextOverflowModes.Ellipsis;
                nicknameInput.textComponent.raycastTarget = false;
                nicknameInput.textComponent.transform.SetAsLastSibling();
            }
            if (nicknameInput.placeholder is TMP_Text placeholder)
            {
                placeholder.color = new Color(.55f, .65f, .7f, .75f);
                placeholder.raycastTarget = false;
            }
            if (nicknameInput.textViewport != null &&
                nicknameInput.textViewport.TryGetComponent<RectMask2D>(out var mask))
                mask.enabled = false;
        }

        void RefreshNicknameText(string value)
        {
            if (nicknameInput?.textComponent == null) return;
            var renderedText = nicknameInput.textComponent;
            renderedText.text = value ?? string.Empty;
            renderedText.color = new Color(.9f, .98f, 1f, 1f);
            renderedText.alpha = 1f;
            renderedText.ForceMeshUpdate(true);
        }

        public void InitializeOnlineServices()
        {
            client ??= new LootLockerLeaderboardClient();
            client.EnsureGuestSession((success, error) =>
            {
                if (!success) Debug.LogWarning("[SumVive][Leaderboard] Guest session failed: " + error);
            });
        }

        public void Present(string authoritativeRunId, long authoritativeFinalScore)
        {
            runId = authoritativeRunId;
            finalScore = authoritativeFinalScore;
            var generation = ++viewGeneration;
            requestInFlight = false;
            submittedForCurrentRun = false;
            if (!IsConfigured) return;
            submitButton.interactable = true;
            statusText.text = "Loading leaderboard...";
            InitializeOnlineServices();
            client.EnsureGuestSession((success, error) =>
            {
                if (generation != viewGeneration) return;
                if (!success)
                {
                    statusText.text = error;
                    Debug.LogWarning("[SumVive][Leaderboard] Guest session unavailable: " + error);
                    return;
                }
                RefreshScores(generation);
            });
        }

        public void ResetView()
        {
            viewGeneration++;
            runId = null;
            finalScore = 0;
            requestInFlight = false;
            submittedForCurrentRun = false;
            if (submitButton != null) submitButton.interactable = true;
            if (statusText != null) statusText.text = string.Empty;
        }

        public void Submit()
        {
            if (!IsConfigured || requestInFlight || submittedForCurrentRun || string.IsNullOrWhiteSpace(runId)) return;
            var nickname = nicknameInput.text?.Trim();
            if (string.IsNullOrEmpty(nickname))
            {
                statusText.text = "Enter a nickname.";
                return;
            }
            if (finalScore < 0 || finalScore > int.MaxValue)
            {
                statusText.text = "Score is outside the leaderboard range.";
                return;
            }

            nicknameInput.text = nickname;
            requestInFlight = true;
            var generation = viewGeneration;
            submitButton.interactable = false;
            statusText.text = "Submitting...";
            client.EnsureGuestSession((sessionReady, sessionError) =>
            {
                if (generation != viewGeneration) return;
                if (!sessionReady) { Fail(sessionError, generation); return; }
                client.SetPlayerName(nickname, (nameUpdated, nameError) =>
                {
                    if (generation != viewGeneration) return;
                    if (!nameUpdated) { Fail(nameError, generation); return; }
                    PlayerPrefs.SetString(NicknamePreferenceKey, nickname);
                    PlayerPrefs.Save();
                    client.SubmitScore(leaderboardKey.Trim(), (int)finalScore, (scoreSubmitted, scoreError) =>
                    {
                        if (generation != viewGeneration) return;
                        if (!scoreSubmitted) { Fail(scoreError, generation); return; }
                        requestInFlight = false;
                        submittedForCurrentRun = true;
                        statusText.text = "Score submitted.";
                        Debug.Log("[SumVive][Leaderboard] Score submitted and leaderboard refresh requested.");
                        RefreshScores(generation);
                    });
                });
            });
        }

        void RefreshScores(int generation)
        {
            client.GetTopScores(leaderboardKey.Trim(), Mathf.Max(1, visibleEntryCount), (success, entries, error) =>
            {
                if (generation != viewGeneration) return;
                if (!success)
                {
                    if (!submittedForCurrentRun) statusText.text = error;
                    Debug.LogWarning("[SumVive][Leaderboard] Top scores unavailable: " + error);
                    return;
                }
                Render(entries);
                if (!submittedForCurrentRun) statusText.text = entries.Count == 0 ? "No scores yet." : string.Empty;
            });
        }

        void Render(IReadOnlyList<LeaderboardEntry> entries)
        {
            while (rows.Count < entries.Count)
            {
                var row = Instantiate(rowTemplate, rowsRoot);
                row.gameObject.SetActive(true);
                rows.Add(row);
            }
            for (var i = 0; i < rows.Count; i++)
            {
                var active = i < entries.Count;
                rows[i].gameObject.SetActive(active);
                if (active) rows[i].Set(entries[i]);
            }
        }

        void Fail(string message, int generation)
        {
            if (generation != viewGeneration) return;
            requestInFlight = false;
            if (submitButton != null) submitButton.interactable = true;
            if (statusText != null) statusText.text = string.IsNullOrWhiteSpace(message) ? "Request failed. Try again." : message;
            Debug.LogError("[SumVive][Leaderboard] Submission failed: " +
                           (string.IsNullOrWhiteSpace(message) ? "Request failed." : message));
        }

#if UNITY_EDITOR
        public void Configure(string key, TMP_InputField nickname, Button submit, TMP_Text status,
            RectTransform listRoot, LeaderboardRowView template)
        {
            leaderboardKey = key;
            nicknameInput = nickname;
            submitButton = submit;
            statusText = status;
            rowsRoot = listRoot;
            rowTemplate = template;
            ConfigureNicknamePresentation();
        }

        public void SetClientForTests(IRunLeaderboardClient value) => client = value;
#endif
    }
}
