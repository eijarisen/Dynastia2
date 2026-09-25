using Dynastia.Contracts;

namespace Dynastia.Mechanics.Economy;

internal static class EventPresentationRegistration
{
    public static void Register(IGamePluginContext context)
    {
        var registry = context.GetService<IEventPresentationRegistry>();
        if (registry is null)
            return;

        registry.Register(
            "economy.careful_management",
            new EventPresentationMetadata { Emoji = "💰" },
            "dynastia.economy");

    }
}
