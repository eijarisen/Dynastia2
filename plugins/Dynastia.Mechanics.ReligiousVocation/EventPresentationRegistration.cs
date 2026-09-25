using Dynastia.Contracts;

namespace Dynastia.Mechanics.ReligiousVocation;

internal static class EventPresentationRegistration
{
    public static void Register(IGamePluginContext context)
    {
        var registry = context.GetService<IEventPresentationRegistry>();
        if (registry is null)
            return;

        registry.Register(
            "religion.calling_nun",
            new EventPresentationMetadata { Emoji = "🙏" },
            "dynastia.religious_vocation");

        registry.Register(
            "religion.calling_priest",
            new EventPresentationMetadata { Emoji = "⛪" },
            "dynastia.religious_vocation");

        registry.Register(
            "religion.vocation_left",
            new EventPresentationMetadata { Emoji = "🚪" },
            "dynastia.religious_vocation");

    }
}
