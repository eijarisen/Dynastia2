using Dynastia.Contracts;

namespace Dynastia.Mechanics.Education;

internal static class EventPresentationRegistration
{
    public static void Register(IGamePluginContext context)
    {
        var registry = context.GetService<IEventPresentationRegistry>();
        if (registry is null)
            return;

        registry.RegisterPrefix(
            "education.",
            new EventPresentationMetadata { Emoji = "🎓" },
            "dynastia.education");

        registry.Register(
            "education.failure",
            new EventPresentationMetadata { Emoji = "🧱" },
            "dynastia.education");

        registry.Register(
            "education.help_learning_failure",
            new EventPresentationMetadata { Emoji = "📖" },
            "dynastia.education");

        registry.Register(
            "education.help_learning_success",
            new EventPresentationMetadata { Emoji = "📚" },
            "dynastia.education");

        registry.Register(
            "education.success",
            new EventPresentationMetadata { Emoji = "🎓" },
            "dynastia.education");

    }
}
