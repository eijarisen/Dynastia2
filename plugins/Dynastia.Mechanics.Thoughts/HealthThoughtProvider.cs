using Dynastia.Contracts;

namespace Dynastia.Mechanics.Thoughts;

internal sealed class HealthThoughtProvider :
    IThoughtProvider
{
    private static readonly HashSet<string>
        BirthConditionIds =
            new(
                StringComparer.OrdinalIgnoreCase)
            {
                "down_syndrome",
                "cerebral_palsy",
                "autism",
                "congenital_heart_defect",
                "cleft_lip_palate",
                "congenital_hearing_loss",
                "limb_difference",
                "craniosynostosis",
                "gastroschisis",
                "spina_bifida",
                "congenital_diaphragmatic_hernia",
                "cystic_fibrosis",
                "anophthalmia_microphthalmia",
                "achondroplasia"
            };

    public string Id =>
        "thoughts.health";

    public IEnumerable<ThoughtCandidate> GetCandidates(
        IPerson person,
        ThoughtContext context)
    {
        var health =
            context.Health.GetHealth(
                person);

        var depression =
            health.Conditions
                .FirstOrDefault(
                    condition =>
                        condition.Id.Equals(
                            "depression",
                            StringComparison.OrdinalIgnoreCase)
                        || condition.Name.Equals(
                            "Depression",
                            StringComparison.OrdinalIgnoreCase));

        if (depression is not null)
        {
            yield return new ThoughtCandidate(
                "health.depression",
                "health.mental",
                "health.mental",
                88,
                "😥",
                "state",
                depression.Id,
                "depression",
                ThoughtProviderUtilities.Context(
                    (
                        "condition",
                        depression.Name
                    )));
        }

        var anxiety =
            health.Conditions
                .FirstOrDefault(
                    condition =>
                        condition.Id.Equals(
                            "anxiety",
                            StringComparison.OrdinalIgnoreCase)
                        || condition.Name.Equals(
                            "Anxiety",
                            StringComparison.OrdinalIgnoreCase));

        if (anxiety is not null)
        {
            yield return new ThoughtCandidate(
                "health.anxiety",
                "health.mental",
                "health.mental",
                80,
                "😟",
                "state",
                anxiety.Id,
                "anxiety",
                ThoughtProviderUtilities.Context(
                    (
                        "condition",
                        anxiety.Name
                    )));
        }

        if (person.Age >= 18)
        {
            var alcoholism =
                health.Conditions
                    .FirstOrDefault(
                        condition =>
                            condition.Id.Equals(
                                "alcoholism",
                                StringComparison.OrdinalIgnoreCase)
                            || condition.Name.Equals(
                                "Alcoholism",
                                StringComparison.OrdinalIgnoreCase));

            if (alcoholism is not null)
            {
                yield return new ThoughtCandidate(
                    "health.alcoholism",
                    "health.mental",
                    "health.mental",
                    76,
                    "🥴",
                    "state",
                    alcoholism.Id,
                    "alcoholism",
                    ThoughtProviderUtilities.Context(
                        (
                            "condition",
                            alcoholism.Name
                        )));
            }
        }

        var physical =
            health.Conditions
                .Where(
                    condition =>
                        !IsMental(
                            condition)
                        && !BirthConditionIds.Contains(
                            condition.Id))
                .Select(
                    condition =>
                        BuildPhysicalCandidate(
                            condition))
                .OrderByDescending(
                    candidate =>
                        candidate.Salience)
                .ThenBy(
                    candidate =>
                        candidate.Id,
                    StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

        if (physical is not null)
        {
            yield return physical;
        }
        else
        {
            var poorHealthSalience =
                health.Percentage switch
                {
                    <= 20 => 90,
                    <= 40 => 76,
                    <= 60 => 58,
                    _ => 0
                };

            if (poorHealthSalience > 0)
            {
                yield return new ThoughtCandidate(
                    "health.poor",
                    "health.physical",
                    "health.physical",
                    poorHealthSalience,
                    "😣",
                    "state",
                    "health",
                    "health.poor");
            }
        }

        foreach (var gameEvent in
            context.Events.Where(
                gameEvent =>
                    gameEvent.SubjectId
                    == person.Id))
        {
            if (gameEvent.Type.Equals(
                "wellbeing.therapy_success",
                StringComparison.OrdinalIgnoreCase))
            {
                yield return new ThoughtCandidate(
                    "therapy.success",
                    "health.mental",
                    "health.therapy",
                    62,
                    "😌",
                    "event",
                    gameEvent.Type,
                    "therapy.success");
            }

            if (gameEvent.Type.Equals(
                "wellbeing.therapy_failure",
                StringComparison.OrdinalIgnoreCase))
            {
                yield return new ThoughtCandidate(
                    "therapy.failure",
                    "health.mental",
                    "health.therapy",
                    48,
                    "😕",
                    "event",
                    gameEvent.Type,
                    "therapy.failure");
            }

            if (gameEvent.Type.Equals(
                "wellbeing.recover",
                StringComparison.OrdinalIgnoreCase))
            {
                var activity =
                    gameEvent.Data.TryGetValue(
                        "text",
                        out var eventText)
                            ? ExtractRecoveryActivity(
                                eventText,
                                context.Family.GetDisplayName(
                                    person))
                            : "Taking some time to recover";

                yield return new ThoughtCandidate(
                    "recover",
                    "health.physical",
                    "health.recovery",
                    45,
                    "😌",
                    "event",
                    gameEvent.Type,
                    "recover",
                    ThoughtProviderUtilities.Context(
                        (
                            "activity",
                            activity
                        )));
            }

            if (gameEvent.Type.Equals(
                    "wellbeing.heal",
                    StringComparison.OrdinalIgnoreCase)
                || gameEvent.Type.Equals(
                    "wellbeing.heal_relative",
                    StringComparison.OrdinalIgnoreCase))
            {
                yield return new ThoughtCandidate(
                    "heal",
                    "health.physical",
                    "health.recovery",
                    45,
                    "🙂",
                    "event",
                    gameEvent.Type,
                    "heal");
            }
        }
    }

    private static ThoughtCandidate BuildPhysicalCandidate(
        HealthConditionInfo condition)
    {
        var impact =
            Math.Abs(
                condition.HealthImpact);

        var type =
            condition.Type
                .ToLowerInvariant();

        if (type.Equals(
            "terminal",
            StringComparison.OrdinalIgnoreCase))
        {
            return new ThoughtCandidate(
                $"health:{condition.Id}",
                "health.physical",
                "health.physical",
                94,
                "😣",
                "state",
                condition.Id,
                "health.terminal",
                ThoughtProviderUtilities.Context(
                    (
                        "condition",
                        condition.Name
                    )));
        }

        if (type.Equals(
            "permanent",
            StringComparison.OrdinalIgnoreCase))
        {
            return new ThoughtCandidate(
                $"health:{condition.Id}",
                "health.physical",
                "health.physical",
                58
                + Math.Min(
                    14,
                    (int)Math.Round(
                        impact)),
                "😣",
                "state",
                condition.Id,
                "health.permanent",
                ThoughtProviderUtilities.Context(
                    (
                        "condition",
                        condition.Name
                    )));
        }

        if (type.Equals(
            "curable",
            StringComparison.OrdinalIgnoreCase))
        {
            var injury =
                condition.Id.Contains(
                    "broken",
                    StringComparison.OrdinalIgnoreCase)
                || condition.Id.Contains(
                    "concussion",
                    StringComparison.OrdinalIgnoreCase);

            return new ThoughtCandidate(
                $"health:{condition.Id}",
                "health.physical",
                "health.physical",
                48
                + (int)Math.Round(
                    impact
                    * 2),
                injury
                    ? "🤕"
                    : "🤒",
                "state",
                condition.Id,
                "health.curable",
                ThoughtProviderUtilities.Context(
                    (
                        "condition",
                        condition.Name
                    )));
        }

        return new ThoughtCandidate(
            $"health:{condition.Id}",
            "health.physical",
            "health.physical",
            35
            + (int)Math.Round(
                impact),
            "🤧",
            "state",
            condition.Id,
            "health.minor",
            ThoughtProviderUtilities.Context(
                (
                    "condition",
                    condition.Name
                )));
    }

    private static bool IsMental(
        HealthConditionInfo condition)
    {
        return condition.Id.Equals(
                "depression",
                StringComparison.OrdinalIgnoreCase)
            || condition.Id.Equals(
                "anxiety",
                StringComparison.OrdinalIgnoreCase)
            || condition.Id.Equals(
                "alcoholism",
                StringComparison.OrdinalIgnoreCase)
            || condition.Name.Equals(
                "Depression",
                StringComparison.OrdinalIgnoreCase)
            || condition.Name.Equals(
                "Anxiety",
                StringComparison.OrdinalIgnoreCase)
            || condition.Name.Equals(
                "Alcoholism",
                StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractRecoveryActivity(
        string text,
        string displayName)
    {
        if (text.StartsWith(
                displayName,
                StringComparison.OrdinalIgnoreCase))
        {
            var activity =
                text[
                    displayName.Length..]
                    .Trim();

            if (activity.EndsWith(
                ".",
                StringComparison.Ordinal))
            {
                activity =
                    activity[..^1];
            }

            if (!string.IsNullOrWhiteSpace(
                activity))
            {
                return activity;
            }
        }

        return "Taking some time to recover";
    }
}
