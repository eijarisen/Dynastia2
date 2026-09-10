using Dynastia.Contracts;

namespace Dynastia.Mechanics.Stats;

public sealed class StandardStatsService : IStatsService
{
    private readonly IGameRandom _random;

    private static readonly (string Id, string Name, string Description)[] Definitions =
    [
        ("immunity", "Immunity",
            "Resistance to illness. Higher values reduce the chance of sickness."),
        ("longevity", "Longevity",
            "Influences natural lifespan and annual health recovery."),
        ("fertility", "Fertility",
            "Influences the chance of having children."),
        ("appeal", "Appeal",
            "Influences success when finding a spouse."),
        ("strength", "Strength",
            "Influences success in physical work and basic employment."),
        ("intellect", "Intellect",
            "Influences education and career progression.")
    ];

    public StandardStatsService(IGameRandom random)
    {
        _random = random;
    }

    public void EnsureStats(IPerson person)
    {
        if (person.Components.Has<StatsComponent>())
            return;

        var stats = new StatsComponent();

        foreach (var definition in Definitions)
        {
            stats.Values[definition.Id] = _random.NextInt(1, 5);
        }

        person.Components.Set(stats);
    }

    public IReadOnlyList<StatValue> GetStats(IPerson person)
    {
        EnsureStats(person);

        var stats = person.Components.Get<StatsComponent>()
            ?? throw new InvalidOperationException(
                "Stats component could not be created.");

        return Definitions
            .Select(definition => new StatValue(
                definition.Id,
                definition.Name,
                stats.Values[definition.Id],
                definition.Description))
            .ToList();
    }
}
