namespace Dynastia.Mechanics.StatImprovements;

internal sealed record PaidStatImprovementDefinition(
    string ActionId,
    string StatId,
    string StatName,
    int MinimumMedicalTier,
    decimal BaseCost);
