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

        var variants = JsonSerializer.Deserialize<
                List<HistoricalActionVariant>>(
                data.ReadText(DataPath),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                })
            ?? [];

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
            throw new InvalidDataException(
                $"{DataPath} contains no action variants.");
        }

        foreach (var variant in variants)
        {
            if (string.IsNullOrWhiteSpace(variant.ActionId)
                || string.IsNullOrWhiteSpace(variant.Label)
                || string.IsNullOrWhiteSpace(variant.Description)
                || string.IsNullOrWhiteSpace(variant.Narrative))
            {
                throw new InvalidDataException(
                    $"{DataPath} contains an incomplete action variant.");
            }

            if (variant.StartYear
                < GameCalendarConfiguration.GameStartYear)
            {
                throw new InvalidDataException(
                    $"{variant.ActionId}: startYear may not precede " +
                    $"{GameCalendarConfiguration.GameStartYear}.");
            }

            if (variant.EndYear is int endYear
                && endYear < variant.StartYear)
            {
                throw new InvalidDataException(
                    $"{variant.ActionId}: endYear precedes startYear.");
            }
        }

        foreach (var group in variants.GroupBy(
            variant => variant.ActionId,
            StringComparer.OrdinalIgnoreCase))
        {
            var ordered = group.OrderBy(
                    variant => variant.StartYear)
                .ToList();

            for (var index = 1; index < ordered.Count; index++)
            {
                var previous = ordered[index - 1];
                var current = ordered[index];

                if (previous.EndYear is null
                    || previous.EndYear.Value >= current.StartYear)
                {
                    throw new InvalidDataException(
                        $"{DataPath} contains overlapping variants for " +
                        $"'{group.Key}'.");
                }
            }
        }
    }
}
