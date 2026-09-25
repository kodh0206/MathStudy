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
        [SerializeField] string leaderboardKey = "100";
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
            submitButton.onClick.RemoveListener(Submit);
            submitButton.onClick.AddListener(Submit);
            nicknameInput.text = PlayerPrefs.GetString(NicknamePreferenceKey, string.Empty);
            rowTemplate.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (submitButton != null) submitButton.onClick.RemoveListener(Submit);
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
        }

        public void SetClientForTests(IRunLeaderboardClient value) => client = value;
#endif
    }
}
