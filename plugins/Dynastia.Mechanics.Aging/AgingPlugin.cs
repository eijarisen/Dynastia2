using Dynastia.Contracts;

namespace Dynastia.Mechanics.Aging;

public sealed class AgingPlugin : IGamePlugin
{
    public void Initialize(
        IGamePluginContext context)
    {
        EventPresentationRegistration.Register(context);
        var registry =
            context.GetService<IYearSystemRegistry>()
            ?? throw new InvalidOperationException(
                "Year system registry is unavailable.");

        var family =
            context.GetService<IFamilyService>()
            ?? throw new InvalidOperationException(
                "Family service is unavailable.");

        var events =
            context.GetService<IGameEventBus>()
            ?? throw new InvalidOperationException(
                "Game event bus is unavailable.");

        registry.Register(
            new AgingSystem(
                family,
                events));

        context.Log(
            "Aging mechanics registered.");
    }
}
