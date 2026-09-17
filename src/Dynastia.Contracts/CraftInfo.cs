namespace Dynastia.Contracts;

public sealed record CraftInfo(
    string Id,
    string Name,
    int StartYear,
    int? EndYear,
    int MinimumLearningAge,
    double BaseWeight,
    string PrimaryStat,
    string? SecondaryStat,
    string TownPreference,
    SettlementClass MinimumSettlementClass,
    IReadOnlyList<string> RequiredOpportunityTags,
    IReadOnlyList<string> PreferredOpportunityTags,
    string Emoji,
    string SelfEmploymentTitle,
    IReadOnlyList<string> PrimaryCareerIds,
    IReadOnlyList<string> SecondaryCareerIds)
{
    public string PrimaryCareerId =>
        PrimaryCareerIds.FirstOrDefault() ?? string.Empty;

    public IReadOnlyList<string> RelatedCareerIds =>
        SecondaryCareerIds;

    public bool IsHistoricallyAvailable(int year) =>
        year >= StartYear
        && (EndYear is null || year <= EndYear.Value);

    public bool MeetsHardAvailability(
        int year,
        int age,
        SettlementClass settlementClass,
        IReadOnlySet<string> opportunityTags)
    {
        if (!IsHistoricallyAvailable(year)
            || age < MinimumLearningAge
            || settlementClass < MinimumSettlementClass)
        {
            return false;
        }

        return RequiredOpportunityTags.All(opportunityTags.Contains);
    }
}
