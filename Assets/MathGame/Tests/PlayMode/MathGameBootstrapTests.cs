using System.Collections;
using MathGame.App;
using MathGame.Stage;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MathGame.Tests.PlayMode
{
    public sealed class MathGameBootstrapTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            DestroyAllBootstraps();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DestroyAllBootstraps();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Bootstrap_InitializesBlankStage()
        {
            MathGameBootstrap bootstrap = EnsureBootstrap();

            yield return null;

            Assert.That(bootstrap.StageController, Is.Not.Null);
            Assert.That(bootstrap.TimeProvider, Is.Not.Null);
            Assert.That(bootstrap.RandomSource, Is.Not.Null);
            Assert.That(bootstrap.StageController.State, Is.EqualTo(StageState.Ready));
            Assert.That(bootstrap.StageController.AcceptsPlayerInput, Is.False);

            Object.Destroy(bootstrap.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LifecycleRelay_PreservesNestedPauseReasons()
        {
            MathGameBootstrap bootstrap = EnsureBootstrap();
            yield return null;

            var relay = bootstrap.GetComponent<ApplicationLifecycleRelay>();
            relay.SendMessage("OnApplicationPause", true);
            relay.SendMessage("OnApplicationFocus", false);

            Assert.That(bootstrap.StageController.State, Is.EqualTo(StageState.Paused));
            Assert.That(bootstrap.StageController.ActivePauseReasonCount, Is.EqualTo(2));

            relay.SendMessage("OnApplicationPause", false);
            Assert.That(bootstrap.StageController.State, Is.EqualTo(StageState.Paused));

            relay.SendMessage("OnApplicationFocus", true);
            Assert.That(bootstrap.StageController.State, Is.EqualTo(StageState.Ready));

            Object.Destroy(bootstrap.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LifecycleRelay_EarlyBackgroundPause_IsReconciledAfterInitialization()
        {
            MathGameBootstrap bootstrap = EnsureBootstrap();
            var relay = bootstrap.GetComponent<ApplicationLifecycleRelay>();

            relay.SendMessage("OnApplicationPause", true);
            yield return null;

            Assert.That(relay.IsApplicationPaused, Is.True);
            Assert.That(bootstrap.StageController.State, Is.EqualTo(StageState.Paused));
            Assert.That(bootstrap.StageController.StateBeforePause, Is.EqualTo(StageState.Ready));
            Assert.That(
                bootstrap.StageController.HasPauseReason(PauseReason.ApplicationBackground),
                Is.True);
            Assert.That(bootstrap.StageController.AcceptsPlayerInput, Is.False);

            Object.Destroy(bootstrap.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LifecycleRelay_EarlyInactiveThenActive_DoesNotApplyStaleReason()
        {
            MathGameBootstrap bootstrap = EnsureBootstrap();
            var relay = bootstrap.GetComponent<ApplicationLifecycleRelay>();

            relay.SendMessage("OnApplicationFocus", false);
            relay.SendMessage("OnApplicationFocus", true);
            yield return null;

            Assert.That(relay.HasApplicationFocus, Is.True);
            Assert.That(bootstrap.StageController.State, Is.EqualTo(StageState.Ready));
            Assert.That(
                bootstrap.StageController.HasPauseReason(PauseReason.ApplicationFocusLost),
                Is.False);

            Object.Destroy(bootstrap.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LifecycleRelay_EarlyFocusLoss_IsReconciledAfterInitialization()
        {
            MathGameBootstrap bootstrap = EnsureBootstrap();
            var relay = bootstrap.GetComponent<ApplicationLifecycleRelay>();

            relay.SendMessage("OnApplicationFocus", false);
            yield return null;

            Assert.That(bootstrap.StageController.State, Is.EqualTo(StageState.Paused));
            Assert.That(bootstrap.StageController.StateBeforePause, Is.EqualTo(StageState.Ready));
            Assert.That(
                bootstrap.StageController.HasPauseReason(PauseReason.ApplicationFocusLost),
                Is.True);
        }

        [UnityTest]
        public IEnumerator LifecycleRelay_EarlyNestedReasons_ClearIndependently()
        {
            MathGameBootstrap bootstrap = EnsureBootstrap();
            var relay = bootstrap.GetComponent<ApplicationLifecycleRelay>();

            relay.SendMessage("OnApplicationFocus", false);
            relay.SendMessage("OnApplicationPause", true);
            yield return null;

            Assert.That(bootstrap.StageController.State, Is.EqualTo(StageState.Paused));
            Assert.That(bootstrap.StageController.StateBeforePause, Is.EqualTo(StageState.Ready));
            Assert.That(bootstrap.StageController.ActivePauseReasonCount, Is.EqualTo(2));

            relay.SendMessage("OnApplicationPause", false);
            Assert.That(bootstrap.StageController.State, Is.EqualTo(StageState.Paused));
            Assert.That(bootstrap.StageController.ActivePauseReasonCount, Is.EqualTo(1));

            relay.SendMessage("OnApplicationFocus", true);
            Assert.That(bootstrap.StageController.State, Is.EqualTo(StageState.Ready));
            Assert.That(bootstrap.StageController.ActivePauseReasonCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator LifecycleRelay_EarlyNestedReasons_ReverseOrderTransitionsOnceToPaused()
        {
            MathGameBootstrap bootstrap = EnsureBootstrap();
            var relay = bootstrap.GetComponent<ApplicationLifecycleRelay>();
            int pausedTransitionCount = 0;
            bootstrap.StageController.StateChanged += transition =>
            {
                if (transition.Current == StageState.Paused)
                {
                    pausedTransitionCount++;
                }
            };

            relay.SendMessage("OnApplicationPause", true);
            relay.SendMessage("OnApplicationFocus", false);
            yield return null;

            Assert.That(bootstrap.StageController.State, Is.EqualTo(StageState.Paused));
            Assert.That(bootstrap.StageController.StateBeforePause, Is.EqualTo(StageState.Ready));
            Assert.That(bootstrap.StageController.ActivePauseReasonCount, Is.EqualTo(2));
            Assert.That(pausedTransitionCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator LifecycleRelay_DuplicateActiveCallbacks_DoNotPublishTransitions()
        {
            MathGameBootstrap bootstrap = EnsureBootstrap();
            yield return null;

            var relay = bootstrap.GetComponent<ApplicationLifecycleRelay>();
            relay.SendMessage("OnApplicationPause", false);
            relay.SendMessage("OnApplicationFocus", true);

            int transitionCount = 0;
            bootstrap.StageController.StateChanged += _ => transitionCount++;

            relay.SendMessage("OnApplicationPause", false);
            relay.SendMessage("OnApplicationPause", false);
            relay.SendMessage("OnApplicationFocus", true);
            relay.SendMessage("OnApplicationFocus", true);

            Assert.That(bootstrap.StageController.State, Is.EqualTo(StageState.Ready));
            Assert.That(transitionCount, Is.Zero);

            Object.Destroy(bootstrap.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DestroyingBootstrap_ExitsStageAndReleasesSingletonGuard()
        {
            MathGameBootstrap first = EnsureBootstrap();
            yield return null;

            StageController firstStage = first.StageController;
            Object.Destroy(first.gameObject);
            yield return null;

            Assert.That(firstStage.State, Is.EqualTo(StageState.Exited));

            var secondObject = new GameObject("SecondBootstrap");
            var second = secondObject.AddComponent<MathGameBootstrap>();
            yield return null;

            Assert.That(second.StageController.State, Is.EqualTo(StageState.Ready));

            Object.Destroy(secondObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DuplicateBootstrap_DoesNotStealLifecycleOwnership()
        {
            MathGameBootstrap owner = EnsureBootstrap();
            yield return null;

            var ownerRelay = owner.GetComponent<ApplicationLifecycleRelay>();
            ownerRelay.SendMessage("OnApplicationPause", true);
            Assert.That(owner.StageController.State, Is.EqualTo(StageState.Paused));

            var duplicateObject = new GameObject("DuplicateBootstrap");
            MathGameBootstrap duplicate = duplicateObject.AddComponent<MathGameBootstrap>();
            yield return null;

            Assert.That(duplicate == null, Is.True);
            Assert.That(
                Object.FindObjectsByType<MathGameBootstrap>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None),
                Has.Length.EqualTo(1));
            Assert.That(owner.StageController.State, Is.EqualTo(StageState.Paused));
            Assert.That(
                owner.StageController.HasPauseReason(PauseReason.ApplicationBackground),
                Is.True);

            ownerRelay.SendMessage("OnApplicationPause", false);
            Assert.That(owner.StageController.State, Is.EqualTo(StageState.Ready));
        }

        private static MathGameBootstrap EnsureBootstrap()
        {
            MathGameBootstrap existing = Object.FindFirstObjectByType<MathGameBootstrap>();
            if (existing != null)
            {
                return existing;
            }

            var gameObject = new GameObject("MathGameBootstrapTest");
            return gameObject.AddComponent<MathGameBootstrap>();
        }

        private static void DestroyAllBootstraps()
        {
            MathGameBootstrap[] bootstraps = Object.FindObjectsByType<MathGameBootstrap>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (MathGameBootstrap bootstrap in bootstraps)
            {
                Object.Destroy(bootstrap.gameObject);
            }
        }
    }
}
