using Dynastia.Contracts;

namespace Dynastia.Mechanics.Community;

internal static class EventPresentationRegistration
{
    public static void Register(IGamePluginContext context)
    {
        var registry = context.GetService<IEventPresentationRegistry>();
        if (registry is null)
            return;

        registry.RegisterPrefix(
            "community.",
            new EventPresentationMetadata { Emoji = "🏛️" },
            "dynastia.community");

        registry.Register(
            "community.lobby",
            new EventPresentationMetadata { Emoji = "🗣️" },
            "dynastia.community");

        registry.Register(
            "community.policy_enacted",
            new EventPresentationMetadata { Emoji = "🏛️" },
            "dynastia.community");

        registry.Register(
            "connection.created",
            new EventPresentationMetadata { Emoji = "🤝" },
            "dynastia.community");

        registry.Register(
            "connection.interaction",
            new EventPresentationMetadata { Emoji = "🤝" },
            "dynastia.community");

        registry.Register(
            "connection.lost",
            new EventPresentationMetadata { Emoji = "👋" },
            "dynastia.community");

        registry.Register(
            "connection.request_accepted",
            new EventPresentationMetadata { Emoji = "🤝" },
            "dynastia.community");

        registry.Register(
            "connection.request_refused",
            new EventPresentationMetadata { Emoji = "🚫" },
            "dynastia.community");

    }
}
