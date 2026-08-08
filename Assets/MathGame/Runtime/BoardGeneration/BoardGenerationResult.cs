using DomainBoard = MathGame.Board.Board;

namespace MathGame.BoardGeneration
{
    public sealed class BoardGenerationResult
    {
        private BoardGenerationResult(
            BoardGenerationFailure failure,
            DomainBoard board,
            int nextBlockIdValue)
        {
            Failure = failure;
            Board = board;
            NextBlockIdValue = nextBlockIdValue;
        }

        public bool Succeeded => Failure == BoardGenerationFailure.None;
        public BoardGenerationFailure Failure { get; }
        public DomainBoard Board { get; }
        public int NextBlockIdValue { get; }

        internal static BoardGenerationResult Success(DomainBoard board, int nextBlockIdValue)
        {
            return new BoardGenerationResult(BoardGenerationFailure.None, board, nextBlockIdValue);
        }

        internal static BoardGenerationResult Failed(BoardGenerationFailure failure)
        {
            return new BoardGenerationResult(failure, null, 0);
        }
    }
}
