using Dynastia.Contracts;

namespace Dynastia.Mechanics.Hobbies;

public sealed class StandardHobbyService : IHobbyService
{
    private const string ContextPath = "Hobbies/hobby_context_weights.csv";

    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly IHouseholdService _households;
    private readonly IExistingLocationService _locations;
    private readonly IPersonalityService _personality;
    private readonly IStatsService _stats;
    private readonly HobbyCatalog _catalog;
    private readonly IContextWeightCatalog _context;

    public StandardHobbyService(
        IGameState gameState,
        IFamilyService family,
        IHouseholdService households,
        IExistingLocationService locations,
        IPersonalityService personality,
        IStatsService stats,
        IGameDataService data,
        IContextWeightService contextWeights)
    {
        _gameState = gameState;
        _family = family;
        _households = households;
        _locations = locations;
        _personality = personality;
        _stats = stats;
        _catalog = HobbyCatalog.Load(data);
        _context = contextWeights.LoadCatalog(ContextPath, _catalog.Hobbies.Select(hobby => hobby.Id));
    }

    public HobbyPersonSnapshot GetHobbies(IPerson person)
    {
        ArgumentNullException.ThrowIfNull(person);

        if (person.Age < 5)
            return new HobbyPersonSnapshot(0, []);

        var component = person.Components.Get<HobbyComponent>()
            ?? throw new InvalidOperationException(
                "Hobby state is missing. Run state reconciliation before reading it.");

        var hobbies = component.HobbyIds
            .Select(_catalog.Find)
            .Where(hobby => hobby is not null && person.Age >= hobby.MinimumAge)
            .Select(hobby => new HobbyInfo(hobby!.Id, hobby.Name, hobby.Emoji))
            .ToList();

        return new HobbyPersonSnapshot(component.HobbyCapacity, hobbies);
    }

    public IReadOnlyList<HobbyInfo> GenerateCandidateHobbies(
        Guid candidateId,
        Sex sex,
        int age,
        int year,
        string temperament,
        SettlementClass settlementClass,
        int strength,
        int intellect,
        int appeal)
    {
        if (age < 5)
            return [];

        var stats = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["strength"] = strength,
            ["intellect"] = intellect,
            ["appeal"] = appeal
        };

        var acquired = new List<HobbyDefinition>();
        var birthYear = year - age;

        for (var lifeAge = 5; lifeAge <= age && acquired.Count < HobbyBalanceRules.MaximumHobbies; lifeAge++)
        {
            var acquisitionYear = birthYear + lifeAge;
            // Hobbies use 1700 as the catalogue floor rather than an invention date.
            // Generated adults can therefore receive retrospective childhood/adolescent
            // acquisition rolls even when part of their life predates the playable era.
            var contextYear = Math.Max(
                acquisitionYear,
                GameCalendarConfiguration.GameStartYear);

            var opportunityRoll = DeterministicHobbyRandom.Roll(
                _gameState.DynastySurname,
                candidateId.ToString("N"),
                acquisitionYear.ToString(),
                "candidate-hobby-acquisition");

            if (opportunityRoll >= HobbyBalanceRules.AnnualAcquisitionChance)
                continue;

            var weighted = _catalog.Hobbies
                .Where(hobby => hobby.IsAvailable(contextYear, lifeAge)
                    && acquired.All(existing => !existing.Id.Equals(hobby.Id, StringComparison.OrdinalIgnoreCase)))
                .Select(hobby => new WeightedHobby(
                    hobby,
                    GetWeight(
                        hobby,
                        contextYear,
                        lifeAge,
                        sex,
                        temperament,
                        settlementClass,
                        stats,
                        practicedInHousehold: EmptySet)))
                .Where(item => item.Weight > 0)
                .ToList();

            if (weighted.Count == 0)
                continue;

            acquired.Add(ChooseWeighted(
                weighted,
                DeterministicHobbyRandom.Roll(
                    _gameState.DynastySurname,
                    candidateId.ToString("N"),
                    acquisitionYear.ToString(),
                    acquired.Count.ToString(),
                    "candidate-hobby-selection")));
        }

        return acquired
            .Select(hobby => new HobbyInfo(hobby.Id, hobby.Name, hobby.Emoji))
            .ToList();
    }

    public void SetHobbies(IPerson person, IReadOnlyCollection<string> hobbyIds)
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
        foreach (var person in _gameState.People.OrderBy(GetBirthYear).ThenBy(person => person.Id))
        {
            if (person.Age >= 5)
                EnsureCurrent(person);
        }
    }

    public void ReconcileAfterLoad() => ReconcileAll();

    internal string? GetThoughtText(IPerson person, string hobbyId, int year)
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
            var intellect = GetStats(person)["intellect"];
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

    private void EnsureCurrent(IPerson person)
    {
        if (person.Age < 5)
            return;

        var birthYear = GetBirthYear(person);
        var endYear = person.DeathDate?.Year ?? _gameState.Year;
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

        component.HobbyCapacity = HobbyBalanceRules.MaximumHobbies;
        if (component.HobbyIds.Count > component.HobbyCapacity)
            component.HobbyIds = component.HobbyIds.Take(component.HobbyCapacity).ToList();

        var firstYear = Math.Max(
            component.LastProcessedYear + 1,
            Math.Max(birthYear + 5, GameCalendarConfiguration.GameStartYear));

        if (firstYear > endYear)
        {
            component.LastProcessedYear = Math.Max(component.LastProcessedYear, endYear);
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
                    age,
                    useHouseholdInfluence: year == _gameState.Year);
            }

            component.LastProcessedYear = year;
        }
    }

    private bool ShouldAttemptAcquisition(IPerson person, int age, int year)
    {
        if (age < 5)
            return false;

        return Roll(person, "acquisition", year.ToString())
            < HobbyBalanceRules.AnnualAcquisitionChance;
    }

    private void TryAcquire(
        IPerson person,
        HobbyComponent component,
        int year,
        int age,
        bool useHouseholdInfluence)
    {
        var practicedInHousehold = useHouseholdInfluence
            ? GetHouseholdHobbies(person)
            : EmptySet;

        var sex = _family.GetSex(person);
        var temperament = _personality.GetPersonality(person)?.Temperament;
        var settlementClass = GetSettlementClass(person, useCurrentHome: useHouseholdInfluence);
        var stats = GetStats(person);

        var weighted = _catalog.Hobbies
            .Where(hobby => hobby.IsAvailable(year, age)
                && !component.HobbyIds.Contains(hobby.Id, StringComparer.OrdinalIgnoreCase))
            .Select(hobby => new WeightedHobby(
                hobby,
                GetWeight(
                    hobby,
                    year,
                    age,
                    sex,
                    temperament,
                    settlementClass,
                    stats,
                    practicedInHousehold)))
            .Where(item => item.Weight > 0)
            .ToList();

        if (weighted.Count == 0)
            return;

        var selected = ChooseWeighted(
            weighted,
            Roll(person, "selection", year.ToString(), component.HobbyIds.Count.ToString()));
        component.HobbyIds.Add(selected.Id);
    }

    private double GetWeight(
        HobbyDefinition hobby,
        int year,
        int age,
        Sex sex,
        string? temperament,
        SettlementClass settlementClass,
        IReadOnlyDictionary<string, int> stats,
        IReadOnlySet<string> practicedInHousehold)
    {
        var context = new ContextWeightContext(
            year,
            age,
            sex,
            temperament,
            SettlementClass: settlementClass);

        var weight = hobby.BaseWeight
            * HobbyBalanceRules.TownMultiplier(hobby.TownPreference, settlementClass)
            * HobbyBalanceRules.TemperamentMultiplier(hobby, temperament)
            * HobbyBalanceRules.StatMultiplier(hobby, stats)
            * _context.GetMultiplier(hobby.Id, context);

        if (practicedInHousehold.Contains(hobby.Id))
            weight *= HobbyBalanceRules.HouseholdInfluenceMultiplier;

        return weight;
    }

    private static HobbyDefinition ChooseWeighted(
        IReadOnlyList<WeightedHobby> weighted,
        double unitRoll)
    {
        var total = weighted.Sum(item => item.Weight);
        var target = unitRoll * total;
        var cumulative = 0.0;

        foreach (var item in weighted)
        {
            cumulative += item.Weight;
            if (target <= cumulative)
                return item.Hobby;
        }

        return weighted[^1].Hobby;
    }

    private HashSet<string> GetHouseholdHobbies(IPerson person)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var household = _households.GetHouseholdInfo(person);
        if (household is null)
            return result;

        foreach (var memberId in household.MemberIds)
        {
            if (memberId == person.Id)
                continue;

            var member = _gameState.People.FirstOrDefault(candidate => candidate.Id == memberId);
            var component = member?.Components.Get<HobbyComponent>();
            if (component is null)
                continue;

            foreach (var hobbyId in component.HobbyIds)
            {
                var hobby = _catalog.Find(hobbyId);
                if (hobby is not null && member is not null && member.Age >= hobby.MinimumAge)
                    result.Add(hobbyId);
            }
        }

        return result;
    }

    private SettlementClass GetSettlementClass(IPerson person, bool useCurrentHome)
    {
        var location = _locations.GetExistingLocation(person);
        if (location is null)
            return SettlementClass.Town;

        return useCurrentHome
            ? location.HomeTown.SettlementClass
            : location.Birthplace.SettlementClass;
    }

    private IReadOnlyDictionary<string, int> GetStats(IPerson person) =>
        _stats.GetStats(person)
            .Where(stat => stat.Id is "strength" or "intellect" or "appeal")
            .ToDictionary(stat => stat.Id, stat => stat.Value, StringComparer.OrdinalIgnoreCase);

    private int GetBirthYear(IPerson person) =>
        person.BirthDate?.Year ?? (_gameState.Year - person.Age);

    private double Roll(IPerson person, params string[] parts)
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

    private static readonly IReadOnlySet<string> EmptySet =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private sealed record WeightedHobby(HobbyDefinition Hobby, double Weight);
}
