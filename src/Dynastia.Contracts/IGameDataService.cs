namespace Dynastia.Contracts;

public interface IGameDataService
{
    IReadOnlyList<string> GetStringList(string relativePath);

    IReadOnlyList<WeightedStringEntry> GetWeightedStringList(
        string relativePath);
}
