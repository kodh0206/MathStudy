namespace MathGame.Stage
{
    public readonly struct StageTransition
    {
        public StageTransition(
            StageState previous,
            StageState current,
            StageTransitionCause cause)
        {
            Previous = previous;
            Current = current;
            Cause = cause;
        }

        public StageState Previous { get; }

        public StageState Current { get; }

        public StageTransitionCause Cause { get; }
    }
}
