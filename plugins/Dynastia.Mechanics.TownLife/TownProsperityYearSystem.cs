using Dynastia.Contracts;

namespace Dynastia.Mechanics.TownLife;

internal sealed class TownProsperityYearSystem : IYearSystem
{
    private readonly IGamePluginContext _context;
    private readonly IGameRandom _random;
    private readonly StandardTownProsperityService _prosperity;
    private readonly IReadOnlyList<HistoricalProsperityEffect> _historicalEffects;

    public TownProsperityYearSystem(
        IGamePluginContext context,
        IGameRandom random,
        StandardTownProsperityService prosperity,
        IReadOnlyList<HistoricalProsperityEffect> historicalEffects)
    {
        _context = context;
        _random = random;
        _prosperity = prosperity;
        _historicalEffects = historicalEffects;
    }

    public string Id => "townlife.prosperity";
    public YearPhase Phase => YearPhase.PreYear;
    public IReadOnlyCollection<string> Before => Array.Empty<string>();
    public IReadOnlyCollection<string> After => Array.Empty<string>();

    public void Execute(IGameState gameState)
    {
        _prosperity.TrackActiveHouseholdTowns(_context);
        _prosperity.AdvanceTrackedTowns(_random);
        ReconcileHistoricalEffects(gameState);
    }

    internal void ReconcileHistoricalEffects(IGameState gameState)
    {
        var historical = _context.GetService<IHistoricalEventService>();
        if (historical is null)
            return;

        foreach (var effect in _historicalEffects)
        {
            var startYear = historical.GetEventStartYear(effect.EventId);
            if (startYear is null || startYear > gameState.Year)
                continue;

            // The economic aftermath can outlive a one-shot/short event. This
            // also migrates saves or start years that begin inside recovery.
            if (gameState.Year - startYear.Value >= effect.RecoveryYears)
                continue;

            var places = historical.GetAffectedPlaceIds(effect.EventId, startYear.Value);
            if (places.Count == 0)
                continue;

            _prosperity.EnsureHistoricalShock(
                places,
                effect.EventId,
                effect.Delta,
                effect.RecoveryYears,
                startYear.Value);
        }
    }

}
