using Dynastia.Contracts;

namespace Dynastia.Mechanics.TownLife;

internal sealed class StandardTownInstitutionService : ITownInstitutionService
{
    private readonly TownInstitutionCatalog _catalog;
    private readonly ILocalCareerOpportunityService _opportunities;

    public StandardTownInstitutionService(
        TownInstitutionCatalog catalog,
        ILocalCareerOpportunityService opportunities)
    {
        _catalog = catalog;
        _opportunities = opportunities;
    }

    public TownInstitutionSnapshot Resolve(
        TownInfo town,
        int year)
    {
        ArgumentNullException.ThrowIfNull(town);

        var opportunitySnapshot =
            _opportunities.GetOpportunitySnapshot(town);

        var activeTags = opportunitySnapshot.TownOpportunityTags
            .Concat(opportunitySnapshot.RegionOpportunityTags)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var tiers = _catalog.InstitutionTypes
            .ToDictionary(
                type => type.Id,
                _ => 0,
                StringComparer.OrdinalIgnoreCase);

        foreach (var rule in _catalog.InferenceRules)
        {
            if (!IsActive(year, rule.StartYear, rule.EndYear)
                || town.Population < rule.MinPopulation
                || !HasRequiredOpportunity(rule.RequiredAnyOpportunityTags, activeTags))
            {
                continue;
            }

            tiers[rule.InstitutionId] =
                Math.Max(tiers[rule.InstitutionId], rule.Tier);
        }

        foreach (var rule in _catalog.Overrides)
        {
            if (!town.Id.Equals(rule.PlaceId, StringComparison.OrdinalIgnoreCase)
                || !IsActive(year, rule.StartYear, rule.EndYear))
            {
                continue;
            }

            tiers[rule.InstitutionId] =
                rule.Mode.Equals("ExactTier", StringComparison.OrdinalIgnoreCase)
                    ? rule.Tier
                    : Math.Max(tiers[rule.InstitutionId], rule.Tier);
        }

        var institutions = _catalog.InstitutionTypes
            .Select(type =>
            {
                var tier = tiers[type.Id];
                return new TownInstitutionInfo(
                    type.Id,
                    type.DisplayName,
                    tier,
                    _catalog.ResolveTierName(type.Id, tier, year));
            })
            .ToArray();

        return new TownInstitutionSnapshot(
            town,
            year,
            institutions);
    }

    private static bool IsActive(
        int year,
        int startYear,
        int? endYear) =>
        year >= startYear
        && (endYear is null || year <= endYear.Value);

    private static bool HasRequiredOpportunity(
        IReadOnlyList<string> requirements,
        IReadOnlySet<string> activeTags) =>
        requirements.Count == 0
        || requirements.Any(activeTags.Contains);
}
