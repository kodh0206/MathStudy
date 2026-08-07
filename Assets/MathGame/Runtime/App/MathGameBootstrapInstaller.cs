using UnityEngine;

namespace MathGame.App
{
    public static class MathGameBootstrapInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (Object.FindFirstObjectByType<MathGameBootstrap>() != null)
            {
                return;
            }

            var bootstrapObject = new GameObject(nameof(MathGameBootstrap));
            bootstrapObject.AddComponent<MathGameBootstrap>();
        }
    }
}
