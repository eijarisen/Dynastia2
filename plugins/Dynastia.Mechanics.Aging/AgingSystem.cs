using Dynastia.Contracts;

namespace Dynastia.Mechanics.Aging;

public sealed class AgingSystem : IYearSystem
{
    public string Id => "aging.increment_age";

    public YearPhase Phase => YearPhase.Aging;

    public IReadOnlyCollection<string> Before =>
        Array.Empty<string>();

    public IReadOnlyCollection<string> After =>
        Array.Empty<string>();

    public void Execute(IGameState gameState)
    {
        foreach (var person in gameState.People)
        {
            if (person.Tags.Has("state.dead"))
                continue;

            person.Age++;

            UpdateAgeTags(person);
        }
    }

    private static void UpdateAgeTags(IPerson person)
    {
        person.Tags.Remove("age.child");
        person.Tags.Remove("age.adult");

        if (person.Age >= 18)
            person.Tags.Add("age.adult");
        else
            person.Tags.Add("age.child");
    }
}