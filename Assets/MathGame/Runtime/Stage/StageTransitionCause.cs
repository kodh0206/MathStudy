namespace MathGame.Stage
{
    public enum StageTransitionCause
    {
        StartRequested = 0,
        InitializationCompleted = 1,
        PauseRequested = 2,
        AllPauseReasonsCleared = 3,
        StageCompleted = 4,
        StageFailed = 5,
        ExitRequested = 6
    }
}
