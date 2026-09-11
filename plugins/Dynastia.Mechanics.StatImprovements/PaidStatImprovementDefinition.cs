namespace Dynastia.Mechanics.StatImprovements;

internal sealed record PaidStatImprovementDefinition(
    string ActionId,
    string StatId,
    string StatName,
    string Label,
    decimal Cost,
    string Description,
    string Narrative);
