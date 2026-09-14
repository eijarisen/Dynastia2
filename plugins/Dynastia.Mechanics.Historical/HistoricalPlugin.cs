using Dynastia.Contracts;

namespace Dynastia.Mechanics.Historical;

public sealed class HistoricalPlugin : IGamePlugin
{
    public void Initialize(IGamePluginContext context)
    {
        var data = context.GetService<IGameDataService>()
            ?? throw new InvalidOperationException(
                "Game data service is unavailable.");

        context.AddService<IHistoricalActionVariantService>(
            HistoricalActionVariantService.Load(data));

        context.Log("Historical action data registered.");
    }
}
