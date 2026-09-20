using Dynastia.Contracts;

namespace Dynastia.Mechanics.FamilyRelations;

public static class FamilyRelationScoreRules
{
    public static string GetDisplayState(double score) => GetSympathyState(score);

    public static string GetFamiliarityState(double familiarity) => Math.Clamp(familiarity, 0, 100) switch
    {
        < 25 => "Distant",
        < 50 => "Known",
        < 75 => "Familiar",
        _ => "Close"
    };

    public static string GetSympathyState(double sympathy) => Math.Clamp(sympathy, 0, 100) switch
    {
        < 20 => "Hostile",
        < 40 => "Cold",
        < 60 => "Neutral",
        < 80 => "Warm",
        _ => "Affectionate"
    };

    public static double GetCompositeScore(double familiarity, double sympathy) =>
        Math.Clamp((Math.Clamp(familiarity, 0, 100) * 0.35) + (Math.Clamp(sympathy, 0, 100) * 0.65), 0, 100);

    public static double GetRequestWillingness(
        double familiarity,
        double sympathy,
        double abilityFactor = 1.0)
    {
        familiarity = Math.Clamp(familiarity, 0, 100);
        sympathy = Math.Clamp(sympathy, 0, 100);
        if (abilityFactor <= 0
            || (familiarity < 25 && sympathy < 25))
        {
            return 0.0;
        }

        var social = ((familiarity * 0.30) + (sympathy * 0.70)) / 100.0;
        var probability = 0.05 + (0.90 * social);
        if (sympathy < 20)
            probability = Math.Min(probability, 0.15);

        return Math.Clamp(probability * Math.Clamp(abilityFactor, 0.25, 1.25), 0.05, 0.95);
    }

    public static double GetOfferWillingness(double familiarity, double sympathy)
    {
        familiarity = Math.Clamp(familiarity, 0, 100);
        sympathy = Math.Clamp(sympathy, 0, 100);
        if (sympathy >= 35)
            return 1.0;
        if (sympathy >= 20)
            return Math.Clamp(0.80 + familiarity / 500.0, 0.80, 0.95);
        return Math.Clamp(0.30 + familiarity / 250.0, 0.30, 0.70);
    }

    public static double GetDeteriorationMultiplier(FamilyRelationshipType type) => type switch
    {
        FamilyRelationshipType.GrandparentGrandchild => 0.40,
        FamilyRelationshipType.ParentChild => 0.60,
        FamilyRelationshipType.Sibling => 0.65,
        FamilyRelationshipType.UncleAuntNieceNephew => 1.00,
        FamilyRelationshipType.FirstCousin => 1.00,
        _ => 1.00
    };
}
