using Dynastia.Contracts;

namespace Dynastia.Mechanics.Inheritance;

public sealed class InheritancePlugin : IGamePlugin
{
    public void Initialize(
        IGamePluginContext context)
    {
        var family =
            context.GetService<IFamilyService>()
            ?? throw new InvalidOperationException(
                "Family service is unavailable.");

        var economy =
            context.GetService<IEconomyService>()
            ?? throw new InvalidOperationException(
                "Economy service is unavailable.");

        var heirlooms =
            context.GetService<IHeirloomService>()
            ?? throw new InvalidOperationException(
                "Heirloom service is unavailable.");

        var events =
            context.GetService<IGameEventBus>()
            ?? throw new InvalidOperationException(
                "Game event bus is unavailable.");

        var systems =
            context.GetService<IYearSystemRegistry>()
            ?? throw new InvalidOperationException(
                "Year system registry is unavailable.");

        systems.Register(
            new AdulthoodInheritanceSystem(
                family,
                economy,
                heirlooms,
                events));

        systems.Register(
            new EstateInheritanceSystem(
                family,
                economy,
                heirlooms,
                events));

        context.Log(
            "Inheritance mechanics registered.");
    }
}
