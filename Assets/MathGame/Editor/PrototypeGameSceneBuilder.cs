using System;
using System.Linq;
using MathGame.App;
using MathGame.Presentation.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MathGame.Editor.SceneBuilder
{
    /// <summary>
    /// Creates the small authored entry-point required by the runtime prototype composition.
    /// Gameplay state and generated placeholder views remain runtime-owned.
    /// </summary>
    public static class PrototypeGameSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/GameScene.unity";
        const string ControllerName = "GameController";
        const string CompositionName = "PrototypeGameSceneComposition";
        const string GameRootName = "GameRoot";

        [MenuItem("MathGame/Build Prototype Scene", priority = 10)]
        public static void BuildPrototypeScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            PrototypePrefabBuilder.EnsurePrototypePrefabs();
            var controller = FindRoot(scene, ControllerName) ?? new GameObject(ControllerName);
            EnsureComponent<ApplicationLifecycleRelay>(controller);
            EnsureComponent<MathGameBootstrap>(controller);

            var composition = FindRoot(scene, CompositionName) ?? new GameObject(CompositionName);
            EnsureComponent<PrototypeGameSceneController>(composition);
            EnsureComponent<PortraitOnlyPolicy>(composition);

            var gameRoot=FindRoot(scene,GameRootName);
            if(gameRoot!=null&&gameRoot.GetComponent<GamePresentationHost>()==null)
                throw new InvalidOperationException("A user-authored root named GameRoot already exists. Rename it before building; no user object was modified.");
            if(gameRoot==null)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrototypePrefabBuilder.GameRootPath);
                if(prefab==null)throw new InvalidOperationException("GameRoot prefab was not created.");
                gameRoot=PrefabUtility.InstantiatePrefab(prefab,scene) as GameObject;
            }
            composition.GetComponent<PrototypeGameSceneController>().ConfigurePresentationHost(gameRoot.GetComponent<GamePresentationHost>());

            var camera = FindMainCamera(scene);
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            EnsureInBuildSettings(ScenePath);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            var validation = ValidateScene(scene);
            if (validation == null)
                Debug.Log("MathGame prototype scene built successfully. Open GameScene and press Play.");
            else
                Debug.LogError("MathGame prototype scene validation failed: " + validation);
        }

        [MenuItem("MathGame/Validate Prototype Scene", priority = 11)]
        public static void ValidatePrototypeScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            var scene = SceneManager.GetActiveScene().path == ScenePath
                ? SceneManager.GetActiveScene()
                : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var error = ValidateScene(scene);
            if (error == null)
                Debug.Log("MathGame prototype scene validation passed.");
            else
                Debug.LogError("MathGame prototype scene validation failed: " + error);
        }

        static string ValidateScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return "GameScene is not loaded.";
            var controller = FindRoot(scene, ControllerName);
            if (controller == null) return "GameController is missing.";
            if (controller.GetComponent<ApplicationLifecycleRelay>() == null) return "ApplicationLifecycleRelay is missing.";
            if (controller.GetComponent<MathGameBootstrap>() == null) return "MathGameBootstrap is missing.";
            var composition = FindRoot(scene, CompositionName);
            if (composition == null) return "PrototypeGameSceneComposition is missing.";
            if (composition.GetComponent<PrototypeGameSceneController>() == null) return "PrototypeGameSceneController is missing.";
            var gameRoot=FindRoot(scene,GameRootName);
            if(gameRoot==null||PrefabUtility.GetCorrespondingObjectFromSource(gameRoot)==null)return "GameRoot prefab instance is missing.";
            var host=gameRoot.GetComponent<GamePresentationHost>();if(host==null||!host.HasValidContext)return "GameRoot presentation context is incomplete.";
            if (FindMainCamera(scene) == null) return "A tagged Main Camera is missing.";
            if (!EditorBuildSettings.scenes.Any(item => item.path == ScenePath && item.enabled)) return "GameScene is not enabled in Build Settings.";
            return null;
        }

        static GameObject FindRoot(Scene scene, string name) =>
            scene.GetRootGameObjects().FirstOrDefault(item => item.name == name);

        static Camera FindMainCamera(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                    if (camera.CompareTag("MainCamera")) return camera;
            return null;
        }

        static T EnsureComponent<T>(GameObject target) where T : Component =>
            target.GetComponent<T>() ?? Undo.AddComponent<T>(target);

        static void EnsureInBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            var index = scenes.FindIndex(item => item.path == scenePath);
            if (index < 0) scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            else if (!scenes[index].enabled) scenes[index] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
