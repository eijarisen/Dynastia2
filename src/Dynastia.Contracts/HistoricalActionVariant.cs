namespace Dynastia.Contracts;

public sealed record HistoricalActionVariant(
    string ActionId,
    int StartYear,
    int? EndYear,
    string Label,
    string Description,
    string Narrative)
{
    public bool IsAvailable(int year) =>
        year >= StartYear
        && (EndYear is null || year <= EndYear.Value);
}
