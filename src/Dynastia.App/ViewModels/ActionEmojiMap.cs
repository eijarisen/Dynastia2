namespace Dynastia.App.ViewModels;

public static class ActionEmojiMap
{
    private static readonly IReadOnlyDictionary<string, string>
        Emojis =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["turn.pass"] = "⏭️",

                ["wellbeing.recover"] = "🧘",
                ["relationship.find_spouse"] = "💍",
                ["reproduction.try_for_baby"] = "👶",
                ["relationship.divorce_spouse"] = "💔",
                ["relationship.marry_off_daughter"] = "💒",
                ["relationship.marry_off_son"] = "💒",
                ["relationship.repair_marriage"] = "❤️‍🩹",

                ["career.quit_job"] = "🚶",
                ["career.work_harder"] = "✨",
                ["career.seek_employment"] = "✅",
                ["career.find_another_job"] = "🔎",
                ["career.use_family_connections"] = "🤝",
                ["career.help_seek_employment"] = "✅",
                ["career.help_find_better_job"] = "🔎",
                ["career.ask_to_recover"] = "🧘",
                ["career.ask_to_quit"] = "🚶",

                ["wellbeing.drink"] = "🍺",
                ["wellbeing.therapy"] = "😊",
                ["personality.religious_study"] = "📖",
                ["wellbeing.heal_relative"] = "❤️‍🩹",

                ["education.get_education"] = "🎓",
                ["education.help_learning"] = "📚",
                ["childhood.raise_child"] = "🫂",

                ["ui.town_affairs"] = "🏛️",
                ["ui.self_improvement"] = "🛠️",
                ["ui.manage_properties"] = "🏘️",
                ["ui.manage_finances"] = "🏦",
                ["stats.improve_strength"] = "🏋️",
                ["stats.improve_intellect"] = "🧠",
                ["stats.improve_immunity"] = "🛡️",
                ["stats.improve_appeal"] = "✨",
                ["stats.improve_longevity"] = "🩺",
                ["stats.improve_fertility"] = "🧬",

                ["household.buy_house"] = "🏠",
                ["household.sell_house"] = "💵",
                ["farming.buy_farmland"] = "🌾",
                ["farming.sell_farmland"] = "🌾",
                ["household.give_house_to_son"] = "🎁",
                ["household.ask_parents_house"] = "🙏",
                ["household.ask_father_house"] = "🙏",
                ["household.ask_mother_house"] = "🙏",
                ["household.hire_nanny"] = "🧑‍🍼",
                ["household.ask_daughter_nanny"] = "🧑‍🍼",
                ["household.ask_move_out"] = "🚪",
                ["household.fire_nanny"] = "👋",

                ["family_support.ask_parents"] = "🙏",

                // One of the two missing action-family emojis:
                // use the same original money-request symbol as parents.
                ["family_support.ask_child"] = "🙏",
                ["craft.stop_occupation"] = "🚶",
                ["family_relations.ask_farmland"] = "🙏",
                ["family_relations.ask_house"] = "🙏",
                ["family_relations.ask_job_help"] = "🤝",
                ["family_relations.ask_money"] = "🙏",
                ["family_relations.give_farmland"] = "🎁",
                ["family_relations.give_house"] = "🎁",
                ["family_relations.give_job_help"] = "🤝",
                ["family_relations.give_money"] = "🎁",
                ["family_relations.improve"] = "❤️‍🩹",
                ["loan.give"] = "🤝",
                ["loan.take"] = "🏦"
            };

    public static string Format(
        string actionId,
        string label)
    {
        if (actionId.StartsWith(
            "household.move.",
            StringComparison.OrdinalIgnoreCase))
        {
            return $"🚚 {label}";
        }

        if (actionId.StartsWith(
            "craft.start.",
            StringComparison.OrdinalIgnoreCase)
            || actionId.StartsWith(
                "craft.teach.",
                StringComparison.OrdinalIgnoreCase))
        {
            return $"🛠️ {label}";
        }

        if (!Emojis.TryGetValue(
            actionId,
            out var emoji))
        {
            emoji = actionId.StartsWith("career.", StringComparison.OrdinalIgnoreCase)
                ? "💼"
                : actionId.StartsWith("relationship.", StringComparison.OrdinalIgnoreCase)
                    ? "💞"
                    : actionId.StartsWith("household.", StringComparison.OrdinalIgnoreCase)
                        ? "🏠"
                        : actionId.StartsWith("family_relations.", StringComparison.OrdinalIgnoreCase)
                            ? "👪"
                            : actionId.StartsWith("loan.", StringComparison.OrdinalIgnoreCase)
                                ? "🏦"
                                : actionId.StartsWith("craft.", StringComparison.OrdinalIgnoreCase)
                                    ? "🛠️"
                                    : actionId.StartsWith("farming.", StringComparison.OrdinalIgnoreCase)
                                        ? "🌾"
                                        : actionId.StartsWith("education.", StringComparison.OrdinalIgnoreCase)
                                            ? "🎓"
                                            : "⚙️";
        }

        return $"{emoji} {label}";
    }
}
