namespace Dynastia.Contracts;

public interface IContextWeightCatalog
{
    double GetMultiplier(
        string itemId,
        ContextWeightContext context);

    double GetDimensionMultiplier(
        string itemId,
        ContextWeightContext context,
        string dimension);
}
