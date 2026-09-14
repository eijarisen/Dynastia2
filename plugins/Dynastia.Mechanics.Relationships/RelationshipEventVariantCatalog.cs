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

        var variants = JsonSerializer.Deserialize<
                List<RelationshipEventVariant>>(
                data.ReadText(DataPath),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                })
            ?? [];

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
            throw new InvalidDataException(
                $"{DataPath} has no variants for '{eventId}'.");
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
            throw new InvalidDataException(
                $"{DataPath} contains no variants.");
        }

        foreach (var variant in variants)
        {
            if (string.IsNullOrWhiteSpace(variant.EventId)
                || string.IsNullOrWhiteSpace(variant.EventType)
                || string.IsNullOrWhiteSpace(variant.TextTemplate)
                || string.IsNullOrWhiteSpace(variant.BiographyTemplate))
            {
                throw new InvalidDataException(
                    $"{DataPath} contains an incomplete event variant.");
            }

            if (variant.EndYear is int endYear
                && endYear < variant.StartYear)
            {
                throw new InvalidDataException(
                    $"{variant.EventId}: endYear precedes startYear.");
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
                throw new InvalidDataException(
                    $"{group.Key}: event variants must begin in " +
                    $"{GameCalendarConfiguration.GameStartYear}.");
            }

            for (var index = 0; index < ordered.Count; index++)
            {
                var current = ordered[index];

                if (index < ordered.Count - 1)
                {
                    if (current.EndYear is not int endYear
                        || ordered[index + 1].StartYear != endYear + 1)
                    {
                        throw new InvalidDataException(
                            $"{group.Key}: event variants contain a gap or overlap.");
                    }
                }
                else if (current.EndYear is not null)
                {
                    throw new InvalidDataException(
                        $"{group.Key}: event variants must end with an open-ended row.");
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
