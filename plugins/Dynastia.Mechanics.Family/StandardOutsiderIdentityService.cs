using Dynastia.Contracts;

namespace Dynastia.Mechanics.Family;

public sealed class StandardOutsiderIdentityService :
    IOutsiderIdentityService
{
    private readonly INationalityService _nationalities;
    private readonly IHistoricalNameService _historicalNames;

    public StandardOutsiderIdentityService(
        INationalityService nationalities,
        IHistoricalNameService historicalNames)
    {
        _nationalities = nationalities;
        _historicalNames = historicalNames;
    }

    public GeneratedOutsiderIdentity Generate(
        TownInfo originTown,
        Sex sex,
        int birthYear,
        int year,
        IGameRandom random)
    {
        ArgumentNullException.ThrowIfNull(originTown);
        ArgumentNullException.ThrowIfNull(random);

        if (string.IsNullOrWhiteSpace(originTown.RegionId))
        {
            throw new InvalidOperationException(
                $"Town '{originTown.Id}' has no RegionId for outsider generation.");
        }

        var nationalityId =
            _nationalities.GenerateNationality(
                originTown.RegionId,
                year,
                random);

        var nameCultureId =
            _nationalities.GetNameCultureId(
                nationalityId);

        var firstName =
            _historicalNames.GetRandomFirstName(
                sex,
                birthYear,
                nameCultureId,
                random);

        var surname =
            _historicalNames.GetRandomSurname(
                sex,
                nameCultureId,
                random);

        return new GeneratedOutsiderIdentity(
            nationalityId,
            _nationalities.GetDisplayName(nationalityId),
            nameCultureId,
            firstName,
            surname);
    }
}
