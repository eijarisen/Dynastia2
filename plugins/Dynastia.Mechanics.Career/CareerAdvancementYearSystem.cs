using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

/// <summary>
/// Resolves promotions immediately after early queued actions so a promotion
/// changes the salary paid in the same annual turn. Job loss remains a later
/// life-event system to preserve the existing "salary before firing" rule.
/// </summary>
public sealed class CareerAdvancementYearSystem :
    IYearSystem
{
    private const int PromotionMinAge =
        21;

    private readonly StandardCareerService _career;
    private readonly IEducationService _education;
    private readonly IStatsService _stats;
    private readonly IGameRandom _random;
    private readonly IFamilyService _family;
    private readonly RetirementRuleCatalog
        _retirementRules;
    private readonly IGameEventBus _events;
    private readonly Func<IStatusService?> _statusResolver;

    public CareerAdvancementYearSystem(
        StandardCareerService career,
        IEducationService education,
        IStatsService stats,
        IGameRandom random,
        IFamilyService family,
        RetirementRuleCatalog retirementRules,
        IGameEventBus events,
        Func<IStatusService?> statusResolver)
    {
        _career = career;
        _education = education;
        _stats = stats;
        _random = random;
        _family = family;
        _retirementRules = retirementRules;
        _events = events;
        _statusResolver = statusResolver;
    }

    public string Id =>
        "career.promotion_resolution";

    public YearPhase Phase =>
        YearPhase.QueuedActionsEarly;

    public IReadOnlyCollection<string> Before =>
        Array.Empty<string>();

    public IReadOnlyCollection<string> After =>
        ["actions.queued.early"];

    public void Execute(
        IGameState gameState)
    {
        var living =
            gameState.People
                .Where(
                    person =>
                        person.Tags.Has(
                            "state.alive"))
                .ToList();

        foreach (var person in living)
        {
            ProcessPerson(
                gameState,
                person);
        }
    }

    private void ProcessPerson(
        IGameState gameState,
        IPerson person)
    {
        if (person.Tags.Has(
                "simulation.peripheral_inactive"))
        {
            return;
        }

        var career =
            _career.GetCareer(
                person);

        if (career.IsRetired
            || (!person.Tags.Has("vocation.religious.active")
                && IsAtOrPastRetirementAge(
                    person,
                    gameState.Year))
            || career.JobLevel <= 0
            || career.JobLevel >= 5)
        {
            return;
        }

        if (person.Age <= PromotionMinAge)
        {
            ApplyFailedWorkHarderSatisfactionPenalty(
                person,
                gameState.Year);
            return;
        }

        var obsolescence =
            _career.GetObsolescencePressure(
                person,
                gameState.Year);

        var intellect =
            GetStat(
                person,
                "intellect");

        var definition =
            _career.GetDefinition(
                person);

        if (definition is null)
            return;

        var careerAbility =
            _career.GetCareerAbility(
                person,
                definition);

        var promotionAptitude =
            CareerBalanceRules.GetPromotionAptitude(
                careerAbility,
                intellect,
                definition.PrimaryStat.Equals(
                    "intellect",
                    StringComparison.OrdinalIgnoreCase),
                career.JobLevel);

        var promotionChance =
            (promotionAptitude / 5.0)
            * 0.02;

        var education =
            _education.GetEducationLevel(
                person);

        if (person.Tags.Has(
            "modifier.work_harder"))
        {
            promotionChance +=
                CareerBalanceRules.GetWorkHarderPromotionBonus(
                    promotionAptitude,
                    education);
        }

        // Status is a small local edge and is deliberately added before the
        // education/level multipliers, so it cannot bypass qualification gates.
        promotionChance +=
            _statusResolver()?.GetCareerPromotionBonus(person) ?? 0;

        var targetJobLevel =
            career.JobLevel + 1;

        // Level 5 is a standout lifetime achievement rather than a normal
        // continuation of the promotion ladder. Education 5 is mandatory.
        if (targetJobLevel >= 5
            && education < 5)
        {
            ApplyFailedWorkHarderSatisfactionPenalty(
                person,
                gameState.Year);
            return;
        }

        var expectedEducation =
            _career.GetExpectedEducation(
                definition,
                targetJobLevel);

        promotionChance *=
            CareerBalanceRules.GetEducationPromotionMultiplier(
                education,
                expectedEducation,
                targetJobLevel);

        promotionChance *=
            CareerBalanceRules.GetTargetLevelPromotionMultiplier(
                targetJobLevel);

        promotionChance *=
            obsolescence
                .PromotionMultiplier;

        promotionChance *= Math.Clamp(
            _career.GetTemperamentMultiplier(
                definition,
                person),
            0.90,
            1.15);

        if (person.Tags.Has(
                "personality.choleric")
            && person.Tags.Has(
                "modifier.work_harder"))
        {
            promotionChance *= 1.10;
        }

        if (_random.NextDouble()
            >= promotionChance)
        {
            ApplyFailedWorkHarderSatisfactionPenalty(
                person,
                gameState.Year);
            return;
        }

        _career.SetJobLevel(
            person,
            targetJobLevel);

        // A promotion is a meaningful morale boost. +2 through the existing
        // temperament-aware adjustment guarantees at least some improvement
        // for anyone below the maximum satisfaction state.
        _career.ChangeJobSatisfaction(
            person,
            2);

        var promoted =
            _career.GetCareer(
                person);

        _events.Publish(
            new GameEvent
            {
                Type =
                    "career.promotion",

                Year =
                    gameState.Year,

                SubjectId =
                    person.Id,

                Data =
                    new Dictionary<string, string>
                    {
                        ["careerId"] =
                            promoted.CareerId
                            ?? string.Empty,

                        ["careerName"] =
                            promoted.CareerName
                            ?? string.Empty,

                        ["jobTitle"] =
                            promoted.JobTitle,

                        ["jobLevel"] =
                            promoted.JobLevel
                                .ToString(),

                        ["promotionChance"] =
                            promotionChance
                                .ToString(
                                    "0.000"),

                        ["achievement"] =
                            promoted.JobLevel >= 5
                                ? "career_level_5"
                                : string.Empty,

                        ["text"] =
                            promoted.JobLevel >= 5
                                ? $"{_family.GetDisplayName(person)} reached the pinnacle of their career as {promoted.JobTitle}, a standout lifetime achievement."
                                : $"{_family.GetDisplayName(person)} was promoted to {promoted.JobTitle}."
                    }
            });
    }

    private void ApplyFailedWorkHarderSatisfactionPenalty(
        IPerson person,
        int year)
    {
        if (!person.Tags.Has("modifier.work_harder"))
            return;

        var streak = 0;
        for (var offset = 0; offset < 5; offset++)
        {
            var usedThisYear = _events.GetEventsForYear(year - offset)
                .Any(gameEvent =>
                    gameEvent.SubjectId == person.Id
                    && gameEvent.Type.Equals(
                        "career.work_harder",
                        StringComparison.OrdinalIgnoreCase));

            if (!usedThisYear)
                break;

            streak++;
        }

        var lossChance =
            CareerPressureRules.GetFailedRepeatedOverworkSatisfactionLossChance(
                streak);

        if (lossChance > 0
            && _random.NextDouble() < lossChance)
        {
            _career.ChangeJobSatisfaction(
                person,
                -1);
        }
    }

    private bool IsAtOrPastRetirementAge(
        IPerson person,
        int gameYear)
    {
        var retirementAge =
            _retirementRules
                .GetRule(
                    gameYear)
                .GetRetirementAge(
                    _family.GetSex(person));

        return person.Age >= retirementAge;
    }

    private int GetStat(
        IPerson person,
        string id)
    {
        return _stats
            .GetStats(
                person)
            .First(
                stat =>
                    stat.Id.Equals(
                        id,
                        StringComparison.OrdinalIgnoreCase))
            .Value;
    }
}
