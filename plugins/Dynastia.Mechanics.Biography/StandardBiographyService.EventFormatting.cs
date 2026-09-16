using Dynastia.Contracts;

namespace Dynastia.Mechanics.Biography;

public sealed partial class StandardBiographyService
{
    private static bool IsFamilyMoneyEvent(
        string type) =>
        type.Equals(
            "family_relations.money_received",
            StringComparison.OrdinalIgnoreCase)
        || type.Equals(
            "family_relations.money_given",
            StringComparison.OrdinalIgnoreCase)
        || type.Equals(
            "family_relations.money_refused",
            StringComparison.OrdinalIgnoreCase)
        || type.Equals(
            "farmland.given",
            StringComparison.OrdinalIgnoreCase)
        || type.Equals(
            "farmland.received",
            StringComparison.OrdinalIgnoreCase)
        || type.Equals(
            "farmland.request_refused",
            StringComparison.OrdinalIgnoreCase);

    private static bool IsImportantForRelatives(
        string type)
    {
        return type.Equals(
                "life.death",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.married",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.partnered",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.remarried",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.divorce",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.low_satisfaction_divorce",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.prison_divorce",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "relationship.affair",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "justice.crime",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "health.serious_illness",
                StringComparison.OrdinalIgnoreCase)
            || type.Equals(
                "birth.condition",
                StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatWithEmoji(
        string type,
        string message)
    {
        var emoji =
            GetEmoji(type);

        return string.IsNullOrEmpty(
            emoji)
            ? message
            : $"{emoji}{message}";
    }

    private static string GetEmoji(
        string type)
    {
        return type switch
        {
            // Structured-event aliases of Dynasty 4's const emojiMap.
            "wellbeing.heal" => "❤️‍🩹 ",
            "wellbeing.recover" => "🛌 ",
            "education.success" => "🎓 ",
            "education.failure" => "🧱 ",
            "education.help_learning_success" => "📚 ",
            "education.help_learning_failure" => "📖 ",
            "wellbeing.therapy_success" => "😊 ",
            "wellbeing.therapy_failure" => "😒 ",
            "wellbeing.drink" => "🍺 ",
            "life.adult" => "🧑 ",

            "inheritance.received_at_adulthood" => "💰 ",
            "inheritance.received" => "💸 ",
            "inheritance.pending_minor" => "⏳ ",
            "inheritance.claimable" => "⏳ ",
            "inheritance.unclaimed" => "💨 ",
            "inheritance.estate_settled" => "🏦 ",
            "inheritance.houses" => "🏡 ",
            "inheritance.promised_houses_received" => "🏡 ",

            "loan.taken" => "🏦 ",
            "loan.given" => "🤝 ",
            "loan.repaid" => "✅ ",
            "loan.receivable_repaid" => "✅ ",
            "loan.debt_inherited" => "📜 ",
            "career.retirement" => "🕊️ ",
            "justice.released" => "✅ ",
            "health.illness" => "🤧 ",
            "health.serious_illness" => "😣 ",
            "life.death" => "💀 ",
            "career.quit" => "🚶 ",
            "relationship.divorce" => "💔 ",
            "relationship.low_satisfaction_divorce" => "💔 ",
            "relationship.repair_marriage" => "❤️‍🩹 ",
            "relationship.prison_divorce" => "💔 ",
            "career.employment" => "✅ ",
            "family_support.parents_success" => "🙏 ",
            "family_support.parents_failure" => "🚫 ",

            "family_support.child_success" => "🙏 ",
            "family_support.child_failure" => "🚫 ",
            "family_relations.money_received" => "💰 ",
            "family_relations.money_given" => "🎁 ",
            "family_relations.money_refused" => "🚫 ",

            "career.ask_quit_success" => "✅ ",
            "career.ask_quit_failure" => "🚫 ",
            "career.ask_recover_success" => "✅ ",
            "career.ask_recover_failure" => "🚫 ",
            "justice.crime" => "⛓️ ",
            "career.fired" => "💥 ",
            "career.promotion" => "✨ ",
            "relationship.partnered" => "👩‍❤️‍👩 ",
            "relationship.courtship" => "💌 ",
            "relationship.married" => "💍 ",
            "relationship.affair" => "🤫 ",
            "birth.condition" => "🧩 ",
            "life.birth" => "👶 ",
            "relationship.remarried" => "💍 ",

            "household.house_bought" => "🏠 ",
            "household.house_sold" => "💵 ",
            "household.house_rented" => "🏘️ ",
            "household.house_given" => "🎁 ",
            "farmland.bought" => "🌾 ",
            "farmland.sold" => "🌾 ",
            "farmland.inherited" => "🌾 ",
            "farmland.given" => "🌾 ",
            "farmland.received" => "🌾 ",
            "farmland.request_refused" => "🚫 ",
            "craft.learned" => "🛠️ ",
            "craft.teaching_failed" => "🧱 ",
            "craft.self_employment_started" => "🛠️ ",
            "craft.self_employment_ended" => "🚶 ",

            "household.house_promised" => "🎁 ",

            "household.nanny_hired" => "🧑‍🍼 ",
            "household.family_nanny_started" => "🧑‍🍼 ",
            "household.family_nanny_ended" => "👋 ",
            "household.nanny_service_ended" => "👋 ",
            "household.nanny_fired" => "👋 ",

            "adoption.with_mother" => "👩‍👧 ",
            "adoption.orphaned" => "🕯️ ",
            "adoption.placed" => "🏠 ",
            "adoption.orphanage" => "🏚️ ",
            "adoption.left_orphanage" => "🧳 ",

            "rare.house_fire" => "🔥 ",
            "rare.burglary" => "🕵️ ",
            "rare.storm_flood_damage" => "🌊 ",
            "rare.structural_accident" => "🧱 ",
            "rare.assault" => "🥊 ",
            "rare.mugging" => "💸 ",
            "rare.workplace_accident" => "⚠️ ",
            "rare.traffic_accident" => "🚗 ",
            "rare.lightning_strike" => "⚡ ",
            "rare.serious_fall" => "🤕 ",
            "rare.lottery_win" => "🎰 ",
            "rare.distant_inheritance" => "💰 ",
            "rare.fraud" => "🎭 ",
            "rare.found_property" => "💎 ",
            "rare.wrongful_arrest" => "⚖️ ",
            "rare.suicide" => "🕯️ ",

            _ => "🔹 "
        };
    }

    private static string? GetEventText(
        GameEvent gameEvent)
    {
        return gameEvent.Data.TryGetValue(
            "text",
            out var text)
            ? text
            : null;
    }

    private static int GetInt(
        GameEvent gameEvent,
        string key,
        int fallback)
    {
        return gameEvent.Data.TryGetValue(
                key,
                out var text)
            && int.TryParse(
                text,
                out var value)
                    ? value
                    : fallback;
    }

    private static string Possessive(
        IPerson person)
    {
        return person.Tags.Has(
            "sex.male")
                ? "His"
                : "Her";
    }

    private static bool IsAlive(
        IPerson? person)
    {
        return person is not null
            && person.Tags.Has(
                "state.alive");
    }

    private static string ToOrdinalWord(
        int value)
    {
        return value switch
        {
            1 => "first",
            2 => "second",
            3 => "third",
            _ => $"{value}th"
        };
    }

}
