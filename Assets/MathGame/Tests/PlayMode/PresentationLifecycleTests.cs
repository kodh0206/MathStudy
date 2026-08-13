using System.Collections;
using MathGame.Presentation;
using MathGame.Presentation.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections.Generic;
using MathGame.Board;

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
