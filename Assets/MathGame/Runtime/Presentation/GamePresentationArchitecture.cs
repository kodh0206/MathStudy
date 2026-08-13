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

}
