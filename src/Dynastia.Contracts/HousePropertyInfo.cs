namespace Dynastia.Contracts;

public sealed record HousePropertyInfo(
    Guid Id,
    TownInfo Town,
    bool IsResidence,
    bool IsRented,
    Guid? AssignedHeirId = null)
{
    public string Status =>
        IsResidence
            ? "Living In"
            : "Rented";
}
