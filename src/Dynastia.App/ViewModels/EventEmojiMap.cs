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
                ["recover"] = "🛌",
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
                ["default"] = "📌",

                // Modular Dynastia event IDs -> original visual meaning.
                ["wellbeing.heal"] = "❤️‍🩹",
                ["wellbeing.recover"] = "🛌",
                ["education.success"] = "🎓",
                ["education.failure"] = "🧱",
                ["education.help_learning_success"] = "📚",
                ["education.help_learning_failure"] = "📖",
                ["wellbeing.therapy_success"] = "😊",
                ["wellbeing.therapy_failure"] = "😒",
                ["family.assimilated_polish"] = "🇵🇱",
                ["family.polish_surname_adopted"] = "🇵🇱",
                ["wellbeing.drink"] = "🍺",
                ["life.adult"] = "🧑",

                ["inheritance.received_at_adulthood"] = "💰",
                ["inheritance.received"] = "💸",
                ["inheritance.pending_minor"] = "⏳",
                ["inheritance.claimable"] = "⏳",
                ["inheritance.unclaimed"] = "💨",
                ["inheritance.estate_settled"] = "🏦",
                ["inheritance.houses"] = "🏡",
                ["inheritance.pending"] = "⏳",
                ["inheritance.pending_houses"] = "🏡",
                ["inheritance.received_at_household"] = "💸",
                ["inheritance.pending_houses_received"] = "🏡",
                ["inheritance.estate_left_dynasty"] = "💨",
                ["household.assets_followed_anchor"] = "🏠",
                ["inheritance.promised_houses_received"] = "🏡",

                ["heirloom.created"] = "🏺",
                ["heirloom.inherited"] = "🎁",
                ["heirloom.pending"] = "⏳",
                ["heirloom.sold"] = "💵",

                ["career.retirement"] = "🕊️",
                ["justice.released"] = "✅",
                ["health.illness"] = "🤧",
                ["health.serious_illness"] = "😣",
                ["health.natural_recovery"] = "❤️‍🩹",
                ["health.second_wind"] = "❤️‍🔥",
                ["economy.careful_management"] = "💰",
                ["loan.taken"] = "🏦",
                ["loan.given"] = "🤝",
                ["loan.repaid"] = "✅",
                ["loan.receivable_repaid"] = "✅",
                ["loan.debt_inherited"] = "📜",
                ["personality.religious_study"] = "📖",
                ["church.attend"] = "⛪",
                ["church.donate"] = "⛪",
                ["church.aid_poor"] = "🤝",
                ["church.welfare"] = "🥖",
                ["religion.calling_priest"] = "⛪",
                ["religion.calling_nun"] = "🙏",
                ["religion.vocation_left"] = "🚪",
                ["community.lobby"] = "🗣️",
                ["community.policy_enacted"] = "🏛️",
                ["connection.created"] = "🤝",
                ["connection.interaction"] = "🤝",
                ["connection.request_refused"] = "🚫",
                ["connection.request_accepted"] = "🤝",
                ["connection.lost"] = "👋",
                ["relationship.divorce_refused"] = "💍",
                ["life.death"] = "💀",
                ["career.quit"] = "🚶",
                ["relationship.divorce"] = "💔",
                ["relationship.marry_off_failed"] = "💒",
                ["relationship.low_satisfaction_divorce"] = "💔",
                ["relationship.repair_marriage"] = "❤️‍🩹",
                ["relationship.prison_divorce"] = "💔",
                ["career.employment"] = "✅",
                ["career.family_connections_success"] = "🤝",
                ["career.family_connections_failure"] = "🚫",
                ["family_support.parents_success"] = "🙏",
                ["family_support.parents_failure"] = "🚫",

                // Filled missing family-support event emojis.
                ["family_support.child_success"] = "🙏",
                ["family_support.child_failure"] = "🚫",
                ["family_relations.money_received"] = "💰",
                ["family_relations.money_given"] = "🎁",
                ["family_relations.money_refused"] = "🚫",

                ["career.ask_quit_success"] = "✅",
                ["career.ask_quit_failure"] = "🚫",
                ["career.ask_recover_success"] = "✅",
                ["career.ask_recover_failure"] = "🚫",
                ["justice.crime"] = "⛓️",
                ["justice.crime_uncaught"] = "🕵️",
                ["career.fired"] = "💥",
                ["career.promotion"] = "✨",
                ["relationship.partnered"] = "👩‍❤️‍👩",
                ["relationship.courtship"] = "💌",
                ["relationship.married"] = "💍",
                ["relationship.affair"] = "🤫",
                ["birth.condition"] = "🧩",
                ["reproduction.unknown_father_birth"] = "👶",
                ["reproduction.birth_and_marriage"] = "💍",
                ["reproduction.teen_birth"] = "👶",
                ["reproduction.teen_marriage"] = "💍",
                ["life.birth"] = "👶",
                ["peripheral.birth"] = "👶",
                ["relationship.remarried"] = "💍",

                ["household.house_bought"] = "🏠",
                ["household.house_sold"] = "💵",
                ["household.moved"] = "🚚",
                ["household.son_moved_out"] = "🚪",
                ["household.member_moved_out"] = "🚪",
                ["household.move_out_refused"] = "🚫",
                ["household.house_rented"] = "🏘️",
                ["household.house_given"] = "🎁",
                ["farmland.bought"] = "🌾",
                ["farmland.sold"] = "🌾",
                ["farmland.inherited"] = "🌾",
                ["farmland.given"] = "🌾",
                ["farmland.received"] = "🌾",
                ["farmland.request_refused"] = "🚫",
                ["craft.learned"] = "🛠️",
                ["craft.teaching_failed"] = "🧱",
                ["craft.self_employment_started"] = "🛠️",
                ["craft.self_employment_ended"] = "🚶",
                ["artistic.work_created"] = "🎨",

                // Filled the other missing action-family event emoji.
                ["household.house_promised"] = "🎁",

                ["household.nanny_hired"] = "🧑‍🍼",
                ["household.family_nanny_started"] = "🧑‍🍼",
                ["household.family_nanny_ended"] = "👋",
                ["household.nanny_service_ended"] = "👋",
                ["household.nanny_fired"] = "👋",

                ["adoption.with_mother"] = "👩‍👧",
                ["adoption.orphaned"] = "🕯️",
                ["adoption.placed"] = "🏠",
                ["adoption.orphanage"] = "🏚️",
                ["adoption.left_orphanage"] = "🧳",

                ["career.changed_job"] = "🔄",
                ["career.relocated"] = "🚚",
                ["career.work_harder"] = "💪",
                ["childhood.raised"] = "🫂",
                ["craft.income"] = "💰",
                ["craft.major_commission"] = "🏅",
                ["farming.income"] = "🌾",
                ["game.started"] = "🏰",
                ["historical.milestone"] = "🗞️",
                ["historical.household_impact"] = "🏛️",
                ["historical.relocation"] = "🚚",
                ["historical.external_departure"] = "🧳",
                ["household.parent_house_gift"] = "🎁",
                ["household.parent_house_refused"] = "🚫",
                ["personality.morals_declined"] = "⚖️",
                ["personality.morals_protected"] = "🛡️",
                ["stats.paid_improvement"] = "📈",

                ["rare.house_fire"] = "🔥",
                ["rare.burglary"] = "🕵️",
                ["rare.storm_flood_damage"] = "🌊", // legacy history
                ["rare.storm_flood"] = "🌊",
                ["rare.structural_accident"] = "🧱",
                ["rare.legal_dispute"] = "⚖️",
                ["rare.major_repair"] = "🔧",
                ["rare.house_discovery"] = "💎",
                ["rare.exceptional_harvest"] = "🌾",
                ["rare.crop_failure"] = "🌾",
                ["rare.local_epidemic"] = "🦠",
                ["rare.assault"] = "🥊",
                ["rare.mugging"] = "💸",
                ["rare.workplace_accident"] = "⚠️",
                ["rare.traffic_accident"] = "🚗",
                ["rare.lightning_strike"] = "⚡",
                ["rare.serious_fall"] = "🤕",
                ["rare.lottery_win"] = "🎰",
                ["rare.distant_inheritance"] = "💰",
                ["rare.fraud"] = "🎭",
                ["rare.found_property"] = "💎",
                ["rare.wrongful_arrest"] = "⚖️",
                ["rare.water_accident"] = "🌊",
                ["rare.animal_accident"] = "🐎",
                ["rare.craft_setback"] = "🛠️",
                ["rare.scholarship"] = "🎓",
                ["rare.professional_recognition"] = "🏅",
                ["rare.patronage"] = "🤝",
                ["rare.prize_award"] = "🏆",
                ["rare.craft_commission"] = "🛠️",
                ["rare.suicide"] = "🕯️"
            };

    public static string GetEmoji(
        string eventType)
    {
        if (EmojiMap.TryGetValue(
                eventType,
                out var emoji))
        {
            return emoji;
        }

        if (eventType.StartsWith("historical.", StringComparison.OrdinalIgnoreCase)) return "🗞️";
        if (eventType.StartsWith("career.", StringComparison.OrdinalIgnoreCase)) return "💼";
        if (eventType.StartsWith("relationship.", StringComparison.OrdinalIgnoreCase)) return "💞";
        if (eventType.StartsWith("household.", StringComparison.OrdinalIgnoreCase)) return "🏠";
        if (eventType.StartsWith("family_", StringComparison.OrdinalIgnoreCase)
            || eventType.StartsWith("family.", StringComparison.OrdinalIgnoreCase)) return "👪";
        if (eventType.StartsWith("health.", StringComparison.OrdinalIgnoreCase)
            || eventType.StartsWith("wellbeing.", StringComparison.OrdinalIgnoreCase)) return "❤️‍🩹";
        if (eventType.StartsWith("education.", StringComparison.OrdinalIgnoreCase)) return "🎓";
        if (eventType.StartsWith("justice.", StringComparison.OrdinalIgnoreCase)) return "⚖️";
        if (eventType.StartsWith("craft.", StringComparison.OrdinalIgnoreCase)) return "🛠️";
        if (eventType.StartsWith("farming.", StringComparison.OrdinalIgnoreCase)) return "🌾";
        if (eventType.StartsWith("loan.", StringComparison.OrdinalIgnoreCase)) return "🏦";
        if (eventType.StartsWith("inheritance.", StringComparison.OrdinalIgnoreCase)) return "💰";
        if (eventType.StartsWith("personality.", StringComparison.OrdinalIgnoreCase)) return "🧭";
        if (eventType.StartsWith("church.", StringComparison.OrdinalIgnoreCase)) return "⛪";
        if (eventType.StartsWith("community.", StringComparison.OrdinalIgnoreCase)) return "🏛️";
        if (eventType.StartsWith("stats.", StringComparison.OrdinalIgnoreCase)) return "📈";
        if (eventType.StartsWith("rare.", StringComparison.OrdinalIgnoreCase)) return "⚠️";
        if (eventType.StartsWith("life.", StringComparison.OrdinalIgnoreCase)) return "📜";

        return EmojiMap["default"];
    }
}
