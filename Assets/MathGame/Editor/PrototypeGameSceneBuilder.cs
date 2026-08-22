using System;
using System.Linq;
using MathGame.App;
using MathGame.Presentation.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

namespace MathGame.Editor.SceneBuilder
{
    /// <summary>
    /// Creates the authored entry-point and serialized prototype board views.
    /// Runtime gameplay binds logical state to these existing views; it does not create them.
    /// </summary>
    public static class PrototypeGameSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/GameScene.unity";
        const string ControllerName = "GameController";
        const string CompositionName = "GameSceneComposition";
        const string LegacyCompositionName = "PrototypeGameSceneComposition";
        const string GameRootName = "GameRoot";

        [MenuItem("MathGame/Build Prototype Scene", priority = 10)]
        public static void BuildPrototypeScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!PrototypePrefabBuilder.EnsurePrototypePrefabsForSceneBuild())
                return;
            var controller = FindRoot(scene, ControllerName) ?? new GameObject(ControllerName);
            EnsureComponent<ApplicationLifecycleRelay>(controller);
            EnsureComponent<MathGameBootstrap>(controller);

            var composition = FindRoot(scene, CompositionName) ?? FindRoot(scene, LegacyCompositionName);
            if (composition == null) composition = new GameObject(CompositionName);
            else if (composition.name == LegacyCompositionName) composition.name = CompositionName;
            EnsureComponent<PrototypeGameSceneController>(composition);
            EnsureComponent<PortraitOnlyPolicy>(composition);

            var gameRoot = FindRoot(scene, GameRootName);
            if (gameRoot != null && !IsManagedGameRoot(gameRoot))
                throw new InvalidOperationException(
                    "A root named GameRoot exists, but MathGame ownership could not be proven. " +
                    "It was not modified. Rename the user-authored object before building.");

            if (gameRoot == null || !IsCurrentManagedGameRoot(gameRoot))
            {
                if(gameRoot!=null)
                    Undo.DestroyObjectImmediate(gameRoot);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrototypePrefabBuilder.GameRootPath);
                if(prefab==null)throw new InvalidOperationException("GameRoot prefab was not created.");
                gameRoot=PrefabUtility.InstantiatePrefab(prefab,scene) as GameObject;
            }
            BakeSerializedBoardPreview(gameRoot);
            composition.GetComponent<PrototypeGameSceneController>().ConfigurePresentationHost(gameRoot.GetComponent<GamePresentationHost>());

            var camera = FindMainCamera(scene);
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }
            camera.orthographic = true;
            camera.orthographicSize = 4.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.transform.position = new Vector3(3.5f, 3.5f, -10f);

            EnsureInBuildSettings(ScenePath);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            var validation = ValidateScene(scene);
            if (validation == null)
                Debug.Log("MathGame production game scene built successfully. Open GameScene and press Play.");
            else
                Debug.LogError("MathGame prototype scene validation failed: " + validation);
        }

        [MenuItem("MathGame/Production/Build Game Scene", priority = 2)]
        public static void BuildProductionGameScene() => BuildPrototypeScene();

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
                Debug.Log("MathGame production game scene validation passed.");
            else
                Debug.LogError("MathGame prototype scene validation failed: " + error);
        }

        [MenuItem("MathGame/Production/Validate Game Scene", priority = 3)]
        public static void ValidateProductionGameScene() => ValidatePrototypeScene();

        static string ValidateScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return "GameScene is not loaded.";
            var controller = FindRoot(scene, ControllerName);
            if (controller == null) return "GameController is missing.";
            if (controller.GetComponent<ApplicationLifecycleRelay>() == null) return "ApplicationLifecycleRelay is missing.";
            if (controller.GetComponent<MathGameBootstrap>() == null) return "MathGameBootstrap is missing.";
            var composition = FindRoot(scene, CompositionName);
            if (composition == null) return "GameSceneComposition is missing.";
            if (composition.GetComponent<PrototypeGameSceneController>() == null) return "PrototypeGameSceneController is missing.";
            var gameRoot=FindRoot(scene,GameRootName);
            if(gameRoot==null||PrefabUtility.GetCorrespondingObjectFromSource(gameRoot)==null)return "GameRoot prefab instance is missing.";
            var ownership=gameRoot.GetComponent<PrototypeGeneratedRoot>();
            if(ownership==null||!ownership.IsMathGameOwned)return "GameRoot MathGame ownership marker is missing or invalid.";
            var host=gameRoot.GetComponent<GamePresentationHost>();if(host==null||!host.HasValidContext)return "GameRoot presentation context is incomplete.";
            if (host.CreateContext().OverlayRoot.GetComponentInChildren<RunResultPopupView>(true) == null)
                return "Serialized RunResultPopup is missing from OverlaySlot.";
            if (host.CreateContext().OverlayRoot.GetComponentInChildren<StartScreenView>(true) == null)
                return "Serialized StartView is missing from OverlaySlot.";
            if (gameRoot.GetComponentInChildren<EventSystem>(true) == null)
                return "Serialized EventSystem is missing from GameRoot.";
            if (FindMainCamera(scene) == null) return "A tagged Main Camera is missing.";
            if (!EditorBuildSettings.scenes.Any(item => item.path == ScenePath && item.enabled)) return "GameScene is not enabled in Build Settings.";
            return null;
        }

        static GameObject FindRoot(Scene scene, string name) =>
            scene.GetRootGameObjects().FirstOrDefault(item => item.name == name);

        static bool IsManagedGameRoot(GameObject root)
        {
            var marker = root.GetComponent<PrototypeGeneratedRoot>();
            if (marker != null && marker.IsMathGameOwned) return true;

            var contract = root.GetComponent<PresentationPrefabContract>();
            if (contract != null && contract.ContractId == GameRootName) return true;

            var source = PrefabUtility.GetCorrespondingObjectFromSource(root);
            if (source != null && AssetDatabase.GetAssetPath(source) == PrototypePrefabBuilder.GameRootPath)
                return true;

            // Strict legacy signature: both MathGame presentation components plus the
            // exact generated hierarchy. Names alone are deliberately insufficient.
            var gameplay = root.transform.Find("GameplayRoot");
            var boardSlot = gameplay != null ? gameplay.Find("BoardSlot") : null;
            var uiRoot = root.transform.Find("UIRoot");
            return gameplay != null && boardSlot != null && uiRoot != null &&
                   root.GetComponentInChildren<GameplayPresentationRoot>(true) != null &&
                   root.GetComponentInChildren<PrototypeUILayout>(true) != null;
        }

        static bool IsCurrentManagedGameRoot(GameObject root)
        {
            var marker = root.GetComponent<PrototypeGeneratedRoot>();
            var source = PrefabUtility.GetCorrespondingObjectFromSource(root);
            return marker != null && marker.IsMathGameOwned &&
                   source != null && AssetDatabase.GetAssetPath(source) == PrototypePrefabBuilder.GameRootPath &&
                   root.GetComponent<GamePresentationHost>()?.HasValidContext == true;
        }

        static void BakeSerializedBoardPreview(GameObject gameRoot)
        {
            var boardView = gameRoot.GetComponent<GamePresentationHost>()?.BoardView;
            if (boardView == null)
                throw new InvalidOperationException("Managed GameRoot has no serialized BoardView.");

            foreach (var cell in boardView.GetComponentsInChildren<PrototypeCellView>(true))
            {
                var position = cell.Position;
                var visible = position.Column >= 0 && position.Column < 5 &&
                              position.Row >= 0 && position.Row < 5;
                Undo.RecordObject(cell.gameObject, "Bake MathGame board preview");
                if (visible)
                {
                    cell.SetGridLayout(0, 0, 5, 5, 6f);
                    var value = 1 + ((position.Column * 3 + position.Row * 2) % 4);
                    var obstacle = position.Column == 1 && position.Row == 1 ? "D" :
                        position.Column == 2 && position.Row == 2 ? "B2" : string.Empty;
                    cell.ConfigureScenePreview(true, value, obstacle);
                }
                else cell.ConfigureScenePreview(false, 0, string.Empty);
                EditorUtility.SetDirty(cell.gameObject);
            }
        }

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
