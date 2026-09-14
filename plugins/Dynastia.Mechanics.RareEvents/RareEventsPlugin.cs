using Dynastia.Contracts;

namespace Dynastia.Mechanics.RareEvents;

public sealed class RareEventsPlugin :
    IGamePlugin
{
    public void Initialize(
        IGamePluginContext context)
    {
        var gameState =
            context.GetService<IGameState>()
            ?? throw new InvalidOperationException(
                "Game state is unavailable.");

        var data =
            context.GetService<IGameDataService>()
            ?? throw new InvalidOperationException(
                "Game data service is unavailable.");

        var family =
            context.GetService<IFamilyService>()
            ?? throw new InvalidOperationException(
                "Family service is unavailable.");

        var health =
            context.GetService<IHealthService>()
            ?? throw new InvalidOperationException(
                "Health service is unavailable.");

        var economy =
            context.GetService<IEconomyService>()
            ?? throw new InvalidOperationException(
                "Economy service is unavailable.");

        var career =
            context.GetService<ICareerService>()
            ?? throw new InvalidOperationException(
                "Career service is unavailable.");

        var justice =
            context.GetService<IJusticeService>()
            ?? throw new InvalidOperationException(
                "Justice service is unavailable.");

        var households =
            context.GetService<IHouseholdService>()
            ?? throw new InvalidOperationException(
                "Household service is unavailable.");

        var random =
            context.GetService<IGameRandom>()
            ?? throw new InvalidOperationException(
                "Random service is unavailable.");

        var calendar =
            context.GetService<IGameCalendar>()
            ?? throw new InvalidOperationException(
                "Calendar service is unavailable.");

        var events =
            context.GetService<IGameEventBus>()
            ?? throw new InvalidOperationException(
                "Event bus is unavailable.");

        var systems =
            context.GetService<IYearSystemRegistry>()
            ?? throw new InvalidOperationException(
                "Year system registry is unavailable.");

        var recent =
            new RecentLifeEventTracker(
                gameState,
                family,
                events);

        var death =
            new RareEventDeathService(
                family,
                health,
                economy,
                random,
                calendar,
                events);

        var availability =
            RareEventAvailabilityCatalog.Load(data);

        systems.Register(
            new RecentLifeEventCleanupYearSystem(
                recent));

        systems.Register(
            new RareEventYearSystem(
                family,
                health,
                economy,
                career,
                justice,
                households,
                random,
                events,
                recent,
                death,
                availability));

        context.Log(
            "Rare life events registered.");
    }
}
