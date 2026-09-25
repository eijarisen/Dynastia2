using Dynastia.Contracts;

namespace Dynastia.Mechanics.Historical;

public sealed class HistoricalPlugin : IGamePlugin
{
    public void Initialize(IGamePluginContext context)
    {
        EventPresentationRegistration.Register(context);
        var data = context.GetService<IGameDataService>()
            ?? throw new InvalidOperationException("Game data service is unavailable.");
        var towns = context.GetService<IHistoricalTownCatalog>()
            ?? throw new InvalidOperationException("Historical town catalog is unavailable.");
        var nationalities = context.GetService<INationalityService>()
            ?? throw new InvalidOperationException("Nationality service is unavailable.");
        var systems = context.GetService<IYearSystemRegistry>()
            ?? throw new InvalidOperationException("Year system registry is unavailable.");

        context.AddService<IHistoricalActionVariantService>(
            HistoricalActionVariantService.Load(data));

        var catalog = HistoricalEventCatalog.Load(data, towns, nationalities);
        var historicalEvents = new HistoricalEventService(catalog, towns);
        context.AddService<IHistoricalEventService>(historicalEvents);
        nationalities.RegisterDistributionModifierProvider(historicalEvents);
        systems.Register(new HistoricalEventYearSystem(context, historicalEvents));

        context.Log($"Historical data registered: {catalog.Events.Count} scheduled events and {catalog.Periods.Count} period labels.");
    }
}
