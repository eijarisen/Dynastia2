using Dynastia.Contracts;

namespace Dynastia.Mechanics.Relationships;

public static class PartnerSearchRules
{
    public static double CalculatePartnerValue(
        Sex sex,
        int age,
        IEnumerable<int> statValues,
        int educationLevel,
        int careerLevel,
        decimal householdWealth = 0m,
        int housesOwned = 0)
    {
        ArgumentNullException.ThrowIfNull(statValues);

        var statTotal = Math.Clamp(statValues.Sum(), 0, 30);
        var education = Math.Clamp(educationLevel, 0, 5)
            / 5.0 * 10.0;

        if (sex == Sex.Female)
        {
            var traits = statTotal / 30.0 * 70.0;
            var reproductiveValue = Math.Clamp(
                (45.0 - age) / (45.0 - 18.0),
                0,
                1) * 20.0;

            return Math.Clamp(
                traits + education + reproductiveValue,
                0,
                100);
        }

        // For male partners, household resources are part of marriage-market
        // attractiveness alongside traits, education and career standing.
        // One ordinary house price (20,000 zł) is enough to fill the cash half
        // of this component; up to two owned houses fill the property half.
        var maleTraits = statTotal / 30.0 * 65.0;
        var career = Math.Clamp(careerLevel, 0, 5)
            / 5.0 * 15.0;
        var cash = Math.Clamp((double)householdWealth / 20_000.0, 0, 1)
            * 5.0;
        var property = Math.Clamp(housesOwned, 0, 2)
            / 2.0 * 5.0;

        return Math.Clamp(
            maleTraits + education + career + cash + property,
            0,
            100);
    }

    public static double CalculateAcceptanceChance(
        double searcherValue,
        double candidateValue) =>
        Math.Clamp(
            0.85 + 0.02 * (searcherValue - candidateValue),
            0.10,
            0.95);
}
