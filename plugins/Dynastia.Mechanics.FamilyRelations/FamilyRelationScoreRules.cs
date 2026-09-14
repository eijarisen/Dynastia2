namespace Dynastia.Mechanics.FamilyRelations;

public static class FamilyRelationScoreRules
{
    public static string GetDisplayState(double score) => Math.Clamp(score, 0, 100) switch
    {
        < 20 => "Hostile",
        < 40 => "Poor",
        < 60 => "Neutral",
        < 80 => "Good",
        _ => "Close"
    };

    public static double GetRequestWillingness(double score, double abilityFactor = 1.0)
    {
        var social = Math.Clamp(score, 0, 100) switch
        {
            < 20 => 0.10,
            < 40 => 0.25,
            < 60 => 0.50,
            < 80 => 0.72,
            _ => 0.95
        };

        return Math.Clamp(
            social * Math.Clamp(abilityFactor, 0.25, 1.25),
            0.05,
            0.95);
    }
}
