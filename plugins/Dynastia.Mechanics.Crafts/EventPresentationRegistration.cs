using Dynastia.Contracts;

namespace Dynastia.Mechanics.Crafts;

internal static class EventPresentationRegistration
{
    public static void Register(IGamePluginContext context)
    {
        var registry = context.GetService<IEventPresentationRegistry>();
        if (registry is null)
            return;

        registry.RegisterPrefix(
            "craft.",
            new EventPresentationMetadata { Emoji = "🛠️" },
            "dynastia.crafts");

        registry.Register(
            "craft.income",
            new EventPresentationMetadata { Emoji = "💰" },
            "dynastia.crafts");

        registry.Register(
            "craft.learned",
            new EventPresentationMetadata { Emoji = "🛠️" },
            "dynastia.crafts");

        registry.Register(
            "craft.major_commission",
            new EventPresentationMetadata { Emoji = "🏅" },
            "dynastia.crafts");

        registry.Register(
            "craft.self_employment_ended",
            new EventPresentationMetadata { Emoji = "🚶" },
            "dynastia.crafts");

        registry.Register(
            "craft.self_employment_started",
            new EventPresentationMetadata { Emoji = "🛠️" },
            "dynastia.crafts");

        registry.Register(
            "craft.teaching_failed",
            new EventPresentationMetadata { Emoji = "🧱" },
            "dynastia.crafts");

    }
}
