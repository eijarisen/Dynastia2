using Dynastia.Contracts;

namespace Dynastia.Mechanics.Locations;

public sealed partial class StandardLocationService :
    ILocationService,
    IExistingLocationService
{
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
    private readonly IHistoricalTownCatalog _catalog;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;

    public StandardLocationService(
        IGameState gameState,
        IFamilyService family,
        IHistoricalTownCatalog catalog,
        IGameRandom random,
        IGameEventBus events)
    {
        _gameState =
            gameState;

        _family =
            family;

        _catalog =
            catalog;

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

        if (GetTowns().Count == 0)
        {
            throw new InvalidOperationException(
                $"Historical town catalogue has no destinations for {_gameState.Year}.");
        }

        _events.EventPublished +=
            OnEventPublished;
    }


    public string GetBirthplaceDisplayName(
        IPerson person)
    {
        ArgumentNullException.ThrowIfNull(person);
        var component = person.Components.Get<LocationComponent>();
        if (component is not null
            && !string.IsNullOrWhiteSpace(component.ForeignBirthplaceCity)
            && !string.IsNullOrWhiteSpace(component.ForeignBirthplaceCountry))
        {
            return $"{component.ForeignBirthplaceCity}, {component.ForeignBirthplaceCountry}";
        }

        return GetLocation(person).Birthplace.DisplayName;
    }

    public void SetForeignBirthplace(
        IPerson person,
        string? city,
        string? country)
    {
        ArgumentNullException.ThrowIfNull(person);
        var component = person.Components.Get<LocationComponent>()
            ?? new LocationComponent();

        component.ForeignBirthplaceCity =
            string.IsNullOrWhiteSpace(city) ? null : city.Trim();
        component.ForeignBirthplaceCountry =
            string.IsNullOrWhiteSpace(country) ? null : country.Trim();
        person.Components.Set(component);
    }

    public LocationSnapshot GetLocation(
        IPerson person)
    {
        ArgumentNullException.ThrowIfNull(
            person);

        var component =
            person.Components.Get<
                LocationComponent>();

        if (component is null
            || string.IsNullOrWhiteSpace(component.BirthplaceId)
            || string.IsNullOrWhiteSpace(component.HomeTownId))
        {
            throw new InvalidOperationException(
                $"Location state is missing for " +
                $"{_family.GetDisplayName(person)}. " +
                "Run state reconciliation before reading locations.");
        }

        var birthplace =
            ResolveTown(
                component.BirthplaceId,
                GetBirthYear(person),
                "birthplace");

        var homeTown =
            ResolveTown(
                component.HomeTownId,
                _gameState.Year,
                "home town");

        TownInfo? deathTown = null;

        if (!string.IsNullOrWhiteSpace(
                component.DeathTownId))
        {
            deathTown =
                ResolveTown(
                    component.DeathTownId,
                    person.DeathDate?.Year
                        ?? _gameState.Year,
                    "death town");
        }

        return new LocationSnapshot(
            birthplace,
            homeTown,
            deathTown);
    }

    public LocationSnapshot? GetExistingLocation(
        IPerson person)
    {
        ArgumentNullException.ThrowIfNull(person);

        var component =
            person.Components.Get<LocationComponent>();

        if (component is null
            || string.IsNullOrWhiteSpace(component.BirthplaceId)
            || string.IsNullOrWhiteSpace(component.HomeTownId))
        {
            return null;
        }

        var birthplace =
            FindTownAtYear(
                component.BirthplaceId,
                GetBirthYear(person));

        var homeTown =
            FindTownAtYear(
                component.HomeTownId,
                _gameState.Year);

        if (birthplace is null
            || homeTown is null)
        {
            return null;
        }

        var deathTown =
            string.IsNullOrWhiteSpace(component.DeathTownId)
                ? null
                : FindTownAtYear(
                    component.DeathTownId,
                    person.DeathDate?.Year
                        ?? _gameState.Year);

        return new LocationSnapshot(
            birthplace,
            homeTown,
            deathTown);
    }

    public IReadOnlyList<TownInfo> GetTowns() =>
        _catalog.GetAvailableTowns(
            _gameState.Year);

    public TownInfo? FindTown(string townId)
    {
        if (string.IsNullOrWhiteSpace(townId))
            return null;

        return FindTownAtYear(
            townId,
            _gameState.Year);
    }

    public TownInfo ChoosePropertyTown(
        IPerson householdHead)
    {
        var homeTown =
            GetLocation(
                householdHead)
            .HomeTown;

        var towns =
            GetCurrentTownsRequired();

        var currentHome =
            towns.FirstOrDefault(
                candidate =>
                    candidate.Id.Equals(
                        homeTown.Id,
                        StringComparison.OrdinalIgnoreCase));

        if (currentHome is not null
            && _random.NextDouble()
                < AdditionalHouseSameTownChance)
        {
            return currentHome;
        }

        var nearby =
            towns
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

        if (nearby.Count > 0)
        {
            return ChooseNearbyWeighted(
                nearby);
        }

        return currentHome
            ?? ChooseRandomTown(towns);
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

    private TownInfo ResolveTown(
        string placeId,
        int year,
        string role)
    {
        return FindTownAtYear(placeId, year)
            ?? throw new InvalidOperationException(
                $"Location {role} '{placeId}' is not a valid permanent PlaceId.");
    }

    public TownInfo? FindTownAtYear(
        string placeId,
        int year)
    {
        if (string.IsNullOrWhiteSpace(placeId))
            return null;

        return _catalog.GetTown(
            placeId,
            Math.Max(
                _catalog.MinYear,
                year));
    }

    private int GetBirthYear(
        IPerson person) =>
        person.BirthDate?.Year
        ?? (_gameState.Year - person.Age);

    private IReadOnlyList<TownInfo>
        GetCurrentTownsRequired()
    {
        var towns = GetTowns();

        if (towns.Count == 0)
        {
            throw new InvalidOperationException(
                $"Historical town catalogue has no destinations for {_gameState.Year}.");
        }

        return towns;
    }
}
