using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Historical;

public sealed class HistoricalActionVariantService :
    IHistoricalActionVariantService
{
    private const string DataPath =
        "Common/historical_action_variants.json";

    private readonly IReadOnlyDictionary<
        string,
        IReadOnlyList<HistoricalActionVariant>> _variants;

    private HistoricalActionVariantService(
        IReadOnlyDictionary<
            string,
            IReadOnlyList<HistoricalActionVariant>> variants)
    {
        _variants = variants;
    }

    public static HistoricalActionVariantService Load(
        IGameDataService data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var variants = CatalogValidation.DeserializeJson<
            List<HistoricalActionVariant>>(
            data,
            DataPath,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        Validate(variants);

        var grouped = variants
            .GroupBy(
                variant => variant.ActionId,
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<HistoricalActionVariant>)
                    group.OrderBy(variant => variant.StartYear)
                        .ToList(),
                StringComparer.OrdinalIgnoreCase);

        return new HistoricalActionVariantService(grouped);
    }

    public HistoricalActionVariant? GetVariant(
        string actionId,
        int year)
    {
        if (!_variants.TryGetValue(actionId, out var variants))
            return null;

        return variants.LastOrDefault(
            variant => variant.IsAvailable(year));
    }

    public HistoricalActionVariant? GetCanonicalVariant(
        string actionId)
    {
        if (!_variants.TryGetValue(actionId, out var variants)
            || variants.Count == 0)
        {
            return null;
        }

        return variants[0];
    }

    private static void Validate(
        IReadOnlyList<HistoricalActionVariant> variants)
    {
        if (variants.Count == 0)
        {
            throw CatalogValidation.Error(
                DataPath,
                "at least one action variant",
                field: "Items",
                value: 0);
        }

        for (var index = 0; index < variants.Count; index++)
        {
            var variant = variants[index];
            var item = string.IsNullOrWhiteSpace(variant.ActionId)
                ? $"index {index}"
                : variant.ActionId;

            if (string.IsNullOrWhiteSpace(variant.ActionId))
                throw CatalogValidation.Error(DataPath, "a non-empty action ID", item: item, field: "actionId", value: variant.ActionId);
            if (string.IsNullOrWhiteSpace(variant.Label))
                throw CatalogValidation.Error(DataPath, "a non-empty label", item: item, field: "label", value: variant.Label);
            if (string.IsNullOrWhiteSpace(variant.Description))
                throw CatalogValidation.Error(DataPath, "a non-empty description", item: item, field: "description", value: variant.Description);
            if (string.IsNullOrWhiteSpace(variant.Narrative))
                throw CatalogValidation.Error(DataPath, "a non-empty narrative", item: item, field: "narrative", value: variant.Narrative);

            if (variant.StartYear < GameCalendarConfiguration.GameStartYear)
            {
                throw CatalogValidation.Error(
                    DataPath,
                    $"a year at or after {GameCalendarConfiguration.GameStartYear}",
                    item: item,
                    field: "startYear",
                    value: variant.StartYear);
            }

            if (variant.EndYear is int endYear && endYear < variant.StartYear)
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
            variant => variant.ActionId,
            StringComparer.OrdinalIgnoreCase))
        {
            var ordered = group.OrderBy(variant => variant.StartYear).ToList();
            for (var index = 1; index < ordered.Count; index++)
            {
                var previous = ordered[index - 1];
                var current = ordered[index];
                if (previous.EndYear is null
                    || previous.EndYear.Value >= current.StartYear)
                {
                    throw CatalogValidation.Error(
                        DataPath,
                        $"a non-overlapping year range after the variant beginning {previous.StartYear}",
                        item: group.Key,
                        field: "startYear",
                        value: current.StartYear);
                }
            }
        }
    }
}
