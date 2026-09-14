using Dynastia.Contracts;

namespace Dynastia.Mechanics.Family;

public sealed class FamilyPlugin : IGamePlugin
{
    public void Initialize(
        IGamePluginContext context)
    {
        var gameState =
            context.GetService<IGameState>()
            ?? throw new InvalidOperationException(
                "Game state is unavailable.");

        var selection =
            context.GetService<ISelectionService>()
            ?? throw new InvalidOperationException(
                "Selection service is unavailable.");

        var data =
            context.GetService<IGameDataService>()
            ?? throw new InvalidOperationException(
                "Game data service is unavailable.");

        var random =
            context.GetService<IGameRandom>()
            ?? throw new InvalidOperationException(
                "Game random service is unavailable.");

        var calendar =
            context.GetService<IGameCalendar>()
            ?? throw new InvalidOperationException(
                "Game calendar service is unavailable.");

        var events =
            context.GetService<IGameEventBus>()
            ?? throw new InvalidOperationException(
                "Game event bus is unavailable.");

        var historicalNames =
            StandardHistoricalNameService.Load(
                data);

        var familyService =
            new StandardFamilyService(
                gameState,
                data,
                historicalNames);

        var newGameService =
            new StandardNewGameService(
                gameState,
                familyService,
                selection,
                data,
                historicalNames,
                random,
                calendar,
                events);

        context.AddService<IHistoricalNameService>(
            historicalNames);

        context.AddService<IFamilyService>(
            familyService);

        context.AddService<INewGameService>(
            newGameService);

        context.Log(
            "Family mechanics registered.");
    }
}
