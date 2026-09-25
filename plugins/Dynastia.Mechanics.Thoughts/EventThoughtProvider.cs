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
                        ThoughtMoodIds.Happy,
                        "💰",
                        ThoughtSalienceTraits.Positive,
                        "rare.lottery"),

                ["rare.lightning_strike"] =
                    new(
                        98,
                        ThoughtMoodIds.Sick,
                        "⚡",
                        ThoughtSalienceTraits.RareTrauma,
                        "rare.accident"),

                ["rare.assault"] =
                    new(
                        92,
                        ThoughtMoodIds.Distressed,
                        "⚠️",
                        ThoughtSalienceTraits.Emotional
                        | ThoughtSalienceTraits.Negative
                        | ThoughtSalienceTraits.ImmediateProblem
                        | ThoughtSalienceTraits.RareTrauma
                        | ThoughtSalienceTraits.MelancholicHighImpact,
                        "rare.assault"),

                ["rare.mugging"] =
                    new(
                        90,
                        ThoughtMoodIds.Angry,
                        "💰",
                        ThoughtSalienceTraits.Emotional
                        | ThoughtSalienceTraits.Negative
                        | ThoughtSalienceTraits.ImmediateProblem
                        | ThoughtSalienceTraits.RareTrauma
                        | ThoughtSalienceTraits.MelancholicHighImpact,
                        "rare.assault"),

                ["rare.traffic_accident"] =
                    Accident(92, "🚗"),

                ["rare.workplace_accident"] =
                    Accident(92, "⚠️"),

                ["rare.structural_accident"] =
                    Accident(92, "🏠"),

                ["rare.serious_fall"] =
                    Accident(88, "⚠️"),

                ["rare.water_accident"] =
                    Accident(90, "🌊", ThoughtMoodIds.Afraid),

                ["rare.animal_accident"] =
                    Accident(86, "🐎", ThoughtMoodIds.Afraid),

                ["rare.storm_flood_damage"] =
                    Accident(88, "🌊", ThoughtMoodIds.Afraid),

                ["rare.storm_flood"] =
                    Accident(88, "🌊", ThoughtMoodIds.Afraid),

                ["rare.burglary"] =
                    new(
                        84,
                        ThoughtMoodIds.Afraid,
                        "🏠",
                        ThoughtSalienceTraits.None,
                        "rare.burglary"),

                ["rare.fraud"] =
                    new(
                        86,
                        ThoughtMoodIds.Angry,
                        "💰",
                        ThoughtSalienceTraits.None,
                        "rare.fraud"),

                ["rare.distant_inheritance"] =
                    new(
                        82,
                        ThoughtMoodIds.Pleased,
                        "💰",
                        ThoughtSalienceTraits.Positive,
                        "rare.inheritance"),

                ["rare.found_property"] =
                    new(
                        66,
                        ThoughtMoodIds.Happy,
                        "💰",
                        ThoughtSalienceTraits.None,
                        "rare.found")
            };

    private static RareEventDefinition Accident(
        int salience,
        string topicEmoji,
        string moodId = ThoughtMoodIds.Sick) =>
        new(
            salience,
            moodId,
            topicEmoji,
            ThoughtSalienceTraits.RareTrauma,
            "rare.accident");

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
                                 ThoughtMoodIds.Afraid,
                                 "🔥",
                                 ThoughtSalienceTraits.None,
                                 "event",
                                 gameEvent.Type,
                                 "rare.fire"
                             );

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
                                 rare.MoodId,
                                 rare.TopicEmoji,
                                 rare.SalienceTraits,
                                 "event",
                                 gameEvent.Type,
                                 rare.WordingKey
                             );
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
                                 ThoughtMoodIds.Pleased,
                                 "💰",
                                 ThoughtSalienceTraits.Positive,
                                 "event",
                                 gameEvent.Type,
                                 "inheritance"
                             );
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
                                 ThoughtMoodIds.Pleased,
                                 emoji,
                                 ThoughtSalienceTraits.None,
                                 "event",
                                 gameEvent.Type,
                                 ResolveImprovementWordingKey(
                                     statId,
                                     restoredFertility),
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
                                 ))
                             );
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
                             ThoughtMoodIds.Concerned,
                             "⚠️",
                             ThoughtSalienceTraits.Emotional
                             | ThoughtSalienceTraits.Negative
                             | ThoughtSalienceTraits.ImmediateProblem
                             | ThoughtSalienceTraits.MelancholicHighImpact,
                             "state",
                             "recent.assault",
                             "recent.assault"
                         );
        }
    }

    private static string ResolveImprovementWordingKey(
        string statId,
        bool restoredFertility)
    {
        if (restoredFertility)
            return "improvement.fertility_restored";

        return statId.ToLowerInvariant() switch
        {
            "strength" => "improvement.strength",
            "intellect" => "improvement.intellect",
            "immunity" => "improvement.immunity",
            "appeal" => "improvement.appeal",
            "longevity" => "improvement.longevity",
            "fertility" => "improvement.fertility",
            _ => "improvement.generic"
        };
    }

    private sealed record RareEventDefinition(
        int Salience,
        string MoodId,
        string TopicEmoji,
        ThoughtSalienceTraits SalienceTraits,
        string WordingKey);
}
