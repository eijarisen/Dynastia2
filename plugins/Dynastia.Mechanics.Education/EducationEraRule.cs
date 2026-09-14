namespace Dynastia.Mechanics.Education;

public sealed record EducationEraRule(
    int StartYear,
    int? EndYear,
    double PassiveChanceMultiplier,
    int PassiveMaxLevel,
    int HelpedMaxLevel,
    int FounderMinLevel,
    int FounderMaxLevel,
    int GeneratedAdultMinLevel,
    int GeneratedAdultMaxLevel)
{
    public bool Covers(int year) =>
        year >= StartYear
        && (EndYear is null || year <= EndYear.Value);
}
