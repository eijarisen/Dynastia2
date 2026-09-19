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

    public string PolityId { get; init; } =
        string.Empty;

    public string PolityName { get; init; } =
        string.Empty;

    public string UrbanStatus { get; init; } =
        "rural_or_unrecorded";

    public bool IsDestinationAvailable { get; init; }

    public SettlementClass SettlementClass =>
        Population switch
        {
            < 5000 => Dynastia.Contracts.SettlementClass.SmallTown,
            < 20000 => Dynastia.Contracts.SettlementClass.Town,
            < 100000 => Dynastia.Contracts.SettlementClass.City,
            _ => Dynastia.Contracts.SettlementClass.MajorCity
        };

    public string SettlementClassDisplayName =>
        SettlementClass switch
        {
            Dynastia.Contracts.SettlementClass.SmallTown => "Small Town",
            Dynastia.Contracts.SettlementClass.MajorCity => "Major City",
            _ => SettlementClass.ToString()
        };

    public decimal HousingIndex =>
        SettlementClass switch
        {
            Dynastia.Contracts.SettlementClass.SmallTown => 0.75m,
            Dynastia.Contracts.SettlementClass.Town => 0.90m,
            Dynastia.Contracts.SettlementClass.City => 1.10m,
            _ => 1.35m
        };

    public decimal LivingCostIndex =>
        SettlementClass switch
        {
            Dynastia.Contracts.SettlementClass.SmallTown => 0.85m,
            Dynastia.Contracts.SettlementClass.Town => 0.95m,
            Dynastia.Contracts.SettlementClass.City => 1.05m,
            _ => 1.20m
        };

    public string DisplayName =>
        string.Equals(
            Town,
            County,
            StringComparison.OrdinalIgnoreCase)
                ? Town
                : $"{Town}, {County}";
}
