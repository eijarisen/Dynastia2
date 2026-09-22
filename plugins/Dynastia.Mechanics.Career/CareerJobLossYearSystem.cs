using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

/// <summary>
/// Keeps job loss after household finances while promotions are resolved
/// before payroll. It also clears Work Harder transient modifiers after all
/// salary and health effects for the turn have had a chance to consume them.
/// </summary>
public sealed class CareerJobLossYearSystem :
    IYearSystem
{
    private const double FiredChance =
        0.01;

    private const string WorkHarderIncomePrefix =
        "modifier.salary.work_harder.";

    private const string RecoverIncomePrefix =
        "modifier.salary.recover.";

    private readonly StandardCareerService _career;
    private readonly IGameRandom _random;
    private readonly IFamilyService _family;
    private readonly IGameEventBus _events;

    public CareerJobLossYearSystem(
        StandardCareerService career,
        IGameRandom random,
        IFamilyService family,
        IGameEventBus events)
    {
        _career = career;
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
        foreach (var person in
            gameState.People.ToList())
        {
            try
            {
                ProcessPerson(
                    gameState,
                    person);
            }
            finally
            {
                ClearTransientModifiers(
                    person);
            }
        }
    }

    private void ProcessPerson(
        IGameState gameState,
        IPerson person)
    {
        if (!person.Tags.Has(
                "state.alive")
            || person.Tags.Has("vocation.religious.active")
            || person.Tags.Has(
                "simulation.peripheral_inactive"))
        {
            return;
        }

        var career =
            _career.GetCareer(
                person);

        if (career.IsRetired
            || career.JobLevel <= 0)
        {
            return;
        }

        var institutionFailure =
            _career.GetCurrentInstitutionFailure(person);
        if (institutionFailure is not null)
        {
            PublishInstitutionClosure(
                gameState,
                person,
                career,
                institutionFailure);
            _career.SetJobLevel(person, 0);
            return;
        }

        if (career.JobLevel >= 5)
            return;

        var obsolescence =
            _career.GetObsolescencePressure(
                person,
                gameState.Year);

        var jobLossChance =
            FiredChance
            + obsolescence
                .AdditionalJobLossChance;

        jobLossChance =
            PersonalityInfluence.AdjustProbability(
                jobLossChance,
                person,
                choleric: 0.15);

        if (_random.NextDouble()
            >= jobLossChance)
        {
            return;
        }

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
    }

    private void PublishInstitutionClosure(
        IGameState gameState,
        IPerson person,
        CareerSnapshot career,
        CareerInstitutionFailure failure)
    {
        var institutionName = failure.Institution.DisplayName;
        var text =
            $"The local {institutionName.ToLowerInvariant()} could no longer support " +
            $"{career.CareerName ?? "this profession"}, and {_family.GetDisplayName(person)} " +
            $"lost their position as {career.JobTitle}.";

        _events.Publish(
            new GameEvent
            {
                Type = "career.fired",
                Year = gameState.Year,
                SubjectId = person.Id,
                Data = new Dictionary<string, string>
                {
                    ["careerId"] = career.CareerId ?? string.Empty,
                    ["careerName"] = career.CareerName ?? string.Empty,
                    ["jobTitle"] = career.JobTitle,
                    ["reason"] = "local_institution_closed",
                    ["institutionId"] = failure.Requirement.InstitutionId,
                    ["institutionName"] = institutionName,
                    ["requiredInstitutionTier"] = failure.Requirement.MinimumTier.ToString(),
                    ["currentInstitutionTier"] = failure.Institution.Tier.ToString(),
                    ["townId"] = failure.Town.Id,
                    ["text"] = text
                }
            });
    }

    private static void ClearTransientModifiers(
        IPerson person)
    {
        person.Tags.Remove(
            "modifier.work_harder");

        foreach (var tag in
            person.Tags.All
                .Where(
                    tag =>
                        tag.StartsWith(
                            WorkHarderIncomePrefix,
                            StringComparison.OrdinalIgnoreCase)
                        || tag.StartsWith(
                            RecoverIncomePrefix,
                            StringComparison.OrdinalIgnoreCase))
                .ToList())
        {
            person.Tags.Remove(
                tag);
        }
    }
}
