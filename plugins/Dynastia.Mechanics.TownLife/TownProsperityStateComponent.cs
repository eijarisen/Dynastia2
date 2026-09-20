using Dynastia.Contracts;

namespace Dynastia.Mechanics.TownLife;

[PersistedComponentId("townlife.prosperity_state")]
public sealed class TownProsperityStateComponent
{
    public List<TownProsperityTownState> Towns { get; set; } = [];
}

public sealed class TownProsperityTownState
{
    public string PlaceId { get; set; } = string.Empty;
    public int BaseIndex { get; set; } = 100;
    public int FirstRelevantYear { get; set; }
    public int LastAdvancedYear { get; set; }
    public int LastTrend { get; set; }
    public List<TownProsperityShockState> Shocks { get; set; } = [];
    public List<TownProsperityHistoryPointState> History { get; set; } = [];
}

public sealed class TownProsperityHistoryPointState
{
    public int Year { get; set; }
    public int Index { get; set; }
}

public sealed class TownProsperityShockState
{
    public string SourceId { get; set; } = string.Empty;
    public int Amount { get; set; }
    public int RecoveryYears { get; set; }
    public int AppliedYear { get; set; }
}
