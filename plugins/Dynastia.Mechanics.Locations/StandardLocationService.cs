using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Locations;

public sealed partial class StandardLocationService :
    ILocationService,
    IExistingLocationService
{
    private const string TownsPath =
        "Towns/towns.csv";

    private const double SpouseSameTownChance =
        0.62;

    private const double SpouseNearbyChance =
        0.30;

    private const double SpouseRandomChance =
        0.08;

    private const double ChildSameTownChance =
        0.72;

    private const double AdditionalHouseSameTownChance =
        0.55;

    private const double NearbySpouseRadiusKm =
        200;

    private const double NearbyBirthRadiusKm =
        160;

    private const double NearbyHouseRadiusKm =
        120;

    private const double EarthRadiusKm =
        6371.0088;

    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;

    private readonly IReadOnlyList<TownInfo>
        _towns;

    private readonly IReadOnlyDictionary<string, TownInfo>
        _townsById;

    private readonly IReadOnlyDictionary<string, TownInfo>
        _townsByLegacyKey;

    public StandardLocationService(
        IGameState gameState,
        IFamilyService family,
        IGameDataService data,
        IGameRandom random,
        IGameEventBus events)
    {
        _gameState =
            gameState;

        _family =
            family;

        _random =
            random;

        _events =
            events;

        var spouseProbabilityTotal =
            SpouseSameTownChance
            + SpouseNearbyChance
            + SpouseRandomChance;

        if (Math.Abs(
                spouseProbabilityTotal
                - 1.0)
            > 0.000001)
        {
            throw new InvalidOperationException(
                "Spouse location probabilities must total 1.");
        }

        _towns =
            ParseTowns(
                data.ReadText(
                    TownsPath));

        if (_towns.Count == 0)
        {
            throw new InvalidDataException(
                $"{TownsPath} contains no towns.");
        }

        _townsById =
            _towns.ToDictionary(
                town => town.Id,
                StringComparer.OrdinalIgnoreCase);

        _townsByLegacyKey =
            _towns.ToDictionary(
                town => LegacyTownKey(
                    town.Town,
                    town.County),
                StringComparer.OrdinalIgnoreCase);

        _events.EventPublished +=
            OnEventPublished;
    }

    public LocationSnapshot GetLocation(
        IPerson person)
    {
        ArgumentNullException.ThrowIfNull(
            person);

        var component =
            person.Components.Get<
                LocationComponent>();

        if (component is not null
            && CanonicalizeComponent(
                component))
        {
            person.Components.Set(
                component);
        }

        if (component?.Birthplace is null
            || component.HomeTown is null)
        {
            EnsureFallbackLocation(
                person);

            component =
                person.Components.Get<
                    LocationComponent>();
        }

        if (component?.Birthplace is null
            || component.HomeTown is null)
        {
            throw new InvalidOperationException(
                $"Could not determine a location for " +
                $"{_family.GetDisplayName(person)}.");
        }

        if (person.Tags.Has(
                "state.dead")
            && component.DeathTown is null)
        {
            // Older saves predate explicit death-town storage.
            // Their last known home town is the best available fallback.
            component.DeathTown =
                component.HomeTown;

            person.Components.Set(
                component);
        }

        return new LocationSnapshot(
            component.Birthplace,
            component.HomeTown,
            component.DeathTown);
    }


    public LocationSnapshot? GetExistingLocation(
        IPerson person)
    {
        ArgumentNullException.ThrowIfNull(person);

        var component =
            person.Components.Get<LocationComponent>();

        if (component?.Birthplace is null
            || component.HomeTown is null)
        {
            return null;
        }

        return new LocationSnapshot(
            component.Birthplace,
            component.HomeTown,
            component.DeathTown);
    }


    public IReadOnlyList<TownInfo> GetTowns() =>
        _towns;

    public TownInfo? FindTown(string townId)
    {
        if (string.IsNullOrWhiteSpace(townId))
            return null;

        return _townsById.TryGetValue(townId, out var town)
            ? town
            : null;
    }

    public TownInfo ChoosePropertyTown(
        IPerson householdHead)
    {
        var homeTown =
            GetLocation(
                householdHead)
            .HomeTown;

        if (_random.NextDouble()
            < AdditionalHouseSameTownChance)
        {
            return homeTown;
        }

        var nearby =
            _towns
                .Where(
                    candidate =>
                        !IsSameTown(
                            candidate,
                            homeTown))
                .Select(
                    candidate =>
                        new WeightedTown(
                            candidate,
                            DistanceKm(
                                homeTown,
                                candidate)))
                .Where(
                    candidate =>
                        candidate.DistanceKm
                        <= NearbyHouseRadiusKm)
                .ToList();

        return nearby.Count == 0
            ? homeTown
            : ChooseNearbyWeighted(
                nearby);
    }

    public void SetHouseholdHomeTown(
        IPerson householdHead,
        TownInfo town)
    {
        // Household membership lives in the Economy service. Locations cannot
        // safely infer post-divorce residence from biological parentage. This
        // compatibility method therefore updates only the supplied head; the
        // economy service updates every authoritative household member.
        SetPersonHomeTown(
            householdHead,
            town);
    }
}
