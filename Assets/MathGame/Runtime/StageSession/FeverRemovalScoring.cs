using System;
using MathGame.BoardResolution;

namespace MathGame.StageSession
{
    public readonly struct FeverEndScoreEvidence
    {
        public FeverEndScoreEvidence(BoardSystemEffectId effectId, int finalMultiplier)
        {
            if (!effectId.IsValid) throw new ArgumentException("A valid effect identity is required.", nameof(effectId));
            if (finalMultiplier != 1 && finalMultiplier != 2 && finalMultiplier != 3 && finalMultiplier != 5)
                throw new ArgumentOutOfRangeException(nameof(finalMultiplier));
            EffectId = effectId;
            FinalMultiplier = finalMultiplier;
        }

        public BoardSystemEffectId EffectId { get; }
        public int FinalMultiplier { get; }
        public bool IsValid => EffectId.IsValid &&
                               (FinalMultiplier == 1 || FinalMultiplier == 2 || FinalMultiplier == 3 || FinalMultiplier == 5);
    }
}
