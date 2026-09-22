using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

public sealed class CareerRetirementYearSystem : IYearSystem
{
    private readonly ICareerService _career;
    private readonly IFamilyService _family;
    private readonly RetirementRuleCatalog
        _retirementRules;
    private readonly IGameEventBus _events;

    public CareerRetirementYearSystem(
        ICareerService career,
        IFamilyService family,
        RetirementRuleCatalog retirementRules,
        IGameEventBus events)
    {
        _career = career;
        _family = family;
        _retirementRules = retirementRules;
        _events = events;
    }

    public string Id => "career.retirement";
    public YearPhase Phase => YearPhase.Status;
    public IReadOnlyCollection<string> Before => Array.Empty<string>();
    public IReadOnlyCollection<string> After => Array.Empty<string>();

    public void Execute(IGameState gameState)
    {
        foreach (var person in gameState.People)
        {
            if (person.Tags.Has("state.dead")
                || person.Tags.Has(
                    "simulation.peripheral_inactive"))
            {
                continue;
            }

            var retirementAge =
                _retirementRules
                    .GetRule(
                        gameState.Year)
                    .GetRetirementAge(
                        _family.GetSex(person));

            var career = _career.GetCareer(person);

            if (person.Age < retirementAge
                || person.Tags.Has("occupation.criminal")
                || career.IsSelfEmployed
                || career.IsRetired
                || career.JobLevel >= 5)
            {
                continue;
            }

            _career.Retire(person);

            _events.Publish(
                new GameEvent
                {
                    Type = "career.retirement",
                    Year = gameState.Year,
                    SubjectId = person.Id,
                    Data = new Dictionary<string, string>
                    {
                        ["text"] =
                            $"{_family.GetDisplayName(person)} has retired at age {person.Age}."
                    }
                });
        }
    }
}
