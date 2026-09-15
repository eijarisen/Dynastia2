namespace Dynastia.Contracts;

public interface IExistingLocationService
{
    LocationSnapshot? GetExistingLocation(
        IPerson person);
}
