namespace Dynastia.Contracts;

public interface IStressService
{
    StressSnapshot GetStress(IPerson person);
}
