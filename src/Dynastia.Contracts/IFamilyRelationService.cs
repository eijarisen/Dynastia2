namespace Dynastia.Contracts;

public interface IFamilyRelationService
{
    FamilyRelationshipSnapshot? GetRelation(
        IPerson first,
        IPerson second);

    FamilyRelationshipSnapshot EnsureRelation(
        IPerson first,
        IPerson second,
        FamilyRelationshipType type,
        double startingScore,
        bool majorInteraction = false);

    FamilyRelationshipSnapshot ModifyRelation(
        IPerson first,
        IPerson second,
        double amount,
        bool majorInteraction = true);

    string GetDisplayState(double score);

    IReadOnlyList<RelatedFamilyHouseholdInfo> GetRelatedHouseholds(
        IPerson activeHouseholdHead);

    double EvaluateRequestWillingness(
        IPerson requester,
        IPerson relative,
        double abilityFactor = 1.0);

    void ReconcileAll();
}
