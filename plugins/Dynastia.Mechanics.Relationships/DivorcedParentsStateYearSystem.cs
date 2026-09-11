using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

internal sealed class DivorcedParentsStateYearSystem :
    IYearSystem
{
    public string Id =>
        "relationships.parents_divorced_state";

    public YearPhase Phase =>
        YearPhase.DerivedState;

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
            if (person.Age >= 18
                || person.Tags.Has(
                    "state.dead"))
            {
                person.Tags.Remove(
                    DivorcedParentsTracker.Tag);
            }
        }
    }
}
