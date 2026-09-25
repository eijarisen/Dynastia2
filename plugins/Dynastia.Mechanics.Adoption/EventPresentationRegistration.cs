using Dynastia.Contracts;

namespace Dynastia.Mechanics.Adoption;

internal static class EventPresentationRegistration
{
    public static void Register(IGamePluginContext context)
    {
        var registry = context.GetService<IEventPresentationRegistry>();
        if (registry is null)
            return;

        registry.Register(
            "adoption.left_orphanage",
            new EventPresentationMetadata { Emoji = "🧳" },
            "dynastia.adoption");

        registry.Register(
            "adoption.orphanage",
            new EventPresentationMetadata { Emoji = "🏚️" },
            "dynastia.adoption");

        registry.Register(
            "adoption.orphaned",
            new EventPresentationMetadata { Emoji = "🕯️" },
            "dynastia.adoption");

        registry.Register(
            "adoption.placed",
            new EventPresentationMetadata { Emoji = "🏠" },
            "dynastia.adoption");

        registry.Register(
            "adoption.with_mother",
            new EventPresentationMetadata { Emoji = "👩‍👧" },
            "dynastia.adoption");

    }
}
