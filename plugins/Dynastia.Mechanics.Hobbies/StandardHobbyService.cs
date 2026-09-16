using Dynastia.Contracts;

namespace Dynastia.Mechanics.Hobbies;

public sealed class StandardHobbyService :
    IHobbyService
{
    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly IHouseholdService _households;
    private readonly IExistingLocationService _locations;
    private readonly IPersonalityService _personality;
    private readonly IStatsService _stats;
    private readonly HobbyCatalog _catalog;

    public StandardHobbyService(
        IGameState gameState,
        IFamilyService family,
        IHouseholdService households,
        IExistingLocationService locations,
        IPersonalityService personality,
        IStatsService stats,
        IGameDataService data)
    {
        _gameState = gameState;
        _family = family;
        _households = households;
        _locations = locations;
        _personality = personality;
        _stats = stats;
        _catalog = HobbyCatalog.Load(data);
    }

    public HobbyPersonSnapshot GetHobbies(
        IPerson person)
    {
        ArgumentNullException.ThrowIfNull(person);

        if (person.Age < 5)
            return new HobbyPersonSnapshot(0, []);

        EnsureCurrent(person);

        var component = person.Components.Get<HobbyComponent>();
        if (component is null)
            return new HobbyPersonSnapshot(0, []);

        var hobbies = component.HobbyIds
            .Select(_catalog.Find)
            .Where(hobby => hobby is not null
                && person.Age >= hobby.MinimumAge)
            .Select(hobby => new HobbyInfo(
                hobby!.Id,
                hobby.Name,
                hobby.Emoji))
            .ToList();

        return new HobbyPersonSnapshot(
            component.HobbyCapacity,
            hobbies);
    }

    public IReadOnlyList<HobbyInfo> GenerateCandidateHobbies(
        Guid candidateId,
        Sex sex,
        int age,
        int year,
        string temperament,
        SettlementClass settlementClass)
    {
        if (age < 5)
            return [];

        var available = _catalog.Hobbies
            .Where(hobby => hobby.IsAvailable(year, age))
            .Select(hobby => new WeightedHobby(
                hobby,
                HobbyBalanceRules.TownMultiplier(
                    hobby.TownPreference,
                    settlementClass)
                * HobbyBalanceRules.GenderMultiplier(
                    hobby.GenderPreference,
                    sex)
                * HobbyBalanceRules.TemperamentMultiplier(
                    hobby,
                    temperament)))
            .Where(item => item.Weight > 0)
            .ToList();

        if (available.Count == 0)
            return [];

        var desiredCount = DeterministicHobbyRandom.Roll(
                _gameState.DynastySurname,
                candidateId.ToString("N"),
                year.ToString(),
                "candidate-hobby-count")
            < 0.35
                ? 1
                : 2;

        var result = new List<HobbyInfo>();

        for (var slot = 0;
            slot < desiredCount && available.Count > 0;
            slot++)
        {
            var total = available.Sum(item => item.Weight);
            var target = DeterministicHobbyRandom.Roll(
                    _gameState.DynastySurname,
                    candidateId.ToString("N"),
                    year.ToString(),
                    slot.ToString(),
                    "candidate-hobby")
                * total;

            var cumulative = 0.0;
            var selectedIndex = available.Count - 1;

            for (var index = 0; index < available.Count; index++)
            {
                cumulative += available[index].Weight;
                if (target <= cumulative)
                {
                    selectedIndex = index;
                    break;
                }
            }

            var selected = available[selectedIndex].Hobby;
            result.Add(new HobbyInfo(
                selected.Id,
                selected.Name,
                selected.Emoji));
            available.RemoveAt(selectedIndex);
        }

        return result;
    }

    public void SetHobbies(
        IPerson person,
        IReadOnlyCollection<string> hobbyIds)
    {
        ArgumentNullException.ThrowIfNull(person);
        ArgumentNullException.ThrowIfNull(hobbyIds);

        var valid = hobbyIds
            .Select(_catalog.Find)
            .Where(hobby => hobby is not null)
            .Select(hobby => hobby!.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(HobbyBalanceRules.MaximumHobbies)
            .ToList();

        person.Components.Set(new HobbyComponent
        {
            HobbyCapacity = HobbyBalanceRules.MaximumHobbies,
            HobbyIds = valid,
            LastProcessedYear = _gameState.Year
        });
    }

    public void ReconcileAll()
    {
        foreach (var person in _gameState.People
            .OrderBy(GetBirthYear)
            .ThenBy(person => person.Id))
        {
            if (person.Age >= 5)
                EnsureCurrent(person);
        }
    }

    public void ReconcileAfterLoad() =>
        ReconcileAll();

    internal string? GetThoughtText(
        IPerson person,
        string hobbyId,
        int year)
    {
        var hobby = _catalog.Find(hobbyId);
        if (hobby is null)
            return null;

        var thoughts = _catalog.GetThoughts(hobbyId);
        IReadOnlyList<string> variants;

        if (person.Age <= 11)
        {
            variants = thoughts.Child;
        }
        else if (person.Age <= 17)
        {
            variants = thoughts.Adolescent;
        }
        else
        {
            var intellect = _stats.GetStats(person)
                .FirstOrDefault(stat => stat.Id.Equals(
                    "intellect",
                    StringComparison.OrdinalIgnoreCase))
                ?.Value
                ?? 3;

            variants = intellect <= 1
                ? thoughts.AdultRough
                : intellect >= 5
                    ? thoughts.AdultElaborate
                    : thoughts.AdultNormal;
        }

        if (variants.Count == 0)
            return null;

        return DeterministicHobbyRandom.Choose(
            variants,
            _gameState.DynastySurname,
            person.Id.ToString(),
            hobbyId,
            year.ToString(),
            "hobby-thought");
    }

    private void EnsureCurrent(
        IPerson person)
    {
        if (person.Age < 5)
            return;

        var birthYear = GetBirthYear(person);
        var endYear = person.DeathDate?.Year
            ?? _gameState.Year;

        var component = person.Components.Get<HobbyComponent>();
        if (component is null)
        {
            component = new HobbyComponent
            {
                HobbyCapacity = HobbyBalanceRules.MaximumHobbies,
                LastProcessedYear = birthYear + 4
            };

            person.Components.Set(component);
        }

        if (component.LastProcessedYear <= 0)
            component.LastProcessedYear = birthYear + 4;

        // Capacity is now a simple lifetime maximum. Existing saves from the
        // earlier 0/1/2-capacity experiment are upgraded without removing
        // hobbies already acquired.
        component.HobbyCapacity = HobbyBalanceRules.MaximumHobbies;

        if (component.HobbyIds.Count > component.HobbyCapacity)
        {
            component.HobbyIds = component.HobbyIds
                .Take(component.HobbyCapacity)
                .ToList();
        }

        var firstYear = Math.Max(
            component.LastProcessedYear + 1,
            Math.Max(
                birthYear + 5,
                GameCalendarConfiguration.GameStartYear));

        if (firstYear > endYear)
        {
            component.LastProcessedYear = Math.Max(
                component.LastProcessedYear,
                endYear);
            return;
        }

        for (var year = firstYear; year <= endYear; year++)
        {
            var age = year - birthYear;

            if (component.HobbyIds.Count < component.HobbyCapacity
                && ShouldAttemptAcquisition(person, age, year))
            {
                TryAcquire(
                    person,
                    component,
                    year,
                    useHouseholdInfluence: year == _gameState.Year);
            }

            component.LastProcessedYear = year;
        }
    }

    private bool ShouldAttemptAcquisition(
        IPerson person,
        int age,
        int year)
    {
        if (age < 5)
            return false;

        return Roll(
            person,
            "acquisition",
            year.ToString())
            < HobbyBalanceRules.AnnualAcquisitionChance;
    }

    private void TryAcquire(
        IPerson person,
        HobbyComponent component,
        int year,
        bool useHouseholdInfluence)
    {
        var practicedInHousehold = useHouseholdInfluence
            ? GetHouseholdHobbies(person)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var sex = _family.GetSex(person);
        var temperament = _personality
            .GetPersonality(person)
            ?.Temperament;

        var settlementClass = GetSettlementClass(
            person,
            useCurrentHome: useHouseholdInfluence);

        var weighted = _catalog.Hobbies
            .Where(hobby =>
                hobby.IsHistoricallyAvailable(year)
                && !component.HobbyIds.Contains(
                    hobby.Id,
                    StringComparer.OrdinalIgnoreCase))
            .Select(hobby => new WeightedHobby(
                hobby,
                GetWeight(
                    hobby,
                    sex,
                    temperament,
                    settlementClass,
                    practicedInHousehold)))
            .Where(item => item.Weight > 0)
            .ToList();

        if (weighted.Count == 0)
            return;

        var total = weighted.Sum(item => item.Weight);
        var target = Roll(
                person,
                "selection",
                year.ToString(),
                component.HobbyIds.Count.ToString())
            * total;

        var cumulative = 0.0;
        foreach (var item in weighted)
        {
            cumulative += item.Weight;
            if (target <= cumulative)
            {
                component.HobbyIds.Add(item.Hobby.Id);
                return;
            }
        }

        component.HobbyIds.Add(weighted[^1].Hobby.Id);
    }

    private double GetWeight(
        HobbyDefinition hobby,
        Sex sex,
        string? temperament,
        SettlementClass settlementClass,
        IReadOnlySet<string> practicedInHousehold)
    {
        var weight =
            HobbyBalanceRules.TownMultiplier(
                hobby.TownPreference,
                settlementClass)
            * HobbyBalanceRules.GenderMultiplier(
                hobby.GenderPreference,
                sex)
            * HobbyBalanceRules.TemperamentMultiplier(
                hobby,
                temperament);

        if (practicedInHousehold.Contains(hobby.Id))
            weight *= HobbyBalanceRules.HouseholdInfluenceMultiplier;

        return weight;
    }

    private HashSet<string> GetHouseholdHobbies(
        IPerson person)
    {
        var result = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        var household = _households.GetHouseholdInfo(person);
        if (household is null)
            return result;

        foreach (var memberId in household.MemberIds)
        {
            if (memberId == person.Id)
                continue;

            var member = _gameState.People.FirstOrDefault(
                candidate => candidate.Id == memberId);

            var component = member?.Components.Get<HobbyComponent>();
            if (component is null)
                continue;

            foreach (var hobbyId in component.HobbyIds)
            {
                var hobby = _catalog.Find(hobbyId);
                if (hobby is not null
                    && member is not null
                    && member.Age >= hobby.MinimumAge)
                {
                    result.Add(hobbyId);
                }
            }
        }

        return result;
    }

    private SettlementClass GetSettlementClass(
        IPerson person,
        bool useCurrentHome)
    {
        var location =
            _locations.GetExistingLocation(person);

        if (location is null)
            return SettlementClass.Town;

        return useCurrentHome
            ? location.HomeTown.SettlementClass
            : location.Birthplace.SettlementClass;
    }

    private int GetBirthYear(
        IPerson person) =>
        person.BirthDate?.Year
        ?? (_gameState.Year - person.Age);

    private double Roll(
        IPerson person,
        params string[] parts)
    {
        var seed = new List<string>
        {
            _gameState.DynastySurname,
            person.Id.ToString(),
            "hobbies"
        };

        seed.AddRange(parts);
        return DeterministicHobbyRandom.Roll(seed.ToArray());
    }

    private sealed record WeightedHobby(
        HobbyDefinition Hobby,
        double Weight);
}
