using Dynastia.Contracts;

namespace Dynastia.Mechanics.Health;

internal static class EventPresentationRegistration
{
    public static void Register(IGamePluginContext context)
    {
        var registry = context.GetService<IEventPresentationRegistry>();
        if (registry is null)
            return;

        registry.RegisterPrefix(
            "health.",
            new EventPresentationMetadata { Emoji = "❤️‍🩹" },
            "dynastia.health");

        registry.Register(
            "health.illness",
            new EventPresentationMetadata { Emoji = "🤧" },
            "dynastia.health");

        registry.Register(
            "health.natural_recovery",
            new EventPresentationMetadata { Emoji = "❤️‍🩹" },
            "dynastia.health");

        registry.Register(
            "health.permanent_injury",
            new EventPresentationMetadata { Emoji = "🩼" },
            "dynastia.health");

        registry.Register(
            "health.second_wind",
            new EventPresentationMetadata { Emoji = "❤️‍🔥" },
            "dynastia.health");

        registry.Register(
            "health.serious_illness",
            new EventPresentationMetadata { Emoji = "😣" },
            "dynastia.health");

    }
}
