using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

public sealed class EconomyPlugin : IGamePlugin
{
    private const decimal FounderStartingWealth =
        1000m;

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

        var systems =
            context.GetService<IYearSystemRegistry>()
            ?? throw new InvalidOperationException(
                "Year system registry is unavailable.");

        var incomeRegistry =
            new IncomeProviderRegistry();

        var householdIncomeRegistry =
            new HouseholdIncomeProviderRegistry();

        var economy =
            new StandardEconomyService(
                gameState,
                family,
                locations,
                incomeRegistry,
                householdIncomeRegistry);

        context.AddService<IIncomeProviderRegistry>(
            incomeRegistry);

        context.AddService<IHouseholdIncomeProviderRegistry>(
            householdIncomeRegistry);

        context.AddService<IEconomyService>(
            economy);

        context.AddService<IEconomyBalanceService>(
            economy);

        systems.Register(
            new EconomyYearSystem(
                economy,
                family,
                stats,
                incomeRegistry,
                householdIncomeRegistry,
                random,
                events,
                locations));

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
