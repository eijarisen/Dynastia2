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

public interface IHouseholdConnectionService
{
    IReadOnlyList<HouseholdConnectionInfo> GetConnections(
        Guid householdId,
        bool activeOnly = true);

    double GetNetworkRenownBonus(Guid householdId);

    decimal GetEstimatedMoneyRequestMaximum(
        IPerson requester,
        Guid connectionId);
}
