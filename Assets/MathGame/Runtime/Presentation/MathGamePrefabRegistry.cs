using UnityEngine;

namespace MathGame.Presentation.Unity
{
    [CreateAssetMenu(menuName = "MathGame/Presentation/Prefab Registry", fileName = "MathGamePrefabRegistry")]
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
        public GameObject StageClearPopupPrefab;
    }
}
