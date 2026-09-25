using Dynastia.Contracts;

namespace Dynastia.Mechanics.FamilySupport;

internal static class EventPresentationRegistration
{
    public static void Register(IGamePluginContext context)
    {
        var registry = context.GetService<IEventPresentationRegistry>();
        if (registry is null)
            return;

        registry.RegisterPrefix(
            "family_support.",
            new EventPresentationMetadata { Emoji = "👪" },
            "dynastia.family_support");

        registry.Register(
            "family_support.child_failure",
            new EventPresentationMetadata { Emoji = "🚫" },
            "dynastia.family_support");

        registry.Register(
            "family_support.child_success",
            new EventPresentationMetadata { Emoji = "🙏" },
            "dynastia.family_support");

        registry.Register(
            "family_support.parents_failure",
            new EventPresentationMetadata { Emoji = "🚫" },
            "dynastia.family_support");

        registry.Register(
            "family_support.parents_success",
            new EventPresentationMetadata { Emoji = "🙏" },
            "dynastia.family_support");

    }
}
