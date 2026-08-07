namespace MathGame.Save
{
    public interface ISaveRepository
    {
        bool TryLoad(out SaveData data);

        void Save(SaveData data);
    }
}
