using Dynastia.Contracts;

namespace Dynastia.Mechanics.Justice;

internal static class EventPresentationRegistration
{
    public static void Register(IGamePluginContext context)
    {
        var registry = context.GetService<IEventPresentationRegistry>();
        if (registry is null)
            return;

        registry.RegisterPrefix(
            "justice.",
            new EventPresentationMetadata { Emoji = "⚖️" },
            "dynastia.justice");

        registry.Register(
            "justice.crime",
            new EventPresentationMetadata { Emoji = "⛓️" },
            "dynastia.justice");

        registry.Register(
            "justice.crime_uncaught",
            new EventPresentationMetadata { Emoji = "🕵️" },
            "dynastia.justice");

        registry.Register(
            "justice.released",
            new EventPresentationMetadata { Emoji = "✅" },
            "dynastia.justice");

    }
}
