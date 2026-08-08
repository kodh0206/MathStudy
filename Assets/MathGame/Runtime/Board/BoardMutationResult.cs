namespace MathGame.Board
{
    public enum BoardMutationResult
    {
        Succeeded = 0,
        AlreadyInRequestedState = 1,
        OutOfBounds = 2,
        InactivePosition = 3,
        InvalidBlock = 4,
        InvalidAccess = 5,
        Blocked = 6,
        Occupied = 7,
        Empty = 8,
        DuplicateBlockId = 9,
        SourceEqualsDestination = 10
    }
}
