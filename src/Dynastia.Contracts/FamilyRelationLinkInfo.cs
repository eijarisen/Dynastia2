namespace Dynastia.Contracts;

public sealed record FamilyRelationLinkInfo(
    Guid RelativeId,
    FamilyRelationshipType Type,
    string Kinship,
    double Score,
    string State,
    double Familiarity,
    string FamiliarityState,
    double Sympathy,
    string SympathyState);
