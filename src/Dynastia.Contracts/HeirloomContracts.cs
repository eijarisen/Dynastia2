namespace Dynastia.Contracts;

public sealed record HeirloomOwnershipRecordInfo(
    int Year,
    Guid HouseholdId,
    Guid? PersonId,
    string Reason);

public sealed record HeirloomAssetInfo(
    Guid Id,
    string TemplateId,
    string Emoji,
    string DisplayName,
    decimal AppraisedValue,
    int AcquiredYear,
    Guid? OriginPersonId,
    string OriginTriggerType,
    string OriginTriggerId,
    string OriginDescription,
    Guid? AssignedHeirId,
    bool IsStolen,
    IReadOnlyList<HeirloomOwnershipRecordInfo> OwnershipHistory);

public sealed record HeirloomCreationRequest(
    string TemplateId,
    Guid? OriginPersonId,
    string OriginTriggerType,
    string OriginTriggerId,
    string OriginDescription,
    bool IsStolen = false,
    IReadOnlyDictionary<string, string>? NewsTokens = null);

public interface IHeirloomService
{
    IReadOnlyList<HeirloomAssetInfo> GetHeirlooms(
        IPerson householdMember);

    HeirloomAssetInfo Create(
        IPerson householdMember,
        HeirloomCreationRequest request);

    HeirloomAssetInfo? Take(
        IPerson householdMember,
        Guid heirloomId);

    IReadOnlyList<HeirloomAssetInfo> TakeAll(
        IPerson householdMember);

    void AddExisting(
        IPerson householdMember,
        HeirloomAssetInfo heirloom,
        int year,
        Guid? personId,
        string reason);

    IReadOnlyList<HeirloomAssetInfo> GetPending(
        IPerson person);

    void AddPending(
        IPerson person,
        HeirloomAssetInfo heirloom,
        int year,
        string reason);

    IReadOnlyList<HeirloomAssetInfo> TakePending(
        IPerson person);

    bool SetInheritanceHeir(
        IPerson householdMember,
        Guid heirloomId,
        Guid? heirId);

    decimal GetSaleValue(
        HeirloomAssetInfo heirloom);

    void ClearInheritanceAssignments(
        Guid heirId);
}
