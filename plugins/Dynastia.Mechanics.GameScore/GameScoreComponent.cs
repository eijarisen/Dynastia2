using Dynastia.Contracts;

namespace Dynastia.Mechanics.GameScore;

[PersistedComponentId("game_score.state")]
public sealed class GameScoreComponent
{
    public int SchemaVersion { get; set; } = 1;
    public long TotalScore { get; set; }
    public Dictionary<int, long> YearDeltas { get; set; } = [];
    public Dictionary<int, long> EventDeltas { get; set; } = [];
    public List<GameScoreEntryState> Entries { get; set; } = [];
    public Dictionary<string, GameScoreClaimState> ActiveClaims { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> LockedClaimKeys { get; set; } = [];
    public List<string> AwardedOutcomeKeys { get; set; } = [];
    public int ProcessedEventCount { get; set; }
}

public sealed class GameScoreEntryState
{
    public int Year { get; set; }
    public int SourceEventIndex { get; set; } = -1;
    public long Delta { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string OutcomeKey { get; set; } = string.Empty;
}

public sealed class GameScoreClaimState
{
    public long Points { get; set; }
    public List<string> AwardedTierKeys { get; set; } = [];
}
