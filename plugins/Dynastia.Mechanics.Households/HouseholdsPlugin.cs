using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

public sealed partial class HouseholdsPlugin : IGamePlugin
{
    private const string SurnamesPath =
        "Names/polish_surnames.csv";

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

        var economy =
            context.GetService<IEconomyService>()
            ?? throw new InvalidOperationException(
                "Economy service is unavailable.");

        var economyBalance =
            context.GetService<IEconomyBalanceService>()
            ?? throw new InvalidOperationException(
                "Economy balance service is unavailable.");

        var locations =
            context.GetService<ILocationService>()
            ?? throw new InvalidOperationException(
                "Location service is unavailable.");

        var career =
            context.GetService<ICareerService>()
            ?? throw new InvalidOperationException(
                "Career service is unavailable.");

        var education =
            context.GetService<IEducationService>()
            ?? throw new InvalidOperationException(
                "Education service is unavailable.");

        var stats =
            context.GetService<IStatsService>()
            ?? throw new InvalidOperationException(
                "Stats service is unavailable.");

        var health =
            context.GetService<IHealthService>()
            ?? throw new InvalidOperationException(
                "Health service is unavailable.");

        var healthModifiers =
            context.GetService<IAnnualHealthModifierRegistry>()
            ?? throw new InvalidOperationException(
                "Annual health modifier registry is unavailable.");

        var stressModifiers =
            context.GetService<IStressModifierRegistry>()
            ?? throw new InvalidOperationException(
                "Stress modifier registry is unavailable.");

        var actions =
            context.GetService<IActionRegistry>()
            ?? throw new InvalidOperationException(
                "Action registry is unavailable.");

        var systems =
            context.GetService<IYearSystemRegistry>()
            ?? throw new InvalidOperationException(
                "Year-system registry is unavailable.");

        var events =
            context.GetService<IGameEventBus>()
            ?? throw new InvalidOperationException(
                "Game event bus is unavailable.");

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
                "Game random service is unavailable.");

        var calendar =
            context.GetService<IGameCalendar>()
            ?? throw new InvalidOperationException(
                "Game calendar service is unavailable.");

        var households =
            new StandardHouseholdService(
                gameState,
                family,
                economy,
                career,
                events);

        context.AddService<IHouseholdService>(
            households);

        var reconciliation = context.GetService<IStateReconciliationLifecycle>()
            ?? throw new InvalidOperationException(
                "State reconciliation lifecycle is unavailable.");

        reconciliation.Register(
            "households.membership_and_headship",
            [
                ReconciliationLifecycleStage.AfterNewGame,
                ReconciliationLifecycleStage.AfterLoad,
                ReconciliationLifecycleStage.BeforeYear,
                ReconciliationLifecycleStage.AfterYear,
                ReconciliationLifecycleStage.AfterImmediateAction
            ],
            _ => households.ReconcileHouseholds(),
            order: 60);

        systems.Register(
            new HouseholdReconcileYearSystem(
                households,
                "households.status_reconcile",
                YearPhase.Status,
                before:
                    Array.Empty<string>(),
                after:
                    Array.Empty<string>()));

        systems.Register(
            new HouseholdReconcileYearSystem(
                households,
                "households.inheritance_reconcile",
                YearPhase.Inheritance,
                before:
                    ["inheritance.estate_settlement"],
                after:
                    ["adoption.child_placement"]));

        systems.Register(
            new HouseholdReconcileYearSystem(
                households,
                "households.post_inheritance_reconcile",
                YearPhase.DerivedState,
                before:
                    Array.Empty<string>(),
                after:
                    Array.Empty<string>()));

        systems.Register(
            new NannyNeedReconcileYearSystem(
                households,
                economy,
                family,
                career,
                events,
                "households.nanny_need_prefinance",
                YearPhase.QueuedActionsEarly,
                before:
                    Array.Empty<string>(),
                after:
                    ["actions.queued.early"]));

        systems.Register(
            new NannyNeedReconcileYearSystem(
                households,
                economy,
                family,
                career,
                events,
                "households.nanny_need_postyear",
                YearPhase.DerivedState,
                before:
                    Array.Empty<string>(),
                after:
                    ["households.post_inheritance_reconcile"]));

        var autonomousStrategy =
            new AdvancedAutonomousHouseholdStrategy(
                context,
                gameState,
                households,
                actions,
                economy,
                health,
                career,
                family,
                stats,
                random);

        var autonomousDecisions =
            new AutonomousHouseholdDecisionService(
                gameState,
                households,
                actions,
                autonomousStrategy);

        context.AddService<IAutonomousHouseholdDecisionService>(
            autonomousDecisions);

        systems.Register(
            new AutonomousHouseholdDecisionSystem(
                autonomousDecisions));

        events.EventPublished +=
            (_, gameEvent) =>
            {
                households.RecordFamilyNewsVisibility(
                    gameEvent);

                households.UpdatePeripheralRelationshipState(
                    gameEvent);

            };

        healthModifiers.Register(
            new HouseholdHealthModifierProvider(
                households,
                family,
                stats,
                career));

        stressModifiers.Register(
            new HouseholdStressModifierProvider(
                households));

        _ =
            new FamilyNannyTracker(
                gameState,
                family,
                economy,
                career,
                events);

        RegisterPropertyActions(
            actions,
            gameState,
            family,
            households,
            economy,
            locations,
            career,
            random,
            events);

        RegisterNannyActions(
            actions,
            gameState,
            family,
            economy,
            economyBalance,
            career,
            education,
            stats,
            health,
            households,
            events,
            data,
            historicalNames,
            random,
            calendar,
            historical);

        context.Log(
            "Household mechanics registered.");
    }

}
