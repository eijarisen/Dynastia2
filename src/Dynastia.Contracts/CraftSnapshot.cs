namespace Dynastia.Contracts;

public sealed record CraftSnapshot(
    IReadOnlyList<CraftInfo> KnownCrafts,
    string? ActiveCraftOccupationId,
    string? ActiveCraftOccupationTitle,
    string? ActiveCraftEmoji,
    IReadOnlyDictionary<string, int> WorkYearsByCraft,
    decimal ExpectedAnnualIncome,
    decimal LastAnnualIncome,
    int LastIncomeYear)
{
    public bool IsSelfEmployed =>
        !string.IsNullOrWhiteSpace(ActiveCraftOccupationId);
}
