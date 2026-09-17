namespace Dynastia.Mechanics.Health;

public sealed record StressOutcomeDefinition(
    string ConditionId,
    int StartYear,
    int? EndYear,
    int MinimumAge,
    double MinimumStress,
    double BaseWeight)
{
    public bool IsEligible(int year, int age, double stress) =>
        year >= StartYear
        && (EndYear is null || year <= EndYear.Value)
        && age >= MinimumAge
        && stress >= MinimumStress;
}
