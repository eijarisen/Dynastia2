namespace Dynastia.Contracts;

public interface IContextWeightCatalog
{
    double GetMultiplier(
        string itemId,
        ContextWeightContext context);
}
