using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

internal sealed class CareerExperienceYearSystem : IYearSystem
{
    private readonly StandardCareerService _career;

    public CareerExperienceYearSystem(
        StandardCareerService career)
    {
        _career = career;
    }

    public string Id =>
        "career.record_experience";

    public YearPhase Phase =>
        YearPhase.Aging;

    public IReadOnlyCollection<string> Before =>
        Array.Empty<string>();

    public IReadOnlyCollection<string> After =>
        ["aging.increment_age"];

    public void Execute(IGameState gameState)
    {
        foreach (var person in gameState.People)
        {
            if (!person.Tags.Has("state.alive")
                || SimulationState.IsInactive(person))
            {
                continue;
            }

            _career.RecordCurrentExperience(person);
        }
    }
}
