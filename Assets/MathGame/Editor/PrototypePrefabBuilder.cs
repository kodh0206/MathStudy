using System;
using System.Collections.Generic;
using System.IO;
using MathGame.Presentation.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MathGame.Editor.SceneBuilder
{
    public static class PrototypePrefabBuilder
    {
        const int ContractVersion=3;
        public const string Root = "Assets/MathGame/Prefabs";
        public const string GameRootPath = Root + "/Core/GameRoot.prefab";
        public const string BoardPath = Root + "/Board/Board.prefab";
        public const string CellPath = Root + "/Board/Cell.prefab";
        public const string BlockPath = Root + "/Board/Block.prefab";
        public const string HudPath = Root + "/UI/HUD.prefab";
        public const string ObjectivePath = Root + "/UI/ObjectiveItem.prefab";
        public const string FeverPath = Root + "/UI/FeverGauge.prefab";
        public const string RestorationPath = Root + "/UI/RestorationGauge.prefab";
        public const string RegistryPath = Root + "/MathGamePrefabRegistry.asset";

        [MenuItem("MathGame/Build Prototype Prefabs",priority=9)]
        public static void EnsurePrototypePrefabs()
        {
            EnsurePrototypePrefabsForSceneBuild();
        }

        public static bool EnsurePrototypePrefabsForSceneBuild()
        {
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
            CreateIfMissing(ObjectivePath,()=>CreateLabelPanel("ObjectivePanel","Objective",26));
            CreateIfMissing(FeverPath,()=>CreateLabelPanel("FeverGauge","FEVER  0/50",29));
            CreateIfMissing(RestorationPath,()=>CreateLabelPanel("RestorationGauge","RESTORATION  0/100",29));
            CreateIfMissing(HudPath,CreateHud);
            EnsureRegistry();
            CreateIfMissing(GameRootPath,CreateGameRoot);
            EnsureGameRootOwnershipMarker();
            EnsureRegistry();
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            Debug.Log("MathGame prototype prefabs are available. Existing prefab assets were preserved.");
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
            GameRootPath, BoardPath, CellPath, BlockPath, HudPath, ObjectivePath,
            FeverPath, RestorationPath
        };

        static void DeleteManagedPrefabSet()
        {
            foreach (var path in ManagedPrefabPaths()) AssetDatabase.DeleteAsset(path);
            AssetDatabase.DeleteAsset(RegistryPath);
            AssetDatabase.SaveAssets();
        }

        static void EnsureFolders()
        {
            Directory.CreateDirectory(Root+"/Core");Directory.CreateDirectory(Root+"/Board");Directory.CreateDirectory(Root+"/UI");
        }

        static void EnsureRegistry()
        {
            var registry=AssetDatabase.LoadAssetAtPath<MathGamePrefabRegistry>(RegistryPath);
            if(registry==null){registry=ScriptableObject.CreateInstance<MathGamePrefabRegistry>();AssetDatabase.CreateAsset(registry,RegistryPath);}
            registry.GameRootPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(GameRootPath);registry.BoardPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(BoardPath);
            registry.CellPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(CellPath);registry.BlockPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(BlockPath);
            registry.HudPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);registry.ObjectiveItemPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(ObjectivePath);
            registry.FeverGaugePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(FeverPath);registry.RestorationGaugePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(RestorationPath);
            EditorUtility.SetDirty(registry);
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
            var root=create();var contract=root.GetComponent<PresentationPrefabContract>()??root.AddComponent<PresentationPrefabContract>();contract.Configure(Path.GetFileNameWithoutExtension(path),ContractVersion);PrefabUtility.SaveAsPrefabAsset(root,path);UnityEngine.Object.DestroyImmediate(root);
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
            var value=new GameObject("ValueText");value.transform.SetParent(root.transform,false);value.transform.localPosition=Vector3.back*.02f;var text=value.AddComponent<TextMesh>();text.text="1";text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.characterSize=.35f;text.fontSize=64;text.color=Color.white;
            new GameObject("OptionalEffectRoot").transform.SetParent(root.transform,false);
            return root;
        }

        static GameObject CreateCell()
        {
            var root=GameObject.CreatePrimitive(PrimitiveType.Quad);root.name="Cell";root.AddComponent<PrototypeCellView>();root.transform.localScale=Vector3.one*.88f;
            var block=new GameObject("BlockRoot");block.transform.SetParent(root.transform,false);var valueObject=new GameObject("ValueText");valueObject.transform.SetParent(block.transform,false);valueObject.transform.localPosition=Vector3.back*.08f;var value=valueObject.AddComponent<TextMesh>();value.anchor=TextAnchor.MiddleCenter;value.alignment=TextAlignment.Center;value.characterSize=.35f;value.fontSize=64;value.color=new Color(.035f,.055f,.09f,1f);value.GetComponent<MeshRenderer>().sortingOrder=20;
            var obstacle=new GameObject("ObstacleRoot");obstacle.transform.SetParent(root.transform,false);var obstacleObject=new GameObject("ObstacleText");obstacleObject.transform.SetParent(obstacle.transform,false);obstacleObject.transform.localPosition=new Vector3(-.3f,.3f,-.1f);var obstacleText=obstacleObject.AddComponent<TextMesh>();obstacleText.anchor=TextAnchor.MiddleCenter;obstacleText.alignment=TextAlignment.Center;obstacleText.characterSize=.2f;obstacleText.fontSize=48;obstacleText.color=new Color(1f,.45f,.05f);obstacleText.GetComponent<MeshRenderer>().sortingOrder=30;
            root.GetComponent<PrototypeCellView>().Configure(0,0,root.transform,value,obstacleText,block,obstacle);return root;
        }

        static GameObject CreateBoard()
        {
            var root=new GameObject("BoardView",typeof(GameplayPresentationRoot),typeof(PlaceholderPresentationFeedback));
            var cells=new GameObject("CellRoot");cells.transform.SetParent(root.transform,false);new GameObject("BlockRoot").transform.SetParent(root.transform,false);new GameObject("EffectRoot").transform.SetParent(root.transform,false);
            var cellPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(CellPath);
            for(var row=0;row<8;row++)for(var column=0;column<8;column++)
            {
                var cell=PrefabUtility.InstantiatePrefab(cellPrefab) as GameObject;cell.name="Cell_"+column+"_"+row;cell.transform.SetParent(cells.transform,false);cell.transform.localPosition=new Vector3(column,row,0);
                cell.GetComponent<PrototypeCellView>().Configure(column,row,cell.transform,cell.transform.Find("BlockRoot/ValueText")?.GetComponent<TextMesh>(),cell.transform.Find("ObstacleRoot/ObstacleText")?.GetComponent<TextMesh>(),cell.transform.Find("BlockRoot")?.gameObject,cell.transform.Find("ObstacleRoot")?.gameObject);
            }
            return root;
        }

        static GameObject CreateHud()
        {
            var root=UI("HUD",typeof(Image));root.GetComponent<Image>().color=new Color(.035f,.055f,.09f,.96f);
            var title=Text("Title","MATH GAME PROTOTYPE",38,TextAnchor.MiddleCenter,root.transform);Set(title.rectTransform,0,1,1,1,20,-72,-20,-14);
            var stats=UI("MainStats",typeof(GridLayoutGroup));stats.transform.SetParent(root.transform,false);Set(stats.GetComponent<RectTransform>(),0,1,1,1,24,-184,-24,-82);
            var grid=stats.GetComponent<GridLayoutGroup>();grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=3;grid.cellSize=new Vector2(320,92);grid.spacing=new Vector2(20,0);
            foreach(var name in new[]{"Target","Moves","Score"}){var stat=UI(name,typeof(Image));stat.transform.SetParent(stats.transform,false);stat.GetComponent<Image>().color=new Color(.11f,.17f,.26f,1);Stretch(Text("Value",name.ToUpperInvariant()+"\n0",30,TextAnchor.MiddleCenter,stat.transform).rectTransform,8);}
            var resources=UI("Resources");resources.transform.SetParent(root.transform,false);Set(resources.GetComponent<RectTransform>(),0,1,1,1,24,-264,-24,-194);
            var restoration=Text("Restoration","RESTORATION  0/100",29,TextAnchor.MiddleLeft,resources.transform);Set(restoration.rectTransform,0,0,.5f,1,0,0,-10,0);
            var fever=Text("Fever","FEVER  0/50",29,TextAnchor.MiddleRight,resources.transform);Set(fever.rectTransform,.5f,0,1,1,10,0,0,0);
            var objectives=UI("Objectives",typeof(VerticalLayoutGroup));objectives.transform.SetParent(root.transform,false);Set(objectives.GetComponent<RectTransform>(),0,0,1,1,24,28,-24,116);
            var vertical=objectives.GetComponent<VerticalLayoutGroup>();vertical.spacing=6;vertical.childControlHeight=true;vertical.childForceExpandHeight=true;
            return root;
        }

        static GameObject CreateGameRoot()
        {
            var root=new GameObject("GameRoot",typeof(GamePresentationHost),typeof(PrototypeGeneratedRoot));
            root.GetComponent<PrototypeGeneratedRoot>().Configure(ContractVersion);
            var gameplay=new GameObject("GameplayRoot");gameplay.transform.SetParent(root.transform,false);
            var boardSlot=new GameObject("BoardSlot");boardSlot.transform.SetParent(gameplay.transform,false);
            var effectSlot=new GameObject("EffectSlot");effectSlot.transform.SetParent(gameplay.transform,false);
            var board=PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BoardPath)) as GameObject;board.transform.SetParent(boardSlot.transform,false);
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
            var bottom=UI("BottomHUD",typeof(Image));bottom.transform.SetParent(bottomSlot.transform,false);bottom.GetComponent<Image>().color=new Color(.035f,.055f,.09f,.96f);Set(bottom.GetComponent<RectTransform>(),0,0,1,0,24,24,-24,280);
            var status=Text("Status","Starting prototype...",26,TextAnchor.UpperCenter,bottom.transform);Set(status.rectTransform,0,1,1,1,24,-104,-24,-16);
            var actions=UI("Actions",typeof(HorizontalLayoutGroup));actions.transform.SetParent(bottom.transform,false);Set(actions.GetComponent<RectTransform>(),0,0,1,1,20,18,-20,-116);var row=actions.GetComponent<HorizontalLayoutGroup>();row.spacing=12;row.childControlWidth=true;row.childForceExpandWidth=true;
            foreach(var pair in new[]{("Continue","Continue +5"),("Retry","Retry"),("Abandon","Abandon"),("RetryTarget","Retry Target"),("Restart","Restart")})Button(pair.Item1,pair.Item2,actions.transform);
            var presentation=new GameObject("PresentationRoot");presentation.transform.SetParent(root.transform,false);
            root.GetComponent<GamePresentationHost>().Configure(AssetDatabase.LoadAssetAtPath<MathGamePrefabRegistry>(RegistryPath),gameplay.transform,boardSlot.transform,effectSlot.transform,topSlot.transform,centerSlot.transform,bottomSlot.transform,overlaySlot.transform,presentation.transform,board.GetComponent<GameplayPresentationRoot>(),canvasObject.GetComponent<PrototypeUILayout>());
            return root;
        }

        static GameObject CreateLabelPanel(string name,string value,int size){var root=UI(name,typeof(Image));root.GetComponent<Image>().color=new Color(.08f,.13f,.2f,1);Stretch(Text("Label",value,size,TextAnchor.MiddleCenter,root.transform).rectTransform,8);return root;}
        static GameObject UI(string name,params Type[] extra){var types=new Type[extra.Length+1];types[0]=typeof(RectTransform);Array.Copy(extra,0,types,1,extra.Length);return new GameObject(name,types);}
        static Text Text(string name,string value,int size,TextAnchor anchor,Transform parent){var text=UI(name,typeof(CanvasRenderer),typeof(Text)).GetComponent<Text>();text.transform.SetParent(parent,false);text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.text=value;text.fontSize=size;text.alignment=anchor;text.color=Color.white;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;return text;}
        static void Button(string name,string label,Transform parent){var root=UI(name,typeof(CanvasRenderer),typeof(Image),typeof(Button));root.transform.SetParent(parent,false);root.GetComponent<Image>().color=new Color(.12f,.42f,.62f,1);Stretch(Text("Label",label,25,TextAnchor.MiddleCenter,root.transform).rectTransform,8);}
        static void Stretch(RectTransform r,float padding){Set(r,0,0,1,1,padding,padding,-padding,-padding);}
        static void Set(RectTransform r,float xmin,float ymin,float xmax,float ymax,float left,float bottom,float right,float top){r.anchorMin=new Vector2(xmin,ymin);r.anchorMax=new Vector2(xmax,ymax);r.offsetMin=new Vector2(left,bottom);r.offsetMax=new Vector2(right,top);}
    }
}
