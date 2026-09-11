using Dynastia.Contracts;

namespace Dynastia.Mechanics.Personality;

internal sealed class PersonalityAssignmentYearSystem :
    IYearSystem
{
    private readonly StandardPersonalityService
        _personality;

    public PersonalityAssignmentYearSystem(
        StandardPersonalityService personality)
    {
        _personality = personality;
    }

    public string Id =>
        "personality.age_five_assignment";

    public YearPhase Phase =>
        YearPhase.Status;

    public IReadOnlyCollection<string> Before =>
        ["education.passive"];

    public IReadOnlyCollection<string> After =>
        ["aging.increment_age"];

    public void Execute(
        IGameState gameState)
    {
        foreach (var person in
            gameState.People)
        {
            _personality.ReconcilePerson(
                person);
        }
    }
}
