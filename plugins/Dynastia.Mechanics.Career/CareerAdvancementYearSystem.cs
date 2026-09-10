using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

public sealed class CareerAdvancementYearSystem : IYearSystem
{
    private const double FiredChance = 0.01;
    private const int PromotionMinAge = 21;
    private const double WorkHarderBaseBonus = 0.15;
    private const double WorkHarderIntellectBonus = 0.05;

    private readonly ICareerService _career;
    private readonly IEducationService _education;
    private readonly IStatsService _stats;
    private readonly IGameRandom _random;
    private readonly IFamilyService _family;
    private readonly IGameEventBus _events;

    public CareerAdvancementYearSystem(
        ICareerService career,
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

    public string Id => "career.employment";
    public YearPhase Phase => YearPhase.LifeEvents;
    public IReadOnlyCollection<string> Before =>
        ["relationships.marriage"];

    public IReadOnlyCollection<string> After =>
        [
            "actions.queued.life_events",
            "justice.crime_and_prison_divorce"
        ];

    public void Execute(IGameState gameState)
    {
        var living = gameState.People
            .Where(person => person.Tags.Has("state.alive"))
            .ToList();

        foreach (var person in living)
            ProcessPerson(gameState, person);
    }

    private void ProcessPerson(
        IGameState gameState,
        IPerson person)
    {
        var career = _career.GetCareer(person);

        if (career.IsRetired)
        {
            person.Tags.Remove("modifier.work_harder");
            return;
        }

        if (career.JobLevel > 0
            && _random.NextDouble() < FiredChance)
        {
            _events.Publish(
                new GameEvent
                {
                    Type = "career.fired",
                    Year = gameState.Year,
                    SubjectId = person.Id,
                    Data = new Dictionary<string, string>
                    {
                        ["text"] =
                            $"{_family.GetDisplayName(person)} was fired from their job."
                    }
                });

            _career.SetJobLevel(person, 0);
            person.Tags.Remove("modifier.work_harder");
            return;
        }

        career = _career.GetCareer(person);

        if (career.JobLevel <= 0
            || person.Age <= PromotionMinAge
            || career.JobLevel >= 5)
        {
            person.Tags.Remove("modifier.work_harder");
            return;
        }

        var intellect = GetStat(person, "intellect");

        var promotionChance =
            (intellect / 5.0) * 0.02;

        if (person.Tags.Has("modifier.work_harder"))
        {
            promotionChance +=
                WorkHarderBaseBonus
                + intellect * WorkHarderIntellectBonus;
        }

        var education =
            _education.GetEducationLevel(person);

        if (career.JobLevel >= 2
            && education < 3)
        {
            promotionChance /= 5;
        }

        if (career.JobLevel >= 3
            && education < 4)
        {
            promotionChance /= 10;
        }

        if (career.JobLevel >= 4
            && education < 5)
        {
            promotionChance = 0;
        }

        if (_random.NextDouble() < promotionChance)
        {
            _career.SetJobLevel(
                person,
                career.JobLevel + 1);

            var promoted =
                _career.GetCareer(person);

            _events.Publish(
                new GameEvent
                {
                    Type = "career.promotion",
                    Year = gameState.Year,
                    SubjectId = person.Id,
                    Data = new Dictionary<string, string>
                    {
                        ["jobTitle"] = promoted.JobTitle,
                        ["jobLevel"] = promoted.JobLevel.ToString(),
                        ["text"] =
                            $"{_family.GetDisplayName(person)} was promoted to {promoted.JobTitle}."
                    }
                });
        }

        person.Tags.Remove("modifier.work_harder");
    }

    private int GetStat(IPerson person, string id) =>
        _stats.GetStats(person)
            .First(stat =>
                stat.Id.Equals(
                    id,
                    StringComparison.OrdinalIgnoreCase))
            .Value;
}
