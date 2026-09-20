using Dynastia.Contracts;

namespace Dynastia.Mechanics.Health;

public static class MentalHealthStressRules
{
    public static double GetReactionChance(
        double stress,
        IPerson person,
        int existingStressConditions)
    {
        if (stress <= 0)
            return 0;

        var chance = 0.0015 + stress * 0.011;
        chance *= GetTemperamentMultiplier(person);

        if (existingStressConditions > 0)
        {
            chance *= existingStressConditions switch
            {
                1 => 0.55,
                _ => 0.40
            };
        }

        return Math.Clamp(chance, 0, 0.12);
    }

    public static double GetTemperamentMultiplier(IPerson person) =>
        person.Tags.Has("personality.melancholic") ? 1.60
        : person.Tags.Has("personality.choleric") ? 1.35
        : person.Tags.Has("personality.sanguine") ? 0.65
        : person.Tags.Has("personality.phlegmatic") ? 0.55
        : 1.0;


    public static double GetOutcomeWeightMultiplier(
        string conditionId,
        StressSnapshot stress)
    {
        if (!conditionId.Equals(
                "burnout",
                StringComparison.OrdinalIgnoreCase))
        {
            return 1.0;
        }

        var lowSatisfaction = stress.Contributions
            .Where(item => item.SourceId.Equals(
                "career.low_satisfaction",
                StringComparison.OrdinalIgnoreCase))
            .Sum(item => item.Value);

        var overwork = stress.Contributions
            .Where(item => item.SourceId.Equals(
                "career.overwork",
                StringComparison.OrdinalIgnoreCase))
            .Sum(item => item.Value);

        // Burnout remains possible under broad life stress, but career strain
        // makes it substantially more likely, especially after repeated
        // Work Harder use. This is an outcome-weight modifier rather than an
        // extra incidence roll, so it remains inside the shared Stress system.
        return Math.Clamp(
            1.0 + lowSatisfaction * 0.20 + overwork * 0.90,
            1.0,
            4.5);
    }

    // Retained for compatibility with existing diagnostics/tests. Automatic
    // outcome choice now comes from health_stress_outcomes.csv + context weights.
    public static double GetAlcoholismWeight(IPerson person, int stress)
    {
        if (person.Age < 18 || stress < 4)
            return 0;

        var weight = stress switch
        {
            >= 8 => 0.60,
            >= 6 => 0.40,
            _ => 0.25
        };

        if (person.Tags.Has("personality.choleric")) weight *= 1.50;
        else if (person.Tags.Has("personality.melancholic")) weight *= 1.15;
        else if (person.Tags.Has("personality.sanguine")) weight *= 0.90;
        else if (person.Tags.Has("personality.phlegmatic")) weight *= 0.75;

        if (person.Tags.Has("morals.good")) weight *= 0.80;
        else if (person.Tags.Has("morals.evil")) weight *= 1.20;

        return weight;
    }
}
