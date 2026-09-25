namespace Dynastia.Contracts;

[Flags]
public enum ThoughtSalienceTraits
{
    None = 0,
    Emotional = 1 << 0,
    Negative = 1 << 1,
    Positive = 1 << 2,
    Career = 1 << 3,
    ImmediateProblem = 1 << 4,
    FamilyLoss = 1 << 5,
    Orphanhood = 1 << 6,
    Placement = 1 << 7,
    ParentsDivorced = 1 << 8,
    Health = 1 << 9,
    Education = 1 << 10,
    FamilyBirth = 1 << 11,
    Poverty = 1 << 12,
    HouseholdStrain = 1 << 13,
    RareTrauma = 1 << 14,

    // The legacy personality adjustment gave an extra melancholic bonus to
    // bereavement, divorce, firing and assault. Keep that semantic explicit
    // rather than re-inferring it from IDs or wording.
    MelancholicHighImpact = 1 << 15
}
