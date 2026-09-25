using Dynastia.Contracts;

namespace Dynastia.Mechanics.Reproduction;

internal static class EventPresentationRegistration
{
    public static void Register(IGamePluginContext context)
    {
        var registry = context.GetService<IEventPresentationRegistry>();
        if (registry is null)
            return;

        registry.Register(
            "birth.condition",
            new EventPresentationMetadata { Emoji = "🧩" },
            "dynastia.reproduction");

        registry.Register(
            "life.birth",
            new EventPresentationMetadata { Emoji = "👶" },
            "dynastia.reproduction");

        registry.Register(
            "peripheral.birth",
            new EventPresentationMetadata { Emoji = "👶" },
            "dynastia.reproduction");

        registry.Register(
            "reproduction.birth_and_marriage",
            new EventPresentationMetadata { Emoji = "💍" },
            "dynastia.reproduction");

        registry.Register(
            "reproduction.teen_birth",
            new EventPresentationMetadata { Emoji = "👶" },
            "dynastia.reproduction");

        registry.Register(
            "reproduction.teen_marriage",
            new EventPresentationMetadata { Emoji = "💍" },
            "dynastia.reproduction");

        registry.Register(
            "reproduction.unknown_father_birth",
            new EventPresentationMetadata { Emoji = "👶" },
            "dynastia.reproduction");

    }
}
