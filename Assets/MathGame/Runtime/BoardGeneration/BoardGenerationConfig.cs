using MathGame.Board;

namespace MathGame.BoardGeneration
{
    public sealed class BoardGenerationConfig
    {
        public BoardGenerationConfig(
            BoardTopology topology,
            int minimumValue,
            int maximumValue,
            int firstBlockIdValue = 1)
        {
            Topology = topology;
            MinimumValue = minimumValue;
            MaximumValue = maximumValue;
            FirstBlockIdValue = firstBlockIdValue;
        }

        public BoardTopology Topology { get; }
        public int MinimumValue { get; }
        public int MaximumValue { get; }
        public int FirstBlockIdValue { get; }
    }
}
