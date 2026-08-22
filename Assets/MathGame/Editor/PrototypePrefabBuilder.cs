using System;
using System.Collections.Generic;
using System.IO;
using MathGame.Presentation.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace MathGame.Editor.SceneBuilder
{
    public static class PrototypePrefabBuilder
    {
        const int ContractVersion=13;
        public const string Root = "Assets/MathGame/Prefabs";
        public const string GameRootPath = Root + "/Core/GameRoot.prefab";
        public const string BoardPath = Root + "/Board/Board.prefab";
        public const string CellPath = Root + "/Board/Cell.prefab";
        public const string BlockPath = Root + "/Board/Block.prefab";
        public const string HudPath = Root + "/UI/HUD.prefab";
        public const string RunResultPopupPath = Root + "/UI/RunResultPopup.prefab";
        public const string StartViewPath = Root + "/UI/StartView.prefab";
        public const string RegistryPath = Root + "/MathGamePrefabRegistry.asset";

        [MenuItem("MathGame/Build Prototype Prefabs",priority=9)]
        public static void EnsurePrototypePrefabs()
        {
            EnsurePrototypePrefabsForSceneBuild();
        }

        [MenuItem("MathGame/Production/Validate Production Prefabs", priority=1)]
        public static void ValidateProductionPrefabs() => EnsurePrototypePrefabs();

        [MenuItem("MathGame/Migrate Authored Run HUD %#h", priority = 11)]
        public static void MigrateAuthoredRunHud()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
            if (prefab == null)
                throw new InvalidOperationException("HUD prefab is missing: " + HudPath);

            var contents = PrefabUtility.LoadPrefabContents(HudPath);
            try
            {
                var contract = contents.GetComponent<PresentationPrefabContract>();
                if (contract == null)
                {
                    var yaml = File.ReadAllText(HudPath);
                    if (!yaml.Contains("MathGame.Presentation.Unity.PresentationPrefabContract") ||
                        !yaml.Contains("contractId: HUD"))
                        throw new InvalidOperationException("HUD prefab ownership could not be proven; it was not modified.");
                    var missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(contents);
                    if (missing > 1)
                        throw new InvalidOperationException("HUD ownership repair found multiple missing scripts and stopped safely: " + missing + ".");
                    if (missing == 1) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(contents);
                    contract = contents.AddComponent<PresentationPrefabContract>();
                }
                EnsureRunHudHierarchy(contents, true);
                contract.Configure("HUD", ContractVersion);
                if (PrefabUtility.SaveAsPrefabAsset(contents, HudPath) == null)
                    throw new InvalidOperationException("Failed to save the authored Run HUD prefab migration.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RemoveLegacyPresentationFromGameRoot();
            Debug.Log("MathGame authored Run HUD migrated: Current target/score plus Time and Fever progress bars are serialized in HUD.prefab.");
        }

        static void RemoveLegacyPresentationFromGameRoot()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameRootPath);
            if (prefab == null) return;
            var marker = prefab.GetComponent<PrototypeGeneratedRoot>();
            if (marker == null || !marker.IsMathGameOwned)
                throw new InvalidOperationException("GameRoot ownership could not be proven; legacy presentation was preserved.");
            var contents = PrefabUtility.LoadPrefabContents(GameRootPath);
            try
            {
                foreach (var path in new[]
                {
                    "UIRoot/PrototypeCanvas/SafeArea/OverlaySlot/StageClearPopup",
                    "UIRoot/PrototypeCanvas/SafeArea/BottomSlot/BottomHUD/Actions/Continue",
                    "UIRoot/PrototypeCanvas/SafeArea/BottomSlot/BottomHUD/Actions/Retry",
                    "UIRoot/PrototypeCanvas/SafeArea/BottomSlot/BottomHUD/Actions/Abandon",
                    "UIRoot/PrototypeCanvas/SafeArea/BottomSlot/BottomHUD/Actions/Restart"
                })
                {
                    var obsolete = contents.transform.Find(path);
                    if (obsolete != null) UnityEngine.Object.DestroyImmediate(obsolete.gameObject);
                }
                if (PrefabUtility.SaveAsPrefabAsset(contents, GameRootPath) == null)
                    throw new InvalidOperationException("Failed to save GameRoot after removing legacy presentation.");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            AssetDatabase.SaveAssets();
        }

        [InitializeOnLoadMethod]
        static void ScheduleAuthoredRunHudMigration()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
                var contract = prefab != null ? prefab.GetComponent<PresentationPrefabContract>() : null;
                try
                {
                    if (prefab != null && prefab.GetComponent<RunHUDView>()?.IsComplete == true &&
                        contract != null && contract.Version >= ContractVersion)
                        RemoveLegacyPresentationFromGameRoot();
                    else if (prefab != null)
                        MigrateAuthoredRunHud();
                }
                catch (Exception exception)
                {
                    Debug.LogError("MathGame could not migrate the authored Run HUD. Run MathGame/Migrate Authored Run HUD after resolving compile errors.\n" + exception);
                }
            };
        }

        public static bool EnsurePrototypePrefabsForSceneBuild()
        {
            MathGameLocalizationBuilder.Build();
            RepairLegacyContractSerialization();
            MigrateP10AContractsInPlace();
            var legacy = FindLegacyManagedPrefabs();
            if (legacy.Count > 0)
            {
                var message = "MathGame generated prefab contracts require migration from an earlier version:\n\n" +
                              string.Join("\n", legacy) +
                              "\n\nThis replaces only the managed prototype prefab set. Continue?";
                if (!EditorUtility.DisplayDialog("Migrate MathGame prototype prefabs?", message, "Migrate", "Cancel"))
                {
                    Debug.LogWarning("MathGame prototype prefab migration was cancelled; the scene was not modified.");
                    return false;
                }
                DeleteManagedPrefabSet();
            }
            BuildCurrentPrefabSet();
            return true;
        }

        static void BuildCurrentPrefabSet()
        {
            EnsureFolders();
            CreateIfMissing(BlockPath,CreateBlock);
            CreateIfMissing(CellPath,CreateCell);
            CreateIfMissing(BoardPath,CreateBoard);
            CreateIfMissing(RunResultPopupPath,CreateRunResultPopup);
            CreateIfMissing(StartViewPath,CreateStartView);
            CreateIfMissing(HudPath,CreateHud);
            EnsureRegistry();
            CreateIfMissing(GameRootPath,CreateGameRoot);
            EnsureRunResultPopupInGameRoot();
            EnsureStartViewInGameRoot();
            EnsureEventSystemInGameRoot();
            EnsureGameRootOwnershipMarker();
            EnsureRegistry();
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            Debug.Log("MathGame prototype prefabs are available. Existing prefab assets were preserved.");
        }

        static void MigrateP10AContractsInPlace()
        {
            var candidates = new List<string>();
            foreach (var path in ManagedPrefabPaths())
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                var contract = prefab.GetComponent<PresentationPrefabContract>();
                if (contract == null || contract.ContractId != Path.GetFileNameWithoutExtension(path) ||
                    contract.Version < 7 || contract.Version >= ContractVersion)
                    continue;
                if (path == GameRootPath)
                {
                    var marker = prefab.GetComponent<PrototypeGeneratedRoot>();
                    if (marker == null || !marker.IsMathGameOwned)
                        throw new InvalidOperationException("GameRoot v7 contract exists without a valid MathGame ownership marker; no prefab was modified.");
                }
                candidates.Add(path);
            }

            foreach (var path in candidates)
            {
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (path == BoardPath && contents.GetComponentInChildren<SelectionLineGraphic>(true) == null)
                    {
                        var selectionLine = UI("SelectionLine", typeof(CanvasRenderer), typeof(SelectionLineGraphic));
                        selectionLine.transform.SetParent(contents.transform, false);
                        Stretch(selectionLine.GetComponent<RectTransform>(), 0);
                        selectionLine.GetComponent<SelectionLineGraphic>().Configure(10f, new Color(.22f, .9f, 1f, .82f));
                    }
                    if (path == HudPath) EnsureRunHudHierarchy(contents);
                    if (path == RunResultPopupPath) EnsureRunResultHome(contents);
                    if (path == GameRootPath) UpgradeRunLayout(contents);
                    if (path == CellPath) UpgradeCellStyle(contents);
                    if (path == BoardPath) UpgradeBoardStyle(contents);
                    if (path == HudPath) ApplyDigitalCoreHudStyle(contents);
                    contents.GetComponent<PresentationPrefabContract>().Configure(Path.GetFileNameWithoutExtension(path), ContractVersion);
                    if (path == GameRootPath)
                    {
                        var marker = contents.GetComponent<PrototypeGeneratedRoot>();
                        marker.Configure(ContractVersion);
                    }
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            AssetDatabase.SaveAssets();
        }

        static void RepairLegacyContractSerialization()
        {
            foreach(var path in ManagedPrefabPaths())
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(prefab==null||prefab.GetComponent<PresentationPrefabContract>()!=null)continue;
                if(path==GameRootPath&&IsProvablyManagedGameRootPrefab(prefab))continue;
                var expectedId=Path.GetFileNameWithoutExtension(path);
                var yaml=File.ReadAllText(path);
                if(!yaml.Contains("MathGame.Presentation.Unity.PresentationPrefabContract")||
                   !yaml.Contains("contractId: "+expectedId))continue;
                var contents=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var missing=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(contents);
                    if(missing!=1)throw new InvalidOperationException(
                        "Managed prefab contract serialization could not be repaired safely; expected exactly one missing root script: "+path);
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(contents);
                    var contract=contents.AddComponent<PresentationPrefabContract>();
                    var version=ReadLegacyContractVersion(yaml);
                    contract.Configure(expectedId,version);
                    if(PrefabUtility.SaveAsPrefabAsset(contents,path)==null)
                        throw new InvalidOperationException("Failed to repair managed prefab contract: "+path);
                }
                finally{PrefabUtility.UnloadPrefabContents(contents);}
            }
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        }

        static int ReadLegacyContractVersion(string yaml)
        {
            const string marker="  version: ";
            var index=yaml.IndexOf(marker,StringComparison.Ordinal);
            if(index<0)return 7;
            index+=marker.Length;
            var end=yaml.IndexOfAny(new[]{'\r','\n'},index);
            var value=end<0?yaml.Substring(index):yaml.Substring(index,end-index);
            return int.TryParse(value.Trim(),out var version)&&version>0?version:7;
        }

        static void UpgradeManagedHudLayout()
        {
            var hud = PrefabUtility.LoadPrefabContents(HudPath);
            try
            {
                var mainStats = hud.transform.Find("MainStats") as RectTransform;
                var resources = hud.transform.Find("Resources") as RectTransform;
                var objectives = hud.transform.Find("Objectives") as RectTransform;
                Set(hud.GetComponent<RectTransform>(), 0, 1, 1, 1, 24, -404, -24, -24);
                if (mainStats != null) Set(mainStats, 0, 1, 1, 1, 24, -184, -24, -82);
                if (resources != null) Set(resources, 0, 1, 1, 1, 24, -274, -24, -194);
                if (objectives != null)
                {
                    Set(objectives, 0, 0, 1, 0, 24, 11, -24, 109);
                    var layout = objectives.GetComponent<VerticalLayoutGroup>();
                    if (layout != null)
                    {
                        layout.padding = new RectOffset(0, 0, 0, 0);
                        layout.spacing = 6;
                        layout.childAlignment = TextAnchor.UpperLeft;
                        layout.childControlWidth = true;
                        layout.childForceExpandWidth = true;
                        layout.childControlHeight = true;
                        layout.childForceExpandHeight = false;
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(hud, HudPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(hud); }

        }

        [MenuItem("MathGame/Development/Recreate Prototype Prefabs",priority=100)]
        public static void RecreatePrototypePrefabs()
        {
            if(!EditorUtility.DisplayDialog("Recreate MathGame prototype prefabs?","This replaces the managed prototype prefab set and discards visual edits in those assets. Use only for an explicit contract migration.","Recreate","Cancel"))return;
            DeleteManagedPrefabSet();
            BuildCurrentPrefabSet();
            Debug.Log("MathGame prototype prefabs were explicitly recreated for contract version "+ContractVersion+".");
        }

        static List<string> FindLegacyManagedPrefabs()
        {
            var legacy = new List<string>();
            foreach (var path in ManagedPrefabPaths())
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                var contract = prefab.GetComponent<PresentationPrefabContract>();
                var expectedId = Path.GetFileNameWithoutExtension(path);
                if (contract == null || contract.ContractId != expectedId)
                {
                    if (path == GameRootPath && IsProvablyManagedGameRootPrefab(prefab))
                    {
                        var marker = prefab.GetComponent<PrototypeGeneratedRoot>();
                        if (marker != null && marker.IsMathGameOwned && marker.SchemaVersion == ContractVersion)
                        {
                            RepairCurrentGameRootContract();
                            continue;
                        }
                        legacy.Add(path + " (legacy MathGame root -> v" + ContractVersion + ")");
                        continue;
                    }
                    throw new InvalidOperationException(
                        "An asset exists at a managed prototype path, but MathGame ownership could not be proven. " +
                        "It was preserved: " + path);
                }
                if (contract.Version == ContractVersion) continue;
                if (contract.Version > 0 && contract.Version < ContractVersion)
                    legacy.Add(path + " (v" + contract.Version + " -> v" + ContractVersion + ")");
                else
                    throw new InvalidOperationException(
                        "Existing managed prefab has an unsupported contract version and was preserved: " + path +
                        " (v" + contract.Version + ").");
            }
            return legacy;
        }

        static bool IsProvablyManagedGameRootPrefab(GameObject root)
        {
            var marker = root.GetComponent<PrototypeGeneratedRoot>();
            if (marker != null && marker.IsMathGameOwned) return true;
            var gameplay = root.transform.Find("GameplayRoot");
            var boardSlot = gameplay != null ? gameplay.Find("BoardSlot") : null;
            return gameplay != null && boardSlot != null && root.transform.Find("UIRoot") != null &&
                   root.GetComponentInChildren<GameplayPresentationRoot>(true) != null &&
                   root.GetComponentInChildren<PrototypeUILayout>(true) != null;
        }

        static void RepairCurrentGameRootContract()
        {
            var contents = PrefabUtility.LoadPrefabContents(GameRootPath);
            try
            {
                var contract = contents.GetComponent<PresentationPrefabContract>() ?? contents.AddComponent<PresentationPrefabContract>();
                contract.Configure("GameRoot", ContractVersion);
                PrefabUtility.SaveAsPrefabAsset(contents, GameRootPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        static string[] ManagedPrefabPaths() => new[]
        {
            GameRootPath, BoardPath, CellPath, BlockPath, HudPath,
            RunResultPopupPath, StartViewPath
        };

        static void DeleteManagedPrefabSet()
        {
            foreach (var path in ManagedPrefabPaths()) AssetDatabase.DeleteAsset(path);
            AssetDatabase.DeleteAsset(RegistryPath);
            AssetDatabase.SaveAssets();
        }

        static void EnsureFolders()
        {
            Directory.CreateDirectory(Root+"/Core");Directory.CreateDirectory(Root+"/Board");Directory.CreateDirectory(Root+"/UI");Directory.CreateDirectory(Root+"/Effects");
            // SaveAsPrefabAsset requires newly created folders to be known to the AssetDatabase.
            AssetDatabase.Refresh();
        }

        static void EnsureRegistry()
        {
            var registry=AssetDatabase.LoadAssetAtPath<MathGamePrefabRegistry>(RegistryPath);
            if(registry==null){registry=ScriptableObject.CreateInstance<MathGamePrefabRegistry>();AssetDatabase.CreateAsset(registry,RegistryPath);}
            registry.GameRootPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(GameRootPath);registry.BoardPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(BoardPath);
            registry.CellPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(CellPath);registry.BlockPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(BlockPath);
            registry.HudPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
            registry.RunResultPopupPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(RunResultPopupPath);
            registry.StartViewPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(StartViewPath);
            EditorUtility.SetDirty(registry);
        }

        static void EnsureRunResultPopupInGameRoot()
        {
            var contents = PrefabUtility.LoadPrefabContents(GameRootPath);
            try
            {
                var overlay = contents.transform.Find("UIRoot/PrototypeCanvas/SafeArea/OverlaySlot");
                if (overlay == null) throw new InvalidOperationException("Managed GameRoot OverlaySlot is missing.");
                if (overlay.GetComponentInChildren<RunResultPopupView>(true) != null) return;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RunResultPopupPath);
                var popup = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                popup.transform.SetParent(overlay, false);
                Stretch(popup.GetComponent<RectTransform>(), 0);
                popup.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(contents, GameRootPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        static void EnsureStartViewInGameRoot()
        {
            var contents = PrefabUtility.LoadPrefabContents(GameRootPath);
            try
            {
                var overlay = contents.transform.Find("UIRoot/PrototypeCanvas/SafeArea/OverlaySlot");
                if (overlay == null) throw new InvalidOperationException("Managed GameRoot OverlaySlot is missing.");
                if (overlay.GetComponentInChildren<StartScreenView>(true) == null)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StartViewPath);
                    if (prefab == null) throw new InvalidOperationException("Managed StartView prefab is missing.");
                    var view = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                    view.transform.SetParent(overlay, false);
                    Stretch(view.GetComponent<RectTransform>(), 0);
                    view.SetActive(true);
                }
                SetActive(contents.transform.Find("GameplayRoot"), false);
                SetActive(contents.transform.Find("UIRoot/PrototypeCanvas/SafeArea/TopSlot"), false);
                SetActive(contents.transform.Find("UIRoot/PrototypeCanvas/SafeArea/CenterSlot"), false);
                SetActive(contents.transform.Find("UIRoot/PrototypeCanvas/SafeArea/BottomSlot"), false);
                PrefabUtility.SaveAsPrefabAsset(contents, GameRootPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        static void SetActive(Transform value, bool active)
        {
            if (value != null) value.gameObject.SetActive(active);
        }

        static void EnsureRunResultHome(GameObject root)
        {
            var view = root.GetComponent<RunResultPopupView>();
            var panel = root.transform.Find("PopupPanel");
            if (view == null || panel == null) return;
            var playAgain = panel.Find("PlayAgainButton")?.GetComponent<Button>();
            var home = panel.Find("HomeButton")?.GetComponent<Button>();
            if (home == null)
            {
                var homeRoot=UI("HomeButton",typeof(CanvasRenderer),typeof(Image),typeof(Button));
                homeRoot.transform.SetParent(panel,false);
                homeRoot.GetComponent<Image>().color=new Color(.08f,.18f,.27f,1);
                Set(homeRoot.GetComponent<RectTransform>(),.62f,.06f,.88f,.25f,0,0,0,0);
                Stretch(Text("Label","HOME",24,TextAnchor.MiddleCenter,homeRoot.transform).rectTransform,8);
                home=homeRoot.GetComponent<Button>();
            }
            if (playAgain != null)
            {
                Set(playAgain.GetComponent<RectTransform>(),.12f,.06f,.58f,.25f,0,0,0,0);
                view.Configure(root.transform.Find("PopupPanel/Result")?.GetComponent<Text>(),playAgain,home);
            }
        }

        static void EnsureEventSystemInGameRoot()
        {
            var contents = PrefabUtility.LoadPrefabContents(GameRootPath);
            try
            {
                PrototypeUILayout.EnsureSerializedEventSystem(contents);
                PrefabUtility.SaveAsPrefabAsset(contents, GameRootPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }


        static void EnsureGameRootOwnershipMarker()
        {
            var contents = PrefabUtility.LoadPrefabContents(GameRootPath);
            try
            {
                var marker = contents.GetComponent<PrototypeGeneratedRoot>();
                if (marker != null && marker.IsMathGameOwned && marker.SchemaVersion == ContractVersion)
                    return;
                marker = marker ?? contents.AddComponent<PrototypeGeneratedRoot>();
                marker.Configure(ContractVersion);
                PrefabUtility.SaveAsPrefabAsset(contents, GameRootPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        static void CreateIfMissing(string path,Func<GameObject> create)
        {
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(existing!=null){ValidateContract(path,existing);return;}
            var root=create();
            try
            {
                var contract=root.GetComponent<PresentationPrefabContract>()??root.AddComponent<PresentationPrefabContract>();
                contract.Configure(Path.GetFileNameWithoutExtension(path),ContractVersion);
                var saved=PrefabUtility.SaveAsPrefabAsset(root,path);
                if(saved==null)throw new InvalidOperationException("Unity failed to save managed prefab: "+path);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }

        static void ValidateContract(string path,GameObject prefab)
        {
            var contract=prefab.GetComponent<PresentationPrefabContract>();
            if(contract==null||contract.Version!=ContractVersion||contract.ContractId!=Path.GetFileNameWithoutExtension(path))
                throw new InvalidOperationException("Existing prefab is incompatible and was preserved: "+path+". Move/rename it, then run Build Prototype Prefabs to create the current contract.");
        }

        static GameObject CreateBlock()
        {
            var root=new GameObject("Block");
            var background=GameObject.CreatePrimitive(PrimitiveType.Quad);background.name="Background";background.transform.SetParent(root.transform,false);background.transform.localScale=Vector3.one*.88f;UnityEngine.Object.DestroyImmediate(background.GetComponent<Collider>());
            var value=new GameObject("ValueText");value.transform.SetParent(root.transform,false);value.transform.localPosition=Vector3.back*.02f;var text=value.AddComponent<TextMesh>();ConfigureTextMesh(text,"1",.22f,48,new Color(.1f,.32f,.72f),20);
            new GameObject("OptionalEffectRoot").transform.SetParent(root.transform,false);
            return root;
        }

        static GameObject CreateCell()
        {
            var root=UI("Cell",typeof(CanvasRenderer),typeof(Image),typeof(Outline),typeof(PrototypeCellView));
            var background=root.GetComponent<Image>();background.color=new Color(.035f,.09f,.16f,.98f);
            var border=root.GetComponent<Outline>();border.effectColor=new Color(.12f,.48f,.64f,.85f);border.effectDistance=new Vector2(2,-2);
            var block=UI("BlockRoot");block.transform.SetParent(root.transform,false);Stretch(block.GetComponent<RectTransform>(),8);
            var value=Text("ValueText","",40,TextAnchor.MiddleCenter,block.transform);value.fontStyle=FontStyle.Bold;value.color=new Color(.90f,.97f,1f);Stretch(value.rectTransform,2);
            var obstacle=UI("ObstacleRoot");obstacle.transform.SetParent(root.transform,false);Stretch(obstacle.GetComponent<RectTransform>(),4);
            var obstacleText=Text("ObstacleText","",22,TextAnchor.MiddleCenter,obstacle.transform);obstacleText.color=new Color(1f,.28f,.25f);Stretch(obstacleText.rectTransform,2);
            root.GetComponent<PrototypeCellView>().Configure(0,0,background,value,obstacleText,block,obstacle);return root;
        }

        static GameObject CreateBoard()
        {
            var root=UI("BoardView",typeof(CanvasRenderer),typeof(Image),typeof(Outline),typeof(AudioSource),typeof(GameplayPresentationRoot),typeof(PlaceholderPresentationFeedback));
            var boardBackground=root.GetComponent<Image>();boardBackground.color=new Color(.018f,.045f,.08f,.96f);boardBackground.raycastTarget=false;
            var boardOutline=root.GetComponent<Outline>();boardOutline.effectColor=new Color(.08f,.58f,.72f,.70f);boardOutline.effectDistance=new Vector2(3,-3);
            var audio=root.GetComponent<AudioSource>();audio.playOnAwake=false;audio.spatialBlend=0f;audio.volume=.22f;
            var cells=UI("CellRoot");cells.transform.SetParent(root.transform,false);Stretch(cells.GetComponent<RectTransform>(),0);
            var blocks=UI("BlockRoot");blocks.transform.SetParent(root.transform,false);Stretch(blocks.GetComponent<RectTransform>(),0);
            var effects=UI("EffectRoot");effects.transform.SetParent(root.transform,false);Stretch(effects.GetComponent<RectTransform>(),0);
            var selectionLine=UI("SelectionLine",typeof(CanvasRenderer),typeof(SelectionLineGraphic));
            selectionLine.transform.SetParent(root.transform,false);
            Stretch(selectionLine.GetComponent<RectTransform>(),0);
            selectionLine.GetComponent<SelectionLineGraphic>().Configure(10f,new Color(.18f,.92f,1f,.88f),34f);
            selectionLine.transform.SetAsLastSibling();
            var cellPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(CellPath);
            for(var row=0;row<8;row++)for(var column=0;column<8;column++)
            {
                var cell=PrefabUtility.InstantiatePrefab(cellPrefab) as GameObject;cell.name="Cell_"+column+"_"+row;cell.transform.SetParent(cells.transform,false);
                cell.GetComponent<PrototypeCellView>().Configure(column,row,cell.GetComponent<Image>(),cell.transform.Find("BlockRoot/ValueText")?.GetComponent<Text>(),cell.transform.Find("ObstacleRoot/ObstacleText")?.GetComponent<Text>(),cell.transform.Find("BlockRoot")?.gameObject,cell.transform.Find("ObstacleRoot")?.gameObject);
            }
            EnsureBoardReconfigurationView(root,boardOutline);
            return root;
        }

        static void ConfigureTextMesh(TextMesh text,string content,float characterSize,int fontSize,Color color,int sortingOrder)
        {
            text.text=content;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;
            text.characterSize=characterSize;text.fontSize=fontSize;text.color=color;
            text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var renderer=text.GetComponent<MeshRenderer>();renderer.sortingOrder=sortingOrder;renderer.sharedMaterial=text.font.material;
        }

        static GameObject CreateHud()
        {
            var root=UI("HUD",typeof(Image));root.GetComponent<Image>().color=new Color(.035f,.055f,.09f,.96f);
            var title=Text("Title","MATH GAME PROTOTYPE",38,TextAnchor.MiddleCenter,root.transform);Set(title.rectTransform,0,1,1,1,20,-72,-20,-14);
            var stats=UI("MainStats",typeof(GridLayoutGroup));stats.transform.SetParent(root.transform,false);Set(stats.GetComponent<RectTransform>(),0,1,1,1,24,-184,-24,-82);
            var grid=stats.GetComponent<GridLayoutGroup>();grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=3;grid.cellSize=new Vector2(320,92);grid.spacing=new Vector2(12,0);
            foreach(var name in new[]{"Target","Moves","Score"}){var stat=UI(name,typeof(Image));stat.transform.SetParent(stats.transform,false);stat.GetComponent<Image>().color=new Color(.11f,.17f,.26f,1);Stretch(Text("Value",name.ToUpperInvariant()+"\n0",30,TextAnchor.MiddleCenter,stat.transform).rectTransform,8);}
            var resources=UI("Resources");resources.transform.SetParent(root.transform,false);Set(resources.GetComponent<RectTransform>(),0,1,1,1,24,-264,-24,-194);
            var restoration=Text("Restoration","RESTORATION  0/100",29,TextAnchor.MiddleLeft,resources.transform);Set(restoration.rectTransform,0,0,.5f,1,0,0,-10,0);
            var fever=Text("Fever","FEVER  0/50",29,TextAnchor.MiddleRight,resources.transform);Set(fever.rectTransform,.5f,0,1,1,10,0,0,0);
            var runStats=UI("RunStats",typeof(GridLayoutGroup));runStats.transform.SetParent(root.transform,false);Set(runStats.GetComponent<RectTransform>(),0,1,1,1,24,-374,-24,-194);
            var runGrid=runStats.GetComponent<GridLayoutGroup>();runGrid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;runGrid.constraintCount=2;runGrid.cellSize=new Vector2(486,84);runGrid.spacing=new Vector2(12,10);runGrid.childAlignment=TextAnchor.MiddleCenter;
            foreach(var pair in new[]{("Time","TIME\n30.0s"),("Fever","FEVER\n0/50"),("Combo","COMBO\nx0"),("Tier","TIER\n1")})
            {var item=UI(pair.Item1,typeof(Image));item.transform.SetParent(runStats.transform,false);item.GetComponent<Image>().color=new Color(.08f,.13f,.2f,1);Stretch(Text("Value",pair.Item2,25,TextAnchor.MiddleCenter,item.transform).rectTransform,6);}
            runStats.SetActive(false);
            var objectives=UI("Objectives",typeof(VerticalLayoutGroup));objectives.transform.SetParent(root.transform,false);Set(objectives.GetComponent<RectTransform>(),0,0,1,1,24,28,-24,116);
            var vertical=objectives.GetComponent<VerticalLayoutGroup>();vertical.spacing=6;vertical.childControlHeight=true;vertical.childForceExpandHeight=true;
            EnsureRunHudHierarchy(root);
            ApplyDigitalCoreHudStyle(root);
            return root;
        }

        static void EnsureRunHudHierarchy(GameObject hud, bool forceRebuild = false)
        {
            if (!forceRebuild && hud.GetComponent<RunHUDView>()?.IsComplete == true) return;
            foreach (var legacyName in new[] { "Title", "MainStats", "Resources", "RunStats", "Objectives" })
            {
                var legacy = hud.transform.Find(legacyName);
                if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy.gameObject);
            }
            var existing = hud.transform.Find("RunHUD");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

            var root=UI("RunHUD");root.transform.SetParent(hud.transform,false);Stretch(root.GetComponent<RectTransform>(),12);

            var survival=UI("SurvivalPanel");survival.transform.SetParent(root.transform,false);Set(survival.GetComponent<RectTransform>(),.02f,.73f,.62f,.98f,0,0,0,0);
            var timeLabel=Text("Label","TIME",21,TextAnchor.UpperLeft,survival.transform);timeLabel.color=new Color(.35f,.82f,1f);Set(timeLabel.rectTransform,0,.52f,.45f,1,4,0,0,0);
            var timeValue=Text("Value","30.0",36,TextAnchor.UpperRight,survival.transform);timeValue.fontStyle=FontStyle.Bold;Set(timeValue.rectTransform,.42f,.48f,1,1,0,0,-4,0);
            var timeTrack=Panel("Gauge",survival.transform,new Color(.06f,.14f,.22f,1));Set(timeTrack.GetComponent<RectTransform>(),0,.08f,1,.34f,4,0,-4,0);
            var timeFill=UI("Fill",typeof(CanvasRenderer),typeof(Image));timeFill.transform.SetParent(timeTrack.transform,false);Stretch(timeFill.GetComponent<RectTransform>(),3);
            var timeImage=timeFill.GetComponent<Image>();timeImage.color=new Color(.18f,.88f,1f);timeImage.type=Image.Type.Filled;timeImage.fillMethod=Image.FillMethod.Horizontal;timeImage.fillOrigin=0;timeImage.fillAmount=1;

            var scorePanel=UI("ScorePanel");scorePanel.transform.SetParent(root.transform,false);Set(scorePanel.GetComponent<RectTransform>(),.64f,.73f,.98f,.98f,0,0,0,0);
            var score=Text("Score","SCORE  12,430",27,TextAnchor.UpperRight,scorePanel.transform);score.fontStyle=FontStyle.Bold;Stretch(score.rectTransform,0);
            var tier=Text("Tier","TIER 1",17,TextAnchor.LowerRight,scorePanel.transform);tier.color=new Color(.58f,.68f,.78f);Set(tier.rectTransform,0,0,1,.42f,0,0,0,0);

            var targetPanel=UI("TargetPanel");targetPanel.transform.SetParent(root.transform,false);Set(targetPanel.GetComponent<RectTransform>(),.24f,.25f,.76f,.76f,0,0,0,0);
            var targetLabel=Text("Label","TARGET",21,TextAnchor.UpperCenter,targetPanel.transform);targetLabel.color=new Color(.35f,.82f,1f);Set(targetLabel.rectTransform,0,.70f,1,1,8,0,-8,0);
            var targetValue=Text("Value","8",92,TextAnchor.MiddleCenter,targetPanel.transform);targetValue.fontStyle=FontStyle.Bold;targetValue.color=new Color(.92f,.99f,1f);Set(targetValue.rectTransform,0,0,1,.82f,8,0,-8,0);

            var secondary=UI("SecondaryStats");secondary.transform.SetParent(root.transform,false);Set(secondary.GetComponent<RectTransform>(),.02f,.02f,.38f,.24f,0,0,0,0);
            var combo=Text("Combo","COMBO  x0",30,TextAnchor.MiddleLeft,secondary.transform);combo.fontStyle=FontStyle.Bold;Stretch(combo.rectTransform,4);

            var feverPanel=UI("FeverPanel");feverPanel.transform.SetParent(root.transform,false);Set(feverPanel.GetComponent<RectTransform>(),.52f,.02f,.98f,.24f,0,0,0,0);
            var fever=Text("Label","FEVER",20,TextAnchor.UpperLeft,feverPanel.transform);fever.color=new Color(1f,.72f,.18f);Set(fever.rectTransform,0,.46f,.38f,1,4,0,0,0);
            var feverTrack=Panel("Gauge",feverPanel.transform,new Color(.12f,.14f,.19f,1));Set(feverTrack.GetComponent<RectTransform>(),.36f,.25f,1,.72f,0,0,-4,0);
            var feverFill=UI("Fill",typeof(CanvasRenderer),typeof(Image));feverFill.transform.SetParent(feverTrack.transform,false);Stretch(feverFill.GetComponent<RectTransform>(),3);
            var feverImage=feverFill.GetComponent<Image>();feverImage.color=new Color(1f,.55f,.12f);feverImage.type=Image.Type.Filled;feverImage.fillMethod=Image.FillMethod.Horizontal;feverImage.fillOrigin=0;

            var view=hud.GetComponent<RunHUDView>()??hud.AddComponent<RunHUDView>();
            view.Configure(root,targetValue,timeValue,timeImage,score,combo,tier,fever,feverImage);
            root.SetActive(false);
        }

        static void UpgradeCellStyle(GameObject cell)
        {
            var image=cell.GetComponent<Image>();
            if(image!=null)image.color=new Color(.035f,.09f,.16f,.98f);
            var outline=cell.GetComponent<Outline>()??cell.AddComponent<Outline>();
            outline.effectColor=new Color(.12f,.48f,.64f,.85f);outline.effectDistance=new Vector2(2,-2);
            var value=cell.transform.Find("BlockRoot/ValueText")?.GetComponent<Text>();
            if(value!=null){value.color=new Color(.94f,.99f,1f);value.fontStyle=FontStyle.Bold;value.fontSize=46;}
            var obstacle=cell.transform.Find("ObstacleRoot/ObstacleText")?.GetComponent<Text>();
            if(obstacle!=null){obstacle.alignment=TextAnchor.MiddleCenter;obstacle.color=new Color(1f,.20f,.18f);obstacle.fontStyle=FontStyle.Bold;obstacle.fontSize=36;}
        }

        static void UpgradeBoardStyle(GameObject board)
        {
            var image=board.GetComponent<Image>()??board.AddComponent<Image>();
            image.color=new Color(.018f,.045f,.08f,.96f);image.raycastTarget=false;
            var outline=board.GetComponent<Outline>()??board.AddComponent<Outline>();
            outline.effectColor=new Color(.08f,.58f,.72f,.70f);outline.effectDistance=new Vector2(3,-3);
            var line=board.GetComponentInChildren<SelectionLineGraphic>(true);
            if(line!=null){line.Configure(10f,new Color(.18f,.92f,1f,.88f),34f);line.transform.SetAsLastSibling();}
            EnsureBoardReconfigurationView(board,outline);
        }

        static void EnsureBoardReconfigurationView(GameObject board,Outline outline)
        {
            var cellRoot=board.transform.Find("CellRoot");if(cellRoot==null)return;
            var group=cellRoot.GetComponent<CanvasGroup>()??cellRoot.gameObject.AddComponent<CanvasGroup>();
            var overlay=board.transform.Find("ReconfigurationOverlay")?.gameObject;
            if(overlay==null)
            {
                overlay=UI("ReconfigurationOverlay",typeof(CanvasRenderer),typeof(Image));overlay.transform.SetParent(board.transform,false);Stretch(overlay.GetComponent<RectTransform>(),0);
                var overlayImage=overlay.GetComponent<Image>();overlayImage.color=new Color(.01f,.08f,.12f,.16f);overlayImage.raycastTarget=false;
                var label=Text("Message","RECONFIGURING...",28,TextAnchor.UpperCenter,overlay.transform);label.fontStyle=FontStyle.Bold;label.color=new Color(.25f,.95f,1f);Set(label.rectTransform,0,.72f,1,.9f,20,0,-20,0);
                var scan=UI("ScanLine",typeof(CanvasRenderer),typeof(Image));scan.transform.SetParent(overlay.transform,false);var scanImage=scan.GetComponent<Image>();scanImage.color=new Color(.25f,.95f,1f,.82f);scanImage.raycastTarget=false;Set(scan.GetComponent<RectTransform>(),0,1,1,1,12,-4,-12,4);
            }
            var view=board.GetComponent<BoardReconfigurationView>()??board.AddComponent<BoardReconfigurationView>();
            view.Configure(group,overlay,overlay.transform.Find("ScanLine") as RectTransform,overlay.transform.Find("Message")?.GetComponent<Text>(),outline);
            overlay.SetActive(false);overlay.transform.SetAsLastSibling();
        }

        static void ApplyDigitalCoreHudStyle(GameObject hud)
        {
            var background=hud.GetComponent<Image>();
            if(background!=null){background.color=new Color(.012f,.035f,.065f,.97f);background.raycastTarget=false;}
            var run=hud.transform.Find("RunHUD");
            if(run==null)return;
            StyleText(run.Find("SurvivalPanel/Label"),22,new Color(.22f,.88f,1f),TextAnchor.UpperLeft,false);
            StyleText(run.Find("SurvivalPanel/Value"),44,Color.white,TextAnchor.UpperRight,true);
            StyleText(run.Find("ScorePanel/Score"),31,Color.white,TextAnchor.UpperRight,true);
            StyleText(run.Find("ScorePanel/Tier"),18,new Color(.48f,.68f,.78f),TextAnchor.LowerRight,false);
            StyleText(run.Find("TargetPanel/Label"),23,new Color(.22f,.88f,1f),TextAnchor.UpperCenter,false);
            StyleText(run.Find("TargetPanel/Value"),104,Color.white,TextAnchor.MiddleCenter,true);
            StyleText(run.Find("SecondaryStats/Combo"),32,Color.white,TextAnchor.MiddleLeft,true);
            StyleText(run.Find("FeverPanel/Label"),21,new Color(1f,.60f,.16f),TextAnchor.UpperLeft,true);
            var targetPanel=run.Find("TargetPanel")?.gameObject;
            if(targetPanel!=null)
            {
                var image=targetPanel.GetComponent<Image>()??targetPanel.AddComponent<Image>();
                image.color=new Color(.02f,.12f,.18f,.42f);image.raycastTarget=false;
                var outline=targetPanel.GetComponent<Outline>()??targetPanel.AddComponent<Outline>();
                outline.effectColor=new Color(.12f,.72f,.86f,.45f);outline.effectDistance=new Vector2(2,-2);
            }
            var feverPanel=run.Find("FeverPanel");
            var segments=feverPanel?.Find("Segments");
            if(feverPanel!=null&&segments==null)
            {
                var segmentRoot=UI("Segments",typeof(HorizontalLayoutGroup));segmentRoot.transform.SetParent(feverPanel,false);
                Set(segmentRoot.GetComponent<RectTransform>(),.36f,.25f,1,.72f,0,0,-4,0);
                var layout=segmentRoot.GetComponent<HorizontalLayoutGroup>();layout.spacing=5;layout.childControlWidth=true;layout.childForceExpandWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=true;
                for(var i=0;i<7;i++){var segment=UI("Segment_"+(i+1),typeof(CanvasRenderer),typeof(Image));segment.transform.SetParent(segmentRoot.transform,false);segment.GetComponent<Image>().color=new Color(.16f,.12f,.10f,.92f);segment.GetComponent<Image>().raycastTarget=false;}
                var gauge=feverPanel.Find("Gauge");if(gauge!=null)gauge.gameObject.SetActive(false);
            }
        }

        static void StyleText(Transform value,int size,Color color,TextAnchor alignment,bool bold)
        {
            var text=value?.GetComponent<Text>();if(text==null)return;text.fontSize=size;text.color=color;text.alignment=alignment;text.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;text.resizeTextForBestFit=true;text.resizeTextMinSize=Mathf.Max(16,size-10);text.resizeTextMaxSize=size;
        }

        static GameObject Panel(string name,Transform parent,Color color)
        {
            var panel=UI(name,typeof(CanvasRenderer),typeof(Image));panel.transform.SetParent(parent,false);panel.GetComponent<Image>().color=color;return panel;
        }

        static void UpgradeRunLayout(GameObject root)
        {
            var boardSlot=root.transform.Find("GameplayRoot/BoardSlot") as RectTransform;
            if(boardSlot!=null)Set(boardSlot,.045f,.215f,.955f,.755f,0,0,0,0);
            var bottom=root.transform.Find("UIRoot/PrototypeCanvas/SafeArea/BottomSlot/BottomHUD") as RectTransform;
            if(bottom!=null)
            {
                Set(bottom,0,0,1,0,24,24,-24,240);
                var image=bottom.GetComponent<Image>();if(image!=null)image.color=new Color(.012f,.035f,.065f,.97f);
                var sum=bottom.Find("SelectionSum")?.GetComponent<Text>();if(sum!=null){sum.fontSize=42;sum.fontStyle=FontStyle.Bold;sum.color=new Color(.55f,.9f,1f);sum.alignment=TextAnchor.MiddleCenter;Set(sum.rectTransform,0,1,1,1,24,-190,-24,-92);}
                var status=bottom.Find("Status")?.GetComponent<Text>();if(status!=null){status.fontSize=32;status.fontStyle=FontStyle.Bold;status.text=string.Empty;Set(status.rectTransform,0,1,1,1,24,-100,-24,-12);}
            }
            var gameplay=root.transform.Find("GameplayRoot");
            if(gameplay!=null)
            {
                var backdrop=gameplay.Find("Backdrop")?.gameObject;
                if(backdrop==null)backdrop=Panel("Backdrop",gameplay,new Color(.004f,.012f,.028f,1f));
                backdrop.GetComponent<Image>().color=new Color(.004f,.012f,.028f,1f);Stretch(backdrop.GetComponent<RectTransform>(),0);
                backdrop.GetComponent<Image>().raycastTarget=false;backdrop.transform.SetAsFirstSibling();
            }
        }

        static GameObject CreateGameRoot()
        {
            var root=new GameObject("GameRoot",typeof(GamePresentationHost),typeof(PrototypeGeneratedRoot));
            root.GetComponent<PrototypeGeneratedRoot>().Configure(ContractVersion);
            PrototypeUILayout.EnsureSerializedEventSystem(root);
            var gameplay=UI("GameplayRoot",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));gameplay.transform.SetParent(root.transform,false);
            var gameplayCanvas=gameplay.GetComponent<Canvas>();gameplayCanvas.renderMode=RenderMode.ScreenSpaceOverlay;gameplayCanvas.sortingOrder=10;
            var gameplayScaler=gameplay.GetComponent<CanvasScaler>();gameplayScaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;gameplayScaler.referenceResolution=new Vector2(1080,1920);gameplayScaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;gameplayScaler.matchWidthOrHeight=.5f;
            var backdrop=Panel("Backdrop",gameplay.transform,new Color(.004f,.012f,.028f,1f));Stretch(backdrop.GetComponent<RectTransform>(),0);backdrop.GetComponent<Image>().raycastTarget=false;
            var boardSlot=UI("BoardSlot");boardSlot.transform.SetParent(gameplay.transform,false);Set(boardSlot.GetComponent<RectTransform>(),.045f,.215f,.955f,.755f,0,0,0,0);
            var effectSlot=UI("EffectSlot");effectSlot.transform.SetParent(gameplay.transform,false);Stretch(effectSlot.GetComponent<RectTransform>(),0);
            var board=PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BoardPath)) as GameObject;board.transform.SetParent(boardSlot.transform,false);
            Stretch(board.GetComponent<RectTransform>(),0);
            var uiRoot=new GameObject("UIRoot");uiRoot.transform.SetParent(root.transform,false);
            var canvasObject=UI("PrototypeCanvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(PrototypeUILayout));canvasObject.transform.SetParent(root.transform,false);
            canvasObject.transform.SetParent(uiRoot.transform,false);
            var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
            var scaler=canvasObject.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1080,1920);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;scaler.matchWidthOrHeight=.5f;
            var safe=UI("SafeArea");safe.transform.SetParent(canvasObject.transform,false);Stretch(safe.GetComponent<RectTransform>(),0);
            var topSlot=UI("TopSlot");topSlot.transform.SetParent(safe.transform,false);Stretch(topSlot.GetComponent<RectTransform>(),0);
            var centerSlot=UI("CenterSlot");centerSlot.transform.SetParent(safe.transform,false);Stretch(centerSlot.GetComponent<RectTransform>(),0);
            var bottomSlot=UI("BottomSlot");bottomSlot.transform.SetParent(safe.transform,false);Stretch(bottomSlot.GetComponent<RectTransform>(),0);
            var overlaySlot=UI("OverlaySlot");overlaySlot.transform.SetParent(safe.transform,false);Stretch(overlaySlot.GetComponent<RectTransform>(),0);
            var hud=PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HudPath)) as GameObject;hud.transform.SetParent(topSlot.transform,false);Set(hud.GetComponent<RectTransform>(),0,1,1,1,24,-390,-24,-24);
            var boardArea=UI("BoardArea");boardArea.transform.SetParent(centerSlot.transform,false);Set(boardArea.GetComponent<RectTransform>(),0,0,1,1,70,300,-70,-410);
            var bottom=UI("BottomHUD",typeof(Image));bottom.transform.SetParent(bottomSlot.transform,false);bottom.GetComponent<Image>().color=new Color(.012f,.035f,.065f,.97f);Set(bottom.GetComponent<RectTransform>(),0,0,1,0,24,24,-24,240);
            var status=Text("Status","",32,TextAnchor.UpperCenter,bottom.transform);status.fontStyle=FontStyle.Bold;Set(status.rectTransform,0,1,1,1,24,-100,-24,-12);
            var selectionSum=Text("SelectionSum","CURRENT\n0 / 0",42,TextAnchor.MiddleCenter,bottom.transform);selectionSum.fontStyle=FontStyle.Bold;selectionSum.color=new Color(.55f,.9f,1f);Set(selectionSum.rectTransform,0,1,1,1,24,-190,-24,-92);
            var actions=UI("Actions",typeof(HorizontalLayoutGroup));actions.transform.SetParent(bottom.transform,false);Set(actions.GetComponent<RectTransform>(),0,0,1,1,20,18,-20,-116);var row=actions.GetComponent<HorizontalLayoutGroup>();row.spacing=12;row.childControlWidth=true;row.childForceExpandWidth=true;
            foreach(var pair in new[]{("RetryTarget","Retry Target"),("Language","English / 한국어")})Button(pair.Item1,pair.Item2,actions.transform);
            var presentation=new GameObject("PresentationRoot");presentation.transform.SetParent(root.transform,false);
            root.GetComponent<GamePresentationHost>().Configure(AssetDatabase.LoadAssetAtPath<MathGamePrefabRegistry>(RegistryPath),gameplay.transform,boardSlot.transform,effectSlot.transform,topSlot.transform,centerSlot.transform,bottomSlot.transform,overlaySlot.transform,presentation.transform,board.GetComponent<GameplayPresentationRoot>(),canvasObject.GetComponent<PrototypeUILayout>());
            return root;
        }

        static GameObject CreateLabelPanel(string name,string value,int size){var root=UI(name,typeof(Image));root.GetComponent<Image>().color=new Color(.08f,.13f,.2f,1);Stretch(Text("Label",value,size,TextAnchor.MiddleCenter,root.transform).rectTransform,8);return root;}
        static GameObject CreateRunResultPopup()
        {
            var root=UI("RunResultPopup",typeof(CanvasRenderer),typeof(Image),typeof(RunResultPopupView));
            root.GetComponent<Image>().color=new Color(0,0,0,.78f);
            var panel=UI("PopupPanel",typeof(CanvasRenderer),typeof(Image));panel.transform.SetParent(root.transform,false);panel.GetComponent<Image>().color=new Color(.06f,.10f,.17f,1);Set(panel.GetComponent<RectTransform>(),.1f,.27f,.9f,.73f,0,0,0,0);
            var result=Text("Result","RUN OVER",38,TextAnchor.MiddleCenter,panel.transform);result.fontStyle=FontStyle.Bold;Set(result.rectTransform,0,.24f,1,1,32,16,-32,-20);
            var buttonRoot=UI("PlayAgainButton",typeof(CanvasRenderer),typeof(Image),typeof(Button));buttonRoot.transform.SetParent(panel.transform,false);buttonRoot.GetComponent<Image>().color=new Color(.12f,.42f,.62f,1);Set(buttonRoot.GetComponent<RectTransform>(),.12f,.06f,.58f,.25f,0,0,0,0);Stretch(Text("Label","PLAY AGAIN",26,TextAnchor.MiddleCenter,buttonRoot.transform).rectTransform,8);
            var homeRoot=UI("HomeButton",typeof(CanvasRenderer),typeof(Image),typeof(Button));homeRoot.transform.SetParent(panel.transform,false);homeRoot.GetComponent<Image>().color=new Color(.08f,.18f,.27f,1);Set(homeRoot.GetComponent<RectTransform>(),.62f,.06f,.88f,.25f,0,0,0,0);Stretch(Text("Label","HOME",24,TextAnchor.MiddleCenter,homeRoot.transform).rectTransform,8);
            root.GetComponent<RunResultPopupView>().Configure(result,buttonRoot.GetComponent<Button>(),homeRoot.GetComponent<Button>());
            root.SetActive(false);
            return root;
        }

        static GameObject CreateStartView()
        {
            var root=UI("StartView",typeof(CanvasRenderer),typeof(Image),typeof(CanvasGroup),typeof(StartScreenView));
            root.GetComponent<Image>().color=new Color(.008f,.018f,.035f,1f);
            var title=Text("Title","SUM//VIVE",64,TextAnchor.MiddleCenter,root.transform);title.fontStyle=FontStyle.Bold;title.color=new Color(.82f,.98f,1f);Set(title.rectTransform,0,1,1,1,50,-320,-50,-210);
            var subtitle=Text("Subtitle","KEEP THE CORE ONLINE",24,TextAnchor.MiddleCenter,root.transform);subtitle.color=new Color(.35f,.78f,.88f);Set(subtitle.rectTransform,0,1,1,1,50,-390,-50,-325);
            var core=UI("CoreVisual",typeof(CanvasRenderer),typeof(Image));core.transform.SetParent(root.transform,false);var coreImage=core.GetComponent<Image>();coreImage.color=new Color(.08f,.72f,.86f,.86f);Set(core.GetComponent<RectTransform>(),.5f,.5f,.5f,.5f,-105,80,105,290);core.transform.localRotation=Quaternion.Euler(0,0,45);
            var center=UI("CoreCenter",typeof(CanvasRenderer),typeof(Image));center.transform.SetParent(core.transform,false);center.GetComponent<Image>().color=new Color(.7f,1f,1f,.95f);Set(center.GetComponent<RectTransform>(),.25f,.25f,.75f,.75f,0,0,0,0);
            var state=Text("CoreStatus","CORE ONLINE",24,TextAnchor.MiddleCenter,root.transform);state.color=new Color(.35f,1f,.88f);Set(state.rectTransform,0,.5f,1,.5f,40,-40,-40,20);
            var start=UI("StartButton",typeof(CanvasRenderer),typeof(Image),typeof(Button));start.transform.SetParent(root.transform,false);start.GetComponent<Image>().color=new Color(.05f,.58f,.72f,1);Set(start.GetComponent<RectTransform>(),.16f,.31f,.84f,.31f,0,-76,0,18);var startLabel=Text("Label","START RUN",32,TextAnchor.MiddleCenter,start.transform);startLabel.fontStyle=FontStyle.Bold;Stretch(startLabel.rectTransform,10);
            var bestTime=Text("BestTime","BEST TIME  0.0s",24,TextAnchor.MiddleCenter,root.transform);Set(bestTime.rectTransform,.1f,.22f,.9f,.22f,0,-22,0,36);
            var bestScore=Text("BestScore","BEST SCORE  0",24,TextAnchor.MiddleCenter,root.transform);Set(bestScore.rectTransform,.1f,.18f,.9f,.18f,0,-22,0,36);
            var language=UI("LanguageButton",typeof(CanvasRenderer),typeof(Image),typeof(Button));language.transform.SetParent(root.transform,false);language.GetComponent<Image>().color=new Color(.04f,.10f,.16f,.92f);Set(language.GetComponent<RectTransform>(),.2f,.07f,.8f,.07f,0,-58,0,12);Stretch(Text("Label","English / 한국어",22,TextAnchor.MiddleCenter,language.transform).rectTransform,8);
            root.GetComponent<StartScreenView>().Configure(root.GetComponent<CanvasGroup>(),title,subtitle,state,bestTime,bestScore,start.GetComponent<Button>(),language.GetComponent<Button>(),core.GetComponent<RectTransform>());
            return root;
        }
        static GameObject UI(string name,params Type[] extra){var types=new Type[extra.Length+1];types[0]=typeof(RectTransform);Array.Copy(extra,0,types,1,extra.Length);return new GameObject(name,types);}
        static Text Text(string name,string value,int size,TextAnchor anchor,Transform parent){var text=UI(name,typeof(CanvasRenderer),typeof(Text)).GetComponent<Text>();text.transform.SetParent(parent,false);text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.text=value;text.fontSize=size;text.alignment=anchor;text.color=Color.white;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;return text;}
        static void Button(string name,string label,Transform parent){var root=UI(name,typeof(CanvasRenderer),typeof(Image),typeof(Button));root.transform.SetParent(parent,false);root.GetComponent<Image>().color=new Color(.12f,.42f,.62f,1);Stretch(Text("Label",label,25,TextAnchor.MiddleCenter,root.transform).rectTransform,8);}
        static void Stretch(RectTransform r,float padding){Set(r,0,0,1,1,padding,padding,-padding,-padding);}
        static void Set(RectTransform r,float xmin,float ymin,float xmax,float ymax,float left,float bottom,float right,float top){r.anchorMin=new Vector2(xmin,ymin);r.anchorMax=new Vector2(xmax,ymax);r.offsetMin=new Vector2(left,bottom);r.offsetMax=new Vector2(right,top);}
    }
}
