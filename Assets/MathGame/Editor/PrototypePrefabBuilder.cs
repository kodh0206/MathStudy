using System;
using System.IO;
using MathGame.Presentation.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MathGame.Editor.SceneBuilder
{
    public static class PrototypePrefabBuilder
    {
        const int ContractVersion=1;
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
            EnsureRegistry();
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            Debug.Log("MathGame prototype prefabs are available. Existing prefab assets were preserved.");
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

        static GameObject CreateCell(){var root=GameObject.CreatePrimitive(PrimitiveType.Quad);root.name="Cell";UnityEngine.Object.DestroyImmediate(root.GetComponent<Collider>());root.transform.localScale=Vector3.one*.88f;return root;}

        static GameObject CreateBoard()
        {
            var root=new GameObject("BoardView",typeof(GameplayPresentationRoot),typeof(PlaceholderPresentationFeedback));
            foreach(var name in new[]{"CellRoot","BlockRoot","EffectRoot"})new GameObject(name).transform.SetParent(root.transform,false);
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
            var root=new GameObject("GameRoot",typeof(GamePresentationHost));
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
