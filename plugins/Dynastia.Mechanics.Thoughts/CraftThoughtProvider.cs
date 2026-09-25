using Dynastia.Contracts;

namespace Dynastia.Mechanics.Thoughts;

internal sealed class CraftThoughtProvider : IThoughtProvider
{
    private readonly ICraftService _crafts;

    public CraftThoughtProvider(ICraftService crafts)
    {
        _crafts = crafts;
    }

    public string Id => "thoughts.crafts";

    public IEnumerable<ThoughtCandidate> GetCandidates(
        IPerson person,
        ThoughtContext context)
    {
        foreach (var gameEvent in context.Events.Where(gameEvent =>
            gameEvent.SubjectId == person.Id))
        {
            if (gameEvent.Type.Equals("craft.learned", StringComparison.OrdinalIgnoreCase))
            {
                yield return new ThoughtCandidate(
                                 "craft.learned",
                                 "career.craft",
                                 "career.craft",
                                 24,
                                 ThoughtMoodIds.Neutral,
                                 ResolveEmoji(gameEvent),
                                 ThoughtSalienceTraits.Career,
                                 "event",
                                 gameEvent.Type,
                                 "craft.learned",
                                 EventContext(gameEvent)
                             );
            }
            else if (gameEvent.Type.Equals(
                "craft.self_employment_started",
                StringComparison.OrdinalIgnoreCase))
            {
                yield return new ThoughtCandidate(
                                 "craft.self_employment_started",
                                 "career.craft",
                                 "career.craft",
                                 30,
                                 ThoughtMoodIds.Neutral,
                                 ResolveEmoji(gameEvent),
                                 ThoughtSalienceTraits.Career,
                                 "event",
                                 gameEvent.Type,
                                 "craft.self_employment_started",
                                 EventContext(gameEvent)
                             );
            }
            else if (gameEvent.Type.Equals("craft.income", StringComparison.OrdinalIgnoreCase)
                && gameEvent.Data.TryGetValue("performance", out var performance)
                && !performance.Equals("ordinary", StringComparison.OrdinalIgnoreCase))
            {
                yield return new ThoughtCandidate(
                                 "craft.income",
                                 "career.craft",
                                 "career.craft",
                                 18,
                                 ThoughtMoodIds.Neutral,
                                 ResolveEmoji(gameEvent),
                                 ThoughtSalienceTraits.Career,
                                 "event",
                                 gameEvent.Type,
                                 ResolvePerformanceWordingKey(
                                     gameEvent.Data.TryGetValue("performance", out var eventPerformance)
                                         ? eventPerformance
                                         : "ordinary"),
                                 EventContext(gameEvent)
                             );
            }
        }

        var active = _crafts.GetActiveCraft(person);
        if (active is not null)
        {
            yield return new ThoughtCandidate(
                             "craft.work",
                             "career.craft",
                             "career.craft",
                             10,
                             ThoughtMoodIds.Neutral,
                             active.Emoji,
                             ThoughtSalienceTraits.Career,
                             "state",
                             active.Id,
                             "craft.work.ordinary",
                             new Dictionary<string, string>
                             {
                             ["craftName"] = active.Name,
                             ["performance"] = "ordinary"
                             }
                         );
        }
    }

    private static string ResolvePerformanceWordingKey(string performance) =>
        performance.Equals("strong", StringComparison.OrdinalIgnoreCase)
            ? "craft.work.strong"
            : performance.Equals("poor", StringComparison.OrdinalIgnoreCase)
                ? "craft.work.poor"
                : "craft.work.ordinary";

    private string ResolveEmoji(GameEvent gameEvent)
    {
        if (gameEvent.Data.TryGetValue("craftId", out var craftId))
        {
            var craft = _crafts.Catalog.FirstOrDefault(candidate =>
                candidate.Id.Equals(craftId, StringComparison.OrdinalIgnoreCase));
            if (craft is not null)
                return craft.Emoji;
        }

        return "🛠️";
    }

    private static IReadOnlyDictionary<string, string> EventContext(GameEvent gameEvent)
    {
        var context = new Dictionary<string, string>(gameEvent.Data, StringComparer.OrdinalIgnoreCase);
        if (!context.ContainsKey("performance"))
            context["performance"] = "ordinary";
        return context;
    }
}
