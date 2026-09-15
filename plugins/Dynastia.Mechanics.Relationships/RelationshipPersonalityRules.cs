using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

internal static class RelationshipPersonalityRules
{
    public const int MaximumNewPartnerAge = 80;

    private static readonly string[] MainStatIds =
    [
        "immunity",
        "longevity",
        "fertility",
        "appeal",
        "strength",
        "intellect"
    ];

    public static bool CanFindPartner(
        IPerson seeker,
        Sex partnerSex) =>
        TryGetPartnerAgeRange(
            seeker,
            partnerSex,
            out _,
            out _,
            out _);

    public static bool TryChoosePartnerAge(
        IPerson seeker,
        Sex partnerSex,
        IGameRandom random,
        out int partnerAge)
    {
        partnerAge = 0;

        if (!TryGetPartnerAgeRange(
                seeker,
                partnerSex,
                out var minimum,
                out var maximum,
                out var prefersReproductiveAge))
        {
            return false;
        }

        // Female partners are preferably generated while still within
        // the game's normal reproductive age range. Morals continue to
        // define the acceptable age gap: Good +/-10, Neutral +/-20,
        // while Evil has no normal gap limit and strongly favors youth.
        if (partnerSex == Sex.Female)
        {
            if (seeker.Tags.Has("morals.evil"))
            {
                var first = random.NextInt(18, 45);
                var second = random.NextInt(18, 45);

                partnerAge = Math.Min(first, second);
                return true;
            }

            var preferredMaximum =
                Math.Min(45, maximum);

            if (prefersReproductiveAge
                && minimum <= preferredMaximum)
            {
                partnerAge = random.NextInt(
                    minimum,
                    preferredMaximum);

                return true;
            }

            partnerAge = random.NextInt(
                minimum,
                maximum);

            return true;
        }

        if (seeker.Tags.Has("morals.good"))
        {
            partnerAge = random.NextInt(
                minimum,
                maximum);

            return true;
        }

        if (!seeker.Tags.Has("morals.evil"))
        {
            partnerAge = random.NextInt(
                minimum,
                maximum);

            return true;
        }

        // Evil characters do not use the normal 10/20-year gap cap.
        // For male partners, preserve the existing bias toward older men.
        var firstMaleAge =
            random.NextInt(
                minimum,
                maximum);

        var secondMaleAge =
            random.NextInt(
                minimum,
                maximum);

        partnerAge = Math.Max(
            firstMaleAge,
            secondMaleAge);

        return true;
    }

    private static bool TryGetPartnerAgeRange(
        IPerson seeker,
        Sex partnerSex,
        out int minimum,
        out int maximum,
        out bool prefersReproductiveAge)
    {
        minimum = 18;
        maximum = MaximumNewPartnerAge;
        prefersReproductiveAge = false;

        if (partnerSex == Sex.Female
            && seeker.Tags.Has("morals.evil"))
        {
            maximum = 45;
            prefersReproductiveAge = true;
            return true;
        }

        if (seeker.Tags.Has("morals.evil"))
        {
            maximum = Math.Min(
                MaximumNewPartnerAge,
                Math.Max(60, seeker.Age + 30));

            return minimum <= maximum;
        }

        var gap =
            seeker.Tags.Has("morals.good")
                ? 10
                : 20;

        minimum = Math.Max(
            18,
            seeker.Age - gap);

        maximum = Math.Min(
            MaximumNewPartnerAge,
            seeker.Age + gap);

        prefersReproductiveAge =
            partnerSex == Sex.Female;

        return minimum <= maximum;
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
                choleric: 0.15,
                good: positive ? 0.10 : -0.15,
                evil: positive ? -0.10 : 0.15);

        var secondMultiplier =
            PersonalityInfluence.Multiplier(
                second,
                melancholic: 0.15,
                phlegmatic: -0.10,
                sanguine: positive ? 0.10 : 0,
                choleric: 0.15,
                good: positive ? 0.10 : -0.15,
                evil: positive ? -0.10 : 0.15);

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
