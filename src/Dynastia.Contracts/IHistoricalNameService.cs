namespace Dynastia.Contracts;

public interface IHistoricalNameService
{
    string GetRandomFirstName(
        Sex sex,
        int birthYear,
        IGameRandom random);

    string GetRandomDifferentFirstName(
        Sex sex,
        int birthYear,
        string excludedName,
        IGameRandom random);

    string GetRandomFirstNameExcluding(
        Sex sex,
        int birthYear,
        IReadOnlyCollection<string> excludedNames,
        IGameRandom random);
}
