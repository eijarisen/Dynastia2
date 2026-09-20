namespace Dynastia.Contracts;

public interface IChildHappinessService
{
    ChildHappinessSnapshot? GetHappiness(IPerson person);
    int? GetFinalChildhoodHappiness(IPerson person);
    void EnsureHappiness(IPerson person);
    void ChangeHappiness(IPerson person, int amount);
}
