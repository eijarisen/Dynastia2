using Dynastia.Contracts;

namespace Dynastia.Mechanics.Justice;

public sealed class CrimeYearSystem : IYearSystem
{
    private readonly StandardJusticeService _justice;
    private readonly IFamilyService _family;
    private readonly IStatsService _stats;
    private readonly ICareerService _career;
    private readonly IEconomyService _economy;
    private readonly IEconomyBalanceService _economyBalance;
    private readonly ILocalCareerOpportunityService _localOpportunities;
    private readonly IStressService? _stress;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;
    private readonly IReadOnlyList<CrimeDefinition> _crimes;
    private readonly CrimeHistoricalCatalog _historical;
    private readonly CrimeAttemptRules _attemptRules;
    private readonly IContextWeightCatalog _attemptContext;
    private readonly IContextWeightCatalog _crimeContext;

    public CrimeYearSystem(
        StandardJusticeService justice,
        IFamilyService family,
        IStatsService stats,
        ICareerService career,
        IEconomyService economy,
        IEconomyBalanceService economyBalance,
        ILocalCareerOpportunityService localOpportunities,
        IStressService? stress,
        IGameRandom random,
        IGameEventBus events,
        IReadOnlyList<CrimeDefinition> crimes,
        CrimeHistoricalCatalog historical,
        CrimeAttemptRules attemptRules,
        IContextWeightCatalog attemptContext,
        IContextWeightCatalog crimeContext)
    {
        _justice = justice;
        _family = family;
        _stats = stats;
        _career = career;
        _economy = economy;
        _economyBalance = economyBalance;
        _localOpportunities = localOpportunities;
        _stress = stress;
        _random = random;
        _events = events;
        _crimes = crimes;
        _historical = historical;
        _attemptRules = attemptRules;
        _attemptContext = attemptContext;
        _crimeContext = crimeContext;
    }

    public string Id => "justice.crime";
    public YearPhase Phase => YearPhase.LifeEvents;
    public IReadOnlyCollection<string> Before => ["career.employment"];
    public IReadOnlyCollection<string> After => ["actions.queued.life_events"];

    public void Execute(IGameState gameState)
    {
        foreach (var person in gameState.People
            .Where(p => p.Tags.Has("state.alive") && !SimulationState.IsInactive(p))
            .ToList())
        {
            if (person.Age < _attemptRules.MinimumAge
                || _justice.IsImprisoned(person)
                || person.Tags.Has("occupation.criminal"))
            {
                continue;
            }

            var household = _economy.GetHousehold(person);
            var broke = household?.HasUnfundedBasicNeeds == true;
            var location = _localOpportunities.GetOpportunitySnapshot(person);
            var stress = _stress?.GetStress(person).Total ?? 0;
            var context = BuildContext(person, gameState.Year, location.Town.SettlementClass);
            var attemptChance = CrimeRules.CalculateAttemptChance(
                _attemptRules,
                _attemptContext.GetMultiplier("global", context),
                broke,
                stress);

            if (_random.NextDouble() >= attemptChance)
                continue;

            AttemptCrime(gameState, person, broke, stress, location);
        }
    }

    private void AttemptCrime(
        IGameState state,
        IPerson person,
        bool broke,
        double stress,
        LocationOpportunitySnapshot location)
    {
        var intellect = GetStat(person, "intellect");
        var career = _career.GetCareer(person);
        var hasHousehold = _economy.GetHousehold(person) is not null;
        var crime = SelectCrime(
            person,
            broke,
            stress,
            career.IsEmployed,
            hasHousehold,
            state.Year,
            location);
        if (crime is null)
            return;

        var presentation = _historical.Resolve(crime, state.Year);
        var aptitude = CrimeRules.CalculateAptitude(crime, statId => GetStat(person, statId));
        var success = !crime.IsProfitCrime
            || _random.NextDouble() < CrimeRules.CalculateProfitSuccessChance(crime, aptitude);

        decimal proceeds = 0;
        if (crime.IsProfitCrime && success && hasHousehold)
        {
            var units = _random.NextInt(crime.ProfitMin, crime.ProfitMax);
            proceeds = units * _economyBalance.OrdinaryLivingCostUnit;
            _economy.ChangeWealth(person, proceeds);
        }

        var detected = _random.NextDouble()
            < CrimeRules.CalculateDetectionChance(crime, intellect);
        var confiscated = 0m;
        if (detected && proceeds > 0)
        {
            confiscated = Math.Floor(proceeds / 2m);
            _economy.ChangeWealth(person, -confiscated);
        }

        if (detected)
        {
            var originalSentence = _random.NextInt(crime.SentenceMin, crime.SentenceMax);
            _career.SetJobLevel(person, 0);
            var finalSentence = _justice.ConvictKnownOffense(
                person,
                originalSentence,
                crime.Id,
                presentation.DisplayName,
                presentation.Description);
            PublishCaughtEvent(
                state,
                person,
                crime,
                presentation,
                finalSentence,
                success,
                proceeds,
                confiscated);
            return;
        }

        if (ShouldRevealUncaughtCrime(state, person))
            PublishUncaughtEvent(state, person, crime, presentation, success, proceeds);
    }

    private CrimeDefinition? SelectCrime(
        IPerson person,
        bool broke,
        double stress,
        bool employed,
        bool hasHousehold,
        int year,
        LocationOpportunitySnapshot location)
    {
        var context = BuildContext(person, year, location.Town.SettlementClass);
        var opportunityTags = location.RegionOpportunityTags
            .Concat(location.TownOpportunityTags)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var careerFamily = _career.GetCareerFamily(person);

        var weighted = _crimes
            .Where(crime => crime.IsAvailable(year, person.Age)
                && (!crime.RequiresEmployment || employed)
                && (hasHousehold || !crime.IsProfitCrime))
            .Select(crime =>
            {
                var aptitude = CrimeRules.CalculateAptitude(
                    crime,
                    statId => GetStat(person, statId));
                var weight = crime.Weight
                    * _crimeContext.GetMultiplier(crime.Id, context)
                    * CrimeRules.CalculateAptitudeSelectionMultiplier(aptitude)
                    * CrimeRules.CalculateStressSelectionMultiplier(crime, stress)
                    * (broke ? crime.PovertyMultiplier : 1.0)
                    * GetSettlementPreferenceMultiplier(crime, location.Town.SettlementClass)
                    * GetOpportunityMultiplier(crime, opportunityTags)
                    * GetCareerFamilyMultiplier(crime, careerFamily);

                return new WeightedCrime(crime, weight);
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

    private ContextWeightContext BuildContext(
        IPerson person,
        int year,
        SettlementClass settlementClass) =>
        new(
            year,
            person.Age,
            _family.GetSex(person),
            ResolveTemperament(person),
            ResolveMorals(person),
            settlementClass);

    private static double GetSettlementPreferenceMultiplier(
        CrimeDefinition crime,
        SettlementClass settlementClass)
    {
        if (crime.SettlementPreference.Equals("Rural", StringComparison.OrdinalIgnoreCase))
        {
            return settlementClass switch
            {
                SettlementClass.SmallTown => 1.35,
                SettlementClass.Town => 1.10,
                SettlementClass.City => 0.80,
                SettlementClass.MajorCity => 0.65,
                _ => 1.0
            };
        }

        if (crime.SettlementPreference.Equals("Urban", StringComparison.OrdinalIgnoreCase))
        {
            return settlementClass switch
            {
                SettlementClass.SmallTown => 0.70,
                SettlementClass.Town => 0.90,
                SettlementClass.City => 1.15,
                SettlementClass.MajorCity => 1.30,
                _ => 1.0
            };
        }

        return 1.0;
    }

    private static double GetOpportunityMultiplier(
        CrimeDefinition crime,
        IReadOnlySet<string> availableTags)
    {
        if (crime.PreferredOpportunityTags.Count == 0)
            return 1.0;
        return crime.PreferredOpportunityTags.Any(availableTags.Contains)
            ? 1.40
            : 1.0;
    }

    private static double GetCareerFamilyMultiplier(
        CrimeDefinition crime,
        string? careerFamily)
    {
        if (string.IsNullOrWhiteSpace(careerFamily)
            || crime.PreferredCareerFamilies.Count == 0)
            return 1.0;
        return crime.PreferredCareerFamilies.Contains(
            careerFamily,
            StringComparer.OrdinalIgnoreCase)
                ? 1.35
                : 1.0;
    }

    private void PublishCaughtEvent(
        IGameState state,
        IPerson person,
        CrimeDefinition crime,
        CrimePresentation presentation,
        int sentence,
        bool success,
        decimal proceeds,
        decimal confiscated)
    {
        var sentenceText = sentence >= 50
            ? "life"
            : sentence == 1
                ? "1 year"
                : $"{sentence} years";
        var moneyText = crime.IsProfitCrime
            ? success
                ? $" The crime brought in {proceeds:N0} zł; {confiscated:N0} zł was confiscated."
                : " The attempt produced no money."
            : string.Empty;

        _events.Publish(new GameEvent
        {
            Type = "justice.crime",
            Year = state.Year,
            SubjectId = person.Id,
            Data = BuildEventData(
                crime,
                presentation,
                success,
                caught: true,
                proceeds,
                $"{_family.GetDisplayName(person)} was caught after {presentation.Description} and sentenced to {sentenceText} in prison.{moneyText}",
                sentence)
        });
    }

    private void PublishUncaughtEvent(
        IGameState state,
        IPerson person,
        CrimeDefinition crime,
        CrimePresentation presentation,
        bool success,
        decimal proceeds)
    {
        var outcome = crime.IsProfitCrime
            ? success ? $" and brought {proceeds:N0} zł home" : " but gained nothing"
            : string.Empty;
        var crimePhrase = crime.Id.Equals("brawling", StringComparison.OrdinalIgnoreCase)
            ? "got into a public brawl"
            : $"committed {presentation.DisplayName}";

        _events.Publish(new GameEvent
        {
            Type = "justice.crime_uncaught",
            Year = state.Year,
            SubjectId = person.Id,
            Data = BuildEventData(
                crime,
                presentation,
                success,
                caught: false,
                proceeds,
                $"{_family.GetDisplayName(person)} {crimePhrase}{outcome} and escaped arrest.")
        });
    }

    private static Dictionary<string, string> BuildEventData(
        CrimeDefinition crime,
        CrimePresentation presentation,
        bool success,
        bool caught,
        decimal proceeds,
        string text,
        int? sentence = null)
    {
        var data = new Dictionary<string, string>
        {
            ["crimeId"] = crime.Id,
            ["crime"] = presentation.DisplayName,
            ["description"] = presentation.Description,
            ["category"] = crime.Category,
            ["behaviorTags"] = string.Join(';', crime.BehaviorTags),
            ["success"] = success.ToString().ToLowerInvariant(),
            ["caught"] = caught.ToString().ToLowerInvariant(),
            ["proceeds"] = proceeds.ToString(),
            ["text"] = text
        };
        if (sentence is int years)
            data["sentence"] = years.ToString();
        return data;
    }

    private bool ShouldRevealUncaughtCrime(IGameState state, IPerson person)
    {
        if (_family.IsMaleLineage(person))
            return true;
        var memberIds = _economy.GetHouseholdMemberIds(person);
        return memberIds
            .Select(id => state is IPersonLookup lookup
                ? lookup.FindPerson(id)
                : state.People.FirstOrDefault(p => p.Id == id))
            .Any(p => p is not null && _family.IsMaleLineage(p));
    }

    private int GetStat(IPerson person, string id) =>
        _stats.GetStats(person)
            .First(stat => stat.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
            .Value;

    private static string? ResolveTemperament(IPerson person)
    {
        if (person.Tags.Has("personality.melancholic")) return "Melancholic";
        if (person.Tags.Has("personality.phlegmatic")) return "Phlegmatic";
        if (person.Tags.Has("personality.sanguine")) return "Sanguine";
        if (person.Tags.Has("personality.choleric")) return "Choleric";
        return null;
    }

    private static string? ResolveMorals(IPerson person)
    {
        if (person.Tags.Has("morals.good")) return "Good";
        if (person.Tags.Has("morals.neutral")) return "Neutral";
        if (person.Tags.Has("morals.evil")) return "Evil";
        return null;
    }

    private sealed record WeightedCrime(CrimeDefinition Crime, double Weight);
}
