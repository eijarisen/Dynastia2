namespace Dynastia.Contracts;

public sealed record GameScoreEntryInfo(
    int Year,
    int SourceEventIndex,
    long Delta,
    string Reason,
    string OutcomeKey);

public interface IGameScoreService
{
    long TotalScore { get; }
    long GetYearDelta(int year);
    long GetScoreAfterYear(int year);
    long GetEventDelta(GameEvent gameEvent);
    IReadOnlyList<GameScoreEntryInfo> GetEntriesForYear(int year);
}
