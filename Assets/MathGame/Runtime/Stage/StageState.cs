namespace MathGame.Stage
{
    public enum StageState
    {
        None = 0,
        Initializing = 1,
        PresentingTarget = 2,
        PlayerInput = 3,
        ResolvingAnswer = 4,
        EnteringFever = 5,
        FeverInput = 6,
        EndingFever = 7,
        Paused = 8,
        Success = 9,
        Failure = 10,
        Exited = 11,
        Ready = 12
    }
}
