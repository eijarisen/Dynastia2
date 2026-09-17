using Dynastia.Contracts;

namespace Dynastia.Mechanics.Stats;

public sealed class StandardStatsService :
    IStatsService
{
    private const int MaximumEffectiveStat =
        5;

    private readonly IGameRandom _random;

    private static readonly
        (string Id, string Name, string Description)[] Definitions =
    [
        (
            "immunity",
            "Immunity",
            "Resistance to illness. Higher values reduce the chance of sickness."
        ),
        (
            "longevity",
            "Longevity",
            "Influences natural lifespan and annual health recovery."
        ),
        (
            "fertility",
            "Fertility",
            "Influences the chance of having children."
        ),
        (
            "appeal",
            "Appeal",
            "Influences success when finding a spouse."
        ),
        (
            "strength",
            "Strength",
            "Influences success in physical work and basic employment."
        ),
        (
            "intellect",
            "Intellect",
            "Influences education and career progression."
        )
    ];

    public StandardStatsService(
        IGameRandom random)
    {
        _random =
            random;
    }

    public void EnsureStats(
        IPerson person)
    {
        ArgumentNullException.ThrowIfNull(
            person);

        if (person.Components.Has<
            StatsComponent>())
        {
            EnsureComponentCompleteness(
                person.Components.Get<
                    StatsComponent>()
                ?? throw new InvalidOperationException(
                    "Stats component is unavailable."));

            return;
        }

        var stats =
            new StatsComponent();

        foreach (var definition in
            Definitions)
        {
            stats.Values[
                definition.Id] =
                    _random.NextInt(
                        1,
                        5);
        }

        person.Components.Set(
            stats);
    }

    public void SetStats(
        IPerson person,
        IReadOnlyDictionary<string, int> values)
    {
        ArgumentNullException.ThrowIfNull(
            person);

        ArgumentNullException.ThrowIfNull(
            values);

        var stats =
            new StatsComponent();

        foreach (var definition in
            Definitions)
        {
            if (!values.TryGetValue(
                definition.Id,
                out var value))
            {
                throw new InvalidOperationException(
                    $"Missing stat '{definition.Id}'.");
            }

            ValidateBaseStatValue(
                definition.Id,
                value);

            stats.Values[
                definition.Id] =
                    value;
        }

        // SetStats is used to establish a newly inherited base profile.
        // A fresh hereditary profile never carries acquired bonuses.
        person.Components.Set(
            stats);
    }

    public IReadOnlyList<StatValue> GetStats(
        IPerson person)
    {
        var stats =
            GetRequired(
                person);

        return Definitions
            .Select(
                definition =>
                {
                    var baseValue =
                        stats.Values[
                            definition.Id];

                    var acquired =
                        stats.AcquiredImprovements
                            .TryGetValue(
                                definition.Id,
                                out var bonus)
                            ? Math.Max(
                                0,
                                bonus)
                            : 0;

                    var effective =
                        Math.Min(
                            MaximumEffectiveStat,
                            baseValue
                            + acquired);

                    return new StatValue(
                        definition.Id,
                        definition.Name,
                        effective,
                        definition.Description);
                })
            .ToList();
    }

    public IReadOnlyList<StatValue> GetBaseStats(
        IPerson person)
    {
        var stats =
            GetRequired(
                person);

        return Definitions
            .Select(
                definition =>
                    new StatValue(
                        definition.Id,
                        definition.Name,
                        stats.Values[
                            definition.Id],
                        definition.Description))
            .ToList();
    }

    public bool TryIncreaseAcquiredStat(
        IPerson person,
        string statId)
    {
        ArgumentNullException.ThrowIfNull(
            person);

        if (string.IsNullOrWhiteSpace(
            statId))
        {
            throw new ArgumentException(
                "Stat ID is required.",
                nameof(statId));
        }

        var definition =
            Definitions.FirstOrDefault(
                candidate =>
                    candidate.Id.Equals(
                        statId,
                        StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(
            definition.Id))
        {
            throw new ArgumentOutOfRangeException(
                nameof(statId),
                $"Unknown stat '{statId}'.");
        }

        var stats =
            GetRequired(
                person);

        var baseValue =
            stats.Values[
                definition.Id];

        var acquired =
            stats.AcquiredImprovements
                .TryGetValue(
                    definition.Id,
                    out var currentBonus)
                ? Math.Max(
                    0,
                    currentBonus)
                : 0;

        if (baseValue + acquired
            >= MaximumEffectiveStat)
        {
            return false;
        }

        stats.AcquiredImprovements[
            definition.Id] =
                acquired + 1;

        return true;
    }

    private static StatsComponent GetRequired(
        IPerson person)
    {
        return person.Components.Get<
            StatsComponent>()
            ?? throw new InvalidOperationException(
                "Stats state is missing. Run state reconciliation before reading stats.");
    }

    private void EnsureComponentCompleteness(
        StatsComponent stats)
    {
        foreach (var definition in
            Definitions)
        {
            if (!stats.Values.TryGetValue(
                definition.Id,
                out var value))
            {
                // Defensive migration for malformed very old saves.
                stats.Values[
                    definition.Id] =
                        _random.NextInt(
                            1,
                            5);

                continue;
            }

            ValidateBaseStatValue(
                definition.Id,
                value);

            if (stats.AcquiredImprovements
                .TryGetValue(
                    definition.Id,
                    out var acquired)
                && acquired < 0)
            {
                stats.AcquiredImprovements[
                    definition.Id] =
                        0;
            }
        }
    }

    private static void ValidateBaseStatValue(
        string statId,
        int value)
    {
        if (statId.Equals(
            "fertility",
            StringComparison.OrdinalIgnoreCase))
        {
            if (value is < 0 or > 5)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "Fertility must be between 0 and 5.");
            }

            return;
        }

        if (value is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                $"Stat '{statId}' must be between 1 and 5.");
        }
    }
}
