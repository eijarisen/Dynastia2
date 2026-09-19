using Dynastia.Contracts;

namespace Dynastia.Mechanics.StatImprovements;

public sealed class StatImprovementsPlugin :
    IGamePlugin
{
    private static readonly PaidStatImprovementDefinition[]
        Definitions =
        [
            new("stats.improve_strength", "strength", "Strength", 20000m),
            new("stats.improve_intellect", "intellect", "Intellect", 20000m),
            new("stats.improve_immunity", "immunity", "Immunity", 20000m),
            new("stats.improve_appeal", "appeal", "Appeal", 20000m),
            new("stats.improve_longevity", "longevity", "Longevity", 20000m),
            new("stats.improve_fertility", "fertility", "Fertility", 20000m)
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

        var historical =
            context.GetService<IHistoricalActionVariantService>()
            ?? throw new InvalidOperationException(
                "Historical action variant service is unavailable.");

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
                    health,
                    historical));
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
        IHealthService health,
        IHistoricalActionVariantService historical)
    {
        var canonical =
            historical.GetCanonicalVariant(
                definition.ActionId)
            ?? throw new InvalidDataException(
                $"Missing historical action data for '{definition.ActionId}'.");

        return new GameActionDefinition
        {
            Id =
                definition.ActionId,

            Label =
                canonical.Label,

            Description =
                canonical.Description,

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
                        households,
                        historical),

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
                        households,
                        historical))
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

                    var variant =
                        historical.GetVariant(
                            definition.ActionId,
                            actionContext.GameState.Year)
                        ?? canonical;

                    var narrative =
                        definition.StatId.Equals(
                                "fertility",
                                StringComparison.OrdinalIgnoreCase)
                            && before == 0
                            && after == 1
                                ? $"{displayName} {variant.Narrative} " +
                                  "and overcame infertility."
                                : $"{displayName} {variant.Narrative} " +
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
        IHouseholdService households,
        IHistoricalActionVariantService historical)
    {
        var actor =
            actionContext.Actor;

        var target =
            actionContext.Target;

        if (!actor.Tags.Has(
                "state.alive")
            || !actionContext.ActorHasControl
            || !target.Tags.Has(
                "state.alive")
            || target.Age < 18)
        {
            return false;
        }

        var historicallyAvailable =
            historical.GetVariant(
                definition.ActionId,
                actionContext.GameState.Year)
            is not null;

        if (!historicallyAvailable
            && !ActionCompatibilityParameters.IsRestoredQueuedAction(
                actionContext.Parameters))
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
