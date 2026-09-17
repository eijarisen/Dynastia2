namespace Dynastia.Contracts;

public interface IContextWeightService
{
    IContextWeightCatalog LoadCatalog(
        string relativePath,
        IEnumerable<string> knownItemIds);

    string GetAgeBand(int age);
}
