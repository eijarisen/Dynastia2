namespace Dynastia.Contracts;

public interface ICriminalOccupationService
{
    CriminalOccupationSnapshot GetSnapshot(IPerson person);

    bool HasStartedLifeOfCrime(IPerson person);

    bool IsActive(IPerson person);

    bool StartLifeOfCrime(IPerson person);

    bool EndLifeOfCrime(IPerson person, string reason = "ended");

    decimal GetExpectedAnnualIncome(IPerson person);
}
