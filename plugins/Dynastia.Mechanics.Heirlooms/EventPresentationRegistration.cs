using Dynastia.Contracts;

namespace Dynastia.Mechanics.Heirlooms;

internal static class EventPresentationRegistration
{
    public static void Register(IGamePluginContext context)
    {
        var registry = context.GetService<IEventPresentationRegistry>();
        if (registry is null)
            return;

        registry.Register(
            "artistic.work_created",
            new EventPresentationMetadata { Emoji = "🎨" },
            "dynastia.heirlooms");

        registry.Register(
            "heirloom.created",
            new EventPresentationMetadata { Emoji = "🏺" },
            "dynastia.heirlooms");

        registry.Register(
            "heirloom.inherited",
            new EventPresentationMetadata { Emoji = "🎁" },
            "dynastia.heirlooms");

        registry.Register(
            "heirloom.pending",
            new EventPresentationMetadata { Emoji = "⏳" },
            "dynastia.heirlooms");

        registry.Register(
            "heirloom.sold",
            new EventPresentationMetadata { Emoji = "💵" },
            "dynastia.heirlooms");

    }
}
