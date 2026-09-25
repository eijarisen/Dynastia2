using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

public sealed partial class EconomyPlugin : IGamePlugin
{
    private const decimal FounderStartingWealth =
        1000m;

    public void Initialize(
        IGamePluginContext context)
    {
        EventPresentationRegistration.Register(context);
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

        var random =
            context.GetService<IGameRandom>()
            ?? throw new InvalidOperationException(
                "Random service is unavailable.");

        var locations =
            context.GetService<ILocationService>()
            ?? throw new InvalidOperationException(
                "Location service is unavailable.");

        var events =
            context.GetService<IGameEventBus>()
            ?? throw new InvalidOperationException(
                "Game event bus is unavailable.");

        var data =
            context.GetService<IGameDataService>()
            ?? throw new InvalidOperationException(
                "Game data service is unavailable.");

        var prosperity =
            context.GetService<ITownProsperityService>()
            ?? throw new InvalidOperationException(
                "Town prosperity service is unavailable.");

        var localServiceTowns =
            context.GetService<ILocalServiceTownResolver>()
            ?? throw new InvalidOperationException(
                "Local service town resolver is unavailable.");

        var systems =
            context.GetService<IYearSystemRegistry>()
            ?? throw new InvalidOperationException(
                "Year system registry is unavailable.");

        var actions =
            context.GetService<IActionRegistry>()
            ?? throw new InvalidOperationException(
                "Action registry is unavailable.");

        var incomeRegistry =
            new IncomeProviderRegistry();

        var householdIncomeRegistry =
            new HouseholdIncomeProviderRegistry();

        var financeProjectionRegistry =
            new HouseholdFinanceProjectionProviderRegistry();

        var houseMarketRules =
            HouseMarketRules.Load(data);

        var economy =
            new StandardEconomyService(
                gameState,
                family,
                locations,
                incomeRegistry,
                householdIncomeRegistry,
                financeProjectionRegistry,
                stats,
                random,
                prosperity,
                houseMarketRules,
                localServiceTowns,
                () => context.GetService<ICommunityPolicyService>());

        var houseMarket =
            new StandardHouseMarketService(
                gameState,
                locations,
                economy,
                prosperity,
                houseMarketRules,
                localServiceTowns,
                () => context.GetService<ICommunityPolicyService>());

        context.AddService<IIncomeProviderRegistry>(
            incomeRegistry);

        context.AddService<IHouseholdIncomeProviderRegistry>(
            householdIncomeRegistry);

        context.AddService<IHouseholdFinanceProjectionProviderRegistry>(
            financeProjectionRegistry);

        context.AddService<IEconomyService>(
            economy);

        context.AddService<IHouseMarketService>(
            houseMarket);

        context.AddService<IEconomyBalanceService>(
            economy);

        context.AddService<IHouseholdCapacityService>(
            economy);

        var reconciliation = context.GetService<IStateReconciliationLifecycle>()
            ?? throw new InvalidOperationException(
                "State reconciliation lifecycle is unavailable.");

        reconciliation.Register(
            "economy.households_and_assets",
            [
                ReconciliationLifecycleStage.AfterNewGame,
                ReconciliationLifecycleStage.AfterLoad,
                ReconciliationLifecycleStage.BeforeYear,
                ReconciliationLifecycleStage.AfterYear,
                ReconciliationLifecycleStage.AfterImmediateAction,
                ReconciliationLifecycleStage.AfterQueuedAction,
                ReconciliationLifecycleStage.AfterPersonCreated
            ],
            _ => economy.ReconcileState(),
            order: 55);

        systems.Register(
            new EconomyYearSystem(
                economy,
                family,
                events));

        RegisterLifestyleActions(
            actions,
            economy,
            family,
            events);

        events.EventPublished +=
            (_, gameEvent) =>
            {
                if (gameEvent.Type.Equals(
                        "life.death",
                        StringComparison.OrdinalIgnoreCase)
                    && gameEvent.SubjectId is Guid deceasedId)
                {
                    economy.ClearHouseInheritanceAssignments(
                        deceasedId);
                }

                if (!gameEvent.Type.Equals(
                    "game.started",
                    StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (gameEvent.SubjectId
                    is not Guid founderId)
                {
                    return;
                }

                var founder =
                    gameState.People
                        .FirstOrDefault(
                            person =>
                                person.Id
                                == founderId);

                if (founder is null)
                    return;

                economy.EnsureHousehold(
                    founder);

                economy.SetWealth(
                    founder,
                    FounderStartingWealth);

                // The starting residence is located in the founder's
                // already-generated household town.
                economy.AddHouse(
                    founder,
                    locations
                        .GetLocation(
                            founder)
                        .HomeTown);
            };

        context.Log(
            "Economy mechanics registered.");
    }
}
