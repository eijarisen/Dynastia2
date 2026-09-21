using Dynastia.Contracts;

namespace Dynastia.Mechanics.TownLife;

internal sealed class TownProsperityYearSystem : IYearSystem
{
    private readonly IGamePluginContext _context;
    private readonly StandardTownProsperityService _prosperity;
    private readonly IReadOnlyList<HistoricalProsperityEffect> _historicalEffects;

    public TownProsperityYearSystem(
        IGamePluginContext context,
        StandardTownProsperityService prosperity,
        IReadOnlyList<HistoricalProsperityEffect> historicalEffects)
    {
        _context = context;
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
        _prosperity.AdvanceTrackedTowns();
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
            var endYear = historical.GetEventEndYear(effect.EventId);
            if (startYear is null
                || endYear is null
                || startYear > gameState.Year)
            {
                continue;
            }

            // The configured shock remains at full strength for the complete
            // historical event. Recovery starts only after EndYear.
            if (gameState.Year - endYear.Value >= effect.RecoveryYears)
                continue;

            var eventIsActive = gameState.Year <= endYear.Value;
            var scopeYear = eventIsActive
                ? gameState.Year
                : endYear.Value;
            var appliedYear = eventIsActive
                ? gameState.Year
                : endYear.Value;

            var places = historical.GetAffectedPlaceIds(
                effect.EventId,
                scopeYear);
            if (places.Count == 0)
                continue;

            _prosperity.EnsureHistoricalShock(
                places,
                effect.EventId,
                effect.Delta,
                effect.RecoveryYears,
                appliedYear);
        }
    }

}
