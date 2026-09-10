using Dynastia.Contracts;

namespace Dynastia.Mechanics.Stats;

public sealed class StandardStatsService : IStatsService
{
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
        _random = random;
    }

    public void EnsureStats(
        IPerson person)
    {
        if (person.Components.Has<StatsComponent>())
            return;

        var stats =
            new StatsComponent();

        foreach (var definition in Definitions)
        {
            stats.Values[definition.Id] =
                _random.NextInt(1, 5);
        }

        person.Components.Set(stats);
    }

    public void SetStats(
        IPerson person,
        IReadOnlyDictionary<string, int> values)
    {
        ArgumentNullException.ThrowIfNull(person);
        ArgumentNullException.ThrowIfNull(values);

        var stats =
            new StatsComponent();

        foreach (var definition in Definitions)
        {
            if (!values.TryGetValue(
                definition.Id,
                out var value))
            {
                throw new InvalidOperationException(
                    $"Missing stat '{definition.Id}'.");
            }

            ValidateStatValue(
                definition.Id,
                value);

            stats.Values[definition.Id] =
                value;
        }

        person.Components.Set(stats);
    }

    public IReadOnlyList<StatValue> GetStats(
        IPerson person)
    {
        EnsureStats(person);

        var stats =
            person.Components.Get<StatsComponent>()
            ?? throw new InvalidOperationException(
                "Stats component could not be created.");

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

    private static void ValidateStatValue(
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
