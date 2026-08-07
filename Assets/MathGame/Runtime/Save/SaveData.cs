using System;

namespace MathGame.Save
{
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion = CurrentSchemaVersion;
    }
}
