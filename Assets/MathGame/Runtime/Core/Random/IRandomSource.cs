namespace MathGame.Core.Random
{
    public interface IRandomSource
    {
        int NextInt(int minInclusive, int maxExclusive);

        float NextFloat();
    }
}
