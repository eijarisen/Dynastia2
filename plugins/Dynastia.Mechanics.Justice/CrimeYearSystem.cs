using Dynastia.Contracts;

namespace Dynastia.Mechanics.Justice;

public sealed class CrimeYearSystem : IYearSystem
{
    private const double BaseAttemptChance = 0.02;

    private readonly StandardJusticeService _justice;
    private readonly IFamilyService _family;
    private readonly IStatsService _stats;
    private readonly ICareerService _career;
    private readonly IEconomyService _economy;
    private readonly IEconomyBalanceService _economyBalance;
    private readonly IGameRandom _random;
    private readonly IGameEventBus _events;
    private readonly IReadOnlyList<CrimeDefinition> _crimes;
    private readonly CrimeHistoricalCatalog _historical;

    public CrimeYearSystem(
        StandardJusticeService justice,
        IFamilyService family,
        IStatsService stats,
        ICareerService career,
        IEconomyService economy,
        IEconomyBalanceService economyBalance,
        IGameRandom random,
        IGameEventBus events,
        IReadOnlyList<CrimeDefinition> crimes,
        CrimeHistoricalCatalog historical)
    {
        _justice = justice;
        _family = family;
        _stats = stats;
        _career = career;
        _economy = economy;
        _economyBalance = economyBalance;
        _random = random;
        _events = events;
        _crimes = crimes;
        _historical = historical;
    }

    public string Id => "justice.crime";
    public YearPhase Phase => YearPhase.LifeEvents;
    public IReadOnlyCollection<string> Before => ["career.employment"];
    public IReadOnlyCollection<string> After => ["actions.queued.life_events"];

    public void Execute(IGameState gameState)
    {
        foreach (var person in gameState.People.Where(p => p.Tags.Has("state.alive") && !SimulationState.IsInactive(p)).ToList())
        {
            if (person.Age < 18 || _justice.IsImprisoned(person))
                continue;

            var household = _economy.GetHousehold(person);
            var broke = household is not null && household.Wealth <= 0;
            var attemptChance = Math.Clamp(
                BaseAttemptChance
                * TemperamentAttemptMultiplier(person)
                * MoralsAttemptMultiplier(person)
                * (broke ? 1.60 : 1.0),
                0.002,
                0.10);

            if (_random.NextDouble() >= attemptChance)
                continue;

            AttemptCrime(gameState, person, broke);
        }
    }

    private void AttemptCrime(IGameState state, IPerson person, bool broke)
    {
        var intellect = GetStat(person, "intellect");
        var career = _career.GetCareer(person);
        var hasHousehold = _economy.GetHousehold(person) is not null;
        var crime = SelectCrime(
            person,
            intellect,
            broke,
            career.JobLevel > 0,
            hasHousehold,
            state.Year);
        if (crime is null)
            return;

        var presentation =
            _historical.Resolve(
                crime,
                state.Year);

        var success = !crime.IsProfitCrime || _random.NextDouble() < ResolveSuccessChance(crime, intellect);
        decimal proceeds = 0;
        if (crime.IsProfitCrime && success && _economy.GetHousehold(person) is not null)
        {
            var units = _random.NextInt(crime.ProfitMin, crime.ProfitMax);
            proceeds = units * _economyBalance.OrdinaryLivingCostUnit;
            _economy.ChangeWealth(person, proceeds);
        }

        var detected = _random.NextDouble() < ResolveDetectionChance(crime, intellect);
        var confiscated = 0m;
        if (detected && proceeds > 0)
        {
            confiscated = Math.Floor(proceeds / 2m);
            _economy.ChangeWealth(person, -confiscated);
        }

        if (detected)
        {
            var sentence = _random.NextInt(crime.SentenceMin, crime.SentenceMax);
            _career.SetJobLevel(person, 0);
            _justice.Imprison(
                person,
                sentence,
                crime.Id,
                presentation.DisplayName,
                presentation.Description);
            PublishCaughtEvent(
                state,
                person,
                crime,
                presentation,
                sentence,
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
        int intellect,
        bool broke,
        bool employed,
        bool hasHousehold,
        int year)
    {
        var weighted = _crimes
            .Where(c => (!c.RequiresEmployment || employed)
                        && (hasHousehold || !c.IsProfitCrime))
            .Select(c => new
            {
                Crime = c,
                Weight = c.Weight
                    * _historical.GetWeightMultiplier(c.Id, year)
                    * CircumstanceWeight(c, person, intellect, broke)
            })
            .Where(x => x.Weight > 0)
            .ToList();
        if (weighted.Count == 0) return null;

        var total = weighted.Sum(x => x.Weight);
        var roll = _random.NextDouble() * total;
        foreach (var entry in weighted)
        {
            if (roll < entry.Weight) return entry.Crime;
            roll -= entry.Weight;
        }
        return weighted[^1].Crime;
    }

    private static double CircumstanceWeight(CrimeDefinition crime, IPerson person, int intellect, bool broke)
    {
        var weight = 1.0;
        if (broke && (crime.Category.Contains("property", StringComparison.OrdinalIgnoreCase) || crime.Category.Contains("financial", StringComparison.OrdinalIgnoreCase)))
            weight *= 2.5;

        if (crime.IsViolentOrImpulsive)
        {
            if (person.Tags.Has("personality.choleric")) weight *= 1.8;
            else if (person.Tags.Has("personality.sanguine")) weight *= 1.2;
            else if (person.Tags.Has("personality.melancholic")) weight *= 0.75;
            else if (person.Tags.Has("personality.phlegmatic")) weight *= 0.65;
        }

        if (crime.Category.Contains("financial", StringComparison.OrdinalIgnoreCase))
            weight *= Math.Clamp(1.0 + (intellect - 3) * 0.25, 0.5, 1.5);
        else if (intellect <= 2 && (crime.Id is "petty_theft" or "vandalism" or "brawling" or "burglary"))
            weight *= intellect == 1 ? 1.55 : 1.30;

        return weight;
    }

    private static double ResolveSuccessChance(CrimeDefinition crime, int intellect)
    {
        if (!crime.IsProfitCrime) return 1;
        return Math.Clamp(crime.SuccessBase + (intellect - 3) * 0.08, 0.10, 0.95);
    }

    private static double ResolveDetectionChance(CrimeDefinition crime, int intellect)
    {
        var intellectEffect = crime.IsPlanned ? (3 - intellect) * 0.05 : (3 - intellect) * 0.01;
        return Math.Clamp(crime.DetectionBase + intellectEffect, 0.10, 0.98);
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
        var sentenceText = sentence >= 50 ? "life" : sentence == 1 ? "1 year" : $"{sentence} years";
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
            Data = new Dictionary<string, string>
            {
                ["crimeId"] = crime.Id,
                ["crime"] = presentation.DisplayName,
                ["description"] = presentation.Description,
                ["category"] = crime.Category,
                ["sentence"] = sentence.ToString(),
                ["success"] = success.ToString().ToLowerInvariant(),
                ["caught"] = "true",
                ["proceeds"] = proceeds.ToString(),
                ["text"] = $"{_family.GetDisplayName(person)} was caught after {presentation.Description} and sentenced to {sentenceText} in prison.{moneyText}"
            }
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
        var crimePhrase = crime.Id.Equals(
            "brawling",
            StringComparison.OrdinalIgnoreCase)
                ? "got into a public brawl"
                : $"committed {presentation.DisplayName}";

        _events.Publish(new GameEvent
        {
            Type = "justice.crime_uncaught",
            Year = state.Year,
            SubjectId = person.Id,
            Data = new Dictionary<string, string>
            {
                ["crimeId"] = crime.Id,
                ["crime"] = presentation.DisplayName,
                ["description"] = presentation.Description,
                ["category"] = crime.Category,
                ["success"] = success.ToString().ToLowerInvariant(),
                ["caught"] = "false",
                ["proceeds"] = proceeds.ToString(),
                ["text"] = $"{_family.GetDisplayName(person)} {crimePhrase}{outcome} and escaped arrest."
            }
        });
    }

    private bool ShouldRevealUncaughtCrime(IGameState state, IPerson person)
    {
        if (_family.IsMaleLineage(person)) return true;
        var memberIds = _economy.GetHouseholdMemberIds(person);
        return memberIds.Select(id => state.People.FirstOrDefault(p => p.Id == id)).Any(p => p is not null && _family.IsMaleLineage(p));
    }

    private int GetStat(IPerson person, string id) => _stats.GetStats(person).First(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase)).Value;
    private static double TemperamentAttemptMultiplier(IPerson p) => p.Tags.Has("personality.phlegmatic") ? .60 : p.Tags.Has("personality.melancholic") ? .70 : p.Tags.Has("personality.sanguine") ? 1.15 : p.Tags.Has("personality.choleric") ? 1.45 : 1.0;
    private static double MoralsAttemptMultiplier(IPerson p) => p.Tags.Has("morals.good") ? .40 : p.Tags.Has("morals.evil") ? 2.00 : 1.0;
}
