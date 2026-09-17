using Dynastia.Contracts;

namespace Dynastia.Mechanics.Crafts;

internal sealed class StandardCraftService : ICraftService, IIncomeProvider
{
    private const string ContextPath = "Crafts/craft_context_weights.csv";

    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly IEconomyService _economy;
    private readonly ICareerService _career;
    private readonly IStatsService _stats;
    private readonly IPersonalityService _personality;
    private readonly ILocalCareerOpportunityService _localOpportunities;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;
    private readonly CraftCatalog _catalog;
    private readonly IContextWeightCatalog _context;

    public StandardCraftService(
        IGameState gameState,
        IFamilyService family,
        IEconomyService economy,
        ICareerService career,
        IStatsService stats,
        IPersonalityService personality,
        ILocalCareerOpportunityService localOpportunities,
        IGameRandom random,
        IGameEventBus events,
        IContextWeightService contextWeights,
        CraftCatalog catalog)
    {
        _gameState = gameState;
        _family = family;
        _economy = economy;
        _career = career;
        _stats = stats;
        _personality = personality;
        _localOpportunities = localOpportunities;
        _random = random;
        _events = events;
        _catalog = catalog;
        _context = contextWeights.LoadCatalog(ContextPath, catalog.All.Select(craft => craft.Id));
    }

    public string Id => "crafts";

    public IReadOnlyList<CraftInfo> Catalog =>
        _catalog.All.Select(craft => _catalog.Present(craft, _gameState.Year)).ToList();

    public CraftSnapshot GetSnapshot(IPerson person)
    {
        var component = GetRequired(person);
        var active = _catalog.Find(component.ActiveCraftOccupationId);
        var presented = active is null ? null : _catalog.Present(active, _gameState.Year);

        return new CraftSnapshot(
            GetKnownCrafts(person),
            active?.Id,
            presented is null ? null : $"Self-employed {presented.SelfEmploymentTitle}",
            presented?.Emoji,
            component.CraftWorkYearsByCraft.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.OrdinalIgnoreCase),
            GetExpectedAnnualIncome(person),
            component.LastAnnualIncome,
            component.LastIncomeYear);
    }

    public IReadOnlyList<CraftInfo> GetKnownCrafts(IPerson person)
    {
        var component = GetRequired(person);
        return component.CraftIds
            .Select(_catalog.Find)
            .Where(craft => craft is not null)
            .Select(craft => _catalog.Present(craft!, _gameState.Year))
            .ToList();
    }

    public bool KnowsCraft(IPerson person, string craftId)
    {
        var canonical = _catalog.CanonicalizeId(craftId);
        return canonical is not null
            && GetRequired(person).CraftIds.Contains(canonical, StringComparer.OrdinalIgnoreCase);
    }

    public bool IsSelfEmployed(IPerson person) => GetActiveCraft(person) is not null;

    public CraftInfo? GetActiveCraft(IPerson person)
    {
        var active = _catalog.Find(GetRequired(person).ActiveCraftOccupationId);
        return active is null ? null : _catalog.Present(active, _gameState.Year);
    }

    public bool CanLearnCraft(IPerson person, string craftId)
    {
        var craft = _catalog.Find(craftId);
        if (craft is null || !person.Tags.Has("state.alive") || SimulationState.IsInactive(person))
            return false;

        var component = GetRequired(person);
        if (component.CraftIds.Count >= CraftRules.MaximumCrafts
            || component.CraftIds.Contains(craft.Id, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var location = _localOpportunities.GetOpportunitySnapshot(person);
        var tags = OpportunityTags(location);
        return craft.MeetsHardAvailability(
            _gameState.Year,
            person.Age,
            location.Town.SettlementClass,
            tags);
    }

    public double GetLearningWeight(IPerson person, string craftId)
    {
        var craft = _catalog.Find(craftId);
        if (craft is null || !CanLearnCraft(person, craft.Id))
            return 0.0;

        var location = _localOpportunities.GetOpportunitySnapshot(person);
        var stats = GetCraftStats(person);
        var temperament = _personality.GetPersonality(person)?.Temperament;
        return GetSelectionWeight(
            craft,
            _gameState.Year,
            person.Age,
            _family.GetSex(person),
            stats,
            temperament,
            location);
    }

    public bool LearnCraft(IPerson person, string craftId)
    {
        var craft = _catalog.Find(craftId);
        if (craft is null || !CanLearnCraft(person, craft.Id))
            return false;

        GetRequired(person).CraftIds.Add(craft.Id);
        return true;
    }

    public void SetCrafts(IPerson person, IEnumerable<string> craftIds)
    {
        ArgumentNullException.ThrowIfNull(craftIds);
        var component = GetRequired(person);
        component.CraftIds = craftIds
            .Select(_catalog.CanonicalizeId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(CraftRules.MaximumCrafts)
            .ToList();
        Normalize(component);
    }

    public IReadOnlyList<string> GenerateCandidateCraftIds(
        string deterministicKey,
        string? formalCareerId,
        int year,
        Sex sex,
        int age,
        int strength,
        int intellect,
        string temperament,
        TownInfo town)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deterministicKey);
        ArgumentNullException.ThrowIfNull(town);

        var location = _localOpportunities.GetOpportunitySnapshot(town);
        var tags = OpportunityTags(location);
        var stats = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["strength"] = strength,
            ["intellect"] = intellect
        };

        var available = _catalog.All
            .Where(craft => craft.MeetsHardAvailability(year, age, town.SettlementClass, tags))
            .Select(craft => new WeightedCraft(
                craft,
                GetSelectionWeight(craft, year, age, sex, stats, temperament, location)
                * CareerAffinityMultiplier(craft, formalCareerId)))
            .Where(item => item.Weight > 0)
            .ToList();

        if (available.Count == 0)
            return [];

        var random = new DeterministicCraftRandom(
            $"{_gameState.DynastySurname}|{deterministicKey}|candidate-crafts");

        var hasCareerLinkedCraft = !string.IsNullOrWhiteSpace(formalCareerId)
            && available.Any(item => CareerAffinityMultiplier(item.Craft, formalCareerId) > 1.0);
        var acquisitionChance = hasCareerLinkedCraft
            ? 0.70
            : CraftRules.GetGeneratedAdultBaseChance(year);

        var result = new List<string>();
        if (random.NextDouble() < acquisitionChance)
        {
            var selected = ChooseWeighted(available, random.NextDouble());
            result.Add(selected.Id);
        }

        if (result.Count > 0 && random.NextDouble() < 0.10)
        {
            var remaining = available
                .Where(item => !result.Contains(item.Craft.Id, StringComparer.OrdinalIgnoreCase))
                .ToList();
            if (remaining.Count > 0)
                result.Add(ChooseWeighted(remaining, random.NextDouble()).Id);
        }

        return result;
    }

    internal IReadOnlyList<string> GenerateCraftIdsForPerson(
        IPerson person,
        string? formalCareerId,
        int year)
    {
        var location = _localOpportunities.GetOpportunitySnapshot(person);
        var stats = GetCraftStats(person);
        return GenerateCandidateCraftIds(
            person.Id.ToString("N"),
            formalCareerId,
            year,
            _family.GetSex(person),
            person.Age,
            stats["strength"],
            stats["intellect"],
            _personality.GetPersonality(person)?.Temperament ?? "Phlegmatic",
            location.Town);
    }

    public bool StartOccupation(IPerson person, string craftId)
    {
        var craft = _catalog.Find(craftId);
        if (craft is null
            || !craft.IsHistoricallyAvailable(_gameState.Year)
            || !person.Tags.Has("state.alive")
            || person.Age < 18
            || person.Tags.Has("state.imprisoned")
            || !KnowsCraft(person, craft.Id))
        {
            return false;
        }

        var component = GetRequired(person);
        if (component.ActiveCraftOccupationId?.Equals(craft.Id, StringComparison.OrdinalIgnoreCase) == true)
            return true;

        var career = _career.GetCareer(person);
        if (!career.IsRetired)
            _career.AssignCareer(person, null, 0, career.JobSatisfaction);

        component.ActiveCraftOccupationId = craft.Id;
        person.Tags.Add("career.craft_self_employed");
        person.Tags.Add("employment.craft");

        var displayName = _catalog.ResolveDisplayName(craft.Id, _gameState.Year);
        _events.Publish(new GameEvent
        {
            Type = "craft.self_employment_started",
            Year = _gameState.Year,
            SubjectId = person.Id,
            Data = new Dictionary<string, string>
            {
                ["craftId"] = craft.Id,
                ["craftName"] = displayName,
                ["text"] =
                    $"At age {person.Age}, {_family.GetDisplayName(person)} began earning a living as a self-employed {craft.SelfEmploymentTitle.ToLowerInvariant()}."
            }
        });

        return true;
    }

    public bool EndOccupation(IPerson person, string reason = "ended")
    {
        var component = GetRequired(person);
        var craft = _catalog.Find(component.ActiveCraftOccupationId);
        if (craft is null)
        {
            component.ActiveCraftOccupationId = null;
            person.Tags.Remove("career.craft_self_employed");
            person.Tags.Remove("employment.craft");
            return false;
        }

        component.ActiveCraftOccupationId = null;
        person.Tags.Remove("career.craft_self_employed");
        person.Tags.Remove("employment.craft");

        var displayName = _catalog.ResolveDisplayName(craft.Id, _gameState.Year);
        _events.Publish(new GameEvent
        {
            Type = "craft.self_employment_ended",
            Year = _gameState.Year,
            SubjectId = person.Id,
            Data = new Dictionary<string, string>
            {
                ["craftId"] = craft.Id,
                ["craftName"] = displayName,
                ["reason"] = reason,
                ["text"] = reason.Equals("formal employment", StringComparison.OrdinalIgnoreCase)
                    ? $"{_family.GetDisplayName(person)} left self-employment in {displayName} to take a formal position."
                    : reason.Equals("retirement", StringComparison.OrdinalIgnoreCase)
                        ? $"{_family.GetDisplayName(person)} retired from self-employment in {displayName}."
                        : reason.Equals("relocation", StringComparison.OrdinalIgnoreCase)
                            ? $"{_family.GetDisplayName(person)} ended self-employment in {displayName} after moving household."
                            : $"{_family.GetDisplayName(person)} ended self-employment in {displayName}."
            }
        });

        return true;
    }

    public double GetApplicationBonus(IPerson person, string careerId) =>
        CraftRules.GetApplicationBonus(GetKnownCrafts(person), careerId);

    public CraftCareerExperience GetCareerExperience(IPerson person, string careerId)
    {
        var component = GetRequired(person);
        var exact = 0;
        var related = 0;

        foreach (var pair in component.CraftWorkYearsByCraft)
        {
            var craft = _catalog.Find(pair.Key);
            if (craft is null || pair.Value <= 0)
                continue;

            if (craft.PrimaryCareerIds.Contains(careerId, StringComparer.OrdinalIgnoreCase))
                exact += pair.Value;
            else if (craft.SecondaryCareerIds.Contains(careerId, StringComparer.OrdinalIgnoreCase))
                related += pair.Value;
        }

        return new CraftCareerExperience(exact, related);
    }

    public decimal GetExpectedAnnualIncome(IPerson person)
    {
        var active = _catalog.Find(GetRequired(person).ActiveCraftOccupationId);
        if (active is null || string.IsNullOrWhiteSpace(active.PrimaryCareerId))
            return 0m;

        return Math.Round(
            _career.GetLevelOneSalary(active.PrimaryCareerId),
            0,
            MidpointRounding.AwayFromZero);
    }

    public decimal GetAnnualIncome(IPerson person)
    {
        var component = GetRequired(person);
        var active = _catalog.Find(component.ActiveCraftOccupationId);
        if (active is null || string.IsNullOrWhiteSpace(active.PrimaryCareerId))
            return 0m;

        if (!person.Tags.Has("state.alive") || person.Tags.Has("state.imprisoned"))
        {
            component.LastAnnualIncome = 0m;
            component.LastIncomeYear = _gameState.Year;
            return 0m;
        }

        var annualReference = _career.GetLevelOneSalary(active.PrimaryCareerId);
        decimal total = 0m;
        for (var month = 0; month < 12; month++)
            total += annualReference / 12m * (decimal)(_random.NextDouble() * 2.0);

        var recoverReduction = ReadPercent(person, "modifier.salary.recover.");
        if (recoverReduction > 0m)
            total *= 1m - recoverReduction / 100m;

        var rounded = Math.Round(total, 0, MidpointRounding.AwayFromZero);
        component.LastAnnualIncome = rounded;
        component.LastIncomeYear = _gameState.Year;

        var performance = annualReference <= 0m
            ? "none"
            : rounded >= annualReference * 1.45m
                ? "strong"
                : rounded <= annualReference * 0.55m
                    ? "poor"
                    : "ordinary";

        _events.Publish(new GameEvent
        {
            Type = "craft.income",
            Year = _gameState.Year,
            SubjectId = person.Id,
            Data = new Dictionary<string, string>
            {
                ["craftId"] = active.Id,
                ["craftName"] = _catalog.ResolveDisplayName(active.Id, _gameState.Year),
                ["amount"] = rounded.ToString(),
                ["expected"] = annualReference.ToString(),
                ["performance"] = performance,
                ["suppressChronicle"] = "true"
            }
        });

        return rounded;
    }

    decimal IIncomeProvider.GetExpectedAnnualIncome(IPerson person) =>
        GetExpectedAnnualIncome(person);

    internal void RecordWorkYear(IPerson person)
    {
        var component = GetRequired(person);
        var active = _catalog.Find(component.ActiveCraftOccupationId);
        if (active is null
            || !person.Tags.Has("state.alive")
            || person.Tags.Has("state.imprisoned"))
        {
            return;
        }

        component.CraftWorkYearsByCraft.TryGetValue(active.Id, out var years);
        component.CraftWorkYearsByCraft[active.Id] = years + 1;
    }

    private double GetSelectionWeight(
        CraftInfo craft,
        int year,
        int age,
        Sex sex,
        IReadOnlyDictionary<string, int> stats,
        string? temperament,
        LocationOpportunitySnapshot location)
    {
        var tags = OpportunityTags(location);
        if (!craft.MeetsHardAvailability(year, age, location.Town.SettlementClass, tags))
            return 0.0;

        var context = new ContextWeightContext(
            year,
            age,
            sex,
            temperament,
            SettlementClass: location.Town.SettlementClass);

        return craft.BaseWeight
            * CraftRules.TownMultiplier(craft.TownPreference, location.Town.SettlementClass)
            * CraftRules.GetStatSelectionMultiplier(craft, stats)
            * CraftRules.PreferredOpportunityMultiplier(craft, tags)
            * _context.GetMultiplier(craft.Id, context);
    }

    private static double CareerAffinityMultiplier(CraftInfo craft, string? formalCareerId)
    {
        if (string.IsNullOrWhiteSpace(formalCareerId))
            return 1.0;
        if (craft.PrimaryCareerIds.Contains(formalCareerId, StringComparer.OrdinalIgnoreCase))
            return 3.0;
        if (craft.SecondaryCareerIds.Contains(formalCareerId, StringComparer.OrdinalIgnoreCase))
            return 1.75;
        return 1.0;
    }

    private static CraftInfo ChooseWeighted(IReadOnlyList<WeightedCraft> choices, double unitRoll)
    {
        var total = choices.Sum(choice => choice.Weight);
        var target = unitRoll * total;
        var cumulative = 0.0;
        foreach (var choice in choices)
        {
            cumulative += choice.Weight;
            if (target <= cumulative)
                return choice.Craft;
        }
        return choices[^1].Craft;
    }

    private IReadOnlyDictionary<string, int> GetCraftStats(IPerson person) =>
        _stats.GetStats(person)
            .Where(stat => stat.Id is "strength" or "intellect")
            .ToDictionary(stat => stat.Id, stat => stat.Value, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlySet<string> OpportunityTags(LocationOpportunitySnapshot location) =>
        location.RegionOpportunityTags
            .Concat(location.TownOpportunityTags)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static decimal ReadPercent(IPerson person, string prefix)
    {
        foreach (var tag in person.Tags.All)
        {
            if (!tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;
            if (decimal.TryParse(tag[prefix.Length..], out var percent))
                return Math.Clamp(percent, 0m, 50m);
        }
        return 0m;
    }

    private CraftComponent GetRequired(IPerson person)
    {
        var component = person.Components.Get<CraftComponent>();
        if (component is not null)
        {
            Normalize(component);
            return component;
        }

        component = new CraftComponent();
        person.Components.Set(component);
        return component;
    }

    private void Normalize(CraftComponent component)
    {
        component.CraftIds ??= [];
        component.CraftWorkYearsByCraft ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        component.CraftIds = component.CraftIds
            .Select(_catalog.CanonicalizeId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(CraftRules.MaximumCrafts)
            .ToList();

        var normalizedYears = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in component.CraftWorkYearsByCraft)
        {
            var canonical = _catalog.CanonicalizeId(pair.Key);
            if (canonical is null || pair.Value <= 0)
                continue;
            normalizedYears.TryGetValue(canonical, out var current);
            normalizedYears[canonical] = current + pair.Value;
        }
        component.CraftWorkYearsByCraft = normalizedYears;

        var active = _catalog.CanonicalizeId(component.ActiveCraftOccupationId);
        component.ActiveCraftOccupationId = active;
        if (active is null || !component.CraftIds.Contains(active, StringComparer.OrdinalIgnoreCase))
            component.ActiveCraftOccupationId = null;
    }

    private sealed record WeightedCraft(CraftInfo Craft, double Weight);
}
