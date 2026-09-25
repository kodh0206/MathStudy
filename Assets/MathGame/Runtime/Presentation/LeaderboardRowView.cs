using TMPro;
using UnityEngine;

namespace MathGame.Presentation.Unity
{
    public sealed class LeaderboardRowView : MonoBehaviour
    {
        [SerializeField] TMP_Text rankText;
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text scoreText;

        public void Set(LeaderboardEntry entry)
        {
            rankText.text = entry.Rank.ToString();
            nameText.text = entry.PlayerName;
            scoreText.text = entry.Score.ToString("N0");
        }

#if UNITY_EDITOR
        public void Configure(TMP_Text rank, TMP_Text playerName, TMP_Text score)
        {
            rankText = rank;
            nameText = playerName;
            scoreText = score;
        }
#endif
    }
}
