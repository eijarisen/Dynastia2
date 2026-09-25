using Dynastia.Contracts;

namespace Dynastia.Mechanics.Inheritance;

internal static class EventPresentationRegistration
{
    public static void Register(IGamePluginContext context)
    {
        var registry = context.GetService<IEventPresentationRegistry>();
        if (registry is null)
            return;

        registry.RegisterPrefix(
            "inheritance.",
            new EventPresentationMetadata { Emoji = "💰" },
            "dynastia.inheritance");

        registry.Register(
            "inheritance.claimable",
            new EventPresentationMetadata { Emoji = "⏳" },
            "dynastia.inheritance");

        registry.Register(
            "inheritance.estate_left_dynasty",
            new EventPresentationMetadata { Emoji = "💨" },
            "dynastia.inheritance");

        registry.Register(
            "inheritance.estate_settled",
            new EventPresentationMetadata { Emoji = "🏦" },
            "dynastia.inheritance");

        registry.Register(
            "inheritance.houses",
            new EventPresentationMetadata { Emoji = "🏡" },
            "dynastia.inheritance");

        registry.Register(
            "inheritance.pending",
            new EventPresentationMetadata { Emoji = "⏳" },
            "dynastia.inheritance");

        registry.Register(
            "inheritance.pending_houses",
            new EventPresentationMetadata { Emoji = "🏡" },
            "dynastia.inheritance");

        registry.Register(
            "inheritance.pending_houses_received",
            new EventPresentationMetadata { Emoji = "🏡" },
            "dynastia.inheritance");

        registry.Register(
            "inheritance.pending_minor",
            new EventPresentationMetadata { Emoji = "⏳" },
            "dynastia.inheritance");

        registry.Register(
            "inheritance.promised_houses_received",
            new EventPresentationMetadata { Emoji = "🏡" },
            "dynastia.inheritance");

        registry.Register(
            "inheritance.received",
            new EventPresentationMetadata { Emoji = "💸" },
            "dynastia.inheritance");

        registry.Register(
            "inheritance.received_at_adulthood",
            new EventPresentationMetadata { Emoji = "💰" },
            "dynastia.inheritance");

        registry.Register(
            "inheritance.received_at_household",
            new EventPresentationMetadata { Emoji = "💸" },
            "dynastia.inheritance");

        registry.Register(
            "inheritance.unclaimed",
            new EventPresentationMetadata { Emoji = "💨" },
            "dynastia.inheritance");

    }
}
