namespace Dynastia.Contracts;

public interface IHouseholdService
{
    IPerson? ResolveHouseholdHead(
        IPerson person);

    HouseholdStatusSnapshot? GetStatus(
        IPerson head);

    IPerson? GetNanny(
        IPerson head);
}
