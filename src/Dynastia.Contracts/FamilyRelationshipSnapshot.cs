namespace Dynastia.Contracts;

public sealed record FamilyRelationshipSnapshot(
    Guid PersonAId,
    Guid PersonBId,
    FamilyRelationshipType Type,
    double Score,
    string State,
    int CreatedYear,
    int LastMajorInteractionYear);
