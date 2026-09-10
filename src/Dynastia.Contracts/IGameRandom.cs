namespace Dynastia.Contracts;

public interface IGameRandom
{
    int NextInt(int minInclusive, int maxInclusive);
    double NextDouble();
    bool Chance(double probability);
}
