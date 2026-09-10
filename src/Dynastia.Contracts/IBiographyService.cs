namespace Dynastia.Contracts;

public interface IBiographyService
{
    string GetAbout(
        IPerson person);

    IReadOnlyList<BiographyEntry> GetBiography(
        IPerson person);

    IReadOnlyDictionary<Guid, IReadOnlyList<BiographyEntry>>
        ExportBiographyState();

    void RestoreBiographyState(
        IReadOnlyDictionary<Guid, IReadOnlyList<BiographyEntry>> entries);
}
