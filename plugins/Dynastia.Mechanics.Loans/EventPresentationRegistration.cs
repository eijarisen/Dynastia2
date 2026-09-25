using Dynastia.Contracts;

namespace Dynastia.Mechanics.Loans;

internal static class EventPresentationRegistration
{
    public static void Register(IGamePluginContext context)
    {
        var registry = context.GetService<IEventPresentationRegistry>();
        if (registry is null)
            return;

        registry.RegisterPrefix(
            "loan.",
            new EventPresentationMetadata { Emoji = "🏦" },
            "dynastia.loans");

        registry.Register(
            "loan.debt_inherited",
            new EventPresentationMetadata { Emoji = "📜" },
            "dynastia.loans");

        registry.Register(
            "loan.given",
            new EventPresentationMetadata { Emoji = "🤝" },
            "dynastia.loans");

        registry.Register(
            "loan.receivable_repaid",
            new EventPresentationMetadata { Emoji = "✅" },
            "dynastia.loans");

        registry.Register(
            "loan.repaid",
            new EventPresentationMetadata { Emoji = "✅" },
            "dynastia.loans");

        registry.Register(
            "loan.taken",
            new EventPresentationMetadata { Emoji = "🏦" },
            "dynastia.loans");

    }
}
