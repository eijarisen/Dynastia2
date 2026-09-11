using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

public sealed class RelationshipsPlugin : IGamePlugin
{
    public void Initialize(
        IGamePluginContext context)
    {
        var gameState =
            context.GetService<IGameState>()
            ?? throw new InvalidOperationException(
                "Game state is unavailable.");

        var family =
            context.GetService<IFamilyService>()
            ?? throw new InvalidOperationException(
                "Family service is unavailable.");

        var stats =
            context.GetService<IStatsService>()
            ?? throw new InvalidOperationException(
                "Stats service is unavailable.");

        var health =
            context.GetService<IHealthService>()
            ?? throw new InvalidOperationException(
                "Health service is unavailable.");

        var economy =
            context.GetService<IEconomyService>()
            ?? throw new InvalidOperationException(
                "Economy service is unavailable.");

        var education =
            context.GetService<IEducationService>()
            ?? throw new InvalidOperationException(
                "Education service is unavailable.");

        var career =
            context.GetService<ICareerService>()
            ?? throw new InvalidOperationException(
                "Career service is unavailable.");

        var households =
            context.GetService<IHouseholdService>()
            ?? throw new InvalidOperationException(
                "Household service is unavailable.");

        var data =
            context.GetService<IGameDataService>()
            ?? throw new InvalidOperationException(
                "Game data service is unavailable.");

        var random =
            context.GetService<IGameRandom>()
            ?? throw new InvalidOperationException(
                "Random service is unavailable.");

        var calendar =
            context.GetService<IGameCalendar>()
            ?? throw new InvalidOperationException(
                "Game calendar service is unavailable.");

        var events =
            context.GetService<IGameEventBus>()
            ?? throw new InvalidOperationException(
                "Game event bus is unavailable.");

        var systems =
            context.GetService<IYearSystemRegistry>()
            ?? throw new InvalidOperationException(
                "Year system registry is unavailable.");

        var actions =
            context.GetService<IActionRegistry>()
            ?? throw new InvalidOperationException(
                "Action registry is unavailable.");

        var breakups =
            new RelationshipBreakupService(
                family,
                health,
                economy,
                data,
                random,
                events);

        var marriageSatisfaction =
            new StandardMarriageSatisfactionService(
                gameState,
                family,
                events);

        context.AddService<IMarriageSatisfactionService>(
            marriageSatisfaction);

        actions.Register(
            CreateFindSpouseAction(
                family));

        actions.Register(
            CreateDivorceAction(
                family,
                breakups));

        actions.Register(
            CreateRepairMarriageAction(
                family,
                marriageSatisfaction,
                events));

        systems.Register(
            new SexualityYearSystem(
                family,
                random));

        systems.Register(
            new MarriageYearSystem(
                family,
                stats,
                data,
                random,
                calendar,
                events));

        systems.Register(
            new AffairYearSystem(
                family,
                random,
                breakups));

        systems.Register(
            new FemaleRemarriageYearSystem(
                family,
                stats,
                health,
                education,
                career,
                data,
                random,
                calendar,
                events));

        systems.Register(
            new MarriageSatisfactionYearSystem(
                marriageSatisfaction,
                family,
                stats,
                health,
                career,
                households,
                random,
                breakups));

        context.Log(
            "Relationship mechanics registered.");
    }

    private static GameActionDefinition
        CreateFindSpouseAction(
            IFamilyService family)
    {
        return new GameActionDefinition
        {
            Id =
                "relationship.find_spouse",

            Label =
                "Find a Spouse",

            Description =
                "Attempt to find a suitable spouse next year. " +
                "Success depends on Appeal.",

            Mode =
                ActionExecutionMode.Queued,

            QueuePhase =
                YearPhase.LifeEvents,

            IsAvailable =
                actionContext =>
                    actionContext.Actor.Id
                        == actionContext.Target.Id
                    && actionContext.Actor.Tags.Has(
                        "state.alive")
                    && actionContext.Actor.Tags.Has(
                        "control.playable")
                    && actionContext.Actor.Age >= 18
                    && family.GetSpouse(
                        actionContext.Actor) is null,

            Execute =
                actionContext =>
                {
                    actionContext.Actor.Tags.Add(
                        "modifier.find_spouse");

                    return new GameActionResult(
                        true);
                }
        };
    }

    private static GameActionDefinition
        CreateRepairMarriageAction(
            IFamilyService family,
            IMarriageSatisfactionService satisfaction,
            IGameEventBus events)
    {
        return new GameActionDefinition
        {
            Id =
                "relationship.repair_marriage",

            Label =
                "Repair Marriage",

            Description =
                "Spend the year working on the marriage. " +
                "This raises Marriage Satisfaction by 20 points.",

            Mode =
                ActionExecutionMode.Queued,

            QueuePhase =
                YearPhase.LifeEvents,

            IsAvailable =
                actionContext =>
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
                        || family.GetSex(
                            actor) != Sex.Male
                        || family.GetSex(
                            target) != Sex.Female)
                    {
                        return false;
                    }

                    var wife =
                        family.GetSpouse(
                            actor);

                    if (wife?.Id
                        != target.Id)
                    {
                        return false;
                    }

                    var current =
                        satisfaction.GetSatisfaction(
                            actor);

                    return current is not null
                        && current.Value < 100;
                },

            Execute =
                actionContext =>
                {
                    var actor =
                        actionContext.Actor;

                    var wife =
                        actionContext.Target;

                    if (family.GetSpouse(
                            actor)?.Id
                            != wife.Id)
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
                                        $"and {family.GetDisplayName(wife)} " +
                                        "worked on their marriage."
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
                    actionContext.Actor.Id
                        == actionContext.Target.Id
                    && actionContext.Actor.Tags.Has(
                        "state.alive")
                    && actionContext.Actor.Tags.Has(
                        "control.playable")
                    && family.GetSpouse(
                        actionContext.Actor) is not null,

            Execute =
                actionContext =>
                    breakups.PlayerDivorce(
                        actionContext.GameState,
                        actionContext.Actor)
        };
    }
}
