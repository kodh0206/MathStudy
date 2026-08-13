using System;
using UnityEngine;

namespace MathGame.Presentation.Unity
{
    public sealed class PresentationPrefabContract : MonoBehaviour
    {
        [SerializeField] string contractId;
        [SerializeField] int version;
        public string ContractId=>contractId;public int Version=>version;
#if UNITY_EDITOR
        public void Configure(string id,int value){contractId=id;version=value;}
#endif
    }
    [CreateAssetMenu(menuName="MathGame/Presentation/Prefab Registry",fileName="MathGamePrefabRegistry")]
    public sealed class MathGamePrefabRegistry : ScriptableObject
    {
        public GameObject GameRootPrefab;
        public GameObject BoardPrefab;
        public GameObject CellPrefab;
        public GameObject BlockPrefab;
        public GameObject HudPrefab;
        public GameObject ObjectiveItemPrefab;
        public GameObject FeverGaugePrefab;
        public GameObject RestorationGaugePrefab;
    }

    public sealed class GamePresentationContext
    {
        public GamePresentationContext(Transform gameplayRoot,Transform boardSlot,Transform effectSlot,
            Transform topUiRoot,Transform centerUiRoot,Transform bottomUiRoot,Transform overlayRoot,Transform presentationRoot,
            MathGamePrefabRegistry registry)
        {GameplayRoot=gameplayRoot;BoardSlot=boardSlot;EffectSlot=effectSlot;TopUIRoot=topUiRoot;CenterUIRoot=centerUiRoot;BottomUIRoot=bottomUiRoot;OverlayRoot=overlayRoot;PresentationRoot=presentationRoot;Registry=registry;}
        public Transform GameplayRoot{get;}public Transform BoardSlot{get;}public Transform EffectSlot{get;}
        public Transform TopUIRoot{get;}public Transform CenterUIRoot{get;}public Transform BottomUIRoot{get;}public Transform OverlayRoot{get;}
        public Transform PresentationRoot{get;}public MathGamePrefabRegistry Registry{get;}
    }

    public interface IGamePresentationModule : IDisposable
    {
        void Initialize(GamePresentationContext context);
    }

    public sealed class GamePresentationHost : MonoBehaviour
    {
        [SerializeField] MathGamePrefabRegistry registry;
        [SerializeField] Transform gameplayRoot,boardSlot,effectSlot,topSlot,centerSlot,bottomSlot,overlaySlot,presentationRoot;
        [SerializeField] GameplayPresentationRoot boardView;
        [SerializeField] PrototypeUILayout uiLayout;
        public MathGamePrefabRegistry Registry=>registry;
        public GameplayPresentationRoot BoardView=>boardView;
        public PrototypeUILayout UILayout=>uiLayout;
        public GamePresentationContext CreateContext()=>new GamePresentationContext(gameplayRoot,boardSlot,effectSlot,topSlot,centerSlot,bottomSlot,overlaySlot,presentationRoot,registry);
        public bool HasValidContext=>registry!=null&&gameplayRoot!=null&&boardSlot!=null&&effectSlot!=null&&topSlot!=null&&centerSlot!=null&&bottomSlot!=null&&overlaySlot!=null&&presentationRoot!=null&&boardView!=null&&uiLayout!=null;

#if UNITY_EDITOR
        public void Configure(MathGamePrefabRegistry value,Transform gameplay,Transform board,Transform effects,Transform top,
            Transform center,Transform bottom,Transform overlay,Transform presentation,GameplayPresentationRoot boardPresentation,PrototypeUILayout layout)
        {registry=value;gameplayRoot=gameplay;boardSlot=board;effectSlot=effects;topSlot=top;centerSlot=center;bottomSlot=bottom;overlaySlot=overlay;presentationRoot=presentation;boardView=boardPresentation;uiLayout=layout;}
#endif
    }
}
