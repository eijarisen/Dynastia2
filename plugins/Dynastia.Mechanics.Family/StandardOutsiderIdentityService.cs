using Dynastia.Contracts;

namespace Dynastia.Mechanics.Family;

public sealed class StandardOutsiderIdentityService :
    IOutsiderIdentityService
{
    private readonly INationalityService _nationalities;
    private readonly IHistoricalNameService _historicalNames;
    private readonly ForeignBirthplaceCatalog? _foreignBirthplaces;

    public StandardOutsiderIdentityService(
        INationalityService nationalities,
        IHistoricalNameService historicalNames)
        : this(nationalities, historicalNames, null)
    {
    }

    internal StandardOutsiderIdentityService(
        INationalityService nationalities,
        IHistoricalNameService historicalNames,
        ForeignBirthplaceCatalog? foreignBirthplaces)
    {
        _nationalities = nationalities;
        _historicalNames = historicalNames;
        _foreignBirthplaces = foreignBirthplaces;
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

        return GenerateForNationality(
            originTown,
            sex,
            birthYear,
            year,
            nationalityId,
            random);
    }

    public GeneratedOutsiderIdentity GenerateForNationality(
        TownInfo originTown,
        Sex sex,
        int birthYear,
        int year,
        string nationalityId,
        IGameRandom random)
    {
        ArgumentNullException.ThrowIfNull(originTown);
        ArgumentException.ThrowIfNullOrWhiteSpace(nationalityId);
        ArgumentNullException.ThrowIfNull(random);

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

        var foreignBirthplace =
            _foreignBirthplaces?.Select(
                originTown,
                nationalityId,
                birthYear,
                year,
                random);

        return new GeneratedOutsiderIdentity(
            nationalityId,
            _nationalities.GetDisplayName(nationalityId),
            nameCultureId,
            firstName,
            surname,
            foreignBirthplace?.City,
            foreignBirthplace?.Country);
    }
}
