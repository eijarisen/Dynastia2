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

        var households =
            context.GetService<IHouseholdService>()
            ?? throw new InvalidOperationException(
                "Household service is unavailable.");

        var properties =
            context.GetService<IPropertyActionService>()
            ?? throw new InvalidOperationException(
                "Property action service is unavailable.");

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
            CreateAskParentsForHouseAction(
                family,
                economy,
                households,
                properties,
                events,
                random));

        actions.Register(
            CreateAskSingleParentForHouseAction(
                Sex.Male,
                "family_support.ask_father_house",
                "Ask Father for a House",
                family,
                economy,
                households,
                properties,
                events,
                random));

        actions.Register(
            CreateAskSingleParentForHouseAction(
                Sex.Female,
                "family_support.ask_mother_house",
                "Ask Mother for a House",
                family,
                economy,
                households,
                properties,
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
                "When broke, ask your living parents for financial help. " +
                "Their household must currently earn more than it spends. " +
                "How willing they are to help, and how much they offer, " +
                "depends on the parent's Morals. The gift is capped at 2,000 zł.",

            Mode =
                ActionExecutionMode.Queued,

            QueuePhase =
                YearPhase.LifeEvents,

            IsAvailable =
                actionContext =>
                {
                    var actor = actionContext.Actor;
                    if (!CanRequestMoney(actor, actionContext.Target, economy))
                        return false;

                    var parents = GetLivingParents(actor, family);
                    if (parents.Count == 0)
                        return false;

                    var parentHousehold = economy.GetHousehold(parents[0]);
                    return parentHousehold is not null
                        && parentHousehold.Wealth > 0
                        && parentHousehold.LastIncome > parentHousehold.LastExpenses;
                },

            Execute =
                actionContext =>
                {
                    var actor = actionContext.Actor;
                    var parents = GetLivingParents(actor, family);
                    if (parents.Count == 0)
                        return new GameActionResult(false);

                    var parentRepresentative = parents[0];
                    var actorHousehold = economy.GetHousehold(actor);
                    var parentHousehold = economy.GetHousehold(parentRepresentative);
                    if (actorHousehold is null
                        || parentHousehold is null
                        || parentHousehold.Wealth <= 0
                        || parentHousehold.LastIncome <= parentHousehold.LastExpenses)
                    {
                        return new GameActionResult(false);
                    }

                    var (sharePercent, agreementChance) =
                        GetParentSupportTerms(parents);

                    var amount = Math.Min(
                        SupportAmount,
                        Math.Floor((parentHousehold.Wealth * sharePercent) / 100m) * 100m);

                    if (amount < 100m)
                        amount = Math.Min(100m, parentHousehold.Wealth);

                    var success = amount > 0
                        && random.NextDouble() < agreementChance;

                    if (success)
                    {
                        economy.ChangeWealth(actor, amount);
                        economy.ChangeWealth(parentRepresentative, -amount);

                        events.Publish(new GameEvent
                        {
                            Type = "family_support.parents_success",
                            Year = actionContext.GameState.Year,
                            SubjectId = actor.Id,
                            RelatedPersonIds = parents.Select(parent => parent.Id).ToList(),
                            Data = new Dictionary<string, string>
                            {
                                ["amount"] = amount.ToString(),
                                ["text"] =
                                    $"{family.GetDisplayName(actor)} received " +
                                    $"{amount:N0} zł in financial help from " +
                                    $"{string.Join(" and ", parents.Select(family.GetDisplayName))}."
                            }
                        });
                    }
                    else
                    {
                        events.Publish(new GameEvent
                        {
                            Type = "family_support.parents_failure",
                            Year = actionContext.GameState.Year,
                            SubjectId = actor.Id,
                            RelatedPersonIds = parents.Select(parent => parent.Id).ToList(),
                            Data = new Dictionary<string, string>
                            {
                                ["text"] =
                                    $"{family.GetDisplayName(actor)} asked his parents for money, " +
                                    "but they decided not to help this time."
                            }
                        });
                    }

                    return new GameActionResult(true);
                }
        };
    }

    private static GameActionDefinition
        CreateAskParentsForHouseAction(
        IFamilyService family,
        IEconomyService economy,
        IHouseholdService households,
        IPropertyActionService properties,
        IGameEventBus events,
        IGameRandom random)
    {
        return new GameActionDefinition
        {
            Id = "family_support.ask_parents_house",
            Label = "Ask Parents for a House",
            Description =
                "Ask your parents for one of their spare rented houses. " +
                "They choose which property to give. If it is in another town, " +
                "your household will move there and current careers will end.",
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.QueuedActionsEarly,
            IsAvailable = context =>
            {
                if (!CanAskForHouse(context.Actor, context.Target, economy))
                    return false;

                var father = family.GetFather(context.Actor);
                var mother = family.GetMother(context.Actor);
                if (father is null || mother is null
                    || !father.Tags.Has("state.alive")
                    || !mother.Tags.Has("state.alive"))
                {
                    return false;
                }

                var fatherHousehold = economy.GetHouseholdId(father);
                var motherHousehold = economy.GetHouseholdId(mother);
                if (fatherHousehold is null || fatherHousehold != motherHousehold)
                    return false;

                return properties.GetTransferableProperties(father).Count > 0;
            },
            Execute = context =>
            {
                var father = family.GetFather(context.Actor);
                var mother = family.GetMother(context.Actor);
                if (father is null || mother is null)
                    return new GameActionResult(false);

                var representative = households.ResolveHouseholdHead(father) ?? father;
                var houses = properties.GetTransferableProperties(representative);
                if (houses.Count == 0)
                    return new GameActionResult(true);

                var chance = HouseGiftChance(representative);
                if (random.NextDouble() >= chance)
                {
                    events.Publish(new GameEvent
                    {
                        Type = "family_support.house_refused",
                        Year = context.GameState.Year,
                        SubjectId = context.Actor.Id,
                        RelatedPersonIds = [father.Id, mother.Id],
                        Data = new Dictionary<string, string>
                        {
                            ["text"] = $"{family.GetDisplayName(context.Actor)} asked their parents for a spare house, but they declined."
                        }
                    });
                    return new GameActionResult(true);
                }

                var house = houses[random.NextInt(0, houses.Count - 1)];
                var result = properties.TransferPropertyFromParent(
                    representative,
                    context.Actor,
                    house,
                    context.GameState.Year);

                if (result.Success)
                {
                    events.Publish(new GameEvent
                    {
                        Type = "family_support.house_received",
                        Year = context.GameState.Year,
                        SubjectId = context.Actor.Id,
                        RelatedPersonIds = [father.Id, mother.Id],
                        Data = new Dictionary<string, string>
                        {
                            ["town"] = house.Town.Town,
                            ["text"] = $"{family.GetDisplayName(context.Actor)} received a house in {house.Town.Town} from their parents."
                        }
                    });
                }

                return result;
            }
        };
    }

    private static GameActionDefinition
        CreateAskSingleParentForHouseAction(
        Sex parentSex,
        string id,
        string label,
        IFamilyService family,
        IEconomyService economy,
        IHouseholdService households,
        IPropertyActionService properties,
        IGameEventBus events,
        IGameRandom random)
    {
        return new GameActionDefinition
        {
            Id = id,
            Label = label,
            Description =
                "Ask this parent for one of their spare rented houses. " +
                "They choose which property to give. If it is in another town, " +
                "your household will move there and current careers will end.",
            Mode = ActionExecutionMode.Queued,
            QueuePhase = YearPhase.QueuedActionsEarly,
            IsAvailable = context =>
            {
                if (!CanAskForHouse(context.Actor, context.Target, economy))
                    return false;

                var parent = parentSex == Sex.Male
                    ? family.GetFather(context.Actor)
                    : family.GetMother(context.Actor);

                if (parent is null || !parent.Tags.Has("state.alive"))
                    return false;

                var other = parentSex == Sex.Male
                    ? family.GetMother(context.Actor)
                    : family.GetFather(context.Actor);

                if (other is not null
                    && other.Tags.Has("state.alive")
                    && economy.GetHouseholdId(other) is Guid otherHousehold
                    && economy.GetHouseholdId(parent) == otherHousehold)
                {
                    return false;
                }

                return properties.GetTransferableProperties(parent).Count > 0;
            },
            Execute = context =>
            {
                var parent = parentSex == Sex.Male
                    ? family.GetFather(context.Actor)
                    : family.GetMother(context.Actor);

                if (parent is null || !parent.Tags.Has("state.alive"))
                    return new GameActionResult(false);

                var representative =
                    households.ResolveHouseholdHead(parent)
                    ?? parent;

                var houses = properties.GetTransferableProperties(representative);
                if (houses.Count == 0)
                    return new GameActionResult(true);

                if (random.NextDouble() >= HouseGiftChance(representative))
                {
                    events.Publish(new GameEvent
                    {
                        Type = "family_support.house_refused",
                        Year = context.GameState.Year,
                        SubjectId = context.Actor.Id,
                        RelatedPersonIds = [parent.Id],
                        Data = new Dictionary<string, string>
                        {
                            ["text"] = $"{family.GetDisplayName(context.Actor)} asked {parent.Name} for a spare house, but was refused."
                        }
                    });
                    return new GameActionResult(true);
                }

                var house = houses[random.NextInt(0, houses.Count - 1)];
                var result = properties.TransferPropertyFromParent(
                    representative,
                    context.Actor,
                    house,
                    context.GameState.Year);

                if (result.Success)
                {
                    events.Publish(new GameEvent
                    {
                        Type = "family_support.house_received",
                        Year = context.GameState.Year,
                        SubjectId = context.Actor.Id,
                        RelatedPersonIds = [parent.Id],
                        Data = new Dictionary<string, string>
                        {
                            ["town"] = house.Town.Town,
                            ["text"] = $"{family.GetDisplayName(context.Actor)} received a house in {house.Town.Town} from {parent.Name}."
                        }
                    });
                }

                return result;
            }
        };
    }

    private static bool CanAskForHouse(
        IPerson actor,
        IPerson target,
        IEconomyService economy)
    {
        if (actor.Id != target.Id
            || !actor.Tags.Has("state.alive")
            || !actor.Tags.Has("control.playable"))
        {
            return false;
        }

        var household = economy.GetHousehold(actor);
        return household is not null && household.Houses.Count == 0;
    }

    private static double HouseGiftChance(IPerson parent) =>
        parent.Tags.Has("morals.good")
            ? 0.65
            : parent.Tags.Has("morals.evil")
                ? 0.35
                : 0.50;

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
                "Ask Child for Money",

            Description =
                "When broke, ask the selected wealthy adult child " +
                "for 2,000 zł. The child must have more than 10,000 zł " +
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
                    var agreementChance =
                        PersonalityInfluence.AdjustProbability(
                            SuccessChance,
                            child,
                            good: 0.20,
                            evil: -0.20);

                    var success =
                        childHousehold.Wealth
                            >= WealthThreshold
                        && random.NextDouble()
                            < agreementChance;

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
                                            $"{SupportAmount:N0} zł from their child, " +
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

    private static List<IPerson> GetLivingParents(
        IPerson actor,
        IFamilyService family)
    {
        var parents = new List<IPerson>();
        var father = family.GetFather(actor);
        var mother = family.GetMother(actor);

        if (father is not null && father.Tags.Has("state.alive"))
            parents.Add(father);
        if (mother is not null && mother.Tags.Has("state.alive"))
            parents.Add(mother);

        return parents;
    }

    private static (decimal SharePercent, double AgreementChance)
        GetParentSupportTerms(IReadOnlyList<IPerson> parents)
    {
        var share = 0m;
        var chance = 0.0;

        foreach (var parent in parents)
        {
            if (parent.Tags.Has("morals.good"))
            {
                share += 0.20m;
                chance += 0.85;
            }
            else if (parent.Tags.Has("morals.evil"))
            {
                share += 0.05m;
                chance += 0.30;
            }
            else
            {
                share += 0.10m;
                chance += 0.60;
            }
        }

        var count = Math.Max(1, parents.Count);
        return (share / count, chance / count);
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
