using Dynastia.Contracts;

namespace Dynastia.Mechanics.TownLife;

internal sealed class StandardLocalEconomicStrengthService : ILocalEconomicStrengthService
{
    private readonly ILocalCareerOpportunityService _opportunities;
    private readonly TownEconomicStrengthCatalog _catalog;

    public StandardLocalEconomicStrengthService(
        ILocalCareerOpportunityService opportunities,
        TownEconomicStrengthCatalog catalog)
    {
        _opportunities = opportunities;
        _catalog = catalog;
    }

    public LocalEconomicStrength ResolveCareer(
        TownInfo town,
        string? careerFamily,
        IReadOnlyCollection<string>? requiredOpportunityTags = null)
    {
        var tags = requiredOpportunityTags is { Count: > 0 }
            ? requiredOpportunityTags
            : !string.IsNullOrWhiteSpace(careerFamily)
                && _catalog.CareerFamilyTags.TryGetValue(careerFamily, out var mapped)
                    ? mapped
                    : Array.Empty<string>();

        return Resolve(town, tags, normalWhenUnmapped: true);
    }

    public LocalEconomicStrength ResolveCraft(
        TownInfo town,
        CraftInfo craft)
    {
        ArgumentNullException.ThrowIfNull(craft);
        var tags = craft.RequiredOpportunityTags.Count > 0
            ? craft.RequiredOpportunityTags
            : craft.PreferredOpportunityTags;
        return Resolve(town, tags, normalWhenUnmapped: true);
    }

    public LocalEconomicStrength ResolveFarming(TownInfo town) =>
        Resolve(town, ["agriculture"], normalWhenUnmapped: false);

    private LocalEconomicStrength Resolve(
        TownInfo town,
        IReadOnlyCollection<string> mappedTags,
        bool normalWhenUnmapped)
    {
        ArgumentNullException.ThrowIfNull(town);
        if (mappedTags.Count == 0)
            return normalWhenUnmapped
                ? LocalEconomicStrength.Normal
                : LocalEconomicStrength.Weak;

        var snapshot = _opportunities.GetOpportunitySnapshot(town);
        var townTags = snapshot.TownOpportunityTags.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (mappedTags.Any(townTags.Contains))
            return LocalEconomicStrength.Strong;

        var regionTags = snapshot.RegionOpportunityTags.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (mappedTags.Any(regionTags.Contains))
            return LocalEconomicStrength.Supported;

        return LocalEconomicStrength.Weak;
    }
}
