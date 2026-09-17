namespace Dynastia.Contracts;

public sealed record MoveResidentBranchResult(
    bool Success,
    string? Reason,
    IReadOnlyList<Guid> MovedMemberIds,
    TownInfo? OriginTown,
    TownInfo? DestinationTown,
    HousePropertyInfo? TransferredHouse)
{
    public bool TownChanged =>
        Success
        && OriginTown is not null
        && DestinationTown is not null
        && !OriginTown.Id.Equals(
            DestinationTown.Id,
            StringComparison.OrdinalIgnoreCase);

    public bool ProvidedHouse =>
        TransferredHouse is not null;
}
