using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

internal static class EventPresentationRegistration
{
    public static void Register(IGamePluginContext context)
    {
        var registry = context.GetService<IEventPresentationRegistry>();
        if (registry is null)
            return;

        registry.RegisterPrefix(
            "relationship.",
            new EventPresentationMetadata { Emoji = "💞" },
            "dynastia.relationships");

        registry.Register(
            "relationship.affair",
            new EventPresentationMetadata { Emoji = "🤫" },
            "dynastia.relationships");

        registry.Register(
            "relationship.courtship",
            new EventPresentationMetadata { Emoji = "💌" },
            "dynastia.relationships");

        registry.Register(
            "relationship.divorce",
            new EventPresentationMetadata { Emoji = "💔" },
            "dynastia.relationships");

        registry.Register(
            "relationship.divorce_refused",
            new EventPresentationMetadata { Emoji = "💍" },
            "dynastia.relationships");

        registry.Register(
            "relationship.low_satisfaction_divorce",
            new EventPresentationMetadata { Emoji = "💔" },
            "dynastia.relationships");

        registry.Register(
            "relationship.married",
            new EventPresentationMetadata { Emoji = "💍" },
            "dynastia.relationships");

        registry.Register(
            "relationship.marry_off_failed",
            new EventPresentationMetadata { Emoji = "💒" },
            "dynastia.relationships");

        registry.Register(
            "relationship.partnered",
            new EventPresentationMetadata { Emoji = "👩‍❤️‍👩" },
            "dynastia.relationships");

        registry.Register(
            "relationship.prison_divorce",
            new EventPresentationMetadata { Emoji = "💔" },
            "dynastia.relationships");

        registry.Register(
            "relationship.remarried",
            new EventPresentationMetadata { Emoji = "💍" },
            "dynastia.relationships");

        registry.Register(
            "relationship.repair_marriage",
            new EventPresentationMetadata { Emoji = "❤️‍🩹" },
            "dynastia.relationships");

    }
}
