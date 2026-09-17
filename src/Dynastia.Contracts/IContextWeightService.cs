namespace Dynastia.Contracts;

public interface IContextWeightService
{
    IContextWeightCatalog LoadCatalog(
        string relativePath,
        IEnumerable<string> knownItemIds);

    IContextWeightCatalog LoadGlobalCatalog(
        string relativePath,
        string itemId = "global");

    string GetAgeBand(int age);
}
