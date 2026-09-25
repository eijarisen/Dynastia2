using Dynastia.Contracts;

namespace Dynastia.Mechanics.Historical;

internal static class EventPresentationRegistration
{
    public static void Register(IGamePluginContext context)
    {
        var registry = context.GetService<IEventPresentationRegistry>();
        if (registry is null)
            return;

        registry.RegisterPrefix(
            "historical.",
            new EventPresentationMetadata { Emoji = "🗞️" },
            "dynastia.historical");

        registry.Register(
            "historical.external_departure",
            new EventPresentationMetadata { Emoji = "🧳" },
            "dynastia.historical");

        registry.Register(
            "historical.household_impact",
            new EventPresentationMetadata { Emoji = "🏛️" },
            "dynastia.historical");

        registry.Register(
            "historical.milestone",
            new EventPresentationMetadata { Emoji = "🗞️" },
            "dynastia.historical");

        registry.Register(
            "historical.relocation",
            new EventPresentationMetadata { Emoji = "🚚" },
            "dynastia.historical");

    }
}
