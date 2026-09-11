namespace Dynastia.Contracts;

public sealed record LocationOpportunitySnapshot(
    TownInfo Town,
    string RegionName,
    IReadOnlyList<string> RegionOpportunityTags,
    IReadOnlyList<string> TownOpportunityTags,
    string Description);
