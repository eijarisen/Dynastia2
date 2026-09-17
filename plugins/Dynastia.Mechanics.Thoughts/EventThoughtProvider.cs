using Dynastia.Contracts;

namespace Dynastia.Mechanics.Thoughts;

internal sealed class EventThoughtProvider :
    IThoughtProvider
{
    private static readonly Dictionary<
        string,
        RareEventDefinition>
        RareEvents =
            new(
                StringComparer.OrdinalIgnoreCase)
            {
                ["rare.lottery_win"] =
                    new(
                        100,
                        "🤩",
                        "rare.lottery"),

                ["rare.lightning_strike"] =
                    new(
                        98,
                        "🤕",
                        "rare.accident"),

                ["rare.assault"] =
                    new(
                        92,
                        "🤕",
                        "rare.assault"),

                ["rare.mugging"] =
                    new(
                        90,
                        "😠",
                        "rare.assault"),

                ["rare.traffic_accident"] =
                    new(
                        92,
                        "🤕",
                        "rare.accident"),

                ["rare.workplace_accident"] =
                    new(
                        92,
                        "🤕",
                        "rare.accident"),

                ["rare.structural_accident"] =
                    new(
                        92,
                        "🤕",
                        "rare.accident"),

                ["rare.serious_fall"] =
                    new(
                        88,
                        "🤕",
                        "rare.accident"),

                ["rare.water_accident"] =
                    new(
                        90,
                        "🌊",
                        "rare.accident"),

                ["rare.animal_accident"] =
                    new(
                        86,
                        "🐎",
                        "rare.accident"),

                ["rare.storm_flood_damage"] =
                    new(
                        88,
                        "😱",
                        "rare.accident"),

                ["rare.storm_flood"] =
                    new(
                        88,
                        "😱",
                        "rare.accident"),

                ["rare.burglary"] =
                    new(
                        84,
                        "😨",
                        "rare.burglary"),

                ["rare.fraud"] =
                    new(
                        86,
                        "😡",
                        "rare.fraud"),

                ["rare.distant_inheritance"] =
                    new(
                        82,
                        "🤑",
                        "rare.inheritance"),

                ["rare.found_property"] =
                    new(
                        66,
                        "😄",
                        "rare.found")
            };

    public string Id =>
        "thoughts.events";

    public IEnumerable<ThoughtCandidate> GetCandidates(
        IPerson person,
        ThoughtContext context)
    {
        foreach (var gameEvent in
            context.Events.Where(
                gameEvent =>
                    ThoughtProviderUtilities
                        .IsEventInvolving(
                            gameEvent,
                            person)))
        {
            if (gameEvent.Type.Equals(
                "rare.suicide",
                StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (gameEvent.Type.Equals(
                "rare.house_fire",
                StringComparison.OrdinalIgnoreCase))
            {
                var catastrophic =
                    gameEvent.Data.TryGetValue(
                        "severity",
                        out var severity)
                    && (
                        severity.Equals(
                            "serious",
                            StringComparison.OrdinalIgnoreCase)
                        || severity.Equals(
                            "catastrophic",
                            StringComparison.OrdinalIgnoreCase)
                    );

                yield return new ThoughtCandidate(
                    "rare.house_fire",
                    "rare.event",
                    "rare.event",
                    catastrophic
                        ? 96
                        : 78,
                    catastrophic
                        ? "😱"
                        : "😰",
                    "event",
                    gameEvent.Type,
                    "rare.fire");

                continue;
            }

            if (RareEvents.TryGetValue(
                    gameEvent.Type,
                    out var rare))
            {
                yield return new ThoughtCandidate(
                    gameEvent.Type,
                    "rare.event",
                    "rare.event",
                    rare.Salience,
                    rare.Emoji,
                    "event",
                    gameEvent.Type,
                    rare.WordingKey);
            }

            if (gameEvent.Type.StartsWith(
                    "inheritance.",
                    StringComparison.OrdinalIgnoreCase)
                && !gameEvent.Type.Equals(
                    "inheritance.unclaimed",
                    StringComparison.OrdinalIgnoreCase))
            {
                yield return new ThoughtCandidate(
                    "inheritance",
                    "inheritance",
                    "money.inheritance",
                    70,
                    "🤑",
                    "event",
                    gameEvent.Type,
                    "inheritance");
            }

            if (gameEvent.Type.Equals(
                "stats.paid_improvement",
                StringComparison.OrdinalIgnoreCase)
                && gameEvent.SubjectId
                    == person.Id)
            {
                var statId =
                    gameEvent.Data.TryGetValue(
                        "statId",
                        out var stat)
                            ? stat
                            : string.Empty;

                var previous =
                    gameEvent.Data.TryGetValue(
                        "previousValue",
                        out var previousValue)
                            ? previousValue
                            : string.Empty;

                var next =
                    gameEvent.Data.TryGetValue(
                        "newValue",
                        out var nextValue)
                            ? nextValue
                            : string.Empty;

                var restoredFertility =
                    statId.Equals(
                        "fertility",
                        StringComparison.OrdinalIgnoreCase)
                    && previous == "0"
                    && next == "1";

                var salience =
                    restoredFertility
                        ? 76
                        : statId.ToLowerInvariant()
                            switch
                            {
                                "appeal" => 50,
                                "fertility" => 62,
                                "immunity" => 48,
                                "longevity" => 48,
                                _ => 45
                            };

                var emoji =
                    statId.ToLowerInvariant()
                        switch
                        {
                            "strength" => "💪",
                            "intellect" => "🧠",
                            "immunity" => "🛡️",
                            "appeal" => "✨",
                            "longevity" => "🩺",
                            "fertility" => "🌱",
                            _ => "🙂"
                        };

                yield return new ThoughtCandidate(
                    $"improvement:{statId}",
                    "personal.improvement",
                    "personal.improvement",
                    salience,
                    emoji,
                    "event",
                    gameEvent.Type,
                    "improvement",
                    ThoughtProviderUtilities.Context(
                        (
                            "statId",
                            statId
                        ),
                        (
                            "previousValue",
                            previous
                        ),
                        (
                            "newValue",
                            next
                        )));
            }
        }

        if (person.Tags.Has(
            "recent.assault")
            && !context.Events.Any(
                gameEvent =>
                    (
                        gameEvent.Type.Equals(
                            "rare.assault",
                            StringComparison.OrdinalIgnoreCase)
                        || gameEvent.Type.Equals(
                            "rare.mugging",
                            StringComparison.OrdinalIgnoreCase)
                    )
                    && ThoughtProviderUtilities
                        .IsEventInvolving(
                            gameEvent,
                            person)))
        {
            yield return new ThoughtCandidate(
                "recent.assault",
                "rare.event",
                "rare.event",
                70,
                "😟",
                "state",
                "recent.assault",
                "recent.assault");
        }
    }

    private sealed record RareEventDefinition(
        int Salience,
        string Emoji,
        string WordingKey);
}
