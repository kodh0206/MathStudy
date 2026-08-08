using System.Collections;
using MathGame.Presentation;
using MathGame.Presentation.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
    }
}
