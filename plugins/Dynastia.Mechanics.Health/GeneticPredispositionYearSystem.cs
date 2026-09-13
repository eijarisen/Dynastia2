using Dynastia.Contracts;

namespace Dynastia.Mechanics.Health;

internal sealed class GeneticPredispositionYearSystem : IYearSystem
{
    private readonly GeneticPredispositionService _genetics;
    public GeneticPredispositionYearSystem(GeneticPredispositionService genetics) => _genetics = genetics;
    public string Id => "health.genetic_predispositions";
    public YearPhase Phase => YearPhase.PreYear;
    public IReadOnlyCollection<string> Before => Array.Empty<string>();
    public IReadOnlyCollection<string> After => Array.Empty<string>();
    public void Execute(IGameState gameState) => _genetics.ReconcileAll();
}
