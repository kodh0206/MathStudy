using System;
using System.Collections.Generic;
using LootLocker;
using LootLocker.Requests;
using UnityEngine;

namespace MathGame.Presentation.Unity
{
    public readonly struct LeaderboardEntry
    {
        public LeaderboardEntry(int rank, string playerName, int score)
        {
            Rank = rank;
            PlayerName = string.IsNullOrWhiteSpace(playerName) ? "Anonymous" : playerName;
            Score = score;
        }

        public int Rank { get; }
        public string PlayerName { get; }
        public int Score { get; }
    }

    public interface IRunLeaderboardClient
    {
        void EnsureGuestSession(Action<bool, string> completed);
        void SetPlayerName(string playerName, Action<bool, string> completed);
        void SubmitScore(string leaderboardKey, int score, Action<bool, string> completed);
        void GetTopScores(string leaderboardKey, int count, Action<bool, IReadOnlyList<LeaderboardEntry>, string> completed);
    }

    /// <summary>Thin LootLocker adapter. It owns no gameplay state and uses LootLocker's persisted guest identity.</summary>
    public sealed class LootLockerLeaderboardClient : IRunLeaderboardClient
    {
        enum SessionState { NotStarted, Starting, Ready, Failed }

        static SessionState sessionState;
        static readonly List<Action<bool, string>> SessionWaiters = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            sessionState = SessionState.NotStarted;
            SessionWaiters.Clear();
        }

        public void EnsureGuestSession(Action<bool, string> completed)
        {
            if (sessionState == SessionState.Ready)
            {
                completed?.Invoke(true, null);
                return;
            }

            if (completed != null) SessionWaiters.Add(completed);
            if (sessionState == SessionState.Starting) return;

            sessionState = SessionState.Starting;
            LootLockerSDKManager.StartGuestSession(response =>
            {
                var success = response != null && response.success;
                sessionState = success ? SessionState.Ready : SessionState.Failed;
                var error = success ? null : ErrorMessage(response, "Unable to connect to the leaderboard.");
                var waiters = SessionWaiters.ToArray();
                SessionWaiters.Clear();
                foreach (var waiter in waiters) waiter(success, error);
            });
        }

        public void SetPlayerName(string playerName, Action<bool, string> completed)
        {
            LootLockerSDKManager.SetPlayerName(playerName, response =>
                completed?.Invoke(response != null && response.success,
                    response != null && response.success ? null : ErrorMessage(response, "Unable to update nickname.")));
        }

        public void SubmitScore(string leaderboardKey, int score, Action<bool, string> completed)
        {
            LootLockerSDKManager.SubmitScore(string.Empty, score, leaderboardKey, response =>
                completed?.Invoke(response != null && response.success,
                    response != null && response.success ? null : ErrorMessage(response, "Unable to submit score.")));
        }

        public void GetTopScores(string leaderboardKey, int count,
            Action<bool, IReadOnlyList<LeaderboardEntry>, string> completed)
        {
            LootLockerSDKManager.GetScoreList(leaderboardKey, count, response =>
            {
                if (response == null || !response.success)
                {
                    completed?.Invoke(false, Array.Empty<LeaderboardEntry>(),
                        ErrorMessage(response, "Unable to load leaderboard."));
                    return;
                }

                var result = new List<LeaderboardEntry>(response.items?.Length ?? 0);
                if (response.items != null)
                {
                    foreach (var item in response.items)
                        result.Add(new LeaderboardEntry(item.rank, item.player?.name, item.score));
                }
                completed?.Invoke(true, result, null);
            });
        }

        static string ErrorMessage(LootLockerResponse response, string fallback)
        {
            var message = response?.errorData?.message;
            return string.IsNullOrWhiteSpace(message) ? fallback : message.Trim();
        }
    }
}
