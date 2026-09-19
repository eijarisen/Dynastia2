namespace Dynastia.Contracts;

public interface INationalityService
{
    string GetNationality(IPerson person);

    void SetNationality(
        IPerson person,
        string id);

    string GetDisplayName(string id);

    string GetNameCultureId(string id);

    IReadOnlyDictionary<string, double> ResolveDistribution(
        string regionId,
        int year);

    string GenerateNationality(
        string regionId,
        int year,
        IGameRandom random);

    void RegisterDistributionModifierProvider(
        INationalityDistributionModifierProvider provider);

    string FormatSurname(
        IPerson person,
        string surname,
        Sex sex);
}
