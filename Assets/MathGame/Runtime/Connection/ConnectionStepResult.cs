namespace MathGame.Connection
{
    public enum ConnectionStepResult
    {
        Added = 0,
        Backtracked = 1,
        OutOfBounds = 2,
        InactivePosition = 3,
        Empty = 4,
        Blocked = 5,
        NotOrthogonallyAdjacent = 6,
        AlreadySelected = 7,
        SumOverflow = 8
    }
}
