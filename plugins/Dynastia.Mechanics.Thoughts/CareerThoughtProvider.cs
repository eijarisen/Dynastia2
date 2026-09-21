using Dynastia.Contracts;

namespace Dynastia.Mechanics.Thoughts;

internal sealed class CareerThoughtProvider :
    IThoughtProvider
{
    private readonly IFarmingService _farming;

    public CareerThoughtProvider(
        IFarmingService farming)
    {
        _farming = farming;
    }

    public string Id =>
        "thoughts.career";

    public IEnumerable<ThoughtCandidate> GetCandidates(
        IPerson person,
        ThoughtContext context)
    {
        if (person.Age < 18)
            yield break;

        foreach (var gameEvent in
            context.Events.Where(
                gameEvent =>
                    gameEvent.SubjectId
                    == person.Id))
        {
            if (gameEvent.Type.Equals(
                "career.fired",
                StringComparison.OrdinalIgnoreCase))
            {
                yield return new ThoughtCandidate(
                    "career.fired",
                    "career.work",
                    "career.work",
                    84,
                    "😠",
                    "event",
                    gameEvent.Type,
                    "career.fired");
            }

            if (gameEvent.Type.Equals(
                    "career.employment",
                    StringComparison.OrdinalIgnoreCase)
                || gameEvent.Type.Equals(
                    "career.family_connections_success",
                    StringComparison.OrdinalIgnoreCase))
            {
                yield return new ThoughtCandidate(
                    "career.employment",
                    "career.work",
                    "career.work",
                    64,
                    "😊",
                    "event",
                    gameEvent.Type,
                    "career.employment");
            }

            if (gameEvent.Type.Equals(
                "career.promotion",
                StringComparison.OrdinalIgnoreCase))
            {
                yield return new ThoughtCandidate(
                    "career.promotion",
                    "career.work",
                    "career.work",
                    70,
                    "🤩",
                    "event",
                    gameEvent.Type,
                    "career.promotion");
            }

            if (gameEvent.Type.Equals(
                "career.quit",
                StringComparison.OrdinalIgnoreCase))
            {
                yield return new ThoughtCandidate(
                    "career.quit",
                    "career.work",
                    "career.work",
                    55,
                    "😌",
                    "event",
                    gameEvent.Type,
                    "career.quit");
            }

            if (gameEvent.Type.Equals(
                "career.retirement",
                StringComparison.OrdinalIgnoreCase))
            {
                yield return new ThoughtCandidate(
                    "career.retirement.new",
                    "career.work",
                    "career.work",
                    62,
                    "😌",
                    "event",
                    gameEvent.Type,
                    "career.retirement.new");
            }
        }

        var career =
            context.Career.GetCareer(
                person);

        if (career.IsRetired)
        {
            if (!context.Events.Any(
                gameEvent =>
                    gameEvent.SubjectId
                        == person.Id
                    && gameEvent.Type.Equals(
                        "career.retirement",
                        StringComparison.OrdinalIgnoreCase)))
            {
                yield return new ThoughtCandidate(
                    "career.retirement",
                    "career.work",
                    "career.work",
                    24,
                    "😌",
                    "state",
                    "career.retired",
                    "career.retirement");
            }

            yield break;
        }

        if (career.IsEmployed)
        {
            var candidate =
                career.JobSatisfaction switch
                {
                    1 =>
                        new ThoughtCandidate(
                            "career.miserable",
                            "career.work",
                            "career.work",
                            68,
                            "🤬",
                            "state",
                            "career.satisfaction",
                            "career.miserable"),

                    2 =>
                        new ThoughtCandidate(
                            "career.unhappy",
                            "career.work",
                            "career.work",
                            50,
                            "😒",
                            "state",
                            "career.satisfaction",
                            "career.unhappy"),

                    4 =>
                        new ThoughtCandidate(
                            "career.satisfied",
                            "career.work",
                            "career.work",
                            28,
                            "🙂",
                            "state",
                            "career.satisfaction",
                            "career.satisfied"),

                    5 =>
                        new ThoughtCandidate(
                            "career.thriving",
                            "career.work",
                            "career.work",
                            46,
                            "😄",
                            "state",
                            "career.satisfaction",
                            "career.thriving"),

                    _ =>
                        null
                };

            if (candidate is not null)
                yield return candidate;

            yield break;
        }

        var householdHead =
            context.Households.ResolveHouseholdHead(person);

        // Farm work is real employment for thought/status purposes. Check it
        // before recent job-loss state so someone who moved onto the family
        // farm does not continue thinking of themselves as unemployed.
        if (householdHead is not null
            && _farming.IsWorkingFarmWorker(person, householdHead))
        {
            yield return new ThoughtCandidate(
                "career.farm_work",
                "career.work",
                "career.work",
                20,
                "🌾",
                "state",
                "farming.work",
                "farming.work",
                new Dictionary<string, string>
                {
                    ["performance"] = "ordinary"
                });

            yield break;
        }

        if (person.Tags.Has(
            "recent.job_loss"))
        {
            yield return new ThoughtCandidate(
                "career.jobloss",
                "career.work",
                "career.work",
                66,
                "😕",
                "state",
                "recent.job_loss",
                "career.jobloss");
        }

        if (context.Justice
                .GetStatus(
                    person)
                .IsImprisoned
            || career.StatusId?.Equals(
                "status.housewife",
                StringComparison.OrdinalIgnoreCase) == true
            || person.Tags.Has(
                "role.nanny")
            || person.Tags.Has(
                "role.family_nanny"))
        {
            yield break;
        }

        yield return new ThoughtCandidate(
            "career.unemployed",
            "career.work",
            "career.work",
            52,
            "😕",
            "state",
            "career.unemployed",
            "career.unemployed");
    }
}
