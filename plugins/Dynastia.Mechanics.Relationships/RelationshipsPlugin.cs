using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

public sealed partial class RelationshipsPlugin : IGamePlugin
{
    public void Initialize(
        IGamePluginContext context)
    {
        EventPresentationRegistration.Register(context);
        var gameState =
            context.GetService<IGameState>()
            ?? throw new InvalidOperationException(
                "Game state is unavailable.");

        var personLookup =
            context.GetService<IPersonLookup>()
            ?? gameState as IPersonLookup;

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

        var farming =
            context.GetService<IFarmingService>()
            ?? throw new InvalidOperationException(
                "Farming service is unavailable.");

        var education =
            context.GetService<IEducationService>()
            ?? throw new InvalidOperationException(
                "Education service is unavailable.");

        var career =
            context.GetService<ICareerService>()
            ?? throw new InvalidOperationException(
                "Career service is unavailable.");

        var crafts =
            context.GetService<ICraftService>()
            ?? throw new InvalidOperationException(
                "Craft service is unavailable.");

        var personality =
            context.GetService<IPersonalityService>()
            ?? throw new InvalidOperationException(
                "Personality service is unavailable.");

        var appearance =
            context.GetService<IAppearanceService>()
            ?? throw new InvalidOperationException(
                "Appearance service is unavailable.");

        var locations =
            context.GetService<ILocationService>()
            ?? throw new InvalidOperationException(
                "Location service is unavailable.");

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

        var nationalities =
            context.GetService<INationalityService>()
            ?? throw new InvalidOperationException(
                "Nationality service is unavailable.");

        var outsiderIdentities =
            context.GetService<IOutsiderIdentityService>()
            ?? throw new InvalidOperationException(
                "Outsider identity service is unavailable.");

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

        var partnerSearch =
            new StandardPartnerSearchService(
                gameState,
                family,
                stats,
                education,
                career,
                crafts,
                economy,
                farming,
                personality,
                appearance,
                () => context.GetService<IHobbyService>(),
                locations,
                nationalities,
                outsiderIdentities,
                historicalNames,
                calendar,
                random,
                events,
                relationshipEventVariants,
                () => context.GetService<IStatusService>());

        context.AddService<IRelationshipEraService>(
            relationshipEras);

        context.AddService<IPartnerSearchService>(
            partnerSearch);

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
                personLookup,
                family,
                stats,
                events);

        context.AddService<IMarriageSatisfactionService>(
            marriageSatisfaction);

        context.GetService<IStateReconciliationLifecycle>()?
            .Register(
                "relationships.marriage_satisfaction",
                Enum.GetValues<ReconciliationLifecycleStage>(),
                _ => marriageSatisfaction.ReconcileAll(),
                order: 85);

        var stressModifiers =
            context.GetService<IStressModifierRegistry>()
            ?? throw new InvalidOperationException(
                "Stress modifier registry is unavailable.");

        stressModifiers.Register(
            new RelationshipStressModifierProvider(
                marriageSatisfaction));

        _ =
            new DivorcedParentsTracker(
                gameState,
                family,
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
                        partnerSearch,
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
                        partnerSearch,
                        RequireHistoricalVariant(
                            historical,
                            "relationship.marry_off_daughter",
                            gameState.Year))
                ]);

        actions.RegisterDynamicProvider(
            (_, _) =>
                [
                    CreateMarryOffSonAction(
                        family,
                        households,
                        partnerSearch,
                        RequireHistoricalVariant(
                            historical,
                            "relationship.marry_off_son",
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
                locations,
                nationalities,
                outsiderIdentities,
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
                marriageSatisfaction,
                events));

        systems.Register(
            new FemaleRemarriageYearSystem(
                family,
                stats,
                health,
                education,
                career,
                locations,
                nationalities,
                outsiderIdentities,
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
                households,
                economy,
                farming,
                personality,
                () => context.GetService<IFamilyRelationService>()));

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
            StandardPartnerSearchService partnerSearch,
            HistoricalActionVariant variant)
    {
        return new GameActionDefinition
        {
            Id =
                "relationship.find_spouse",
            Presentation = new()
            {
                Emoji = "💍",
                Categories = [ActionPresentationCategories.Family]
            },

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
                {
                    var partnerSex =
                        actionContext.Actor.Tags.Has(
                            "sexuality.homosexual")
                            ? Sex.Male
                            : Sex.Female;

                    return actionContext.Actor.Id
                            == actionContext.Target.Id
                        && actionContext.Actor.Tags.Has(
                            "state.alive")
                        && actionContext.ActorHasControl
                        && actionContext.Actor.Age >= 18
                        && !actionContext.Actor.Tags.Has("vocation.religious.active")
                        && family.GetSpouse(
                            actionContext.Actor) is null
                        && RelationshipPersonalityRules.CanFindPartner(
                            actionContext.Actor,
                            partnerSex);
                },

            Execute =
                actionContext =>
                {
                    if (actionContext.Parameters.ContainsKey(
                        "partner.candidateKey"))
                    {
                        return partnerSearch.ResolveCourtship(
                            actionContext);
                    }

                    // Compatibility for a spouse search that was already
                    // queued before candidate selection was introduced.
                    if (ActionCompatibilityParameters.IsRestoredQueuedAction(
                            actionContext.Parameters))
                    {
                        actionContext.Actor.Tags.Add(
                            "modifier.find_spouse");

                        return new GameActionResult(true);
                    }

                    return new GameActionResult(false);
                }
        };
    }

    private static GameActionDefinition
        CreateMarryOffDaughterAction(
            IFamilyService family,
            IHouseholdService households,
            IPartnerSearchService partnerSearch,
            HistoricalActionVariant variant)
    {
        return new GameActionDefinition
        {
            Id =
                "relationship.marry_off_daughter",
            Presentation = new()
            {
                Emoji = "💒",
                Categories = [ActionPresentationCategories.Family]
            },

            Label =
                variant.EndYear == 1945
                    ? "Arrange Marriage"
                    : "Help Find a Spouse",

            Description =
                "Help the selected unmarried adult relative living in this household find a suitable spouse.",

            Mode =
                ActionExecutionMode.Queued,

            QueuePhase =
                YearPhase.LifeEvents,

            IsAvailable =
                actionContext =>
                    IsEligibleDaughter(
                        actionContext.Actor,
                        actionContext.Target,
                        actionContext.ActorHasControl,
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
                        actionContext.ActorHasControl,
                        family,
                        households))
                    {
                        return new GameActionResult(
                            false,
                            "The selected relative is no longer eligible for an arranged marriage.");
                    }

                    if (!actionContext.Parameters.ContainsKey(
                            "partner.candidateKey"))
                    {
                        return new GameActionResult(
                            false,
                            "No arranged-marriage candidate was selected.");
                    }

                    return partnerSearch.ResolveArrangedMarriage(
                        actionContext,
                        variant);
                }
        };
    }

    private static GameActionDefinition
        CreateMarryOffSonAction(
            IFamilyService family,
            IHouseholdService households,
            IPartnerSearchService partnerSearch,
            HistoricalActionVariant variant)
    {
        return new GameActionDefinition
        {
            Id =
                "relationship.marry_off_son",
            Presentation = new()
            {
                Emoji = "💒",
                Categories = [ActionPresentationCategories.Family]
            },

            Label =
                variant.EndYear == 1945
                    ? "Arrange Marriage"
                    : "Help Find a Spouse",

            Description =
                "Help the selected unmarried adult relative living in this household find a suitable spouse.",

            Mode =
                ActionExecutionMode.Queued,

            QueuePhase =
                YearPhase.LifeEvents,

            IsAvailable =
                actionContext =>
                    IsEligibleSon(
                        actionContext.Actor,
                        actionContext.Target,
                        actionContext.ActorHasControl,
                        family,
                        households),

            Execute =
                actionContext =>
                {
                    if (!IsEligibleSon(
                            actionContext.Actor,
                            actionContext.Target,
                            actionContext.ActorHasControl,
                            family,
                            households))
                    {
                        return new GameActionResult(
                            false,
                            "The selected relative is no longer eligible for an arranged marriage.");
                    }

                    if (!actionContext.Parameters.ContainsKey(
                            "partner.candidateKey"))
                    {
                        return new GameActionResult(
                            false,
                            "A proposed spouse must be selected.");
                    }

                    return partnerSearch.ResolveArrangedMarriage(
                        actionContext,
                        variant);
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
