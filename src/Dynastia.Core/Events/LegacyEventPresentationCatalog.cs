using Dynastia.Contracts;

namespace Dynastia.Core.Events;

public static class LegacyEventPresentationCatalog
{
    public static void Register(IEventPresentationRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        // Preserve the two broad legacy fallbacks that did not belong to a
        // single modular mechanics owner. Exact modular registrations still
        // win over these prefixes.
        registry.RegisterPrefix(
            "family_",
            new EventPresentationMetadata { Emoji = "👪" },
            "core.legacy");

        registry.RegisterPrefix(
            "life.",
            new EventPresentationMetadata { Emoji = "📜" },
            "core.legacy");

        registry.Register(
            "adult",
            new EventPresentationMetadata { Emoji = "🧑" },
            "core.legacy");

        registry.Register(
            "affair",
            new EventPresentationMetadata { Emoji = "🤫" },
            "core.legacy");

        registry.Register(
            "askForMoneyFail",
            new EventPresentationMetadata { Emoji = "🚫" },
            "core.legacy");

        registry.Register(
            "askForMoneySuccess",
            new EventPresentationMetadata { Emoji = "🙏" },
            "core.legacy");

        registry.Register(
            "askToQuitJobFail",
            new EventPresentationMetadata { Emoji = "🚫" },
            "core.legacy");

        registry.Register(
            "askToQuitJobSuccess",
            new EventPresentationMetadata { Emoji = "✅" },
            "core.legacy");

        registry.Register(
            "askToRecoverFail",
            new EventPresentationMetadata { Emoji = "🚫" },
            "core.legacy");

        registry.Register(
            "askToRecoverSuccess",
            new EventPresentationMetadata { Emoji = "✅" },
            "core.legacy");

        registry.Register(
            "birth",
            new EventPresentationMetadata { Emoji = "👶" },
            "core.legacy");

        registry.Register(
            "birthDefect",
            new EventPresentationMetadata { Emoji = "🧩" },
            "core.legacy");

        registry.Register(
            "buyHouse",
            new EventPresentationMetadata { Emoji = "🏠" },
            "core.legacy");

        registry.Register(
            "claimableInheritance",
            new EventPresentationMetadata { Emoji = "⏳" },
            "core.legacy");

        registry.Register(
            "crime",
            new EventPresentationMetadata { Emoji = "⛓️" },
            "core.legacy");

        registry.Register(
            "death",
            new EventPresentationMetadata { Emoji = "💀" },
            "core.legacy");

        registry.Register(
            "developsAlcoholism",
            new EventPresentationMetadata { Emoji = " " },
            "core.legacy");

        registry.Register(
            "divorce",
            new EventPresentationMetadata { Emoji = "💔" },
            "core.legacy");

        registry.Register(
            "drink",
            new EventPresentationMetadata { Emoji = "🍺" },
            "core.legacy");

        registry.Register(
            "educationFail",
            new EventPresentationMetadata { Emoji = "🧱" },
            "core.legacy");

        registry.Register(
            "educationSuccess",
            new EventPresentationMetadata { Emoji = "🎓" },
            "core.legacy");

        registry.Register(
            "estateSettled",
            new EventPresentationMetadata { Emoji = "🏦" },
            "core.legacy");

        registry.Register(
            "fired",
            new EventPresentationMetadata { Emoji = "💥" },
            "core.legacy");

        registry.Register(
            "fireNanny",
            new EventPresentationMetadata { Emoji = "👋" },
            "core.legacy");

        registry.Register(
            "giveHouse",
            new EventPresentationMetadata { Emoji = "🎁" },
            "core.legacy");

        registry.Register(
            "heal",
            new EventPresentationMetadata { Emoji = "❤️‍🩹" },
            "core.legacy");

        registry.Register(
            "hireNanny",
            new EventPresentationMetadata { Emoji = "🧑‍🍼" },
            "core.legacy");

        registry.Register(
            "householdInheritance",
            new EventPresentationMetadata { Emoji = "💸" },
            "core.legacy");

        registry.Register(
            "houseInheritance",
            new EventPresentationMetadata { Emoji = "🏡" },
            "core.legacy");

        registry.Register(
            "houseTransfer",
            new EventPresentationMetadata { Emoji = "🏡" },
            "core.legacy");

        registry.Register(
            "illness",
            new EventPresentationMetadata { Emoji = "🤧" },
            "core.legacy");

        registry.Register(
            "lostInheritance",
            new EventPresentationMetadata { Emoji = "💨" },
            "core.legacy");

        registry.Register(
            "marriage",
            new EventPresentationMetadata { Emoji = "💍" },
            "core.legacy");

        registry.Register(
            "partnership",
            new EventPresentationMetadata { Emoji = "👩‍❤️‍👩" },
            "core.legacy");

        registry.Register(
            "pendingInheritance",
            new EventPresentationMetadata { Emoji = "⏳" },
            "core.legacy");

        registry.Register(
            "promotion",
            new EventPresentationMetadata { Emoji = "✨" },
            "core.legacy");

        registry.Register(
            "quitJob",
            new EventPresentationMetadata { Emoji = "🚶" },
            "core.legacy");

        registry.Register(
            "receiveInheritance",
            new EventPresentationMetadata { Emoji = "💰" },
            "core.legacy");

        registry.Register(
            "recover",
            new EventPresentationMetadata { Emoji = "🛌" },
            "core.legacy");

        registry.Register(
            "releaseFromPrison",
            new EventPresentationMetadata { Emoji = "✅" },
            "core.legacy");

        registry.Register(
            "remarriage",
            new EventPresentationMetadata { Emoji = "💍" },
            "core.legacy");

        registry.Register(
            "rentHouse",
            new EventPresentationMetadata { Emoji = "🏘️" },
            "core.legacy");

        registry.Register(
            "retire",
            new EventPresentationMetadata { Emoji = "🕊️" },
            "core.legacy");

        registry.Register(
            "seekEmploymentSuccess",
            new EventPresentationMetadata { Emoji = "✅" },
            "core.legacy");

        registry.Register(
            "sellHouse",
            new EventPresentationMetadata { Emoji = "💵" },
            "core.legacy");

        registry.Register(
            "seriousIllness",
            new EventPresentationMetadata { Emoji = "😣" },
            "core.legacy");

        registry.Register(
            "spouseInheritance",
            new EventPresentationMetadata { Emoji = "💍" },
            "core.legacy");

        registry.Register(
            "therapyFail",
            new EventPresentationMetadata { Emoji = "😒" },
            "core.legacy");

        registry.Register(
            "therapySuccess",
            new EventPresentationMetadata { Emoji = "😊" },
            "core.legacy");

    }
}
