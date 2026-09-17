using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.RareEvents;

internal static class RareEventDataValidation
{
    public static void Validate(
        IGameDataService data,
        RareEventCatalog catalog,
        RareEventEpidemicCatalog epidemics,
        IReadOnlySet<string> knownCareerFamilies)
    {
        var opportunityTags = ParseOpportunityTags(data.ReadText("Towns/opportunity_tags.csv"));
        foreach (var definition in catalog.Events)
        {
            foreach (var tag in definition.RequiredOpportunityTags.Concat(definition.PreferredOpportunityTags))
                if (!opportunityTags.Contains(tag))
                    throw new InvalidDataException($"{definition.EventId}: unknown opportunity tag '{tag}'.");
            foreach (var family in definition.PreferredCareerFamilies)
                if (!knownCareerFamilies.Contains(family))
                    throw new InvalidDataException($"{definition.EventId}: unknown CareerFamily '{family}'.");
        }

        using var document = JsonDocument.Parse(data.ReadText("Common/health_conditions.json"));
        var healthIds = document.RootElement.EnumerateArray()
            .Select(element => element.GetProperty("id").GetString())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in epidemics.Entries)
            if (!healthIds.Contains(entry.ConditionId))
                throw new InvalidDataException($"Rare epidemic condition '{entry.ConditionId}' is not present in Health data.");
    }

    private static IReadOnlySet<string> ParseOpportunityTags(string text)
    {
        var lines = text.Split(['\r','\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2 || !lines[0].TrimStart('\uFEFF').StartsWith("Tag,", StringComparison.Ordinal))
            throw new InvalidDataException("Towns/opportunity_tags.csv is empty or invalid.");
        return lines.Skip(1)
            .Select(line => line.Split(',')[0].Trim())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
