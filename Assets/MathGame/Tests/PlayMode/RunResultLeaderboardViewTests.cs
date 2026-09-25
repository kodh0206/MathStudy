using System;
using System.Collections.Generic;
using MathGame.Presentation.Unity;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace MathGame.Tests.PlayMode
{
    public sealed class RunResultLeaderboardViewTests
    {
        readonly List<GameObject> objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var value in objects) UnityEngine.Object.DestroyImmediate(value);
            objects.Clear();
        }

        [Test]
        public void Submit_TrimsName_UsesAuthoritativeScore_AndDeduplicatesRun()
        {
            var fake = new FakeClient();
            var setup = CreateView(fake);
            setup.Input.text = "  Ada  ";
            setup.View.Present("run-1", 4321);

            setup.View.Submit();
            setup.View.Submit();

            Assert.That(fake.PlayerName, Is.EqualTo("Ada"));
            Assert.That(fake.SubmittedScore, Is.EqualTo(4321));
            Assert.That(fake.SubmitCalls, Is.EqualTo(1));
            Assert.That(setup.Submit.interactable, Is.False);
        }

        [Test]
        public void Submit_Failure_ReenablesButtonWithoutSubmittingScoreTwice()
        {
            var fake = new FakeClient { FailName = true };
            var setup = CreateView(fake);
            setup.Input.text = "Ada";
            setup.View.Present("run-2", 22);

            LogAssert.Expect(LogType.Error, "[SumVive][Leaderboard] Submission failed: name failed");
            setup.View.Submit();

            Assert.That(setup.Submit.interactable, Is.True);
            Assert.That(fake.SubmitCalls, Is.Zero);
            Assert.That(setup.Status.text, Does.Contain("name failed"));
        }

        [Test]
        public void Submit_RejectsWhitespaceAndScoreOverflow()
        {
            var fake = new FakeClient();
            var setup = CreateView(fake);
            setup.Input.text = "   ";
            setup.View.Present("run-3", 10);
            setup.View.Submit();
            Assert.That(fake.SubmitCalls, Is.Zero);
            Assert.That(setup.Status.text, Does.Contain("nickname"));

            setup.Input.text = "Ada";
            setup.View.Present("run-4", (long)int.MaxValue + 1);
            setup.View.Submit();
            Assert.That(fake.SubmitCalls, Is.Zero);
            Assert.That(setup.Status.text, Does.Contain("range"));
        }

        Setup CreateView(FakeClient fake)
        {
            var root = New("Leaderboard");
            var view = root.AddComponent<RunResultLeaderboardView>();
            var input = New("Input").AddComponent<TMP_InputField>();
            var submit = New("Submit").AddComponent<Button>();
            var status = New("Status").AddComponent<TextMeshProUGUI>();
            var rows = New("Rows").GetComponent<RectTransform>();
            var templateObject = New("Template");
            var template = templateObject.AddComponent<LeaderboardRowView>();
            var rank = New("Rank").AddComponent<TextMeshProUGUI>();
            var player = New("Name").AddComponent<TextMeshProUGUI>();
            var score = New("Score").AddComponent<TextMeshProUGUI>();
            template.Configure(rank, player, score);
            view.Configure("test-board", input, submit, status, rows, template);
            view.SetClientForTests(fake);
            return new Setup(view, input, submit, status);
        }

        GameObject New(string name)
        {
            var value = new GameObject(name, typeof(RectTransform));
            objects.Add(value);
            return value;
        }

        readonly struct Setup
        {
            public Setup(RunResultLeaderboardView view, TMP_InputField input, Button submit, TMP_Text status)
            { View = view; Input = input; Submit = submit; Status = status; }
            public RunResultLeaderboardView View { get; }
            public TMP_InputField Input { get; }
            public Button Submit { get; }
            public TMP_Text Status { get; }
        }

        sealed class FakeClient : IRunLeaderboardClient
        {
            public bool FailName { get; set; }
            public string PlayerName { get; private set; }
            public int SubmittedScore { get; private set; }
            public int SubmitCalls { get; private set; }
            public void EnsureGuestSession(Action<bool, string> completed) => completed(true, null);
            public void SetPlayerName(string playerName, Action<bool, string> completed)
            {
                PlayerName = playerName;
                completed(!FailName, FailName ? "name failed" : null);
            }
            public void SubmitScore(string leaderboardKey, int score, Action<bool, string> completed)
            {
                SubmitCalls++;
                SubmittedScore = score;
                completed(true, null);
            }
            public void GetTopScores(string leaderboardKey, int count,
                Action<bool, IReadOnlyList<LeaderboardEntry>, string> completed) =>
                completed(true, Array.Empty<LeaderboardEntry>(), null);
        }
    }
}
