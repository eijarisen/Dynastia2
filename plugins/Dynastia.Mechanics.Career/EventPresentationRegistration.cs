using Dynastia.Contracts;

namespace Dynastia.Mechanics.Career;

internal static class EventPresentationRegistration
{
    public static void Register(IGamePluginContext context)
    {
        var registry = context.GetService<IEventPresentationRegistry>();
        if (registry is null)
            return;

        registry.RegisterPrefix(
            "career.",
            new EventPresentationMetadata { Emoji = "💼" },
            "dynastia.career");

        registry.Register(
            "career.ask_quit_failure",
            new EventPresentationMetadata { Emoji = "🚫" },
            "dynastia.career");

        registry.Register(
            "career.ask_quit_success",
            new EventPresentationMetadata { Emoji = "✅" },
            "dynastia.career");

        registry.Register(
            "career.ask_recover_failure",
            new EventPresentationMetadata { Emoji = "🚫" },
            "dynastia.career");

        registry.Register(
            "career.ask_recover_success",
            new EventPresentationMetadata { Emoji = "✅" },
            "dynastia.career");

        registry.Register(
            "career.changed_job",
            new EventPresentationMetadata { Emoji = "🔄" },
            "dynastia.career");

        registry.Register(
            "career.employment",
            new EventPresentationMetadata { Emoji = "✅" },
            "dynastia.career");

        registry.Register(
            "career.family_connections_failure",
            new EventPresentationMetadata { Emoji = "🚫" },
            "dynastia.career");

        registry.Register(
            "career.family_connections_success",
            new EventPresentationMetadata { Emoji = "🤝" },
            "dynastia.career");

        registry.Register(
            "career.fired",
            new EventPresentationMetadata { Emoji = "💥" },
            "dynastia.career");

        registry.Register(
            "career.promotion",
            new EventPresentationMetadata { Emoji = "✨" },
            "dynastia.career");

        registry.Register(
            "career.quit",
            new EventPresentationMetadata { Emoji = "🚶" },
            "dynastia.career");

        registry.Register(
            "career.relocated",
            new EventPresentationMetadata { Emoji = "🚚" },
            "dynastia.career");

        registry.Register(
            "career.retirement",
            new EventPresentationMetadata { Emoji = "🕊️" },
            "dynastia.career");

        registry.Register(
            "career.work_harder",
            new EventPresentationMetadata { Emoji = "💪" },
            "dynastia.career");

    }
}
