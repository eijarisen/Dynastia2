using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

public sealed partial class RelationshipsPlugin : IGamePlugin
{
    private const string SurnamesPath =
        "Names/polish_surnames.csv";

    private static readonly double[]
        ArrangedMarriageChanceByAppeal =
            [0, 0.25, 0.35, 0.50, 0.60, 0.80];

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

        var historical =
            context.GetService<IHistoricalActionVariantService>()
            ?? throw new InvalidOperationException(
                "Historical action variant service is unavailable.");

        var historicalNames =
            context.GetService<IHistoricalNameService>()
            ?? throw new InvalidOperationException(
                "Historical name service is unavailable.");

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

        var relationshipEras =
            StandardRelationshipEraService.Load(
                data);

        var relationshipEventVariants =
            RelationshipEventVariantCatalog.Load(
                data);

        context.AddService<IRelationshipEraService>(
            relationshipEras);

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
                stats,
                events);

        context.AddService<IMarriageSatisfactionService>(
            marriageSatisfaction);

        _ =
            new DivorcedParentsTracker(
                gameState,
                family,
                health,
                events);

        systems.Register(
            new DivorcedParentsStateYearSystem(
                family,
                health));

        actions.RegisterDynamicProvider(
            (_, _) =>
                [
                    CreateFindSpouseAction(
                        family,
                        RequireHistoricalVariant(
                            historical,
                            "relationship.find_spouse",
                            gameState.Year))
                ]);

        actions.RegisterDynamicProvider(
            (_, _) =>
                [
                    CreateMarryOffDaughterAction(
                        family,
                        households,
                        stats,
                        health,
                        education,
                        career,
                        data,
                        historicalNames,
                        random,
                        calendar,
                        events,
                        relationshipEras,
                        RequireHistoricalVariant(
                            historical,
                            "relationship.marry_off_daughter",
                            gameState.Year))
                ]);

        actions.Register(
            CreateDivorceAction(
                family,
                breakups));

        actions.RegisterDynamicProvider(
            (_, _) =>
                [
                    CreateRepairMarriageAction(
                        family,
                        marriageSatisfaction,
                        events,
                        RequireHistoricalVariant(
                            historical,
                            "relationship.repair_marriage",
                            gameState.Year))
                ]);

        systems.Register(
            new SexualityYearSystem(
                family,
                random));

        systems.Register(
            new MarriageYearSystem(
                family,
                stats,
                career,
                data,
                historicalNames,
                random,
                calendar,
                events,
                relationshipEras,
                relationshipEventVariants));

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
                historicalNames,
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
                households));

        systems.Register(
            new MarriageDivorceYearSystem(
                marriageSatisfaction,
                family,
                stats,
                random,
                breakups,
                relationshipEras));

        context.Log(
            "Relationship mechanics registered.");
    }

    private static GameActionDefinition
        CreateFindSpouseAction(
            IFamilyService family,
            HistoricalActionVariant variant)
    {
        return new GameActionDefinition
        {
            Id =
                "relationship.find_spouse",

            Label =
                variant.Label,

            Description =
                variant.Description,

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
        CreateMarryOffDaughterAction(
            IFamilyService family,
            IHouseholdService households,
            IStatsService stats,
            IHealthService health,
            IEducationService education,
            ICareerService career,
            IGameDataService data,
            IHistoricalNameService historicalNames,
            IGameRandom random,
            IGameCalendar calendar,
            IGameEventBus events,
            IRelationshipEraService relationshipEras,
            HistoricalActionVariant variant)
    {
        return new GameActionDefinition
        {
            Id =
                "relationship.marry_off_daughter",

            Label =
                variant.Label,

            Description =
                variant.Description,

            Mode =
                ActionExecutionMode.Queued,

            QueuePhase =
                YearPhase.LifeEvents,

            IsAvailable =
                actionContext =>
                    IsEligibleDaughter(
                        actionContext.Actor,
                        actionContext.Target,
                        family,
                        households),

            Execute =
                actionContext =>
                {
                    var father =
                        actionContext.Actor;

                    var daughter =
                        actionContext.Target;

                    if (!IsEligibleDaughter(
                        father,
                        daughter,
                        family,
                        households))
                    {
                        return new GameActionResult(
                            false,
                            "The selected daughter is no longer eligible.");
                    }

                    var appeal =
                        stats.GetStats(
                            daughter)
                        .First(
                            stat =>
                                stat.Id.Equals(
                                    "appeal",
                                    StringComparison.OrdinalIgnoreCase))
                        .Value;

                    var chance =
                        relationshipEras
                            .GetRule(
                                actionContext.GameState.Year)
                            .ApplyArrangedMarriageChance(
                                ArrangedMarriageChanceByAppeal[
                                    Math.Clamp(
                                        appeal,
                                        1,
                                        5)]);

                    if (random.NextDouble()
                        >= chance)
                    {
                        events.Publish(
                            new GameEvent
                            {
                                Type =
                                    "relationship.marry_off_failed",

                                Year =
                                    actionContext.GameState.Year,

                                SubjectId =
                                    father.Id,

                                RelatedPersonIds =
                                    [daughter.Id],

                                Data =
                                    new Dictionary<string, string>
                                    {
                                        ["appeal"] =
                                            appeal.ToString(),

                                        ["chance"] =
                                            chance.ToString(
                                                "0.00"),

                                        ["text"] =
                                            $"{family.GetDisplayName(father)} " +
                                            $"{variant.Narrative}, but no suitable match " +
                                            $"was found for {family.GetDisplayName(daughter)}."
                                    }
                            });

                        return new GameActionResult(
                            true);
                    }

                    CreateArrangedHusband(
                        actionContext.GameState,
                        father,
                        daughter,
                        family,
                        stats,
                        health,
                        education,
                        career,
                        data,
                        historicalNames,
                        random,
                        calendar,
                        events,
                        chance,
                        variant);

                    return new GameActionResult(
                        true);
                }
        };
    }

    private static HistoricalActionVariant RequireHistoricalVariant(
        IHistoricalActionVariantService historical,
        string actionId,
        int year)
    {
        return historical.GetVariant(actionId, year)
            ?? throw new InvalidDataException(
                $"Missing historical action data for '{actionId}' in {year}.");
    }

}
