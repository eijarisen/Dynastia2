namespace Dynastia.Contracts;

public interface IHistoricalEventService
{
    bool IsEventActive(
        string eventId,
        int year);

    HistoricalResidenceSnapshot? GetExternalResidence(
        IPerson person);
}

public sealed record HistoricalResidenceSnapshot(
    string DestinationLabel,
    int DepartureYear,
    string CauseEventId,
    bool Forced);
