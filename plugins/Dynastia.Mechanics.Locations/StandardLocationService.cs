using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Locations;

public sealed class StandardLocationService :
    ILocationService
{
    private const string TownsPath =
        "Common/towns.csv";

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
        SetHomeTown(
            householdHead,
            town);

        var spouse =
            _family.GetSpouse(
                householdHead);

        if (spouse is not null
            && spouse.Tags.Has(
                "state.alive"))
        {
            SetHomeTown(
                spouse,
                town);
        }

        foreach (var child in
            _family.GetChildren(
                householdHead))
        {
            if (!child.Tags.Has(
                    "state.alive"))
            {
                continue;
            }

            var remainsInHousehold =
                child.Age < 18
                || (
                    _family.GetSex(
                        child)
                        == Sex.Female
                    && _family.GetSpouse(
                        child) is null
                );

            if (remainsInHousehold)
            {
                SetHomeTown(
                    child,
                    town);
            }
        }
    }

    private void OnEventPublished(
        object? sender,
        GameEvent gameEvent)
    {
        if (gameEvent.Type.Equals(
            "game.started",
            StringComparison.OrdinalIgnoreCase))
        {
            InitializeStartingFamily(
                gameEvent);

            return;
        }

        if (gameEvent.Type.Equals(
                "relationship.married",
                StringComparison.OrdinalIgnoreCase)
            || gameEvent.Type.Equals(
                "relationship.partnered",
                StringComparison.OrdinalIgnoreCase)
            || gameEvent.Type.Equals(
                "relationship.remarried",
                StringComparison.OrdinalIgnoreCase))
        {
            InitializeGeneratedSpouse(
                gameEvent);

            return;
        }

        if (gameEvent.Type.Equals(
                "life.birth",
                StringComparison.OrdinalIgnoreCase))
        {
            InitializeNewborn(
                gameEvent);

            return;
        }

        if (gameEvent.Type.Equals(
                "life.death",
                StringComparison.OrdinalIgnoreCase))
        {
            RecordDeathTown(
                gameEvent);
        }
    }

    private void InitializeStartingFamily(
        GameEvent gameEvent)
    {
        var founder =
            FindPerson(
                gameEvent.SubjectId);

        if (founder is null)
            return;

        var father =
            _family.GetFather(
                founder);

        var mother =
            _family.GetMother(
                founder);

        var fatherTown =
            ChoosePopulationWeighted(
                _towns);

        if (father is not null)
        {
            SetLocation(
                father,
                fatherTown,
                fatherTown);
        }

        var householdTown =
            fatherTown;

        if (mother is not null)
        {
            var motherBirthplace =
                ChooseSpouseBirthplace(
                    householdTown);

            SetLocation(
                mother,
                motherBirthplace,
                householdTown);
        }

        var founderBirthplace =
            ChooseChildBirthplace(
                householdTown);

        SetLocation(
            founder,
            founderBirthplace,
            householdTown);
    }

    private void InitializeGeneratedSpouse(
        GameEvent gameEvent)
    {
        var anchor =
            FindPerson(
                gameEvent.SubjectId);

        var spouse =
            FindRelatedPerson(
                gameEvent,
                0);

        if (anchor is null
            || spouse is null)
        {
            return;
        }

        var anchorLocation =
            GetLocation(
                anchor);

        var existing =
            spouse.Components.Get<
                LocationComponent>();

        if (existing?.Birthplace is null)
        {
            existing ??=
                new LocationComponent();

            existing.Birthplace =
                ChooseSpouseBirthplace(
                    anchorLocation.HomeTown);

            spouse.Components.Set(
                existing);
        }

        existing.HomeTown =
            anchorLocation.HomeTown;
    }

    private void InitializeNewborn(
        GameEvent gameEvent)
    {
        var child =
            FindPerson(
                gameEvent.SubjectId);

        var father =
            FindRelatedPerson(
                gameEvent,
                0);

        var mother =
            FindRelatedPerson(
                gameEvent,
                1);

        if (child is null)
            return;

        TownInfo householdTown;

        if (father is not null)
        {
            householdTown =
                GetLocation(
                    father)
                .HomeTown;
        }
        else if (mother is not null)
        {
            householdTown =
                GetLocation(
                    mother)
                .HomeTown;
        }
        else
        {
            householdTown =
                ChoosePopulationWeighted(
                    _towns);
        }

        var birthplace =
            ChooseChildBirthplace(
                householdTown);

        SetLocation(
            child,
            birthplace,
            householdTown);
    }

    private void RecordDeathTown(
        GameEvent gameEvent)
    {
        var person =
            FindPerson(
                gameEvent.SubjectId);

        if (person is null)
            return;

        var location =
            person.Components.Get<
                LocationComponent>();

        if (location?.HomeTown is null)
        {
            EnsureFallbackLocation(
                person);

            location =
                person.Components.Get<
                    LocationComponent>();
        }

        if (location?.HomeTown is null)
            return;

        location.DeathTown =
            location.HomeTown;

        person.Components.Set(
            location);
    }

    private void EnsureFallbackLocation(
        IPerson person)
    {
        var existing =
            person.Components.Get<
                LocationComponent>();

        if (existing?.Birthplace is not null
            && existing.HomeTown is not null)
        {
            return;
        }

        var father =
            _family.GetFather(
                person);

        if (father is not null
            && father.Id != person.Id)
        {
            var parentLocation =
                GetLocation(
                    father);

            SetLocation(
                person,
                ChooseChildBirthplace(
                    parentLocation.HomeTown),
                parentLocation.HomeTown);

            return;
        }

        var spouse =
            _family.GetSpouse(
                person);

        var spouseComponent =
            spouse?.Components.Get<
                LocationComponent>();

        if (spouseComponent?.HomeTown is not null)
        {
            SetLocation(
                person,
                ChooseSpouseBirthplace(
                    spouseComponent.HomeTown),
                spouseComponent.HomeTown);

            return;
        }

        var town =
            ChoosePopulationWeighted(
                _towns);

        SetLocation(
            person,
            town,
            town);
    }

    private TownInfo ChooseSpouseBirthplace(
        TownInfo householdTown)
    {
        var roll =
            _random.NextDouble();

        if (roll
            < SpouseSameTownChance)
        {
            return householdTown;
        }

        if (roll
            < SpouseSameTownChance
                + SpouseNearbyChance)
        {
            var nearby =
                _towns
                    .Where(
                        candidate =>
                            !IsSameTown(
                                candidate,
                                householdTown))
                    .Select(
                        candidate =>
                            new WeightedTown(
                                candidate,
                                DistanceKm(
                                    householdTown,
                                    candidate)))
                    .Where(
                        candidate =>
                            candidate.DistanceKm
                            <= NearbySpouseRadiusKm)
                    .ToList();

            if (nearby.Count > 0)
            {
                return ChooseNearbyWeighted(
                    nearby);
            }
        }

        // The remaining branch is intentionally small (8%) in normal
        // circumstances, but also acts as a safe fallback when no nearby
        // town exists.
        return ChooseRandomTown();
    }

    private TownInfo ChooseChildBirthplace(
        TownInfo householdTown)
    {
        if (_random.NextDouble()
            < ChildSameTownChance)
        {
            return householdTown;
        }

        var largerNearby =
            _towns
                .Where(
                    candidate =>
                        candidate.Population
                            > householdTown.Population
                        && !IsSameTown(
                            candidate,
                            householdTown))
                .Select(
                    candidate =>
                        new WeightedTown(
                            candidate,
                            DistanceKm(
                                householdTown,
                                candidate)))
                .Where(
                    candidate =>
                        candidate.DistanceKm
                        <= NearbyBirthRadiusKm)
                .ToList();

        if (largerNearby.Count == 0)
            return householdTown;

        return ChooseNearbyWeighted(
            largerNearby);
    }

    private TownInfo ChooseNearbyWeighted(
        IReadOnlyList<WeightedTown> candidates)
    {
        return ChooseWeighted(
            candidates,
            candidate =>
            {
                var populationFactor =
                    Math.Sqrt(
                        Math.Max(
                            1,
                            candidate.Town.Population));

                var proximityFactor =
                    1.0
                    / Math.Pow(
                        1.0
                        + candidate.DistanceKm
                            / 30.0,
                        2.0);

                return populationFactor
                    * proximityFactor;
            })
            .Town;
    }

    private TownInfo ChooseRandomTown()
    {
        return _towns[
            _random.NextInt(
                0,
                _towns.Count - 1)];
    }

    private TownInfo ChoosePopulationWeighted(
        IReadOnlyList<TownInfo> towns)
    {
        return ChooseWeighted(
            towns,
            town =>
                Math.Max(
                    1,
                    town.Population));
    }

    private T ChooseWeighted<T>(
        IReadOnlyList<T> items,
        Func<T, double> weightSelector)
    {
        var weighted =
            items
                .Select(
                    item =>
                        new
                        {
                            Item =
                                item,

                            Weight =
                                Math.Max(
                                    0,
                                    weightSelector(item))
                        })
                .ToList();

        var total =
            weighted.Sum(
                item =>
                    item.Weight);

        if (total <= 0)
        {
            return items[
                _random.NextInt(
                    0,
                    items.Count - 1)];
        }

        var roll =
            _random.NextDouble()
            * total;

        foreach (var item in
            weighted)
        {
            if (roll
                < item.Weight)
            {
                return item.Item;
            }

            roll -=
                item.Weight;
        }

        return weighted[^1].Item;
    }

    private static IReadOnlyList<TownInfo>
        ParseTowns(
            string text)
    {
        var result =
            new List<TownInfo>();

        var lines =
            text.Split(
                ['\r', '\n'],
                StringSplitOptions
                    .RemoveEmptyEntries);

        for (var index = 1;
            index < lines.Length;
            index++)
        {
            var fields =
                lines[index]
                    .Split(',');

            if (fields.Length != 5)
            {
                throw new InvalidDataException(
                    $"Invalid towns.csv row " +
                    $"{index + 1}: expected 5 fields.");
            }

            if (!double.TryParse(
                    fields[2],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var longitude)
                || !double.TryParse(
                    fields[3],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var latitude)
                || !int.TryParse(
                    fields[4],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var population))
            {
                throw new InvalidDataException(
                    $"Invalid numeric town data on row " +
                    $"{index + 1}.");
            }

            result.Add(
                new TownInfo(
                    fields[0],
                    fields[1],
                    longitude,
                    latitude,
                    population));
        }

        return result;
    }

    private void SetHomeTown(
        IPerson person,
        TownInfo homeTown)
    {
        var component =
            person.Components.Get<
                LocationComponent>()
            ?? new LocationComponent();

        component.Birthplace ??=
            homeTown;

        component.HomeTown =
            homeTown;

        person.Components.Set(
            component);
    }

    private void SetLocation(
        IPerson person,
        TownInfo birthplace,
        TownInfo homeTown)
    {
        var component =
            person.Components.Get<
                LocationComponent>()
            ?? new LocationComponent();

        component.Birthplace =
            birthplace;

        component.HomeTown =
            homeTown;

        person.Components.Set(
            component);
    }

    private IPerson? FindPerson(
        Guid? id)
    {
        if (id is null)
            return null;

        return _gameState.People
            .FirstOrDefault(
                person =>
                    person.Id
                    == id.Value);
    }

    private IPerson? FindRelatedPerson(
        GameEvent gameEvent,
        int index)
    {
        if (index < 0
            || index
                >= gameEvent
                    .RelatedPersonIds
                    .Count)
        {
            return null;
        }

        return FindPerson(
            gameEvent
                .RelatedPersonIds[
                    index]);
    }

    private static bool IsSameTown(
        TownInfo first,
        TownInfo second)
    {
        return Math.Abs(
                   first.Longitude
                   - second.Longitude)
                < 0.000001
            && Math.Abs(
                   first.Latitude
                   - second.Latitude)
                < 0.000001
            && first.Town.Equals(
                second.Town,
                StringComparison.OrdinalIgnoreCase);
    }

    private static double DistanceKm(
        TownInfo first,
        TownInfo second)
    {
        var firstLatitude =
            DegreesToRadians(
                first.Latitude);

        var secondLatitude =
            DegreesToRadians(
                second.Latitude);

        var latitudeDelta =
            DegreesToRadians(
                second.Latitude
                - first.Latitude);

        var longitudeDelta =
            DegreesToRadians(
                second.Longitude
                - first.Longitude);

        var sinLatitude =
            Math.Sin(
                latitudeDelta / 2);

        var sinLongitude =
            Math.Sin(
                longitudeDelta / 2);

        var a =
            sinLatitude
                * sinLatitude
            + Math.Cos(
                firstLatitude)
            * Math.Cos(
                secondLatitude)
            * sinLongitude
            * sinLongitude;

        var clampedA =
            Math.Clamp(
                a,
                0,
                1);

        var c =
            2
            * Math.Atan2(
                Math.Sqrt(
                    clampedA),
                Math.Sqrt(
                    1 - clampedA));

        return EarthRadiusKm
            * c;
    }

    private static double DegreesToRadians(
        double degrees)
    {
        return degrees
            * Math.PI
            / 180.0;
    }

    private sealed record WeightedTown(
        TownInfo Town,
        double DistanceKm);
}
