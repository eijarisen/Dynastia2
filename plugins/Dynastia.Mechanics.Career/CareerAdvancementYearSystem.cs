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

    public CareerAdvancementYearSystem(
        StandardCareerService career,
        IEducationService education,
        IStatsService stats,
        IGameRandom random,
        IFamilyService family,
        RetirementRuleCatalog retirementRules,
        IGameEventBus events)
    {
        _career = career;
        _education = education;
        _stats = stats;
        _random = random;
        _family = family;
        _retirementRules = retirementRules;
        _events = events;
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
            || IsAtOrPastRetirementAge(
                person,
                gameState.Year)
            || career.JobLevel <= 0
            || person.Age <= PromotionMinAge
            || career.JobLevel >= 5)
        {
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

        var strength =
            GetStat(
                person,
                "strength");

        var definition =
            _career.GetDefinition(
                person);

        var strengthDrivenCareer =
            definition is not null
            && CareerEntryAptitudeClassifier.Get(
                definition)
                == CareerEntryAptitude.Strength;

        var promotionAptitude =
            CareerBalanceRules.GetPromotionAptitudeStat(
                strengthDrivenCareer,
                career.JobLevel,
                strength,
                intellect);

        var educationMatters =
            CareerBalanceRules.PromotionUsesEducation(
                strengthDrivenCareer,
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
                    education,
                    educationMatters);
        }

        if (educationMatters
            && career.JobLevel >= 2
            && education < 3)
        {
            promotionChance /=
                5;
        }

        if (educationMatters
            && career.JobLevel >= 3
            && education < 4)
        {
            promotionChance /=
                10;
        }

        if (educationMatters
            && career.JobLevel >= 4
            && education < 5)
        {
            promotionChance =
                0;
        }

        promotionChance *=
            obsolescence
                .PromotionMultiplier;

        var personalityModifier =
            person.Tags.Has(
                "personality.melancholic")
                ? -0.10
                : person.Tags.Has(
                    "personality.sanguine")
                    ? 0.10
                    : person.Tags.Has(
                        "personality.choleric")
                        ? 0.15
                        : 0.0;

        if (person.Tags.Has(
                "personality.choleric")
            && person.Tags.Has(
                "modifier.work_harder"))
        {
            personalityModifier +=
                0.10;
        }

        personalityModifier =
            Math.Clamp(
                personalityModifier,
                -PersonalityInfluence.MaximumModifier,
                PersonalityInfluence.MaximumModifier);

        promotionChance *=
            1.0 + personalityModifier;

        if (_random.NextDouble()
            >= promotionChance)
        {
            return;
        }

        _career.SetJobLevel(
            person,
            career.JobLevel + 1);

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

                        ["text"] =
                            $"{_family.GetDisplayName(person)} " +
                            $"was promoted to " +
                            $"{promoted.JobTitle}."
                    }
            });
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
