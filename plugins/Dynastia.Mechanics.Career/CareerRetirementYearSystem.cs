using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

public sealed class CareerRetirementYearSystem : IYearSystem
{
    private const int MaleRetirementAge = 65;
    private const int FemaleRetirementAge = 60;

    private readonly ICareerService _career;
    private readonly IFamilyService _family;
    private readonly IGameEventBus _events;

    public CareerRetirementYearSystem(
        ICareerService career,
        IFamilyService family,
        IGameEventBus events)
    {
        _career = career;
        _family = family;
        _events = events;
    }

    public string Id => "career.retirement";
    public YearPhase Phase => YearPhase.Status;
    public IReadOnlyCollection<string> Before => Array.Empty<string>();
    public IReadOnlyCollection<string> After => ["aging.increment_age"];

    public void Execute(IGameState gameState)
    {
        foreach (var person in gameState.People)
        {
            if (person.Tags.Has("state.dead"))
                continue;

            var retirementAge =
                _family.GetSex(person) == Sex.Male
                    ? MaleRetirementAge
                    : FemaleRetirementAge;

            var career = _career.GetCareer(person);

            if (person.Age != retirementAge
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
