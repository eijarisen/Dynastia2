using Dynastia.Contracts;

namespace Dynastia.Mechanics.Mortality;

internal static class EventPresentationRegistration
{
    public static void Register(IGamePluginContext context)
    {
        var registry = context.GetService<IEventPresentationRegistry>();
        if (registry is null)
            return;

        registry.Register(
            "life.death",
            new EventPresentationMetadata { Emoji = "💀" },
            "dynastia.mortality");

    }
}
