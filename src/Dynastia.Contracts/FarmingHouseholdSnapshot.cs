namespace Dynastia.Contracts;

public sealed record FarmingHouseholdSnapshot(
    IReadOnlyList<FarmlandAssetInfo> Farmland,
    string ResidenceTownId,
    int LocalParcelCount,
    int AvailableWorkers,
    int LocalWorkerCapacity,
    decimal LastAnnualIncome,
    decimal ExpectedAnnualIncome)
{
    public int TotalParcelCount => Farmland.Count;
}
