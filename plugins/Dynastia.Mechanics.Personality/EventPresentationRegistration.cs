using Dynastia.Contracts;

namespace Dynastia.Mechanics.Personality;

internal static class EventPresentationRegistration
{
    public static void Register(IGamePluginContext context)
    {
        var registry = context.GetService<IEventPresentationRegistry>();
        if (registry is null)
            return;

        registry.RegisterPrefix(
            "personality.",
            new EventPresentationMetadata { Emoji = "🧭" },
            "dynastia.personality");

        registry.Register(
            "personality.morals_declined",
            new EventPresentationMetadata { Emoji = "⚖️" },
            "dynastia.personality");

        registry.Register(
            "personality.morals_protected",
            new EventPresentationMetadata { Emoji = "🛡️" },
            "dynastia.personality");

        registry.Register(
            "personality.religious_study",
            new EventPresentationMetadata { Emoji = "📖" },
            "dynastia.personality");

    }
}
