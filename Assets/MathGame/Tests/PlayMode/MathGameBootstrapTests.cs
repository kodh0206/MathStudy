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
        [UnityTest]
        public IEnumerator Bootstrap_InitializesBlankStage()
        {
            MathGameBootstrap bootstrap = EnsureBootstrap();

            yield return null;

            Assert.That(bootstrap.StageController, Is.Not.Null);
            Assert.That(bootstrap.TimeProvider, Is.Not.Null);
            Assert.That(bootstrap.RandomSource, Is.Not.Null);
            Assert.That(bootstrap.StageController.State, Is.EqualTo(StageState.PlayerInput));

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
            Assert.That(bootstrap.StageController.State, Is.EqualTo(StageState.PlayerInput));

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

            Assert.That(second.StageController.State, Is.EqualTo(StageState.PlayerInput));

            Object.Destroy(secondObject);
            yield return null;
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
    }
}
