namespace Dynastia.Contracts;

public sealed record HouseholdStatusSnapshot(
    Guid HeadId,
    int UnderageChildren,
    int BaseChildCapacity,
    int EffectiveChildCapacity,
    Guid? NannyId,
    string? NannyName,
    bool HasNannyReference,
    bool IsLargeFamilyStrained,
    bool IsAtCapacityWarning,
    bool IsBroke,
    IReadOnlyList<string> Warnings,
    string? NannyRoleLabel = null);
