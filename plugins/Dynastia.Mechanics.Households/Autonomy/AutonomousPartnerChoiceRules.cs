using Dynastia.Contracts;

namespace Dynastia.Mechanics.Households;

internal static class AutonomousPartnerChoiceRules
{
    public static PartnerCandidateInfo? Choose(
        IReadOnlyList<PartnerCandidateInfo> candidates,
        Sex partnerSex,
        bool needsContinuity,
        bool needsIncome)
    {
        var eligible = candidates.Where(candidate =>
            candidate.AcceptanceChance > 0
            && (!needsContinuity || candidate.Sex == partnerSex
                && candidate.Stats.GetValueOrDefault("fertility") > 0
                // Marriage resolves next year, followed by a year before births.
                && (candidate.Sex != Sex.Female || candidate.Age is >= 18 and <= 43)));

        return eligible
            .OrderByDescending(candidate => needsContinuity
                ? ReproductiveValue(candidate) : 0)
            .ThenByDescending(candidate =>
                candidate.AcceptanceChance * 60
                + candidate.PartnerValue * 0.35
                + (needsIncome ? (double)candidate.AnnualIncome / 100 : 0))
            .ThenBy(candidate => candidate.CandidateKey, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static double ReproductiveValue(PartnerCandidateInfo candidate)
    {
        var years = candidate.Sex == Sex.Female
            ? Math.Clamp(44 - candidate.Age, 1, 20)
            : Math.Clamp(70 - candidate.Age, 1, 20);
        return Math.Clamp(candidate.AcceptanceChance, 0, 1)
            * Math.Clamp(candidate.Stats.GetValueOrDefault("fertility"), 0, 5)
            * years;
    }
}
