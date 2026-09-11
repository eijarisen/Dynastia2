namespace Dynastia.Contracts;

public sealed record TownInfo(
    string Town,
    string County,
    double Longitude,
    double Latitude,
    int Population)
{
    public string Id { get; init; } =
        string.Empty;

    public string RegionId { get; init; } =
        string.Empty;

    public SettlementClass SettlementClass =>
        Population switch
        {
            < 5000 => Dynastia.Contracts.SettlementClass.SmallTown,
            < 20000 => Dynastia.Contracts.SettlementClass.Town,
            < 100000 => Dynastia.Contracts.SettlementClass.City,
            _ => Dynastia.Contracts.SettlementClass.MajorCity
        };

    public string DisplayName =>
        string.Equals(
            Town,
            County,
            StringComparison.OrdinalIgnoreCase)
                ? Town
                : $"{Town}, {County}";
}
