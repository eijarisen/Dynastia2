using Dynastia.Contracts;

namespace Dynastia.Mechanics.Hobbies;

public sealed class HobbiesPlugin :
    IGamePlugin
{
    public void Initialize(
        IGamePluginContext context)
    {
        var gameState = Require<IGameState>(context, "Game state");
        var family = Require<IFamilyService>(context, "Family service");
        var households = Require<IHouseholdService>(context, "Household service");
        var locations = Require<IExistingLocationService>(context, "Existing-location lookup");
        var personality = Require<IPersonalityService>(context, "Personality service");
        var stats = Require<IStatsService>(context, "Stats service");
        var data = Require<IGameDataService>(context, "Game data service");
        var contextWeights = Require<IContextWeightService>(context, "Context-weight service");
        var events = Require<IGameEventBus>(context, "Event bus");
        var systems = Require<IYearSystemRegistry>(context, "Year-system registry");
        var thoughtProviders = Require<IThoughtProviderRegistry>(context, "Thought provider registry");

        var hobbies = new StandardHobbyService(
            gameState,
            family,
            households,
            locations,
            personality,
            stats,
            data,
            contextWeights);

        context.AddService<IHobbyService>(hobbies);

        thoughtProviders.Register(
            new HobbyThoughtProvider(hobbies));

        systems.Register(
            new HobbyYearSystem(hobbies));

        events.EventPublished += (_, gameEvent) =>
        {
            if (gameEvent.Type.Equals(
                    "game.started",
                    StringComparison.OrdinalIgnoreCase))
            {
                hobbies.ReconcileAll();
            }
        };

        hobbies.ReconcileAll();

        context.Log(
            "Flavor-only hobbies and pastime thoughts registered.");
    }

    private static T Require<T>(
        IGamePluginContext context,
        string name)
        where T : class =>
        context.GetService<T>()
        ?? throw new InvalidOperationException(
            $"{name} is unavailable.");
}
