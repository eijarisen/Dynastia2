namespace Dynastia.Contracts;

public sealed record ContextWeightContext(
    int Year,
    int Age,
    Sex? Sex = null,
    string? Temperament = null,
    string? Morals = null,
    SettlementClass? SettlementClass = null);
