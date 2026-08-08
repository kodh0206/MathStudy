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
        ExitRequested = 6,
        TargetPresentationBegan = 7,
        PlayerInputEnabled = 8,
        AnswerResolutionBegan = 9,
        MissResolutionFinished = 10,
        DeadlockRecoveryBegan = 11
    }
}
