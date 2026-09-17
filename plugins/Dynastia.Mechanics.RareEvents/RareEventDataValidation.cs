using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.RareEvents;

internal static class RareEventDataValidation
{
    private const string OpportunityTagsPath = "Towns/opportunity_tags.csv";
    private const string HealthConditionsPath = "Common/health_conditions.json";

    public static void Validate(
        IGameDataService data,
        RareEventCatalog catalog,
        RareEventEpidemicCatalog epidemics,
        IReadOnlySet<string> knownCareerFamilies)
    {
        var opportunityTags = ParseOpportunityTags(data.ReadText(OpportunityTagsPath));
        foreach (var definition in catalog.Events)
        {
            foreach (var tag in definition.RequiredOpportunityTags.Concat(definition.PreferredOpportunityTags))
            {
                if (!opportunityTags.Contains(tag))
                {
                    throw CatalogValidation.Error(
                        "RareEvents/rare_events.csv",
                        "an opportunity tag defined in Towns/opportunity_tags.csv",
                        item: definition.EventId,
                        field: "RequiredOpportunityTags/PreferredOpportunityTags",
                        value: tag);
                }
            }

            foreach (var family in definition.PreferredCareerFamilies)
            {
                if (!knownCareerFamilies.Contains(family))
                {
                    throw CatalogValidation.Error(
                        "RareEvents/rare_events.csv",
                        "a known CareerFamily",
                        item: definition.EventId,
                        field: "PreferredCareerFamilies",
                        value: family);
                }
            }
        }

        var healthIds = ParseHealthConditionIds(data.ReadText(HealthConditionsPath));
        foreach (var entry in epidemics.Entries)
        {
            if (!healthIds.Contains(entry.ConditionId))
            {
                throw CatalogValidation.Error(
                    "RareEvents/rare_event_epidemic_conditions.csv",
                    "a ConditionId defined in Common/health_conditions.json",
                    item: entry.ConditionId,
                    field: "ConditionId",
                    value: entry.ConditionId);
            }
        }
    }

    private static IReadOnlySet<string> ParseOpportunityTags(string text)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        const string expectedHeader = "Tag,DisplayName,DefaultStartYear,EndYear";
        if (lines.Length < 2
            || !lines[0].TrimStart('\uFEFF').Equals(expectedHeader, StringComparison.Ordinal))
        {
            throw CatalogValidation.UnexpectedHeader(
                OpportunityTagsPath,
                lines.Length == 0 ? null : lines[0].TrimStart('\uFEFF'),
                expectedHeader);
        }

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            var row = index + 1;
            if (fields.Length != 4)
                throw CatalogValidation.FieldCount(OpportunityTagsPath, row, fields.Length, 4);

            var tag = fields[0].Trim();
            if (string.IsNullOrWhiteSpace(tag))
                throw CatalogValidation.Error(OpportunityTagsPath, "a non-empty tag ID", row, field: "Tag", value: tag);
            result.Add(tag);
        }

        return result;
    }

    private static IReadOnlySet<string> ParseHealthConditionIds(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw CatalogValidation.Error(
                    HealthConditionsPath,
                    "a JSON array of health conditions",
                    field: "Root",
                    value: document.RootElement.ValueKind);
            }

            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var index = 0;
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (!element.TryGetProperty("id", out var idProperty)
                    || idProperty.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(idProperty.GetString()))
                {
                    throw CatalogValidation.Error(
                        HealthConditionsPath,
                        "a non-empty string ID",
                        item: $"index {index}",
                        field: "id",
                        value: idProperty.ValueKind == JsonValueKind.String
                            ? idProperty.GetString()
                            : idProperty.ValueKind.ToString());
                }

                result.Add(idProperty.GetString()!);
                index++;
            }

            return result;
        }
        catch (JsonException exception)
        {
            var row = exception.LineNumber is long lineNumber && lineNumber < int.MaxValue
                ? (int)lineNumber + 1
                : (int?)null;
            throw new InvalidDataException(
                $"{HealthConditionsPath}" +
                (row is int sourceRow ? $" row {sourceRow}" : string.Empty) +
                $" field '{exception.Path ?? "$"}': invalid JSON; expected a JSON array of health conditions. {exception.Message}",
                exception);
        }
    }
}
