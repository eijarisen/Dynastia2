namespace Dynastia.App.ViewModels;

public static class ActionEmojiMap
{
    private static readonly IReadOnlyDictionary<string, string>
        Emojis =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["wellbeing.recover"] = "🧘",
                ["relationship.find_spouse"] = "💍",
                ["reproduction.try_for_baby"] = "👶",
                ["relationship.divorce_spouse"] = "💔",

                ["career.quit_job"] = "🚶",
                ["career.work_harder"] = "✨",
                ["career.seek_employment"] = "✅",
                ["career.help_seek_employment"] = "✅",
                ["career.ask_to_recover"] = "🧘",
                ["career.ask_to_quit"] = "🚶",

                ["wellbeing.drink"] = "🍺",
                ["wellbeing.therapy"] = "😊",
                ["wellbeing.heal_relative"] = "❤️‍🩹",

                ["education.get_education"] = "🎓",
                ["education.help_learning"] = "📚",

                ["household.buy_house"] = "🏠",
                ["household.sell_house"] = "💵",
                ["household.give_house_to_son"] = "🎁",
                ["household.hire_nanny"] = "🧑‍🍼",
                ["household.fire_nanny"] = "👋",

                ["family_support.ask_parents"] = "🙏",

                // One of the two missing action-family emojis:
                // use the same original money-request symbol as parents.
                ["family_support.ask_child"] = "🙏"
            };

    public static string Format(
        string actionId,
        string label)
    {
        if (!Emojis.TryGetValue(
            actionId,
            out var emoji))
        {
            return $"🔹 {label}";
        }

        return $"{emoji} {label}";
    }
}
