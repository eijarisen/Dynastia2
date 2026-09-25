using Dynastia.Contracts;

namespace Dynastia.Mechanics.Childhood;

internal static class EventPresentationRegistration
{
    public static void Register(IGamePluginContext context)
    {
        var registry = context.GetService<IEventPresentationRegistry>();
        if (registry is null)
            return;

        registry.Register(
            "childhood.raised",
            new EventPresentationMetadata { Emoji = "🫂" },
            "dynastia.childhood");

    }
}
