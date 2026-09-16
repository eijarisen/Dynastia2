using Dynastia.Contracts;

namespace Dynastia.Mechanics.Crafts;

internal sealed class CraftExperienceYearSystem : IYearSystem
{
    private readonly StandardCraftService _crafts;

    public CraftExperienceYearSystem(StandardCraftService crafts)
    {
        _crafts = crafts;
    }

    public string Id => "craft.record_experience";
    public YearPhase Phase => YearPhase.Aging;
    public IReadOnlyCollection<string> Before => Array.Empty<string>();
    public IReadOnlyCollection<string> After => ["aging.increment_age"];

    public void Execute(IGameState gameState)
    {
        foreach (var person in gameState.People)
        {
            if (!person.Tags.Has("state.alive") || SimulationState.IsInactive(person))
                continue;
            _crafts.RecordWorkYear(person);
        }
    }
}
