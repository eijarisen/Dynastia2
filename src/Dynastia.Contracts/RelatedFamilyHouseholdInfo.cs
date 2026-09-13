namespace Dynastia.Contracts;

public sealed record RelatedFamilyHouseholdInfo(
    Guid? HouseholdId,
    Guid? HeadId,
    HouseholdClass? HouseholdClass,
    IReadOnlyList<FamilyRelationLinkInfo> Relations)
{
    public FamilyRelationLinkInfo PrimaryRelation =>
        Relations[0];
}
