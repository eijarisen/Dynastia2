namespace Dynastia.Contracts;

public interface IHouseholdService
{
    IPerson? ResolveHouseholdHead(
        IPerson person);

    HouseholdStatusSnapshot? GetStatus(
        IPerson head);

    IPerson? GetNanny(
        IPerson head);

    IReadOnlyList<HouseholdInfo> GetActiveHouseholds();

    HouseholdInfo? GetHouseholdInfo(
        IPerson person);

    bool IsAutonomousHousehold(
        IPerson person);

    MoveResidentBranchResult EstablishIndependentResidentBranch(
        IPerson sourceHead,
        IPerson newHead,
        Guid? propertyId = null);

    void ReconcileHouseholds();

    bool ShouldShowFamilyNews(
        GameEvent gameEvent);
}
