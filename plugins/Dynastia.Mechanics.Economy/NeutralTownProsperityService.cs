using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

internal sealed class NeutralTownProsperityService : ITownProsperityService
{
    public static NeutralTownProsperityService Instance { get; } = new();

    private NeutralTownProsperityService()
    {
    }

    public TownProsperitySnapshot Get(TownInfo town) =>
        new(
            100,
            "Stable",
            0,
            Array.Empty<string>(),
            Array.Empty<TownProsperityHistoryPoint>());

    public TownProsperitySnapshot Get(TownInfo town, int year) =>
        Get(town);

    public decimal GetIncomeMultiplier(
        TownInfo town,
        LocalEconomicStrength strength) =>
        1m;

    public void ApplyHistoricalShock(
        IEnumerable<string> placeIds,
        string sourceId,
        int delta,
        int recoveryYears)
    {
    }
}
