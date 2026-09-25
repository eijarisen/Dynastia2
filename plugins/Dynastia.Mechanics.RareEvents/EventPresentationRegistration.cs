using Dynastia.Contracts;

namespace Dynastia.Mechanics.RareEvents;

internal static class EventPresentationRegistration
{
    public static void Register(IGamePluginContext context)
    {
        var registry = context.GetService<IEventPresentationRegistry>();
        if (registry is null)
            return;

        registry.RegisterPrefix(
            "rare.",
            new EventPresentationMetadata { Emoji = "⚠️" },
            "dynastia.rare_events");

        registry.Register(
            "rare.animal_accident",
            new EventPresentationMetadata { Emoji = "🐎" },
            "dynastia.rare_events");

        registry.Register(
            "rare.assault",
            new EventPresentationMetadata { Emoji = "🥊" },
            "dynastia.rare_events");

        registry.Register(
            "rare.burglary",
            new EventPresentationMetadata { Emoji = "🕵️" },
            "dynastia.rare_events");

        registry.Register(
            "rare.craft_commission",
            new EventPresentationMetadata { Emoji = "🛠️" },
            "dynastia.rare_events");

        registry.Register(
            "rare.craft_setback",
            new EventPresentationMetadata { Emoji = "🛠️" },
            "dynastia.rare_events");

        registry.Register(
            "rare.crop_failure",
            new EventPresentationMetadata { Emoji = "🌾" },
            "dynastia.rare_events");

        registry.Register(
            "rare.distant_inheritance",
            new EventPresentationMetadata { Emoji = "💰" },
            "dynastia.rare_events");

        registry.Register(
            "rare.exceptional_harvest",
            new EventPresentationMetadata { Emoji = "🌾" },
            "dynastia.rare_events");

        registry.Register(
            "rare.found_property",
            new EventPresentationMetadata { Emoji = "💎" },
            "dynastia.rare_events");

        registry.Register(
            "rare.fraud",
            new EventPresentationMetadata { Emoji = "🎭" },
            "dynastia.rare_events");

        registry.Register(
            "rare.house_discovery",
            new EventPresentationMetadata { Emoji = "💎" },
            "dynastia.rare_events");

        registry.Register(
            "rare.house_fire",
            new EventPresentationMetadata { Emoji = "🔥" },
            "dynastia.rare_events");

        registry.Register(
            "rare.legal_dispute",
            new EventPresentationMetadata { Emoji = "⚖️" },
            "dynastia.rare_events");

        registry.Register(
            "rare.lightning_strike",
            new EventPresentationMetadata { Emoji = "⚡" },
            "dynastia.rare_events");

        registry.Register(
            "rare.local_epidemic",
            new EventPresentationMetadata { Emoji = "🦠" },
            "dynastia.rare_events");

        registry.Register(
            "rare.lottery_win",
            new EventPresentationMetadata { Emoji = "🎰" },
            "dynastia.rare_events");

        registry.Register(
            "rare.major_repair",
            new EventPresentationMetadata { Emoji = "🔧" },
            "dynastia.rare_events");

        registry.Register(
            "rare.mugging",
            new EventPresentationMetadata { Emoji = "💸" },
            "dynastia.rare_events");

        registry.Register(
            "rare.patronage",
            new EventPresentationMetadata { Emoji = "🤝" },
            "dynastia.rare_events");

        registry.Register(
            "rare.prize_award",
            new EventPresentationMetadata { Emoji = "🏆" },
            "dynastia.rare_events");

        registry.Register(
            "rare.professional_recognition",
            new EventPresentationMetadata { Emoji = "🏅" },
            "dynastia.rare_events");

        registry.Register(
            "rare.scholarship",
            new EventPresentationMetadata { Emoji = "🎓" },
            "dynastia.rare_events");

        registry.Register(
            "rare.serious_fall",
            new EventPresentationMetadata { Emoji = "🤕" },
            "dynastia.rare_events");

        registry.Register(
            "rare.storm_flood",
            new EventPresentationMetadata { Emoji = "🌊" },
            "dynastia.rare_events");

        registry.Register(
            "rare.storm_flood_damage",
            new EventPresentationMetadata { Emoji = "🌊" },
            "dynastia.rare_events");

        registry.Register(
            "rare.structural_accident",
            new EventPresentationMetadata { Emoji = "🧱" },
            "dynastia.rare_events");

        registry.Register(
            "rare.suicide",
            new EventPresentationMetadata { Emoji = "🕯️" },
            "dynastia.rare_events");

        registry.Register(
            "rare.traffic_accident",
            new EventPresentationMetadata { Emoji = "🚗" },
            "dynastia.rare_events");

        registry.Register(
            "rare.water_accident",
            new EventPresentationMetadata { Emoji = "🌊" },
            "dynastia.rare_events");

        registry.Register(
            "rare.workplace_accident",
            new EventPresentationMetadata { Emoji = "⚠️" },
            "dynastia.rare_events");

        registry.Register(
            "rare.wrongful_arrest",
            new EventPresentationMetadata { Emoji = "⚖️" },
            "dynastia.rare_events");

    }
}
