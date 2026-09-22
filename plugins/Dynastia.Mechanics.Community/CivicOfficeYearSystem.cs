using Dynastia.Contracts;

namespace Dynastia.Mechanics.Community;

internal sealed class CivicOfficeYearSystem : IYearSystem
{
    private readonly CivicOfficeService _civic;

    public CivicOfficeYearSystem(CivicOfficeService civic) =>
        _civic = civic;

    public string Id => "community.civic_office";
    public YearPhase Phase => YearPhase.LifeEvents;
    public IReadOnlyCollection<string> Before => Array.Empty<string>();
    public IReadOnlyCollection<string> After => ["career.employment", "justice.crime"];

    public void Execute(IGameState gameState) =>
        _civic.ProcessRelevantTowns();
}
