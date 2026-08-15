namespace MathGame.PlayerProgress
{
    public enum ProgressLoadStatus { LoadedPrimary = 0, LoadedBackup = 1, NewPlayer = 2, InvalidDataFallback = 3, ReadFailedFallback = 4 }
    public enum ProgressSaveStatus { Saved = 0, InvalidProgress = 1, WriteFailed = 2 }

    public sealed class ProgressLoadResult
    {
        public ProgressLoadResult(ProgressLoadStatus status, PlayerProgress progress, string diagnostic = null)
        { Status = status; Progress = progress; Diagnostic = diagnostic; }
        public ProgressLoadStatus Status { get; }
        public PlayerProgress Progress { get; }
        public string Diagnostic { get; }
    }

    public sealed class ProgressSaveResult
    {
        public ProgressSaveResult(ProgressSaveStatus status, string diagnostic = null)
        { Status = status; Diagnostic = diagnostic; }
        public ProgressSaveStatus Status { get; }
        public string Diagnostic { get; }
        public bool Succeeded => Status == ProgressSaveStatus.Saved;
    }

    public interface IPlayerProgressRepository
    {
        ProgressLoadResult Load();
        ProgressSaveResult Save(PlayerProgress progress);
    }
}
