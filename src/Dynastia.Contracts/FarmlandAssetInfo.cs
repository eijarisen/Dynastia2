namespace Dynastia.Contracts;

public sealed record FarmlandAssetInfo(
    Guid Id,
    TownInfo Town,
    int AcquiredYear,
    string AcquisitionSource,
    Guid? AssignedHeirId = null);
