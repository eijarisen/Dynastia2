using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

public sealed class CareerAdvancementYearSystem :
    IYearSystem
{
    private const double FiredChance =
        0.01;

    private const int PromotionMinAge =
        21;

    private const double WorkHarderBaseBonus =
        0.15;

    private const double WorkHarderIntellectBonus =
        0.05;

    private readonly StandardCareerService _career;
    private readonly IEducationService _education;
    private readonly IStatsService _stats;
    private readonly IGameRandom _random;
    private readonly IFamilyService _family;
    private readonly IGameEventBus _events;

    public CareerAdvancementYearSystem(
        StandardCareerService career,
        IEducationService education,
        IStatsService stats,
        IGameRandom random,
        IFamilyService family,
        IGameEventBus events)
    {
        _career = career;
        _education = education;
        _stats = stats;
        _random = random;
        _family = family;
        _events = events;
    }

    public string Id =>
        "career.employment";

    public YearPhase Phase =>
        YearPhase.LifeEvents;

    public IReadOnlyCollection<string> Before =>
        Array.Empty<string>();

    public IReadOnlyCollection<string> After =>
        ["actions.queued.life_events"];

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
        var career =
            _career.GetCareer(
                person);

        if (career.IsRetired)
        {
            person.Tags.Remove(
                "modifier.work_harder");

            return;
        }

        var obsolescence =
            _career.GetObsolescencePressure(
                person,
                gameState.Year);

        if (career.JobLevel > 0
            && career.JobLevel < 5)
        {
            var jobLossChance =
                FiredChance
                + obsolescence
                    .AdditionalJobLossChance;

            if (_random.NextDouble()
                < jobLossChance)
            {
                var text =
                    obsolescence.YearsAfterEnd > 0
                        ? $"{_family.GetDisplayName(person)} " +
                          $"lost their job as {career.JobTitle} " +
                          $"as {career.CareerName ?? "the profession"} declined."
                        : $"{_family.GetDisplayName(person)} " +
                          $"was fired from their job as {career.JobTitle}.";

                _events.Publish(
                    new GameEvent
                    {
                        Type =
                            "career.fired",

                        Year =
                            gameState.Year,

                        SubjectId =
                            person.Id,

                        Data =
                            new Dictionary<string, string>
                            {
                                ["careerId"] =
                                    career.CareerId
                                    ?? string.Empty,

                                ["careerName"] =
                                    career.CareerName
                                    ?? string.Empty,

                                ["jobTitle"] =
                                    career.JobTitle,

                                ["jobLossChance"] =
                                    jobLossChance
                                        .ToString(
                                            "0.000"),

                                ["yearsAfterCareerEnd"] =
                                    obsolescence
                                        .YearsAfterEnd
                                        .ToString(),

                                ["text"] =
                                    text
                            }
                    });

                _career.SetJobLevel(
                    person,
                    0);

                person.Tags.Remove(
                    "modifier.work_harder");

                return;
            }
        }

        career =
            _career.GetCareer(
                person);

        if (career.JobLevel <= 0
            || person.Age <= PromotionMinAge
            || career.JobLevel >= 5)
        {
            person.Tags.Remove(
                "modifier.work_harder");

            return;
        }

        var intellect =
            GetStat(
                person,
                "intellect");

        var promotionChance =
            (intellect / 5.0)
            * 0.02;

        if (person.Tags.Has(
            "modifier.work_harder"))
        {
            promotionChance +=
                WorkHarderBaseBonus
                + intellect
                    * WorkHarderIntellectBonus;
        }

        var education =
            _education.GetEducationLevel(
                person);

        // Preserve the existing universal education influence.
        // The new feature intentionally does not add career-specific
        // qualification requirements.
        if (career.JobLevel >= 2
            && education < 3)
        {
            promotionChance /=
                5;
        }

        if (career.JobLevel >= 3
            && education < 4)
        {
            promotionChance /=
                10;
        }

        if (career.JobLevel >= 4
            && education < 5)
        {
            promotionChance =
                0;
        }

        promotionChance *=
            obsolescence
                .PromotionMultiplier;

        if (_random.NextDouble()
            < promotionChance)
        {
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

        person.Tags.Remove(
            "modifier.work_harder");
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
