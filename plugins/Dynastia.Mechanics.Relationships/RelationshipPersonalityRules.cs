using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

internal static class RelationshipPersonalityRules
{
    private static readonly string[] MainStatIds =
    [
        "immunity",
        "longevity",
        "fertility",
        "appeal",
        "strength",
        "intellect"
    ];

    public static int ChoosePartnerAge(
        IPerson seeker,
        Sex partnerSex,
        IGameRandom random)
    {
        if (seeker.Tags.Has("morals.good"))
        {
            return random.NextInt(
                Math.Max(18, seeker.Age - 10),
                Math.Max(18, seeker.Age + 10));
        }

        if (!seeker.Tags.Has("morals.evil"))
        {
            return random.NextInt(
                Math.Max(18, seeker.Age - 20),
                Math.Max(18, seeker.Age + 20));
        }

        // Evil characters do not use the normal 10/20-year gap cap.
        // Draw twice from a broad adult range and bias the selection; the
        // opposite direction remains possible, so this stays a preference.
        var broadUpper =
            Math.Max(
                60,
                seeker.Age + 30);

        var first =
            random.NextInt(
                18,
                broadUpper);

        var second =
            random.NextInt(
                18,
                broadUpper);

        return partnerSex == Sex.Female
            ? Math.Min(first, second)
            : Math.Max(first, second);
    }

    public static bool ApplyExceptionalPartnerStats(
        IPerson seeker,
        IPerson partner,
        IStatsService stats,
        IGameRandom random)
    {
        if (GetStat(stats, seeker, "appeal") != 5)
            return false;

        var values =
            stats.GetBaseStats(partner)
                .ToDictionary(
                    stat => stat.Id,
                    stat => stat.Value,
                    StringComparer.OrdinalIgnoreCase);

        var candidates = MainStatIds.ToList();
        for (var i = 0; i < 2 && candidates.Count > 0; i++)
        {
            var index = random.NextInt(0, candidates.Count - 1);
            var statId = candidates[index];
            candidates.RemoveAt(index);

            values[statId] = Math.Min(5, values[statId] + 1);
        }

        stats.SetStats(partner, values);
        return true;
    }

    public static void ApplyExceptionalPartnerCareer(
        bool exceptionalMatch,
        IPerson partner,
        ICareerService career,
        IGameRandom random)
    {
        if (!exceptionalMatch || random.NextDouble() >= 0.35)
            return;

        var current = career.GetCareer(partner);
        if (current.IsRetired || current.JobLevel >= 5)
            return;

        career.SetJobLevel(partner, current.JobLevel + 1);
    }

    public static double AdjustMarriageChange(
        IPerson first,
        IPerson second,
        double amount,
        IStatsService stats)
    {
        if (Math.Abs(amount) < double.Epsilon)
            return amount;

        var positive = amount > 0;

        var firstMultiplier =
            PersonalityInfluence.Multiplier(
                first,
                melancholic: 0.15,
                phlegmatic: -0.10,
                sanguine: positive ? 0.10 : 0,
                choleric: 0.15);

        var secondMultiplier =
            PersonalityInfluence.Multiplier(
                second,
                melancholic: 0.15,
                phlegmatic: -0.10,
                sanguine: positive ? 0.10 : 0,
                choleric: 0.15);

        var multiplier =
            (firstMultiplier + secondMultiplier) / 2.0;

        if (!positive
            && (GetStat(stats, first, "strength") == 5
                || GetStat(stats, second, "strength") == 5))
        {
            multiplier *= 0.90;
        }

        return amount * multiplier;
    }

    public static double AdjustAutonomousDivorceChance(
        double chance,
        IPerson husband,
        IPerson wife,
        IStatsService stats)
    {
        var husbandMultiplier =
            PersonalityInfluence.Multiplier(
                husband,
                phlegmatic: -0.10,
                choleric: 0.10,
                good: -0.20);

        var wifeMultiplier =
            PersonalityInfluence.Multiplier(
                wife,
                phlegmatic: -0.10,
                choleric: 0.10,
                good: -0.20);

        var result =
            chance
            * ((husbandMultiplier + wifeMultiplier) / 2.0);

        if (GetStat(stats, husband, "strength") == 5
            || GetStat(stats, wife, "strength") == 5)
        {
            result *= 0.90;
        }

        return Math.Clamp(result, 0, 1);
    }

    private static int GetStat(
        IStatsService stats,
        IPerson person,
        string id)
    {
        return stats.GetStats(person)
            .First(stat =>
                stat.Id.Equals(
                    id,
                    StringComparison.OrdinalIgnoreCase))
            .Value;
    }
}
