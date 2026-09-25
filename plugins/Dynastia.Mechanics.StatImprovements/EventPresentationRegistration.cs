using Dynastia.Contracts;

namespace Dynastia.Mechanics.StatImprovements;

internal static class EventPresentationRegistration
{
    public static void Register(IGamePluginContext context)
    {
        var registry = context.GetService<IEventPresentationRegistry>();
        if (registry is null)
            return;

        registry.RegisterPrefix(
            "stats.",
            new EventPresentationMetadata { Emoji = "📈" },
            "dynastia.stat_improvements");

        registry.Register(
            "stats.paid_improvement",
            new EventPresentationMetadata { Emoji = "📈" },
            "dynastia.stat_improvements");

    }
}
