using Dynastia.Contracts;

namespace Dynastia.Mechanics.Locations;

public sealed class LocationsPlugin :
    IGamePlugin
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

        var data =
            context.GetService<IGameDataService>()
            ?? throw new InvalidOperationException(
                "Game data service is unavailable.");

        var random =
            context.GetService<IGameRandom>()
            ?? throw new InvalidOperationException(
                "Game random service is unavailable.");

        var events =
            context.GetService<IGameEventBus>()
            ?? throw new InvalidOperationException(
                "Game event bus is unavailable.");

        var historicalTowns =
            HistoricalTownCatalog.Load(
                data);

        context.AddService<IHistoricalTownCatalog>(
            historicalTowns);

        var localServiceTowns =
            new StandardLocalServiceTownResolver(
                historicalTowns,
                gameState);

        context.AddService<ILocalServiceTownResolver>(
            localServiceTowns);

        var locations =
            new StandardLocationService(
                gameState,
                family,
                historicalTowns,
                random,
                events);

        context.AddService<ILocationService>(
            locations);

        context.AddService<IExistingLocationService>(
            locations);

        context.GetService<IStateReconciliationLifecycle>()?
            .Register(
                "locations.components",
                Enum.GetValues<ReconciliationLifecycleStage>(),
                _ => locations.ReconcileAll(),
                order: 15);

        var localCareers =
            new StandardLocalCareerOpportunityService(
                gameState,
                locations,
                historicalTowns,
                data,
                localServiceTowns);

        context.AddService<ILocalCareerOpportunityService>(
            localCareers);

        context.Log(
            $"Historical town catalog loaded: {historicalTowns.GetAvailableTowns(gameState.Year).Count} destinations for {gameState.Year}; " +
            "location, birthplace and local career opportunity mechanics registered.");
    }
}
