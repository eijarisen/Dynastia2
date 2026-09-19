namespace Dynastia.Contracts;

public interface IHistoricalNameService
{
    string GetRandomFirstName(
        Sex sex,
        int birthYear,
        IGameRandom random);

    string GetRandomFirstName(
        Sex sex,
        int birthYear,
        string nameCultureId,
        IGameRandom random);

    string GetRandomDifferentFirstName(
        Sex sex,
        int birthYear,
        string excludedName,
        IGameRandom random);

    string GetRandomDifferentFirstName(
        Sex sex,
        int birthYear,
        string excludedName,
        string nameCultureId,
        IGameRandom random);

    string GetRandomFirstNameExcluding(
        Sex sex,
        int birthYear,
        IReadOnlyCollection<string> excludedNames,
        IGameRandom random);

    string GetRandomFirstNameExcluding(
        Sex sex,
        int birthYear,
        IReadOnlyCollection<string> excludedNames,
        string nameCultureId,
        IGameRandom random);

    string GetRandomSurname(
        Sex sex,
        string nameCultureId,
        IGameRandom random);

    string FormatSurname(
        string surname,
        Sex sex,
        string nameCultureId);

    bool HasNameCulture(string nameCultureId);
}
