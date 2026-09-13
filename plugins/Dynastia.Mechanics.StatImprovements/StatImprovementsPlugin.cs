using Dynastia.Contracts;

namespace Dynastia.Mechanics.StatImprovements;

public sealed class StatImprovementsPlugin :
    IGamePlugin
{
    private static readonly PaidStatImprovementDefinition[]
        Definitions =
        [
            new(
                ActionId:
                    "stats.improve_strength",
                StatId:
                    "strength",
                StatName:
                    "Strength",
                Label:
                    "Gym Membership",
                Cost:
                    10000m,
                Description:
                    "Fund an intensive long-term fitness program, coaching, equipment and diet. Guaranteed Strength +1. The improvement is acquired rather than hereditary.",
                Narrative:
                    "completed an intensive physical training program and became considerably stronger"),

            new(
                ActionId:
                    "stats.improve_intellect",
                StatId:
                    "intellect",
                StatName:
                    "Intellect",
                Label:
                    "Intelligence Training",
                Cost:
                    10000m,
                Description:
                    "Fund intensive intelligence training, private instruction and demanding mental exercises. Guaranteed Intellect +1. Education level is unchanged.",
                Narrative:
                    "completed intensive intelligence training that substantially developed their intellectual abilities"),

            new(
                ActionId:
                    "stats.improve_immunity",
                StatId:
                    "immunity",
                StatName:
                    "Immunity",
                Label:
                    "Immune Therapy",
                Cost:
                    10000m,
                Description:
                    "Fund an extensive specialist medical program intended to strengthen resistance to illness. Guaranteed Immunity +1.",
                Narrative:
                    "underwent an extensive course of treatment intended to strengthen their resistance to illness"),

            new(
                ActionId:
                    "stats.improve_appeal",
                StatId:
                    "appeal",
                StatName:
                    "Appeal",
                Label:
                    "Plastic Surgery",
                Cost:
                    10000m,
                Description:
                    "Pay for substantial cosmetic surgery. Guaranteed Appeal +1. The acquired improvement affects future relationship calculations but is not inherited.",
                Narrative:
                    "underwent cosmetic surgery that noticeably improved their appearance"),

            new(
                ActionId:
                    "stats.improve_longevity",
                StatId:
                    "longevity",
                StatName:
                    "Longevity",
                Label:
                    "Preventive Medicine",
                Cost:
                    10000m,
                Description:
                    "Fund prolonged preventive medicine, specialist monitoring, rehabilitation and risk-factor treatment. Guaranteed Longevity +1.",
                Narrative:
                    "completed an extensive preventive medicine program intended to improve their long-term health"),

            new(
                ActionId:
                    "stats.improve_fertility",
                StatId:
                    "fertility",
                StatName:
                    "Fertility",
                Label:
                    "Fertility Treatment",
                Cost:
                    10000m,
                Description:
                    "Pay for specialist fertility diagnosis and treatment. Guaranteed Fertility +1, including Fertility 0 → 1. Hereditary Fertility is unchanged.",
                Narrative:
                    "underwent an extensive course of fertility treatment")
        ];

    public void Initialize(
        IGamePluginContext context)
    {
        var stats =
            context.GetService<IStatsService>()
            ?? throw new InvalidOperationException(
                "Stats service is unavailable.");

        var family =
            context.GetService<IFamilyService>()
            ?? throw new InvalidOperationException(
                "Family service is unavailable.");

        var economy =
            context.GetService<IEconomyService>()
            ?? throw new InvalidOperationException(
                "Economy service is unavailable.");

        var households =
            context.GetService<IHouseholdService>()
            ?? throw new InvalidOperationException(
                "Household service is unavailable.");

        var health =
            context.GetService<IHealthService>()
            ?? throw new InvalidOperationException(
                "Health service is unavailable.");

        var actions =
            context.GetService<IActionRegistry>()
            ?? throw new InvalidOperationException(
                "Action registry is unavailable.");

        foreach (var definition in
            Definitions)
        {
            actions.Register(
                CreateAction(
                    definition,
                    stats,
                    family,
                    economy,
                    households,
                    health));
        }

        context.Log(
            "Paid stat-improvement actions registered.");
    }

    private static GameActionDefinition CreateAction(
        PaidStatImprovementDefinition definition,
        IStatsService stats,
        IFamilyService family,
        IEconomyService economy,
        IHouseholdService households,
        IHealthService health)
    {
        return new GameActionDefinition
        {
            Id =
                definition.ActionId,

            Label =
                $"{definition.Label} " +
                $"({definition.Cost:N0} zł)",

            Description =
                definition.Description,

            Mode =
                ActionExecutionMode.Queued,

            QueuePhase =
                YearPhase.QueuedActionsEarly,

            IsAvailable =
                actionContext =>
                    CanPurchase(
                        actionContext,
                        definition,
                        stats,
                        economy,
                        households),

            Execute =
                actionContext =>
                {
                    var actor =
                        actionContext.Actor;

                    var target =
                        actionContext.Target;

                    if (!CanPurchase(
                        actionContext,
                        definition,
                        stats,
                        economy,
                        households))
                    {
                        return new GameActionResult(
                            false,
                            "This stat improvement is not currently available.");
                    }

                    var before =
                        GetEffectiveStat(
                            stats,
                            target,
                            definition.StatId);

                    if (!stats.TryIncreaseAcquiredStat(
                        target,
                        definition.StatId))
                    {
                        return new GameActionResult(
                            false,
                            $"{definition.StatName} is already at its maximum.");
                    }

                    economy.ChangeWealth(
                        actor,
                        -definition.Cost);

                    var after =
                        GetEffectiveStat(
                            stats,
                            target,
                            definition.StatId);

                    if (definition.StatId.Equals(
                            "fertility",
                            StringComparison.OrdinalIgnoreCase)
                        && before == 0
                        && after >= 1)
                    {
                        // No separate infertility condition exists in the
                        // current game, but clear common future-compatible
                        // state names if one is later introduced.
                        health.RemoveCondition(
                            target,
                            "infertility");

                        target.Tags.Remove(
                            "state.infertile");

                        target.Tags.Remove(
                            "trait.infertile");
                    }

                    var displayName =
                        family.GetDisplayName(
                            target);

                    var narrative =
                        definition.StatId.Equals(
                                "fertility",
                                StringComparison.OrdinalIgnoreCase)
                            && before == 0
                            && after == 1
                                ? $"{displayName} {definition.Narrative} " +
                                  "and overcame infertility."
                                : $"{displayName} {definition.Narrative} " +
                                  $"and improved their {definition.StatName}.";

                    actionContext.EventBus.Publish(
                        new GameEvent
                        {
                            Type =
                                "stats.paid_improvement",

                            Year =
                                actionContext.GameState.Year,

                            SubjectId =
                                target.Id,

                            RelatedPersonIds =
                                actor.Id == target.Id
                                    ? []
                                    : [actor.Id],

                            Data =
                                new Dictionary<string, string>
                                {
                                    ["actionId"] =
                                        definition.ActionId,

                                    ["statId"] =
                                        definition.StatId,

                                    ["statName"] =
                                        definition.StatName,

                                    ["cost"] =
                                        definition.Cost.ToString(),

                                    ["previousValue"] =
                                        before.ToString(),

                                    ["newValue"] =
                                        after.ToString(),

                                    ["text"] =
                                        narrative
                                }
                        });

                    return new GameActionResult(
                        true);
                }
        };
    }

    private static bool CanPurchase(
        GameActionContext actionContext,
        PaidStatImprovementDefinition definition,
        IStatsService stats,
        IEconomyService economy,
        IHouseholdService households)
    {
        var actor =
            actionContext.Actor;

        var target =
            actionContext.Target;

        if (!actor.Tags.Has(
                "state.alive")
            || !actor.Tags.Has(
                "control.playable")
            || !target.Tags.Has(
                "state.alive")
            || target.Age < 18)
        {
            return false;
        }

        var actorHousehold =
            households.ResolveHouseholdHead(
                actor);

        if (actorHousehold?.Id
            != actor.Id)
        {
            return false;
        }

        var targetHousehold =
            households.ResolveHouseholdHead(
                target);

        if (targetHousehold?.Id
            != actor.Id)
        {
            return false;
        }

        var finance =
            economy.GetHousehold(
                actor);

        if (finance is null
            || finance.Wealth
                < definition.Cost)
        {
            return false;
        }

        return GetEffectiveStat(
            stats,
            target,
            definition.StatId)
            < 5;
    }

    private static int GetEffectiveStat(
        IStatsService stats,
        IPerson person,
        string statId)
    {
        return stats
            .GetStats(
                person)
            .First(
                stat =>
                    stat.Id.Equals(
                        statId,
                        StringComparison.OrdinalIgnoreCase))
            .Value;
    }

    private static bool WasUsedThisYear(
        IPerson person,
        int year)
    {
        return person.Components
            .Get<PaidStatImprovementComponent>()?
            .LastImprovementYear
            == year;
    }

    private static void MarkUsedThisYear(
        IPerson person,
        int year)
    {
        var component =
            person.Components
                .Get<PaidStatImprovementComponent>()
            ?? new PaidStatImprovementComponent();

        component.LastImprovementYear =
            year;

        person.Components.Set(
            component);
    }
}
