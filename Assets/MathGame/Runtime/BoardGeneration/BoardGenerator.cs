using System;
using MathGame.Board;
using MathGame.Core.Random;
using DomainBoard = MathGame.Board.Board;

namespace MathGame.BoardGeneration
{
    public sealed class BoardGenerator
    {
        private readonly IRandomSource randomSource;

        public BoardGenerator(IRandomSource randomSource)
        {
            this.randomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
        }

        public BoardGenerationResult Generate(BoardGenerationConfig config)
        {
            var validationFailure = Validate(config, out var activeCount);
            if (validationFailure != BoardGenerationFailure.None)
            {
                return BoardGenerationResult.Failed(validationFailure);
            }

            var board = new DomainBoard(config.Topology);
            var nextBlockId = config.FirstBlockIdValue;
            foreach (var position in config.Topology.EnumerateActivePositions())
            {
                var value = randomSource.NextInt(config.MinimumValue, config.MaximumValue + 1);
                if (value < config.MinimumValue || value > config.MaximumValue)
                {
                    throw new InvalidOperationException("The random source returned a value outside the requested range.");
                }

                var block = new NumberBlock(new BlockId(nextBlockId), value);
                if (board.TryPlaceBlock(position, block) != BoardMutationResult.Succeeded)
                {
                    return BoardGenerationResult.Failed(BoardGenerationFailure.BoardMutationRejected);
                }

                nextBlockId++;
            }

            if (board.BlockCount != activeCount)
            {
                return BoardGenerationResult.Failed(BoardGenerationFailure.BoardMutationRejected);
            }

            return BoardGenerationResult.Success(board, nextBlockId);
        }

        private static BoardGenerationFailure Validate(
            BoardGenerationConfig config,
            out int activeCount)
        {
            activeCount = 0;
            if (config == null)
            {
                return BoardGenerationFailure.MissingConfiguration;
            }

            if (config.Topology == null)
            {
                return BoardGenerationFailure.MissingTopology;
            }

            if (config.MinimumValue <= 0 ||
                config.MaximumValue < config.MinimumValue ||
                config.MaximumValue == int.MaxValue)
            {
                return BoardGenerationFailure.InvalidValueRange;
            }

            if (config.FirstBlockIdValue <= 0)
            {
                return BoardGenerationFailure.InvalidFirstBlockId;
            }

            foreach (var _ in config.Topology.EnumerateActivePositions())
            {
                activeCount++;
            }

            if (config.FirstBlockIdValue > int.MaxValue - activeCount)
            {
                return BoardGenerationFailure.BlockIdRangeExhausted;
            }

            return BoardGenerationFailure.None;
        }
    }
}
