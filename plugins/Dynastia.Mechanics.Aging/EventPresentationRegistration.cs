using Dynastia.Contracts;

namespace Dynastia.Mechanics.Aging;

internal static class EventPresentationRegistration
{
    public static void Register(IGamePluginContext context)
    {
        var registry = context.GetService<IEventPresentationRegistry>();
        if (registry is null)
            return;

        registry.Register(
            "life.adult",
            new EventPresentationMetadata { Emoji = "🧑" },
            "dynastia.aging");

    }
}
