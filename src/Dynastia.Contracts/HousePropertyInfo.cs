namespace Dynastia.Contracts;

public sealed record HousePropertyInfo(
    Guid Id,
    TownInfo Town,
    bool IsResidence,
    bool IsRented)
{
    public string Status =>
        IsResidence
            ? "Living In"
            : "Rented";
}
