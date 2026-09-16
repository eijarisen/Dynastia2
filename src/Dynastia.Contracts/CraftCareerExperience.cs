namespace Dynastia.Contracts;

public sealed record CraftCareerExperience(
    int ExactYears,
    int RelatedYears)
{
    public int TotalYears => ExactYears + RelatedYears;
}
