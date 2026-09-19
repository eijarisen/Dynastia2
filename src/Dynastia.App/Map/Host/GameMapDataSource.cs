namespace Dynastia.App.Map.Host;

using Dynastia.Contracts;
using Dynastia.StandardUI.Map.Models;
using Dynastia.StandardUI.Map.Projection;

public sealed class GameMapDataSource
{
    private readonly IGameState _gameState;
    private readonly ILocationService _locations;
    private readonly IFamilyService? _family;
    private readonly IEconomyService? _economy;
    private readonly IHouseholdService? _households;
    private readonly ISuccessionService _succession;

    private readonly IReadOnlyList<ProjectedTownRecord>
        _projectedTowns;

    private readonly double _minX;
    private readonly double _maxX;
    private readonly double _minY;
    private readonly double _maxY;

    public GameMapDataSource(
        IGameState gameState,
        ILocationService locations,
        IFamilyService? family,
        IEconomyService? economy,
        IHouseholdService? households,
        ISuccessionService succession)
    {
        _gameState = gameState;
        _locations = locations;
        _family = family;
        _economy = economy;
        _households = households;
        _succession = succession;

        var projection =
            new PolandCs92Projection();

        var towns =
            locations
                .GetTowns()
                .ToArray();

        if (towns.Length == 0)
        {
            throw new InvalidDataException(
                "The town catalogue is empty.");
        }

        var duplicateTownId =
            towns
                .Where(town =>
                    !string.IsNullOrWhiteSpace(
                        town.Id))
                .GroupBy(
                    town => town.Id,
                    StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group =>
                    group.Count() > 1);

        if (duplicateTownId is not null)
        {
            throw new InvalidDataException(
                $"Duplicate town id '{duplicateTownId.Key}' in the town catalogue.");
        }

        _projectedTowns =
            towns
                .Select(town =>
                {
                    var point =
                        projection.Project(
                            town.Longitude,
                            town.Latitude);

                    return new ProjectedTownRecord(
                        town,
                        point.X,
                        point.Y);
                })
                .ToArray();

        _minX =
            _projectedTowns.Min(town => town.X);

        _maxX =
            _projectedTowns.Max(town => town.X);

        _minY =
            _projectedTowns.Min(town => town.Y);

        _maxY =
            _projectedTowns.Max(town => town.Y);
    }

    public TownMapSnapshot GetSnapshot()
    {
        var residentsByTown =
            new Dictionary<
                string,
                List<TownMapResident>>(
                    StringComparer.OrdinalIgnoreCase);

        var activeHouseholdsByTown =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        var playableHouseholdsByTown =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        var housesByTown =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        var currentTownId =
            ResolveCurrentHouseholdTownId();

        if (_family is not null)
        {
            foreach (var person in _gameState.People)
            {
                if (!person.Tags.Has("state.alive")
                    || !_family.IsBloodline(person))
                {
                    continue;
                }

                TownInfo town;

                try
                {
                    town =
                        _locations
                            .GetLocation(person)
                            .HomeTown;
                }
                catch
                {
                    continue;
                }

                if (!residentsByTown.TryGetValue(
                        town.Id,
                        out var residents))
                {
                    residents = [];
                    residentsByTown[town.Id] =
                        residents;
                }

                var isPlayable =
                    _succession.IsControllable(
                        person);

                residents.Add(
                    new TownMapResident(
                        person.Id,
                        _family.GetDisplayName(
                            person),
                        isPlayable,
                        person.Id
                            == _succession.ActiveControllerId));
            }
        }

        if (_households is not null)
        {
            foreach (var household in
                     _households.GetActiveHouseholds())
            {
                var head =
                    _gameState.People.FirstOrDefault(
                        person =>
                            person.Id
                            == household.HeadId);

                if (head is null
                    || !head.Tags.Has("state.alive"))
                {
                    continue;
                }

                TownInfo town;

                try
                {
                    town =
                        _economy is not null
                            ? _economy.GetResidenceTown(
                                head)
                            : _locations
                                .GetLocation(head)
                                .HomeTown;
                }
                catch
                {
                    continue;
                }

                activeHouseholdsByTown[town.Id] =
                    activeHouseholdsByTown
                        .GetValueOrDefault(town.Id)
                    + 1;

                if (_succession.IsControllable(head))
                {
                    playableHouseholdsByTown[town.Id] =
                        playableHouseholdsByTown
                            .GetValueOrDefault(
                                town.Id)
                        + 1;
                }

                if (_economy is null)
                    continue;

                foreach (var house in
                         _economy.GetHouses(head))
                {
                    housesByTown[house.Town.Id] =
                        housesByTown
                            .GetValueOrDefault(
                                house.Town.Id)
                        + 1;
                }
            }
        }

        var items =
            _projectedTowns
                .Select(projected =>
                {
                    residentsByTown.TryGetValue(
                        projected.Town.Id,
                        out var residents);

                    var orderedResidents =
                        (residents
                         ?? [])
                            .OrderByDescending(
                                resident =>
                                    resident.IsActiveHouseholdHead)
                            .ThenByDescending(
                                resident =>
                                    resident.IsPlayableHouseholdHead)
                            .ThenBy(
                                resident =>
                                    resident.DisplayName,
                                StringComparer.CurrentCultureIgnoreCase)
                            .ToArray();

                    return new TownMapItem(
                        projected.Town.Id,
                        projected.Town.Town,
                        projected.Town.County,
                        projected.Town.Population,
                        projected.X,
                        projected.Y,
                        orderedResidents,
                        activeHouseholdsByTown
                            .GetValueOrDefault(
                                projected.Town.Id),
                        playableHouseholdsByTown
                            .GetValueOrDefault(
                                projected.Town.Id),
                        housesByTown
                            .GetValueOrDefault(
                                projected.Town.Id),
                        string.Equals(
                            currentTownId,
                            projected.Town.Id,
                            StringComparison.OrdinalIgnoreCase));
                })
                .ToArray();

        return new TownMapSnapshot(
            items,
            _minX,
            _maxX,
            _minY,
            _maxY,
            currentTownId);
    }

    private string? ResolveCurrentHouseholdTownId()
    {
        var active =
            _succession.ActiveController;

        if (active is null)
            return null;

        try
        {
            return (
                _economy is not null
                    ? _economy.GetResidenceTown(active)
                    : _locations
                        .GetLocation(active)
                        .HomeTown
            ).Id;
        }
        catch
        {
            return null;
        }
    }

    private sealed record ProjectedTownRecord(
        TownInfo Town,
        double X,
        double Y);
}
