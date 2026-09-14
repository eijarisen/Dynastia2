namespace Dynastia.Contracts;

public interface IHistoricalActionVariantService
{
    HistoricalActionVariant? GetVariant(
        string actionId,
        int year);

    HistoricalActionVariant? GetCanonicalVariant(
        string actionId);
}
