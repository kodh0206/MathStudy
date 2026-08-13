using UnityEngine;

namespace MathGame.Presentation.Unity
{
    [DisallowMultipleComponent]
    public sealed class PrototypeGeneratedRoot : MonoBehaviour
    {
        public const string MathGameOwnerId = "MathGame.Prototype.GameRoot";

        [SerializeField] string ownerId;
        [SerializeField] int schemaVersion;

        public string OwnerId => ownerId;
        public int SchemaVersion => schemaVersion;
        public bool IsMathGameOwned => ownerId == MathGameOwnerId && schemaVersion > 0;

#if UNITY_EDITOR
        public void Configure(int version)
        {
            ownerId = MathGameOwnerId;
            schemaVersion = version;
        }
#endif
    }
}
