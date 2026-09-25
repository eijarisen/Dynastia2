using Dynastia.Contracts;

namespace Dynastia.Mechanics.Family;

internal static class EventPresentationRegistration
{
    public static void Register(IGamePluginContext context)
    {
        var registry = context.GetService<IEventPresentationRegistry>();
        if (registry is null)
            return;

        registry.RegisterPrefix(
            "family.",
            new EventPresentationMetadata { Emoji = "👪" },
            "dynastia.family");

        registry.Register(
            "game.started",
            new EventPresentationMetadata { Emoji = "🏰" },
            "dynastia.family");

    }
}
