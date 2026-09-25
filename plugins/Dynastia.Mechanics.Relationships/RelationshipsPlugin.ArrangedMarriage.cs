using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

public sealed partial class RelationshipsPlugin
{
    private static bool IsEligibleDaughter(
        IPerson father,
        IPerson daughter,
        bool fatherHasControl,
        IFamilyService family,
        IHouseholdService households)
    {
        if (!father.Tags.Has(
                "state.alive")
            || !fatherHasControl
            || !daughter.Tags.Has(
                "state.alive")
            || daughter.Tags.Has(
                "state.dead")
            || daughter.Id == father.Id
            || daughter.Age < 18
            || daughter.Tags.Has("vocation.religious.active")
            || family.GetSex(
                daughter) != Sex.Female
            || family.GetSpouse(
                daughter) is not null
            || !RelationshipPersonalityRules.CanFindPartner(
                daughter,
                Sex.Male))
        {
            return false;
        }

        return IsSameOrLowerGeneration(
                father,
                daughter,
                family)
            && households
                .ResolveHouseholdHead(
                    daughter)?
                .Id
                == father.Id;
    }

    private static bool IsEligibleSon(
        IPerson father,
        IPerson son,
        bool fatherHasControl,
        IFamilyService family,
        IHouseholdService households)
    {
        if (!father.Tags.Has(
                "state.alive")
            || !fatherHasControl
            || !son.Tags.Has(
                "state.alive")
            || son.Tags.Has(
                "state.dead")
            || son.Tags.Has(
                "state.imprisoned")
            || son.Id == father.Id
            || son.Age < 18
            || son.Tags.Has("vocation.religious.active")
            || family.GetSex(son) != Sex.Male
            || family.GetSpouse(son) is not null
            || !RelationshipPersonalityRules.CanFindPartner(
                son,
                Sex.Female))
        {
            return false;
        }

        return IsSameOrLowerGeneration(
                father,
                son,
                family)
            && households.ResolveHouseholdHead(son)?.Id == father.Id;
    }


    private static bool IsSameOrLowerGeneration(
        IPerson controller,
        IPerson target,
        IFamilyService family)
    {
        if (family.GetFather(controller)?.Id == target.Id
            || family.GetMother(controller)?.Id == target.Id)
        {
            return false;
        }

        var controllerGeneration =
            family.GetGeneration(controller);

        var targetGeneration =
            family.GetGeneration(target);

        return controllerGeneration is int activeGeneration
            && targetGeneration is int selectedGeneration
            && selectedGeneration >= activeGeneration;
    }

    private static GameActionDefinition
        CreateRepairMarriageAction(
            IFamilyService family,
            IMarriageSatisfactionService satisfaction,
            IGameEventBus events,
            HistoricalActionVariant variant)
    {
        return new GameActionDefinition
        {
            Id =
                "relationship.repair_marriage",
            Presentation = new()
            {
                Emoji = "❤️‍🩹",
                Categories = [ActionPresentationCategories.Family],
                AdjacencyGroup = ActionPresentationGroups.MarriageFamily,
                GroupOrder = 20
            },

            Label =
                variant.Label,

            Description =
                variant.Description,

            Mode =
                ActionExecutionMode.Queued,

            QueuePhase =
                YearPhase.MarriageRepair,

            IsAvailable =
                actionContext =>
                {
                    var actor =
                        actionContext.Actor;

                    var target =
                        actionContext.Target;

                    if (!actor.Tags.Has(
                            "state.alive")
                        || !actionContext.ActorHasControl
                        || family.GetSex(
                            actor) != Sex.Male)
                    {
                        return false;
                    }

                    var wife =
                        family.GetSpouse(
                            actor);

                    if (wife is null
                        || !wife.Tags.Has(
                            "state.alive")
                        || (target.Id != actor.Id
                            && target.Id != wife.Id))
                    {
                        return false;
                    }

                    var current =
                        satisfaction.GetSatisfaction(
                            actor);

                    return current is not null
                        && current.Value < 80;
                },

            Execute =
                actionContext =>
                {
                    var actor =
                        actionContext.Actor;

                    var wife =
                        family.GetSpouse(
                            actor);

                    if (wife is null
                        || !wife.Tags.Has(
                            "state.alive"))
                    {
                        return new GameActionResult(
                            false,
                            "The marriage no longer exists.");
                    }

                    satisfaction.ChangeSatisfaction(
                        actor,
                        20);

                    var updated =
                        satisfaction.GetSatisfaction(
                            actor);

                    events.Publish(
                        new GameEvent
                        {
                            Type =
                                "relationship.repair_marriage",

                            Year =
                                actionContext.GameState.Year,

                            SubjectId =
                                actor.Id,

                            RelatedPersonIds =
                                [wife.Id],

                            Data =
                                new Dictionary<string, string>
                                {
                                    ["satisfaction"] =
                                        updated?.Value
                                            .ToString(
                                                "0")
                                        ?? string.Empty,

                                    ["text"] =
                                        $"{family.GetDisplayName(actor)} " +
                                        $"{variant.Narrative}."
                                }
                        });

                    return new GameActionResult(
                        true);
                }
        };
    }

    private static GameActionDefinition
        CreateDivorceAction(
            IFamilyService family,
            RelationshipBreakupService breakups)
    {
        return new GameActionDefinition
        {
            Id =
                "relationship.divorce_spouse",
            Presentation = new()
            {
                Emoji = "💔",
                Categories = [ActionPresentationCategories.Family],
                AdjacencyGroup = ActionPresentationGroups.MarriageFamily,
                GroupOrder = 30
            },

            Label =
                "Divorce the Spouse",

            Description =
                "End the current marriage. Household wealth is halved " +
                "and the health of the family is harmed.",

            Mode =
                ActionExecutionMode.Queued,

            QueuePhase =
                YearPhase.LifeEvents,

            IsAvailable =
                actionContext =>
                {
                    var actor =
                        actionContext.Actor;

                    var spouse =
                        family.GetSpouse(
                            actor);

                    return actor.Tags.Has(
                            "state.alive")
                        && actionContext.ActorHasControl
                        && spouse is not null
                        && spouse.Tags.Has(
                            "state.alive")
                        && (actionContext.Target.Id == actor.Id
                            || actionContext.Target.Id == spouse.Id);
                },

            Execute =
                actionContext =>
                    breakups.PlayerDivorce(
                        actionContext.GameState,
                        actionContext.Actor)
        };
    }


}
