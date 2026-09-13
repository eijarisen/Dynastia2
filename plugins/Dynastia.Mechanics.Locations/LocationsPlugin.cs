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

        var locations =
            new StandardLocationService(
                gameState,
                family,
                data,
                random,
                events);

        context.AddService<ILocationService>(
            locations);

        context.AddService<ITownDirectoryService>(
            locations);

        context.AddService<ITownEconomyService>(
            new StandardTownEconomyService(
                locations));

        var localCareers =
            new StandardLocalCareerOpportunityService(
                locations,
                data);

        context.AddService<ILocalCareerOpportunityService>(
            localCareers);

        context.AddService<ITownCareerOpportunityService>(
            localCareers);

        context.Log(
            "Location, birthplace and local career opportunity mechanics registered.");
    }
}
