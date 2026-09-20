namespace Dynastia.Contracts;

public interface IWorkCapacityService
{
    WorkCapacitySnapshot GetWorkCapacity(IPerson person);
}
