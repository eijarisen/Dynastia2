using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

internal static class EventPresentationRegistration
{
    public static void Register(IGamePluginContext context)
    {
        var registry = context.GetService<IEventPresentationRegistry>();
        if (registry is null)
            return;

        registry.RegisterPrefix(
            "household.",
            new EventPresentationMetadata { Emoji = "🏠" },
            "dynastia.households");

        registry.Register(
            "family.assimilated_polish",
            new EventPresentationMetadata { Emoji = "🇵🇱" },
            "dynastia.households");

        registry.Register(
            "family.polish_surname_adopted",
            new EventPresentationMetadata { Emoji = "🇵🇱" },
            "dynastia.households");

        registry.Register(
            "household.assets_followed_anchor",
            new EventPresentationMetadata { Emoji = "🏠" },
            "dynastia.households");

        registry.Register(
            "household.family_nanny_ended",
            new EventPresentationMetadata { Emoji = "👋" },
            "dynastia.households");

        registry.Register(
            "household.family_nanny_started",
            new EventPresentationMetadata { Emoji = "🧑‍🍼" },
            "dynastia.households");

        registry.Register(
            "household.house_bought",
            new EventPresentationMetadata { Emoji = "🏠" },
            "dynastia.households");

        registry.Register(
            "household.house_given",
            new EventPresentationMetadata { Emoji = "🎁" },
            "dynastia.households");

        registry.Register(
            "household.house_promised",
            new EventPresentationMetadata { Emoji = "🎁" },
            "dynastia.households");

        registry.Register(
            "household.house_rented",
            new EventPresentationMetadata { Emoji = "🏘️" },
            "dynastia.households");

        registry.Register(
            "household.house_sold",
            new EventPresentationMetadata { Emoji = "💵" },
            "dynastia.households");

        registry.Register(
            "household.member_moved_out",
            new EventPresentationMetadata { Emoji = "🚪" },
            "dynastia.households");

        registry.Register(
            "household.move_out_refused",
            new EventPresentationMetadata { Emoji = "🚫" },
            "dynastia.households");

        registry.Register(
            "household.moved",
            new EventPresentationMetadata { Emoji = "🚚" },
            "dynastia.households");

        registry.Register(
            "household.nanny_fired",
            new EventPresentationMetadata { Emoji = "👋" },
            "dynastia.households");

        registry.Register(
            "household.nanny_hired",
            new EventPresentationMetadata { Emoji = "🧑‍🍼" },
            "dynastia.households");

        registry.Register(
            "household.nanny_service_ended",
            new EventPresentationMetadata { Emoji = "👋" },
            "dynastia.households");

        registry.Register(
            "household.parent_house_gift",
            new EventPresentationMetadata { Emoji = "🎁" },
            "dynastia.households");

        registry.Register(
            "household.parent_house_refused",
            new EventPresentationMetadata { Emoji = "🚫" },
            "dynastia.households");

        registry.Register(
            "household.son_moved_out",
            new EventPresentationMetadata { Emoji = "🚪" },
            "dynastia.households");

    }
}
