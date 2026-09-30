namespace Dynastia.Contracts;

public sealed record HouseholdConnectionInfo(
    Guid Id,
    Guid HouseholdId,
    string Name,
    Sex Sex,
    int Age,
    int? DeathYear,
    string NationalityId,
    string TownId,
    string OccupationLabel,
    string ArchetypeId,
    string WealthBand,
    double Renown,
    double Reputation,
    int Familiarity,
    int Sympathy,
    string RelationState,
    string? SpouseName,
    IReadOnlyList<string> Children,
    bool HasSpareHouse,
    bool HasSpareFarmland,
    string OriginPolicyId,
    bool IsActive);

public sealed record PoorFamilyProspectInfo(
    Guid ContactId,
    string FamilyName,
    string ContactName,
    Sex ContactSex,
    int ContactAge,
    string NationalityId,
    string TownId,
    string HouseholdSummary,
    string Portrait);

public sealed record HouseholdConnectionSeed(
    Guid ConnectionId,
    Guid HouseholdId,
    string Name,
    Sex Sex,
    int BirthYear,
    string NationalityId,
    string TownId,
    string OccupationLabel,
    string ArchetypeId,
    string WealthBand,
    double Renown,
    double Reputation,
    int StartingFamiliarity,
    int StartingSympathy,
    string? SpouseName,
    IReadOnlyList<string> Children,
    string OriginId);

public sealed record HouseholdNetworkSnapshot(
    int ActiveCount,
    int WarmCount,
    int CloseCount,
    double BreadthRenownBonus,
    double QualityRenownBonus,
    double TotalRenownBonus,
    double CivicCandidateWeightBonus);

public interface IHouseholdConnectionService
{
    IReadOnlyList<HouseholdConnectionInfo> GetConnections(
        Guid householdId,
        bool activeOnly = true);

    HouseholdNetworkSnapshot GetNetworkSnapshot(Guid householdId) =>
        new(0, 0, 0, 0, 0, GetNetworkRenownBonus(householdId), 0);

    HouseholdNetworkSnapshot GetLocalNetworkSnapshot(
        Guid householdId,
        string townId) =>
        new(0, 0, 0, 0, 0, 0, 0);

    double GetNetworkRenownBonus(Guid householdId);

    decimal GetEstimatedMoneyRequestMaximum(
        IPerson requester,
        Guid connectionId);

    HouseholdConnectionInfo EnsureLocalConnection(HouseholdConnectionSeed seed) =>
        throw new NotSupportedException(
            "This household-connection implementation does not support controlled connection creation.");
}
