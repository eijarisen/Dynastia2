using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

public sealed class RelationshipEventVariantCatalog
{
    private const string DataPath =
        "Relationships/relationship_event_variants.json";

    private readonly IReadOnlyDictionary<
        string,
        IReadOnlyList<RelationshipEventVariant>> _variants;

    private RelationshipEventVariantCatalog(
        IReadOnlyDictionary<
            string,
            IReadOnlyList<RelationshipEventVariant>> variants)
    {
        _variants = variants;
    }

    public static RelationshipEventVariantCatalog Load(
        IGameDataService data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var variants = CatalogValidation.DeserializeJson<
            List<RelationshipEventVariant>>(
            data,
            DataPath,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        Validate(variants);

        return new RelationshipEventVariantCatalog(
            variants
                .GroupBy(
                    variant => variant.EventId,
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<RelationshipEventVariant>)
                        group.OrderBy(variant => variant.StartYear).ToList(),
                    StringComparer.OrdinalIgnoreCase));
    }

    public RelationshipEventVariant GetVariant(
        string eventId,
        int year)
    {
        if (!_variants.TryGetValue(eventId, out var variants))
        {
            throw CatalogValidation.Error(
                DataPath,
                "an event ID with configured variants",
                item: eventId,
                field: "eventId",
                value: eventId);
        }

        var effectiveYear = Math.Max(
            year,
            GameCalendarConfiguration.GameStartYear);

        return variants.First(variant => variant.Covers(effectiveYear));
    }

    private static void Validate(
        IReadOnlyList<RelationshipEventVariant> variants)
    {
        if (variants.Count == 0)
        {
            throw CatalogValidation.Error(
                DataPath,
                "at least one relationship event variant",
                field: "Items",
                value: 0);
        }

        for (var index = 0; index < variants.Count; index++)
        {
            var variant = variants[index];
            var item = string.IsNullOrWhiteSpace(variant.EventId)
                ? $"index {index}"
                : variant.EventId;

            if (string.IsNullOrWhiteSpace(variant.EventId))
                throw CatalogValidation.Error(DataPath, "a non-empty event ID", item: item, field: "eventId", value: variant.EventId);
            if (string.IsNullOrWhiteSpace(variant.EventType))
                throw CatalogValidation.Error(DataPath, "a non-empty event type", item: item, field: "eventType", value: variant.EventType);
            if (string.IsNullOrWhiteSpace(variant.TextTemplate))
                throw CatalogValidation.Error(DataPath, "a non-empty text template", item: item, field: "textTemplate", value: variant.TextTemplate);
            if (string.IsNullOrWhiteSpace(variant.BiographyTemplate))
                throw CatalogValidation.Error(DataPath, "a non-empty biography template", item: item, field: "biographyTemplate", value: variant.BiographyTemplate);

            if (variant.EndYear is int endYear
                && endYear < variant.StartYear)
            {
                throw CatalogValidation.Error(
                    DataPath,
                    $"a year at or after startYear ({variant.StartYear})",
                    item: item,
                    field: "endYear",
                    value: endYear);
            }
        }

        foreach (var group in variants.GroupBy(
            variant => variant.EventId,
            StringComparer.OrdinalIgnoreCase))
        {
            var ordered = group.OrderBy(variant => variant.StartYear).ToList();

            if (ordered[0].StartYear
                != GameCalendarConfiguration.GameStartYear)
            {
                throw CatalogValidation.Error(
                    DataPath,
                    $"{GameCalendarConfiguration.GameStartYear} for the first variant",
                    item: group.Key,
                    field: "startYear",
                    value: ordered[0].StartYear);
            }

            for (var index = 0; index < ordered.Count; index++)
            {
                var current = ordered[index];

                if (index < ordered.Count - 1)
                {
                    var next = ordered[index + 1];
                    if (current.EndYear is not int endYear
                        || next.StartYear != endYear + 1)
                    {
                        throw CatalogValidation.Error(
                            DataPath,
                            current.EndYear is int closedEnd
                                ? $"{closedEnd + 1} so event-variant coverage is contiguous"
                                : "an endYear on every non-final variant",
                            item: group.Key,
                            field: "startYear",
                            value: next.StartYear);
                    }
                }
                else if (current.EndYear is not null)
                {
                    throw CatalogValidation.Error(
                        DataPath,
                        "an empty endYear for the final open-ended variant",
                        item: group.Key,
                        field: "endYear",
                        value: current.EndYear.Value);
                }
            }
        }
    }
}

public sealed record RelationshipEventVariant(
    string EventId,
    int StartYear,
    int? EndYear,
    string EventType,
    string TextTemplate,
    string BiographyTemplate)
{
    public bool Covers(int year) =>
        year >= StartYear
        && (EndYear is null || year <= EndYear.Value);

    public string FormatText(
        string person,
        string spouse) =>
        TextTemplate
            .Replace("{person}", person, StringComparison.Ordinal)
            .Replace("{spouse}", spouse, StringComparison.Ordinal);

    public string FormatBiographyVerb(string spouse) =>
        BiographyTemplate.Replace(
            "{spouse}",
            spouse,
            StringComparison.Ordinal);
}
