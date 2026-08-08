namespace MathGame.BoardGeneration
{
    public enum BoardGenerationFailure
    {
        None = 0,
        MissingConfiguration = 1,
        MissingTopology = 2,
        InvalidValueRange = 3,
        InvalidFirstBlockId = 4,
        BlockIdRangeExhausted = 5,
        BoardMutationRejected = 6
    }
}
