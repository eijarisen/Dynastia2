namespace Dynastia.Contracts;

public interface IHistoricalEventService
{
    bool IsEventActive(
        string eventId,
        int year);

    HistoricalResidenceSnapshot? GetExternalResidence(
        IPerson person);

    IReadOnlyCollection<string> GetAffectedPlaceIds(
        string eventId,
        int year) =>
        Array.Empty<string>();

    int? GetEventStartYear(string eventId) =>
        null;

    int? GetEventEndYear(string eventId) =>
        null;
}

public sealed record HistoricalResidenceSnapshot(
    string DestinationLabel,
    int DepartureYear,
    string CauseEventId,
    bool Forced);
