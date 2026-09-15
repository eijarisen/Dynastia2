namespace Dynastia.Contracts;

public sealed record FamilyRelationshipSnapshot(
    Guid PersonAId,
    Guid PersonBId,
    FamilyRelationshipType Type,
    double Score,
    string State,
    double Familiarity,
    string FamiliarityState,
    double Sympathy,
    string SympathyState,
    int CreatedYear,
    int LastMajorInteractionYear);
