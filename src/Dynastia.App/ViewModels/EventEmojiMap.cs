namespace Dynastia.App.ViewModels;

public static class EventEmojiMap
{
    private static readonly IReadOnlyDictionary<string, string>
        EmojiMap =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase)
            {
                // Exact Dynasty 4 emojiMap keys.
                ["heal"] = "❤️‍🩹",
                ["recover"] = " ",
                ["educationSuccess"] = "🎓",
                ["educationFail"] = "🧱",
                ["therapySuccess"] = "😊",
                ["therapyFail"] = "😒",
                ["drink"] = "🍺",
                ["developsAlcoholism"] = " ",
                ["adult"] = "🧑",
                ["receiveInheritance"] = "💰",
                ["retire"] = "🕊️",
                ["releaseFromPrison"] = "✅",
                ["illness"] = "🤧",
                ["seriousIllness"] = "😣",
                ["death"] = "💀",
                ["quitJob"] = "🚶",
                ["divorce"] = "💔",
                ["seekEmploymentSuccess"] = "✅",
                ["askForMoneySuccess"] = "🙏",
                ["askForMoneyFail"] = "🚫",
                ["askToQuitJobSuccess"] = "✅",
                ["askToQuitJobFail"] = "🚫",
                ["askToRecoverSuccess"] = "✅",
                ["askToRecoverFail"] = "🚫",
                ["crime"] = "⛓️",
                ["fired"] = "💥",
                ["promotion"] = "✨",
                ["spouseInheritance"] = "💍",
                ["partnership"] = "👩‍❤️‍👩",
                ["marriage"] = "💍",
                ["affair"] = "🤫",
                ["birthDefect"] = "🧩",
                ["birth"] = "👶",
                ["remarriage"] = "💍",
                ["estateSettled"] = "🏦",
                ["pendingInheritance"] = "⏳",
                ["claimableInheritance"] = "⏳",
                ["householdInheritance"] = "💸",
                ["lostInheritance"] = "💨",
                ["buyHouse"] = "🏠",
                ["sellHouse"] = "💵",
                ["rentHouse"] = "🏘️",
                ["giveHouse"] = "🎁",
                ["houseInheritance"] = "🏡",
                ["houseTransfer"] = "🏡",
                ["hireNanny"] = "🧑‍🍼",
                ["fireNanny"] = "👋",
                ["default"] = "🔹",

                // Modular Dynastia event IDs -> original visual meaning.
                ["wellbeing.heal"] = "❤️‍🩹",
                ["wellbeing.recover"] = " ",
                ["education.success"] = "🎓",
                ["education.failure"] = "🧱",
                ["education.help_learning_success"] = "📚",
                ["education.help_learning_failure"] = "📖",
                ["wellbeing.therapy_success"] = "😊",
                ["wellbeing.therapy_failure"] = "😒",
                ["wellbeing.drink"] = "🍺",
                ["life.adult"] = "🧑",

                ["inheritance.received_at_adulthood"] = "💰",
                ["inheritance.received"] = "💸",
                ["inheritance.pending_minor"] = "⏳",
                ["inheritance.claimable"] = "⏳",
                ["inheritance.unclaimed"] = "💨",
                ["inheritance.estate_settled"] = "🏦",
                ["inheritance.houses"] = "🏡",
                ["inheritance.promised_houses_received"] = "🏡",

                ["career.retirement"] = "🕊️",
                ["justice.released"] = "✅",
                ["health.illness"] = "🤧",
                ["health.serious_illness"] = "😣",
                ["life.death"] = "💀",
                ["career.quit"] = "🚶",
                ["relationship.divorce"] = "💔",
                ["relationship.low_satisfaction_divorce"] = "💔",
                ["relationship.repair_marriage"] = "❤️‍🩹",
                ["relationship.prison_divorce"] = "💔",
                ["career.employment"] = "✅",
                ["family_support.parents_success"] = "🙏",
                ["family_support.parents_failure"] = "🚫",

                // Filled missing family-support event emojis.
                ["family_support.child_success"] = "🙏",
                ["family_support.child_failure"] = "🚫",

                ["career.ask_quit_success"] = "✅",
                ["career.ask_quit_failure"] = "🚫",
                ["career.ask_recover_success"] = "✅",
                ["career.ask_recover_failure"] = "🚫",
                ["justice.crime"] = "⛓️",
                ["career.fired"] = "💥",
                ["career.promotion"] = "✨",
                ["relationship.partnered"] = "👩‍❤️‍👩",
                ["relationship.married"] = "💍",
                ["relationship.affair"] = "🤫",
                ["birth.condition"] = "🧩",
                ["life.birth"] = "👶",
                ["relationship.remarried"] = "💍",

                ["household.house_bought"] = "🏠",
                ["household.house_sold"] = "💵",
                ["household.house_rented"] = "🏘️",
                ["household.house_given"] = "🎁",

                // Filled the other missing action-family event emoji.
                ["household.house_promised"] = "🎁",

                ["household.nanny_hired"] = "🧑‍🍼",
                ["household.nanny_fired"] = "👋",

                ["adoption.with_mother"] = "👩‍👧",
                ["adoption.orphaned"] = "🕯️",
                ["adoption.placed"] = "🏠",
                ["adoption.orphanage"] = "🏚️",
                ["adoption.left_orphanage"] = "🧳"
            };

    public static string GetEmoji(
        string eventType)
    {
        return EmojiMap.TryGetValue(
            eventType,
            out var emoji)
                ? emoji
                : EmojiMap["default"];
    }
}
