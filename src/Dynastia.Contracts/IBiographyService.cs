namespace Dynastia.Contracts;

public interface IBiographyService
{
    string GetAbout(
        IPerson person);

    IReadOnlyList<BiographyEntry> GetBiography(
        IPerson person);
}
