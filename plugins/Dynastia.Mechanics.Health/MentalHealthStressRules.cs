using Dynastia.Contracts;

namespace Dynastia.Mechanics.Health;

public static class MentalHealthStressRules
{
    public static double GetReactionChance(
        int stress,
        IPerson person,
        int existingStressConditions)
    {
        if (stress <= 0)
            return 0;

        var chance =
            0.0015
            + stress * 0.011;

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

    public static double GetDepressionWeight(IPerson person)
    {
        if (person.Tags.Has("personality.melancholic"))
            return 1.60;

        if (person.Tags.Has("personality.choleric"))
            return 0.70;

        return 1.0;
    }

    public static double GetAnxietyWeight(IPerson person)
    {
        if (person.Tags.Has("personality.melancholic"))
            return 0.85;

        if (person.Tags.Has("personality.choleric"))
            return 1.30;

        return 1.0;
    }

    public static double GetAlcoholismWeight(
        IPerson person,
        int stress)
    {
        if (person.Age < 18 || stress < 4)
            return 0;

        var weight = stress switch
        {
            >= 8 => 0.60,
            >= 6 => 0.40,
            _ => 0.25
        };

        if (person.Tags.Has("personality.choleric"))
            weight *= 1.25;
        else if (person.Tags.Has("personality.sanguine"))
            weight *= 1.15;
        else if (person.Tags.Has("personality.phlegmatic"))
            weight *= 0.70;
        else if (person.Tags.Has("personality.melancholic"))
            weight *= 0.90;

        if (person.Tags.Has("morals.good"))
            weight *= 0.80;
        else if (person.Tags.Has("morals.evil"))
            weight *= 1.20;

        return weight;
    }

    private static double GetTemperamentMultiplier(IPerson person) =>
        person.Tags.Has("personality.melancholic") ? 1.60
        : person.Tags.Has("personality.choleric") ? 1.35
        : person.Tags.Has("personality.sanguine") ? 0.65
        : person.Tags.Has("personality.phlegmatic") ? 0.55
        : 1.0;
}
