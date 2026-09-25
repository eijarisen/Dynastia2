using Dynastia.Contracts;

namespace Dynastia.Mechanics.FamilyRelations;

internal static class EventPresentationRegistration
{
    public static void Register(IGamePluginContext context)
    {
        var registry = context.GetService<IEventPresentationRegistry>();
        if (registry is null)
            return;

        registry.RegisterPrefix(
            "family_relations.",
            new EventPresentationMetadata { Emoji = "👪" },
            "dynastia.family_relations");

        registry.Register(
            "family_relations.money_given",
            new EventPresentationMetadata { Emoji = "🎁" },
            "dynastia.family_relations");

        registry.Register(
            "family_relations.money_received",
            new EventPresentationMetadata { Emoji = "💰" },
            "dynastia.family_relations");

        registry.Register(
            "family_relations.money_refused",
            new EventPresentationMetadata { Emoji = "🚫" },
            "dynastia.family_relations");

    }
}
