using Dynastia.Contracts;

namespace Dynastia.Mechanics.Crafts;

internal sealed class StandardCraftService : ICraftService, IIncomeProvider
{
    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly IEconomyService _economy;
    private readonly ICareerService _career;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;
    private readonly CraftCatalog _catalog;

    public StandardCraftService(
        IGameState gameState,
        IFamilyService family,
        IEconomyService economy,
        ICareerService career,
        IGameRandom random,
        IGameEventBus events,
        CraftCatalog catalog)
    {
        _gameState = gameState;
        _family = family;
        _economy = economy;
        _career = career;
        _random = random;
        _events = events;
        _catalog = catalog;
    }

    public string Id => "crafts";

    public IReadOnlyList<CraftInfo> Catalog => _catalog.All;

    public CraftSnapshot GetSnapshot(IPerson person)
    {
        var component = GetRequired(person);
        Normalize(component);
        var active = _catalog.Find(component.ActiveCraftOccupationId);

        return new CraftSnapshot(
            GetKnownCrafts(person),
            active?.Id,
            active is null ? null : $"Self-employed {active.SelfEmploymentTitle}",
            active?.Emoji,
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
        Normalize(component);

        return component.CraftIds
            .Select(_catalog.Find)
            .Where(craft => craft is not null)
            .Cast<CraftInfo>()
            .ToList();
    }

    public bool KnowsCraft(IPerson person, string craftId) =>
        GetRequired(person).CraftIds.Contains(craftId, StringComparer.OrdinalIgnoreCase);

    public bool IsSelfEmployed(IPerson person) =>
        GetActiveCraft(person) is not null;

    public CraftInfo? GetActiveCraft(IPerson person) =>
        _catalog.Find(GetRequired(person).ActiveCraftOccupationId);

    public bool LearnCraft(IPerson person, string craftId)
    {
        var craft = _catalog.Find(craftId);
        if (craft is null || craft.StartYear > _gameState.Year)
            return false;

        var component = GetRequired(person);
        Normalize(component);
        if (component.CraftIds.Count >= CraftRules.MaximumCrafts
            || component.CraftIds.Contains(craft.Id, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        component.CraftIds.Add(craft.Id);
        return true;
    }

    public void SetCrafts(IPerson person, IEnumerable<string> craftIds)
    {
        ArgumentNullException.ThrowIfNull(craftIds);
        var component = GetRequired(person);
        component.CraftIds = craftIds
            .Select(id => _catalog.Find(id)?.Id)
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
        int year)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deterministicKey);
        var available = _catalog.All
            .Where(craft => craft.StartYear <= year)
            .ToList();
        if (available.Count == 0)
            return [];

        var random = new DeterministicCraftRandom(
            $"{_gameState.DynastySurname}|{deterministicKey}|candidate-crafts");
        var result = new List<string>();
        var exact = available
            .Where(craft => craft.PrimaryCareerId.Equals(
                formalCareerId,
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (exact.Count > 0)
        {
            if (random.NextDouble() < 0.70)
                result.Add(exact[random.NextInt(0, exact.Count - 1)].Id);
        }
        else if (random.NextDouble() < CraftRules.GetGeneratedAdultBaseChance(year))
        {
            result.Add(available[random.NextInt(0, available.Count - 1)].Id);
        }

        if (result.Count > 0 && random.NextDouble() < 0.10)
        {
            var remaining = available
                .Where(craft => !result.Contains(craft.Id, StringComparer.OrdinalIgnoreCase))
                .ToList();
            if (remaining.Count > 0)
                result.Add(remaining[random.NextInt(0, remaining.Count - 1)].Id);
        }

        return result;
    }

    public bool StartOccupation(IPerson person, string craftId)
    {
        var craft = _catalog.Find(craftId);
        if (craft is null
            || craft.StartYear > _gameState.Year
            || !person.Tags.Has("state.alive")
            || person.Age < 18
            || person.Tags.Has("state.imprisoned")
            || _career.GetCareer(person).IsRetired
            || !KnowsCraft(person, craft.Id))
        {
            return false;
        }

        var component = GetRequired(person);
        if (component.ActiveCraftOccupationId?.Equals(
                craft.Id,
                StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        // A craft occupation replaces any formal career. Career assignment is
        // cleared before activating the craft so its transition hook cannot
        // clear the newly selected occupation.
        var satisfaction = _career.GetCareer(person).JobSatisfaction;
        _career.AssignCareer(person, null, 0, satisfaction);
        component.ActiveCraftOccupationId = craft.Id;
        person.Tags.Add("career.craft_self_employed");
        person.Tags.Add("employment.craft");

        _events.Publish(new GameEvent
        {
            Type = "craft.self_employment_started",
            Year = _gameState.Year,
            SubjectId = person.Id,
            Data = new Dictionary<string, string>
            {
                ["craftId"] = craft.Id,
                ["craftName"] = craft.Name,
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

        _events.Publish(new GameEvent
        {
            Type = "craft.self_employment_ended",
            Year = _gameState.Year,
            SubjectId = person.Id,
            Data = new Dictionary<string, string>
            {
                ["craftId"] = craft.Id,
                ["craftName"] = craft.Name,
                ["reason"] = reason,
                ["text"] = reason.Equals("formal employment", StringComparison.OrdinalIgnoreCase)
                    ? $"{_family.GetDisplayName(person)} left self-employment in {craft.Name} to take a formal position."
                    : reason.Equals("retirement", StringComparison.OrdinalIgnoreCase)
                        ? $"{_family.GetDisplayName(person)} retired from self-employment in {craft.Name}."
                        : reason.Equals("relocation", StringComparison.OrdinalIgnoreCase)
                            ? $"{_family.GetDisplayName(person)} ended self-employment in {craft.Name} after moving household."
                            : $"{_family.GetDisplayName(person)} ended self-employment in {craft.Name}."
            }
        });

        return true;
    }

    public double GetApplicationBonus(IPerson person, string careerId) =>
        CraftRules.GetApplicationBonus(GetKnownCrafts(person), careerId);

    public CraftCareerExperience GetCareerExperience(IPerson person, string careerId)
    {
        var component = GetRequired(person);
        Normalize(component);
        var exact = 0;
        var related = 0;

        foreach (var pair in component.CraftWorkYearsByCraft)
        {
            var craft = _catalog.Find(pair.Key);
            if (craft is null || pair.Value <= 0)
                continue;

            if (craft.PrimaryCareerId.Equals(careerId, StringComparison.OrdinalIgnoreCase))
                exact += pair.Value;
            else if (craft.RelatedCareerIds.Contains(careerId, StringComparer.OrdinalIgnoreCase))
                related += pair.Value;
        }

        return new CraftCareerExperience(exact, related);
    }

    public decimal GetExpectedAnnualIncome(IPerson person)
    {
        var active = _catalog.Find(GetRequired(person).ActiveCraftOccupationId);
        if (active is null)
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
        if (active is null)
            return 0m;

        if (!person.Tags.Has("state.alive")
            || person.Tags.Has("state.imprisoned"))
        {
            component.LastAnnualIncome = 0m;
            component.LastIncomeYear = _gameState.Year;
            return 0m;
        }

        var annualReference = _career.GetLevelOneSalary(active.PrimaryCareerId);
        decimal total = 0m;
        for (var month = 0; month < 12; month++)
        {
            total += annualReference / 12m * (decimal)(_random.NextDouble() * 2.0);
        }

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
                ["craftName"] = active.Name,
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
        component.CraftWorkYearsByCraft ??=
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        component.CraftIds = component.CraftIds
            .Select(id => _catalog.Find(id)?.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(CraftRules.MaximumCrafts)
            .ToList();

        if (_catalog.Find(component.ActiveCraftOccupationId) is null
            || !component.CraftIds.Contains(
                component.ActiveCraftOccupationId ?? string.Empty,
                StringComparer.OrdinalIgnoreCase))
        {
            component.ActiveCraftOccupationId = null;
        }
    }
}
