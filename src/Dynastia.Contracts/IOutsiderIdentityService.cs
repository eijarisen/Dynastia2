namespace Dynastia.Contracts;

public interface IOutsiderIdentityService
{
    GeneratedOutsiderIdentity Generate(
        TownInfo originTown,
        Sex sex,
        int birthYear,
        int year,
        IGameRandom random);
}
