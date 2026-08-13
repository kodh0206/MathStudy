using UnityEngine;

namespace MathGame.Presentation.Unity
{
    public sealed class GamePresentationHost : MonoBehaviour
    {
        [SerializeField] MathGamePrefabRegistry registry;
        [SerializeField] Transform gameplayRoot;
        [SerializeField] Transform boardSlot;
        [SerializeField] Transform effectSlot;
        [SerializeField] Transform topSlot;
        [SerializeField] Transform centerSlot;
        [SerializeField] Transform bottomSlot;
        [SerializeField] Transform overlaySlot;
        [SerializeField] Transform presentationRoot;
        [SerializeField] GameplayPresentationRoot boardView;
        [SerializeField] PrototypeUILayout uiLayout;

        public MathGamePrefabRegistry Registry => registry;
        public GameplayPresentationRoot BoardView => boardView;
        public PrototypeUILayout UILayout => uiLayout;
        public GamePresentationContext CreateContext() => new GamePresentationContext(
            gameplayRoot, boardSlot, effectSlot, topSlot, centerSlot, bottomSlot, overlaySlot, presentationRoot, registry);
        public bool HasValidContext => registry != null && gameplayRoot != null && boardSlot != null &&
            effectSlot != null && topSlot != null && centerSlot != null && bottomSlot != null &&
            overlaySlot != null && presentationRoot != null && boardView != null &&
            boardView.transform.IsChildOf(boardSlot) && boardView.SerializedCellViewCount > 0 && uiLayout != null;

#if UNITY_EDITOR
        public void Configure(MathGamePrefabRegistry value, Transform gameplay, Transform board, Transform effects,
            Transform top, Transform center, Transform bottom, Transform overlay, Transform presentation,
            GameplayPresentationRoot boardPresentation, PrototypeUILayout layout)
        {
            registry = value;
            gameplayRoot = gameplay;
            boardSlot = board;
            effectSlot = effects;
            topSlot = top;
            centerSlot = center;
            bottomSlot = bottom;
            overlaySlot = overlay;
            presentationRoot = presentation;
            boardView = boardPresentation;
            uiLayout = layout;
        }
#endif
    }

}
