using Dynastia.Contracts;

namespace Dynastia.Mechanics.Farming;

internal static class EventPresentationRegistration
{
    public static void Register(IGamePluginContext context)
    {
        var registry = context.GetService<IEventPresentationRegistry>();
        if (registry is null)
            return;

        registry.RegisterPrefix(
            "farming.",
            new EventPresentationMetadata { Emoji = "🌾" },
            "dynastia.farming");

        registry.Register(
            "farming.income",
            new EventPresentationMetadata { Emoji = "🌾" },
            "dynastia.farming");

        registry.Register(
            "farming.weather",
            new EventPresentationMetadata { Emoji = "🌦️" },
            "dynastia.farming");

        registry.Register(
            "farmland.bought",
            new EventPresentationMetadata { Emoji = "🌾" },
            "dynastia.farming");

        registry.Register(
            "farmland.given",
            new EventPresentationMetadata { Emoji = "🌾" },
            "dynastia.farming");

        registry.Register(
            "farmland.inherited",
            new EventPresentationMetadata { Emoji = "🌾" },
            "dynastia.farming");

        registry.Register(
            "farmland.received",
            new EventPresentationMetadata { Emoji = "🌾" },
            "dynastia.farming");

        registry.Register(
            "farmland.request_refused",
            new EventPresentationMetadata { Emoji = "🚫" },
            "dynastia.farming");

        registry.Register(
            "farmland.sold",
            new EventPresentationMetadata { Emoji = "🌾" },
            "dynastia.farming");

    }
}
