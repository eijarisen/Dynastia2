using Dynastia.Contracts;

namespace Dynastia.Mechanics.Thoughts;

internal sealed class RelationshipThoughtProvider :
    IThoughtProvider
{
    private readonly IFarmingService _farming;

    public RelationshipThoughtProvider(
        IFarmingService farming)
    {
        _farming = farming;
    }

    public string Id =>
        "thoughts.relationships";

    public IEnumerable<ThoughtCandidate> GetCandidates(
        IPerson person,
        ThoughtContext context)
    {
        if (person.Age < 18)
            yield break;

        foreach (var gameEvent in
            context.Events.Where(
                gameEvent =>
                    ThoughtProviderUtilities
                        .IsEventInvolving(
                            gameEvent,
                            person)))
        {
            if (gameEvent.Type.Equals(
                    "relationship.married",
                    StringComparison.OrdinalIgnoreCase)
                || gameEvent.Type.Equals(
                    "relationship.remarried",
                    StringComparison.OrdinalIgnoreCase)
                || gameEvent.Type.Equals(
                    "relationship.partnered",
                    StringComparison.OrdinalIgnoreCase))
            {
                yield return new ThoughtCandidate(
                    "relationship.marriage.current",
                    "relationship.marriage",
                    "relationship.marriage",
                    88,
                    "🥰",
                    "event",
                    gameEvent.Type,
                    "marriage.new");
            }

            if (gameEvent.Type.Equals(
                    "relationship.divorce",
                    StringComparison.OrdinalIgnoreCase)
                || gameEvent.Type.Equals(
                    "relationship.low_satisfaction_divorce",
                    StringComparison.OrdinalIgnoreCase)
                || gameEvent.Type.Equals(
                    "relationship.prison_divorce",
                    StringComparison.OrdinalIgnoreCase))
            {
                yield return new ThoughtCandidate(
                    "relationship.divorce.current",
                    "relationship.divorce",
                    "relationship.divorce",
                    94,
                    "💔",
                    "event",
                    gameEvent.Type,
                    "divorce.current");
            }

            if (gameEvent.Type.Equals(
                "relationship.affair",
                StringComparison.OrdinalIgnoreCase))
            {
                var actor =
                    gameEvent.SubjectId
                    == person.Id;

                yield return new ThoughtCandidate(
                    "relationship.affair",
                    "relationship.satisfaction",
                    "relationship.satisfaction",
                    96,
                    "💔",
                    "event",
                    gameEvent.Type,
                    actor
                        ? "affair.actor"
                        : "affair.victim");
            }

            if (gameEvent.Type.Equals(
                "relationship.repair_marriage",
                StringComparison.OrdinalIgnoreCase))
            {
                yield return new ThoughtCandidate(
                    "relationship.repaired",
                    "relationship.satisfaction",
                    "relationship.satisfaction",
                    70,
                    "❤️‍🩹",
                    "event",
                    gameEvent.Type,
                    "marriage.repaired");
            }
        }

        if (person.Tags.Has(
                "recent.divorce")
            && !context.Events.Any(
                gameEvent =>
                    (
                        gameEvent.Type.Equals(
                            "relationship.divorce",
                            StringComparison.OrdinalIgnoreCase)
                        || gameEvent.Type.Equals(
                            "relationship.low_satisfaction_divorce",
                            StringComparison.OrdinalIgnoreCase)
                        || gameEvent.Type.Equals(
                            "relationship.prison_divorce",
                            StringComparison.OrdinalIgnoreCase)
                    )
                    && ThoughtProviderUtilities
                        .IsEventInvolving(
                            gameEvent,
                            person)))
        {
            yield return new ThoughtCandidate(
                "relationship.divorce.recent",
                "relationship.divorce",
                "relationship.divorce",
                74,
                "💔",
                "state",
                "recent.divorce",
                "divorce.recent");
        }

        var hasNewRelationshipEvent =
            context.Events.Any(
                gameEvent =>
                    ThoughtProviderUtilities
                        .IsEventInvolving(
                            gameEvent,
                            person)
                    && (
                        gameEvent.Type.Equals(
                            "relationship.married",
                            StringComparison.OrdinalIgnoreCase)
                        || gameEvent.Type.Equals(
                            "relationship.remarried",
                            StringComparison.OrdinalIgnoreCase)
                        || gameEvent.Type.Equals(
                            "relationship.partnered",
                            StringComparison.OrdinalIgnoreCase)
                    ));

        if (hasNewRelationshipEvent)
            yield break;

        var satisfaction =
            context.MarriageSatisfaction
                .GetSatisfaction(
                    person);

        if (satisfaction is null)
            yield break;

        var salience =
            satisfaction.Value switch
            {
                < 20 => 76,
                < 40 => 60,
                < 60 => 0,
                < 80 => 26,
                _ => 44
            };

        if (salience <= 0)
            yield break;

        var emoji =
            satisfaction.Value switch
            {
                < 20 => "💔",
                < 40 => "😔",
                < 80 => "🙂",
                _ => "🥰"
            };

        var currentIssues =
            satisfaction.CurrentIssues
                .Where(issue => IsIssueStillCurrent(issue, person, context))
                .ToList();

        var issue =
            satisfaction.Label.Equals(
                "Thriving",
                StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : SelectIssue(
                    person,
                    currentIssues,
                    context.Year,
                    context.GameState.DynastySurname);

        yield return new ThoughtCandidate(
            "relationship.satisfaction",
            "relationship.satisfaction",
            "relationship.satisfaction",
            salience,
            emoji,
            "state",
            "marriage.satisfaction",
            "marriage.satisfaction",
            ThoughtProviderUtilities.Context(
                (
                    "issue",
                    issue
                ),
                (
                    "satisfactionLabel",
                    satisfaction.Label
                )));
    }


    private bool IsIssueStillCurrent(
        string issue,
        IPerson person,
        ThoughtContext context)
    {
        if (!issue.Contains(
                "unemployed",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var spouse = context.Family.GetSpouse(person);
        if (spouse is null)
            return true;

        var husband = context.Family.GetSex(person) == Sex.Male
            ? person
            : spouse;
        var wife = husband.Id == person.Id
            ? spouse
            : person;

        if (issue.Contains(
                "husband",
                StringComparison.OrdinalIgnoreCase))
        {
            return !IsEconomicallyEmployed(husband, context);
        }

        if (issue.Contains(
                "wife",
                StringComparison.OrdinalIgnoreCase))
        {
            return !IsEconomicallyEmployed(wife, context);
        }

        return true;
    }

    private bool IsEconomicallyEmployed(
        IPerson person,
        ThoughtContext context) =>
        context.Career.GetCareer(person).IsEmployed
        || _farming.IsWorkingFarmWorker(person, person);

    private static string SelectIssue(
        IPerson person,
        IReadOnlyList<string> issues,
        int year,
        string dynastyKey)
    {
        if (issues.Count == 0)
            return string.Empty;

        int Severity(
            string issue)
        {
            if (issue.Equals(
                "being broke",
                StringComparison.OrdinalIgnoreCase))
            {
                return 4;
            }

            if (issue.Equals(
                "household strain",
                StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }

            if (issue.Contains(
                    "fertility",
                    StringComparison.OrdinalIgnoreCase)
                || issue.Contains(
                    "unemployed",
                    StringComparison.OrdinalIgnoreCase)
                || issue.Contains(
                    "intellect",
                    StringComparison.OrdinalIgnoreCase)
                || issue.Contains(
                    "appeal",
                    StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            return 1;
        }

        var max =
            issues.Max(
                Severity);

        var strongest =
            issues
                .Where(
                    issue =>
                        Severity(issue)
                        == max)
                .OrderBy(
                    issue =>
                        issue,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        return DeterministicThoughtRandom.Choose(
            strongest,
            dynastyKey,
            person.Id.ToString(),
            year.ToString(),
            "marriage-issue");
    }
}
