namespace Dynastia.Contracts;

public sealed record PropertySaleOption(
    Guid PropertyId,
    TownInfo Town,
    string RegionName,
    bool IsResidence,
    decimal CurrentHousePrice,
    decimal SaleValue)
{
    public string Status =>
        IsResidence
            ? "Residence"
            : "Rented";
}
