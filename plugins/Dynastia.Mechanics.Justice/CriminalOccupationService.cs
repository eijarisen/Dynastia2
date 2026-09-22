using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Justice;

internal sealed class CriminalOccupationService :
    ICriminalOccupationService,
    IIncomeProvider
{
    private readonly IGameState _gameState;
    private readonly IFamilyService _family;
    private readonly IStatsService _stats;
    private readonly ICareerService _career;
    private readonly IEconomyService _economy;
    private readonly StandardJusticeService _justice;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;
    private readonly CriminalOccupationCatalog _catalog;
    private readonly IReadOnlyList<CrimeDefinition> _crimes;
    private readonly CrimeHistoricalCatalog _historical;
    private readonly Func<ICraftService?> _craftResolver;

    public CriminalOccupationService(
        IGameState gameState,
        IFamilyService family,
        IStatsService stats,
        ICareerService career,
        IEconomyService economy,
        StandardJusticeService justice,
        IGameRandom random,
        IGameEventBus events,
        CriminalOccupationCatalog catalog,
        IReadOnlyList<CrimeDefinition> crimes,
        CrimeHistoricalCatalog historical,
        Func<ICraftService?> craftResolver)
    {
        _gameState = gameState;
        _family = family;
        _stats = stats;
        _career = career;
        _economy = economy;
        _justice = justice;
        _random = random;
        _events = events;
        _catalog = catalog;
        _crimes = crimes;
        _historical = historical;
        _craftResolver = craftResolver;
    }

    public string Id => "justice.life_of_crime";

    public CriminalOccupationSnapshot GetSnapshot(IPerson person)
    {
        var component = GetMutable(person);
        var mastery = _catalog.ResolveMastery(component.ActiveHeistYears);
        var archetype = ResolveArchetype(person);
        return new CriminalOccupationSnapshot(
            component.HasStartedLifeOfCrime,
            component.IsActive,
            component.ActiveHeistYears,
            component.LastHeistYear,
            mastery.Level,
            mastery.DisplayName,
            archetype.ArchetypeId,
            archetype.DisplayName,
            component.IsActive ? CalculateExpectedIncome(person, mastery.Level, archetype) : 0m,
            component.LastAnnualIncome,
            component.LastIncomeYear);
    }

    public bool HasStartedLifeOfCrime(IPerson person) =>
        GetMutable(person).HasStartedLifeOfCrime;

    public bool IsActive(IPerson person) =>
        GetMutable(person).IsActive;

    public bool StartLifeOfCrime(IPerson person)
    {
        var component = GetMutable(person);
        if (component.HasStartedLifeOfCrime
            || !person.Tags.Has("state.alive")
            || person.Tags.Has("vocation.religious.active")
            || person.Age < _catalog.Rules.MinimumAge
            || _justice.IsImprisoned(person))
        {
            return false;
        }

        var career = _career.GetCareer(person);
        _career.AssignCareer(person, null, 0, career.JobSatisfaction);
        _craftResolver()?.EndOccupation(person, "life of crime");

        component.HasStartedLifeOfCrime = true;
        component.IsActive = true;
        person.Tags.Add(_catalog.Rules.OccupationTag);

        var archetype = ResolveArchetype(person);
        _events.Publish(new GameEvent
        {
            Type = "justice.life_of_crime_started",
            Year = _gameState.Year,
            SubjectId = person.Id,
            Data = new Dictionary<string, string>
            {
                ["archetype"] = archetype.DisplayName,
                ["text"] = $"{_family.GetDisplayName(person)} turned to a life of crime as a {archetype.DisplayName}."
            }
        });

        return true;
    }

    public bool EndLifeOfCrime(IPerson person, string reason = "ended")
    {
        var component = GetMutable(person);
        if (!component.IsActive)
            return false;

        component.IsActive = false;
        component.PendingHeistYear = 0;
        component.PendingHeistProceeds = 0m;
        person.Tags.Remove(_catalog.Rules.OccupationTag);

        _events.Publish(new GameEvent
        {
            Type = "justice.life_of_crime_ended",
            Year = _gameState.Year,
            SubjectId = person.Id,
            Data = new Dictionary<string, string>
            {
                ["reason"] = reason,
                ["text"] = $"{_family.GetDisplayName(person)} left the life of crime."
            }
        });
        return true;
    }

    public decimal GetExpectedAnnualIncome(IPerson person)
    {
        var component = GetMutable(person);
        if (!component.IsActive
            || _justice.IsImprisoned(person)
            || !person.Tags.Has("state.alive")
            || _economy.GetHouseholdId(person) is null)
        {
            return 0m;
        }

        var mastery = _catalog.ResolveMastery(component.ActiveHeistYears);
        return CalculateExpectedIncome(person, mastery.Level, ResolveArchetype(person));
    }

    public decimal GetAnnualIncome(IPerson person)
    {
        var component = GetMutable(person);
        if (!component.IsActive
            || !person.Tags.Has("state.alive")
            || _justice.IsImprisoned(person)
            || _economy.GetHouseholdId(person) is null)
        {
            component.PendingHeistYear = 0;
            component.PendingHeistProceeds = 0m;
            component.LastAnnualIncome = 0m;
            component.LastIncomeYear = _gameState.Year;
            return 0m;
        }

        if (component.LastHeistYear == _gameState.Year)
            return 0m;

        var proceeds = PrepareHeistProceeds(person, component);
        return proceeds;
    }

    decimal IIncomeProvider.GetExpectedAnnualIncome(IPerson person) =>
        GetExpectedAnnualIncome(person);

    internal bool PerformFirstHeist(IPerson person)
    {
        var component = GetMutable(person);
        if (!component.IsActive
            || component.LastHeistYear == _gameState.Year
            || _justice.IsImprisoned(person)
            || _economy.GetHouseholdId(person) is null)
        {
            return false;
        }

        var proceeds = PrepareHeistProceeds(person, component);
        if (proceeds > 0m)
            _economy.ChangeWealth(person, proceeds);
        ResolveHeist(person, component, proceeds);
        return true;
    }

    internal void ResolveAnnualHeist(IPerson person)
    {
        var component = GetMutable(person);
        if (!component.IsActive
            || component.LastHeistYear == _gameState.Year
            || !person.Tags.Has("state.alive")
            || _justice.IsImprisoned(person)
            || _economy.GetHouseholdId(person) is null)
        {
            return;
        }

        var incomeWasPrepared = component.PendingHeistYear == _gameState.Year;
        var proceeds = PrepareHeistProceeds(person, component);
        if (!incomeWasPrepared && proceeds > 0m)
            _economy.ChangeWealth(person, proceeds);
        ResolveHeist(person, component, proceeds);
    }

    internal void ReconcileAll(IEnumerable<IPerson> people)
    {
        foreach (var person in people)
        {
            var component = GetMutable(person);
            if (component.IsActive)
                person.Tags.Add(_catalog.Rules.OccupationTag);
            else
                person.Tags.Remove(_catalog.Rules.OccupationTag);
        }
    }

    private decimal PrepareHeistProceeds(
        IPerson person,
        CriminalOccupationComponent component)
    {
        if (component.PendingHeistYear == _gameState.Year)
            return component.PendingHeistProceeds;

        var mastery = _catalog.ResolveMastery(component.ActiveHeistYears);
        var archetype = ResolveArchetype(person);
        var roll = _random.NextInt(
            _catalog.Rules.Heist.IncomeRollMinimum,
            _catalog.Rules.Heist.IncomeRollMaximumInclusive);
        var proceeds = CalculateIncome(person, mastery.Level, archetype, roll);

        component.PendingHeistYear = _gameState.Year;
        component.PendingHeistProceeds = proceeds;
        component.LastAnnualIncome = proceeds;
        component.LastIncomeYear = _gameState.Year;

        return proceeds;
    }

    private void ResolveHeist(
        IPerson person,
        CriminalOccupationComponent component,
        decimal proceeds)
    {
        var beforeMastery = _catalog.ResolveMastery(component.ActiveHeistYears);
        var archetype = ResolveArchetype(person);
        var crime = SelectProfitCrime(person, archetype);
        if (crime is null)
        {
            component.PendingHeistYear = 0;
            component.PendingHeistProceeds = 0m;
            return;
        }

        var presentation = _historical.Resolve(crime, _gameState.Year);
        var detected = _random.NextDouble() < beforeMastery.BaseDetectionChance;
        var confiscated = 0m;
        var finalSentence = 0;

        if (detected)
        {
            confiscated = Math.Floor(proceeds / 2m);
            if (confiscated > 0m)
                _economy.ChangeWealth(person, -confiscated);

            var originalSentence = _random.NextInt(crime.SentenceMin, crime.SentenceMax);
            var sentenceMultiplier = archetype.ArchetypeId.Equals("mastermind", StringComparison.OrdinalIgnoreCase)
                ? _catalog.Rules.Heist.MastermindSentenceMultiplier
                : 1m;

            _career.SetJobLevel(person, 0);
            finalSentence = _justice.ConvictKnownOffense(
                person,
                originalSentence,
                crime.Id,
                presentation.DisplayName,
                presentation.Description,
                sentenceMultiplier);

            PublishCrimeEvent(
                person,
                crime,
                presentation,
                caught: true,
                proceeds,
                confiscated,
                finalSentence);
        }
        else
        {
            PublishCrimeEvent(
                person,
                crime,
                presentation,
                caught: false,
                proceeds,
                0m,
                null);
        }

        component.ActiveHeistYears++;
        component.LastHeistYear = _gameState.Year;
        component.PendingHeistYear = 0;
        component.PendingHeistProceeds = 0m;

        var afterMastery = _catalog.ResolveMastery(component.ActiveHeistYears);
        if (afterMastery.Level > beforeMastery.Level)
            PublishMastery(person, archetype, afterMastery);
    }

    private CrimeDefinition? SelectProfitCrime(
        IPerson person,
        CriminalArchetypeRule archetype)
    {
        var weighted = _crimes
            .Where(crime => crime.IsProfitCrime
                && crime.IsAvailable(_gameState.Year, person.Age)
                && !crime.RequiresEmployment)
            .Select(crime => new
            {
                Crime = crime,
                Weight = crime.Weight
                    * (archetype.DominantStats.Contains(crime.PrimaryStat, StringComparer.OrdinalIgnoreCase) ? 2.0 : 1.0)
                    * (!string.IsNullOrWhiteSpace(crime.SecondaryStat)
                        && archetype.DominantStats.Contains(crime.SecondaryStat!, StringComparer.OrdinalIgnoreCase)
                            ? 1.5
                            : 1.0)
            })
            .Where(entry => entry.Weight > 0)
            .ToList();

        if (weighted.Count == 0)
            return null;

        var total = weighted.Sum(entry => entry.Weight);
        var roll = _random.NextDouble() * total;
        foreach (var entry in weighted)
        {
            if (roll < entry.Weight)
                return entry.Crime;
            roll -= entry.Weight;
        }
        return weighted[^1].Crime;
    }

    private void PublishCrimeEvent(
        IPerson person,
        CrimeDefinition crime,
        CrimePresentation presentation,
        bool caught,
        decimal proceeds,
        decimal confiscated,
        int? sentence)
    {
        var data = new Dictionary<string, string>
        {
            ["crimeId"] = crime.Id,
            ["crime"] = presentation.DisplayName,
            ["description"] = presentation.Description,
            ["category"] = crime.Category,
            ["behaviorTags"] = string.Join(';', crime.BehaviorTags),
            ["success"] = "true",
            ["caught"] = caught.ToString().ToLowerInvariant(),
            ["proceeds"] = proceeds.ToString(CultureInfo.InvariantCulture),
            ["lifeOfCrime"] = "true"
        };

        if (caught)
        {
            data["sentence"] = sentence!.Value.ToString(CultureInfo.InvariantCulture);
            data["confiscated"] = confiscated.ToString(CultureInfo.InvariantCulture);
            var sentenceText = sentence.Value >= 50
                ? "life"
                : sentence.Value == 1 ? "1 year" : $"{sentence.Value} years";
            data["text"] =
                $"{_family.GetDisplayName(person)} was caught after {presentation.Description} and sentenced to {sentenceText} in prison. " +
                $"The Heist brought in {proceeds:N0} zł; {confiscated:N0} zł was confiscated.";
        }
        else
        {
            data["text"] =
                $"{_family.GetDisplayName(person)} committed {presentation.DisplayName}, brought {proceeds:N0} zł home and escaped arrest.";
        }

        _events.Publish(new GameEvent
        {
            Type = caught ? "justice.crime" : "justice.crime_uncaught",
            Year = _gameState.Year,
            SubjectId = person.Id,
            Data = data
        });
    }

    private void PublishMastery(
        IPerson person,
        CriminalArchetypeRule archetype,
        CriminalMasteryRule mastery)
    {
        var isMaster = mastery.Level >= 5;
        _events.Publish(new GameEvent
        {
            Type = isMaster ? "justice.criminal_master" : "justice.criminal_mastery",
            Year = _gameState.Year,
            SubjectId = person.Id,
            Data = new Dictionary<string, string>
            {
                ["mastery"] = mastery.DisplayName,
                ["archetype"] = archetype.DisplayName,
                ["text"] = isMaster
                    ? $"{_family.GetDisplayName(person)} became a Master {archetype.DisplayName}."
                    : $"{_family.GetDisplayName(person)} advanced to {mastery.DisplayName} in the criminal underworld."
            }
        });
    }

    private CriminalArchetypeRule ResolveArchetype(IPerson person)
    {
        var values = _stats.GetStats(person)
            .ToDictionary(stat => stat.Id, stat => stat.Value, StringComparer.OrdinalIgnoreCase);
        return _catalog.ResolveArchetype(
            values.GetValueOrDefault("appeal", 3),
            values.GetValueOrDefault("strength", 3),
            values.GetValueOrDefault("intellect", 3));
    }

    private decimal CalculateExpectedIncome(
        IPerson person,
        int masteryLevel,
        CriminalArchetypeRule archetype)
    {
        decimal total = 0m;
        var min = _catalog.Rules.Heist.IncomeRollMinimum;
        var max = _catalog.Rules.Heist.IncomeRollMaximumInclusive;
        for (var roll = min; roll <= max; roll++)
            total += CalculateIncome(person, masteryLevel, archetype, roll);
        return Math.Round(total / (max - min + 1), 0, MidpointRounding.AwayFromZero);
    }

    private decimal CalculateIncome(
        IPerson person,
        int masteryLevel,
        CriminalArchetypeRule archetype,
        int roll)
    {
        var values = _stats.GetStats(person)
            .ToDictionary(stat => stat.Id, stat => stat.Value, StringComparer.OrdinalIgnoreCase);
        var average = (
            values.GetValueOrDefault("appeal", 3)
            + values.GetValueOrDefault("strength", 3)
            + values.GetValueOrDefault("intellect", 3)) / 3m;
        var aptitude = Math.Clamp(1m + 0.05m * (average - 3m), 0.90m, 1.10m);
        var incomeMultiplier = OccupationalIncomeCurve.GetMultiplier(
            roll,
            masteryLevel,
            _catalog.Rules.Heist.IncomeRollMinimum,
            _catalog.Rules.Heist.IncomeRollMaximumInclusive);
        var income = _catalog.Rules.Heist.BaseIncome
            * incomeMultiplier
            * aptitude
            * archetype.IncomeMultiplier;
        return Math.Round(income, 0, MidpointRounding.AwayFromZero);
    }

    private static CriminalOccupationComponent GetMutable(IPerson person)
    {
        var component = person.Components.Get<CriminalOccupationComponent>();
        if (component is not null)
            return component;
        component = new CriminalOccupationComponent();
        person.Components.Set(component);
        return component;
    }
}
