namespace Dynastia.Contracts;

public sealed record HouseholdInfo(
    Guid HouseholdId,
    Guid HeadId,
    Guid DynastyAnchorId,
    int? Generation,
    string Surname,
    string HeadName,
    HouseholdClass Class,
    IReadOnlyList<Guid> MemberIds);
