namespace MathGame.Core.Diagnostics
{
    public interface IGameLogger
    {
        void Info(string category, string message);

        void Warning(string category, string message);

        void Error(string category, string message);
    }
}
