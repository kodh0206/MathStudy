using System.Collections;
using MathGame.Presentation;
using MathGame.Presentation.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections.Generic;
using MathGame.Board;
using UnityEngine.UI;

namespace MathGame.Tests
{
    public sealed class PresentationLifecycleTests
    {
        [UnityTest]
        public IEnumerator PortraitPolicy_EnforcesPortraitAndPresentationRootCleansUp()
        {
            var go = new GameObject("STEP12 Presentation Root");
            go.AddComponent<PortraitOnlyPolicy>();
            var root = go.AddComponent<GameplayPresentationRoot>();
            root.Configure(PresentationTiming.Approved);
            yield return null;
            Assert.That(Screen.orientation, Is.EqualTo(ScreenOrientation.Portrait));
            Object.Destroy(go);
            yield return null;
            Assert.That(go == null, Is.True);
        }

        [Test]
        public void ReducedMotion_ApprovedDurationsNeverExceedFiftyMilliseconds()
        {
            Assert.That(PresentationTiming.Approved.ForReducedMotion(PresentationTiming.Approved.GravityMilliseconds), Is.LessThanOrEqualTo(50));
            Assert.That(new PresentationSettings(true).PortraitOnly, Is.True);
        }

        [Test]
        public void SelectionLine_UsesSuppliedOrderedPath_AndClearsDeterministically()
        {
            var gameObject = new GameObject("SelectionLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(SelectionLineGraphic));
            var line = gameObject.GetComponent<SelectionLineGraphic>();
#if UNITY_EDITOR
            line.Configure(10f, Color.cyan);
#endif
            var path = new List<Vector2> { new Vector2(0, 0), new Vector2(0, 20), new Vector2(30, 20) };
            line.SetPoints(path);
            Assert.That(line.Points, Is.EqualTo(path));
            line.Clear();
            Assert.That(line.Points, Is.Empty);
            Assert.That(line.raycastTarget, Is.False);
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void SelectionLine_MatchStateBrightensWithoutChangingPath()
        {
            var gameObject = new GameObject("SelectionLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(SelectionLineGraphic));
            var line = gameObject.GetComponent<SelectionLineGraphic>();
#if UNITY_EDITOR
            line.Configure(12f, new Color(.18f, .88f, 1f, .9f));
#endif
            line.SetPoints(new[] { Vector2.zero, Vector2.right * 20 });
            var normal = line.color;
            line.SetMatched(true);
            Assert.That(line.Points.Count, Is.EqualTo(2));
            Assert.That(line.color, Is.Not.EqualTo(normal));
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void RunHud_UsesConfiguredCapacityForTimeAndFeverGauges()
        {
            var root = new GameObject("RunHUD", typeof(RectTransform));
            var view = root.AddComponent<RunHUDView>();
            Text Label(string name) => new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
            var target = Label("Target"); var time = Label("Time"); var score = Label("Score");
            var combo = Label("Combo"); var tier = Label("Tier"); var fever = Label("Fever");
            var timeFill = new GameObject("TimeFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            var feverFill = new GameObject("FeverFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
#if UNITY_EDITOR
            view.Configure(root, target, time, timeFill, score, combo, tier, fever, feverFill);
#endif
            Assert.That(view.IsComplete, Is.True, "The run HUD contract must not require a manual Pause control.");
            view.Present(8, 15, 30, 1240, 4, 2, 25, 50);
            Assert.That(target.text, Is.EqualTo("8"));
            Assert.That(timeFill.fillAmount, Is.EqualTo(.5f).Within(.001f));
            Assert.That(feverFill.fillAmount, Is.EqualTo(.5f).Within(.001f));
            Assert.That(combo.text, Does.Contain("x4"));
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(target.gameObject); Object.DestroyImmediate(time.gameObject); Object.DestroyImmediate(score.gameObject);
            Object.DestroyImmediate(combo.gameObject); Object.DestroyImmediate(tier.gameObject); Object.DestroyImmediate(fever.gameObject);
            Object.DestroyImmediate(timeFill.gameObject); Object.DestroyImmediate(feverFill.gameObject);
        }

        [Test]
        public void CorrectCue_IsAnswerLevelAndGradeSpecific()
        {
            var gameObject = new GameObject("Feedback");
            var feedback = gameObject.AddComponent<PlaceholderPresentationFeedback>();
            var root = gameObject.AddComponent<GameplayPresentationRoot>();
            root.PlayCorrectCue(MathGame.Answer.SpeedGrade.Perfect, false, false);
            Assert.That(feedback.Played, Is.EqualTo(new[] { PresentationFeedbackCue.Correct, PresentationFeedbackCue.Perfect }));
            Object.DestroyImmediate(gameObject);
        }

        [UnityTest]
        public IEnumerator InterruptedMiss_ResetRestoresAuthoredSelectionSumPosition()
        {
            var existingEventSystem = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            var gameObject = new GameObject("HUD");
            var layout = gameObject.AddComponent<PrototypeUILayout>();
            layout.Build(null, null, null, null, null, null, null);
            var selection = gameObject.transform.Find("SafeArea/BottomHUD/SelectionSum") as RectTransform;
            var authored = selection.anchoredPosition;
            layout.SetSelectionSum(7, 2);
            layout.PresentMiss();
            yield return null;
            layout.ResetPolish();
            Assert.That(selection.anchoredPosition, Is.EqualTo(authored));
            var createdEventSystem = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (existingEventSystem == null && createdEventSystem != null) Object.Destroy(createdEventSystem.gameObject);
            Object.Destroy(gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RemovalEffectPool_ReusesCompletedInstance_AndResetsOnDisable()
        {
            var effectRoot = new GameObject("EffectRoot", typeof(RectTransform));
            var prefab = new GameObject("RemovalEffect", typeof(RectTransform), typeof(BlockRemovalEffectView));
            var dot = new GameObject("Dot", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            dot.transform.SetParent(prefab.transform, false);
#if UNITY_EDITOR
            prefab.GetComponent<BlockRemovalEffectView>().Configure(
                new UnityEngine.UI.Graphic[] { dot.GetComponent<UnityEngine.UI.Image>() }, .03f, 10f);
#endif
            prefab.SetActive(false);
            var owner = new GameObject("Pool", typeof(BlockRemovalEffectPool));
            var pool = owner.GetComponent<BlockRemovalEffectPool>();
            pool.Configure(prefab, effectRoot.transform);

            pool.PlayAt(Vector3.zero);
            Assert.That(pool.InstanceCount, Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(.05f);
            pool.PlayAt(Vector3.one);
            Assert.That(pool.InstanceCount, Is.EqualTo(1));
            owner.SetActive(false);
            yield return null;

            Object.Destroy(owner);
            Object.Destroy(prefab);
            Object.Destroy(effectRoot);
            yield return null;
        }

        [Test]
        public void InteractionTimings_RemainShort()
        {
            var timing = new PresentationTiming(45, 80, 100, 80, 180);
            Assert.That(timing.SelectionMilliseconds, Is.LessThanOrEqualTo(50));
            Assert.That(timing.RemovalMilliseconds, Is.LessThanOrEqualTo(100));
            Assert.That(timing.GravityMilliseconds, Is.LessThanOrEqualTo(120));
            Assert.That(timing.RefillMilliseconds, Is.LessThanOrEqualTo(100));
        }

        [Test]
        public void PolishFeedbackCues_AreDistinctPresentationFacts()
        {
            Assert.That(PresentationFeedbackCue.Perfect, Is.Not.EqualTo(PresentationFeedbackCue.Fast));
            Assert.That(PresentationFeedbackCue.TimeRecovery, Is.Not.EqualTo(PresentationFeedbackCue.Combo));
            Assert.That(PresentationFeedbackCue.RunEnd, Is.Not.EqualTo(PresentationFeedbackCue.PlayAgain));
        }

        [Test]
        public void TouchAdapter_ForwardsImmediatePredecessorForBacktrack_RejectsSecondPointer_AndCancels()
        {
            var go=new GameObject("Touch Adapter");var adapter=go.AddComponent<LogicalBoardTouchAdapter>();
            adapter.CellSize=1;adapter.Width=5;adapter.Height=5;var entered=new List<BoardPosition>();var cancelled=0;
            adapter.LogicalCellEntered+=entered.Add;adapter.Cancelled+=()=>cancelled++;
            Assert.That(adapter.Begin(1,new Vector2(0,0)),Is.True);
            Assert.That(adapter.Drag(2,new Vector2(1,0)),Is.False);
            Assert.That(adapter.Drag(1,new Vector2(1,0)),Is.True);
            Assert.That(adapter.Drag(1,new Vector2(0,0)),Is.True);
            Assert.That(entered,Is.EqualTo(new[]{new BoardPosition(0,0),new BoardPosition(1,0),new BoardPosition(0,0)}));
            Assert.That(adapter.Cancel(1),Is.True);Assert.That(cancelled,Is.EqualTo(1));Object.DestroyImmediate(go);
        }
    }
}
