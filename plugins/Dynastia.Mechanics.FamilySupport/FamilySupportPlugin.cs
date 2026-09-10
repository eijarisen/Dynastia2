using Dynastia.Contracts;

namespace Dynastia.Mechanics.FamilySupport;

public sealed class FamilySupportPlugin :
    IGamePlugin
{
    private const decimal WealthThreshold =
        10000m;

    private const decimal SupportAmount =
        2000m;

    private const double SuccessChance =
        0.50;

    public void Initialize(
        IGamePluginContext context)
    {
        var family =
            context.GetService<IFamilyService>()
            ?? throw new InvalidOperationException(
                "Family service is unavailable.");

        var economy =
            context.GetService<IEconomyService>()
            ?? throw new InvalidOperationException(
                "Economy service is unavailable.");

        var events =
            context.GetService<IGameEventBus>()
            ?? throw new InvalidOperationException(
                "Game event bus is unavailable.");

        var random =
            context.GetService<IGameRandom>()
            ?? throw new InvalidOperationException(
                "Random service is unavailable.");

        var actions =
            context.GetService<IActionRegistry>()
            ?? throw new InvalidOperationException(
                "Action registry is unavailable.");

        actions.Register(
            CreateAskParentsAction(
                family,
                economy,
                events,
                random));

        actions.Register(
            CreateAskChildAction(
                family,
                economy,
                events,
                random));

        context.Log(
            "Family financial-support mechanics registered.");
    }

    private static GameActionDefinition
        CreateAskParentsAction(
            IFamilyService family,
            IEconomyService economy,
            IGameEventBus events,
            IGameRandom random)
    {
        return new GameActionDefinition
        {
            Id =
                "family_support.ask_parents",

            Label =
                "Ask Parents for Money",

            Description =
                "When broke, ask your living father for $2,000. " +
                "He must have at least $10,000 when the request resolves, " +
                "and there is a 50% chance he agrees.",

            Mode =
                ActionExecutionMode.Queued,

            QueuePhase =
                YearPhase.LifeEvents,

            IsAvailable =
                actionContext =>
                {
                    var actor =
                        actionContext.Actor;

                    if (!CanRequestMoney(
                        actor,
                        actionContext.Target,
                        economy))
                    {
                        return false;
                    }

                    var father =
                        family.GetFather(
                            actor);

                    return father is not null
                        && father.Tags.Has(
                            "state.alive")
                        && family.IsMaleLineage(
                            father)
                        && economy.GetHousehold(
                            father) is not null;
                },

            Execute =
                actionContext =>
                {
                    var actor =
                        actionContext.Actor;

                    var father =
                        family.GetFather(
                            actor);

                    if (father is null
                        || !father.Tags.Has(
                            "state.alive")
                        || !family.IsMaleLineage(
                            father))
                    {
                        return new GameActionResult(
                            false);
                    }

                    var actorHousehold =
                        economy.GetHousehold(
                            actor);

                    var fatherHousehold =
                        economy.GetHousehold(
                            father);

                    if (actorHousehold is null
                        || fatherHousehold is null)
                    {
                        return new GameActionResult(
                            false);
                    }

                    var success =
                        fatherHousehold.Wealth
                            >= WealthThreshold
                        && random.NextDouble()
                            < SuccessChance;

                    if (success)
                    {
                        economy.ChangeWealth(
                            actor,
                            SupportAmount);

                        economy.ChangeWealth(
                            father,
                            -SupportAmount);

                        events.Publish(
                            new GameEvent
                            {
                                Type =
                                    "family_support.parents_success",

                                Year =
                                    actionContext.GameState.Year,

                                SubjectId =
                                    actor.Id,

                                RelatedPersonIds =
                                    [father.Id],

                                Data =
                                    new Dictionary<string, string>
                                    {
                                        ["amount"] =
                                            SupportAmount.ToString(),

                                        ["text"] =
                                            $"{family.GetDisplayName(actor)} " +
                                            $"successfully borrowed " +
                                            $"${SupportAmount:N0} from his father, " +
                                            $"{family.GetDisplayName(father)}."
                                    }
                            });
                    }
                    else
                    {
                        events.Publish(
                            new GameEvent
                            {
                                Type =
                                    "family_support.parents_failure",

                                Year =
                                    actionContext.GameState.Year,

                                SubjectId =
                                    actor.Id,

                                RelatedPersonIds =
                                    [father.Id],

                                Data =
                                    new Dictionary<string, string>
                                    {
                                        ["text"] =
                                            $"{family.GetDisplayName(actor)} " +
                                            "asked his father for money, " +
                                            "but was denied."
                                    }
                            });
                    }

                    return new GameActionResult(
                        true);
                }
        };
    }

    private static GameActionDefinition
        CreateAskChildAction(
            IFamilyService family,
            IEconomyService economy,
            IGameEventBus events,
            IGameRandom random)
    {
        return new GameActionDefinition
        {
            Id =
                "family_support.ask_child",

            Label =
                "Ask Selected Child for Money",

            Description =
                "When broke, ask the selected wealthy adult child " +
                "for $2,000. The child must have more than $10,000 " +
                "when queued, and there is a 50% chance they agree.",

            Mode =
                ActionExecutionMode.Queued,

            QueuePhase =
                YearPhase.LifeEvents,

            IsAvailable =
                actionContext =>
                {
                    var actor =
                        actionContext.Actor;

                    var child =
                        actionContext.Target;

                    if (!CanRequestMoneyFromSelectedChild(
                        actor,
                        child,
                        family,
                        economy))
                    {
                        return false;
                    }

                    var childHousehold =
                        economy.GetHousehold(
                            child);

                    // Source action panel uses strict > 10,000.
                    return childHousehold is not null
                        && childHousehold.Wealth
                            > WealthThreshold;
                },

            Execute =
                actionContext =>
                {
                    var actor =
                        actionContext.Actor;

                    var child =
                        actionContext.Target;

                    if (!CanRequestMoneyFromSelectedChild(
                        actor,
                        child,
                        family,
                        economy))
                    {
                        return new GameActionResult(
                            false);
                    }

                    var actorHousehold =
                        economy.GetHousehold(
                            actor);

                    var childHousehold =
                        economy.GetHousehold(
                            child);

                    if (actorHousehold is null
                        || childHousehold is null)
                    {
                        return new GameActionResult(
                            false);
                    }

                    // Source execution check uses >= 10,000,
                    // even though the button appears only at > 10,000.
                    var success =
                        childHousehold.Wealth
                            >= WealthThreshold
                        && random.NextDouble()
                            < SuccessChance;

                    if (success)
                    {
                        economy.ChangeWealth(
                            actor,
                            SupportAmount);

                        economy.ChangeWealth(
                            child,
                            -SupportAmount);

                        events.Publish(
                            new GameEvent
                            {
                                Type =
                                    "family_support.child_success",

                                Year =
                                    actionContext.GameState.Year,

                                SubjectId =
                                    actor.Id,

                                RelatedPersonIds =
                                    [child.Id],

                                Data =
                                    new Dictionary<string, string>
                                    {
                                        ["amount"] =
                                            SupportAmount.ToString(),

                                        ["text"] =
                                            $"{family.GetDisplayName(actor)} " +
                                            $"successfully borrowed " +
                                            $"${SupportAmount:N0} from their child, " +
                                            $"{family.GetDisplayName(child)}."
                                    }
                            });
                    }
                    else
                    {
                        events.Publish(
                            new GameEvent
                            {
                                Type =
                                    "family_support.child_failure",

                                Year =
                                    actionContext.GameState.Year,

                                SubjectId =
                                    actor.Id,

                                RelatedPersonIds =
                                    [child.Id],

                                Data =
                                    new Dictionary<string, string>
                                    {
                                        ["text"] =
                                            $"{family.GetDisplayName(actor)} " +
                                            $"asked their child {child.Name} " +
                                            "for money, but was denied."
                                    }
                            });
                    }

                    return new GameActionResult(
                        true);
                }
        };
    }

    private static bool CanRequestMoney(
        IPerson actor,
        IPerson target,
        IEconomyService economy)
    {
        if (actor.Id != target.Id
            || !actor.Tags.Has(
                "state.alive")
            || !actor.Tags.Has(
                "control.playable"))
        {
            return false;
        }

        var household =
            economy.GetHousehold(
                actor);

        return household is not null
            && household.Wealth <= 0;
    }

    private static bool CanRequestMoneyFromSelectedChild(
        IPerson actor,
        IPerson child,
        IFamilyService family,
        IEconomyService economy)
    {
        if (!actor.Tags.Has(
                "state.alive")
            || !actor.Tags.Has(
                "control.playable")
            || actor.Id == child.Id
            || !child.Tags.Has(
                "state.alive")
            || child.Age < 18)
        {
            return false;
        }

        var actorHousehold =
            economy.GetHousehold(
                actor);

        if (actorHousehold is null
            || actorHousehold.Wealth > 0)
        {
            return false;
        }

        if (!family.GetChildren(
                actor)
            .Any(
                candidate =>
                    candidate.Id
                    == child.Id))
        {
            return false;
        }

        return economy.GetHousehold(
            child) is not null;
    }
}
