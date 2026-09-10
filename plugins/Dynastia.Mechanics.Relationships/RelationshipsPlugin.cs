using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

public sealed class RelationshipsPlugin : IGamePlugin
{
    public void Initialize(
        IGamePluginContext context)
    {
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

        actions.Register(
            CreateFindSpouseAction(
                family));

        actions.Register(
            CreateDivorceAction(
                family,
                breakups));

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
