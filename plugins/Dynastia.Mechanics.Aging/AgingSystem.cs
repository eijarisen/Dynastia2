using Dynastia.Contracts;

namespace Dynastia.Mechanics.Aging;

public sealed class AgingSystem : IYearSystem
{
    public string Id => "aging.increment_age";
    public YearPhase Phase => YearPhase.Aging;
    public IReadOnlyCollection<string> Before => Array.Empty<string>();
    public IReadOnlyCollection<string> After => Array.Empty<string>();

    public void Execute(IGameState gameState)
    {
        foreach (var person in gameState.People)
        {
            if (person.Tags.Has("state.dead"))
                continue;

            person.Age++;

            person.Tags.Remove("age.child");
            person.Tags.Remove("age.adult");
            person.Tags.Add(person.Age >= 18 ? "age.adult" : "age.child");
        }
    }
}
