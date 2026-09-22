using Dynastia.Contracts;

namespace Dynastia.Mechanics.Justice;

internal sealed class CriminalOccupationYearSystem : IYearSystem
{
    private readonly CriminalOccupationService _crime;

    public CriminalOccupationYearSystem(CriminalOccupationService crime)
    {
        _crime = crime;
    }

    public string Id => "justice.criminal_occupation";

    public YearPhase Phase => YearPhase.LifeEvents;

    public IReadOnlyCollection<string> Before => ["justice.crime"];

    public IReadOnlyCollection<string> After => ["actions.queued.life_events"];

    public void Execute(IGameState gameState)
    {
        foreach (var person in gameState.People
            .Where(person => person.Tags.Has("state.alive")
                && !SimulationState.IsInactive(person))
            .ToList())
        {
            _crime.ResolveAnnualHeist(person);
        }
    }
}
