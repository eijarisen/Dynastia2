namespace Dynastia.Contracts;

public sealed record FarmlandAssetInfo(
    Guid Id,
    TownInfo Town,
    int AcquiredYear,
    string AcquisitionSource,
    Guid? AssignedHeirId = null,
    string FarmTypeId = "",
    string FarmTypeDisplayName = "",
    string FarmTypeEmoji = "",
    string? LivestockTypeId = null,
    string? LivestockDisplayName = null,
    string? LivestockEmoji = null);
