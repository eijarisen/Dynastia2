using Dynastia.Contracts;

namespace Dynastia.Mechanics.Church;

internal static class EventPresentationRegistration
{
    public static void Register(IGamePluginContext context)
    {
        var registry = context.GetService<IEventPresentationRegistry>();
        if (registry is null)
            return;

        registry.RegisterPrefix(
            "church.",
            new EventPresentationMetadata { Emoji = "⛪" },
            "dynastia.church");

        registry.Register(
            "church.aid_poor",
            new EventPresentationMetadata { Emoji = "🤝" },
            "dynastia.church");

        registry.Register(
            "church.attend",
            new EventPresentationMetadata { Emoji = "⛪" },
            "dynastia.church");

        registry.Register(
            "church.donate",
            new EventPresentationMetadata { Emoji = "⛪" },
            "dynastia.church");

        registry.Register(
            "church.welfare",
            new EventPresentationMetadata { Emoji = "🥖" },
            "dynastia.church");

    }
}
