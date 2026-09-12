using Dynastia.Contracts;

namespace Dynastia.Mechanics.Aging;

public sealed class AgingSystem : IYearSystem
{
    private readonly IFamilyService _family;
    private readonly IGameEventBus _events;

    public AgingSystem(
        IFamilyService family,
        IGameEventBus events)
    {
        _family = family;
        _events = events;
    }

    public string Id =>
        "aging.increment_age";

    public YearPhase Phase =>
        YearPhase.Aging;

    public IReadOnlyCollection<string> Before =>
        Array.Empty<string>();

    public IReadOnlyCollection<string> After =>
        Array.Empty<string>();

    public void Execute(
        IGameState gameState)
    {
        foreach (var person in
            gameState.People)
        {
            if (person.Tags.Has(
                    "state.dead")
                || SimulationState.IsInactive(
                    person))
            {
                continue;
            }

            person.Age++;

            person.Tags.Remove(
                "age.child");

            person.Tags.Remove(
                "age.adult");

            person.Tags.Add(
                person.Age >= 18
                    ? "age.adult"
                    : "age.child");

            if (person.Age != 18)
                continue;

            _events.Publish(
                new GameEvent
                {
                    Type =
                        "life.adult",

                    Year =
                        gameState.Year,

                    SubjectId =
                        person.Id,

                    Data =
                        new Dictionary<string, string>
                        {
                            ["text"] =
                                $"{_family.GetDisplayName(person)} " +
                                "has become an adult."
                        }
                });
        }
    }
}
