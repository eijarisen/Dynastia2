using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Farming;

internal sealed record FarmingWorkerAgeBand(
    int MinAge,
    int? MaxAge,
    decimal Multiplier);

internal sealed class FarmingWorkerContributionCatalog
{
    private const string Path = "Farming/worker_age_contribution.json";

    public IReadOnlyList<FarmingWorkerAgeBand> AgeBands { get; }

    private FarmingWorkerContributionCatalog(
        IReadOnlyList<FarmingWorkerAgeBand> ageBands) =>
        AgeBands = ageBands;

    public decimal GetAgeContribution(int age)
    {
        if (age < 10)
            return 0m;

        var band = AgeBands.FirstOrDefault(item =>
            age >= item.MinAge
            && (item.MaxAge is null || age <= item.MaxAge.Value));

        return band?.Multiplier ?? 0m;
    }

    public static FarmingWorkerContributionCatalog Load(IGameDataService data)
    {
        ArgumentNullException.ThrowIfNull(data);

        using var document = JsonDocument.Parse(data.ReadText(Path));
        var root = document.RootElement;
        var bands = root.GetProperty("ageBands")
            .EnumerateArray()
            .Select(item => new FarmingWorkerAgeBand(
                item.GetProperty("minAge").GetInt32(),
                item.TryGetProperty("maxAge", out var maxAge)
                    && maxAge.ValueKind != JsonValueKind.Null
                        ? maxAge.GetInt32()
                        : null,
                item.GetProperty("multiplier").GetDecimal()))
            .OrderBy(item => item.MinAge)
            .ToArray();

        Validate(bands);
        return new FarmingWorkerContributionCatalog(bands);
    }

    internal static void Validate(IReadOnlyList<FarmingWorkerAgeBand> bands)
    {
        if (bands.Count == 0)
            throw new InvalidDataException("Farming worker age contribution must define age bands.");

        if (bands[0].MinAge != 10)
            throw new InvalidDataException("Farming worker age contribution must start at age 10.");

        for (var index = 0; index < bands.Count; index++)
        {
            var band = bands[index];
            if (band.MinAge < 0
                || band.MaxAge is int maximum && maximum < band.MinAge
                || band.Multiplier < 0m)
            {
                throw new InvalidDataException("Farming worker age contribution contains an invalid age band.");
            }

            if (index < bands.Count - 1)
            {
                if (band.MaxAge is null)
                    throw new InvalidDataException("Only the final farming worker age band may be open-ended.");

                var next = bands[index + 1];
                if (next.MinAge != band.MaxAge.Value + 1)
                    throw new InvalidDataException("Farming worker age contribution bands must cover every age from 10 upward without gaps or overlap.");
            }
        }

        if (bands[^1].MaxAge is not null || bands[^1].MinAge > 18)
            throw new InvalidDataException("The final farming worker age band must cover all adults.");
    }
}
