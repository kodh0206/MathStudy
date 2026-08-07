namespace MathGame.Stage
{
    public enum TransitionResult
    {
        Succeeded = 0,
        AlreadyInRequestedState = 1,
        InvalidFromCurrentState = 2,
        BlockedByPauseReason = 3,
        StageAlreadyTerminated = 4
    }
}
