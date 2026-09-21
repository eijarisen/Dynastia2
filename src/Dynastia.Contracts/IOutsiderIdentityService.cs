namespace Dynastia.Contracts;

public interface IOutsiderIdentityService
{
    GeneratedOutsiderIdentity Generate(
        TownInfo originTown,
        Sex sex,
        int birthYear,
        int year,
        IGameRandom random);

    GeneratedOutsiderIdentity GenerateForNationality(
        TownInfo originTown,
        Sex sex,
        int birthYear,
        int year,
        string nationalityId,
        IGameRandom random) =>
        Generate(originTown, sex, birthYear, year, random);
}
