using Dynastia.Contracts;

namespace Dynastia.Mechanics.Wellbeing;

internal static class EventPresentationRegistration
{
    public static void Register(IGamePluginContext context)
    {
        var registry = context.GetService<IEventPresentationRegistry>();
        if (registry is null)
            return;

        registry.RegisterPrefix(
            "wellbeing.",
            new EventPresentationMetadata { Emoji = "❤️‍🩹" },
            "dynastia.wellbeing");

        registry.Register(
            "wellbeing.drink",
            new EventPresentationMetadata { Emoji = "🍺" },
            "dynastia.wellbeing");

        registry.Register(
            "wellbeing.heal",
            new EventPresentationMetadata { Emoji = "❤️‍🩹" },
            "dynastia.wellbeing");

        registry.Register(
            "wellbeing.recover",
            new EventPresentationMetadata { Emoji = "🛌" },
            "dynastia.wellbeing");

        registry.Register(
            "wellbeing.therapy_failure",
            new EventPresentationMetadata { Emoji = "😒" },
            "dynastia.wellbeing");

        registry.Register(
            "wellbeing.therapy_success",
            new EventPresentationMetadata { Emoji = "😊" },
            "dynastia.wellbeing");

    }
}
