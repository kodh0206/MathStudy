using UnityEngine;

namespace MathGame.Presentation.Unity
{
    public sealed class PresentationPrefabContract : MonoBehaviour
    {
        [SerializeField] string contractId;
        [SerializeField] int version;

        public string ContractId => contractId;
        public int Version => version;

#if UNITY_EDITOR
        public void Configure(string id, int value)
        {
            contractId = id;
            version = value;
        }
#endif
    }
}
