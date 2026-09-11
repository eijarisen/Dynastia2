namespace Dynastia.Contracts;

public sealed record CareerLocationRequirement(
    CareerLocationType LocationType,
    SettlementClass MinimumSettlementClass,
    IReadOnlyCollection<string> RequiredOpportunityTags);
