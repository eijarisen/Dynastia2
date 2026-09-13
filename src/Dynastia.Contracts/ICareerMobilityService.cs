namespace Dynastia.Contracts;

public interface ICareerMobilityService
{
    CareerMobilityResult FindAnotherJob(IPerson person);

    CareerMobilityResult ReestablishCareerAfterMove(IPerson person);
}
