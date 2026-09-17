namespace Dynastia.Mechanics.Justice;

public sealed class CrimeDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public double Weight { get; init; }
    public int SentenceMin { get; init; }
    public int SentenceMax { get; init; }
    public int ProfitMin { get; init; }
    public int ProfitMax { get; init; }
    public double SuccessBase { get; init; }
    public double DetectionBase { get; init; }
    public bool RequiresEmployment { get; init; }
    public string Description { get; init; } = string.Empty;

    public int StartYear { get; init; } = 1700;
    public int? EndYear { get; init; }
    public int MinimumAge { get; init; } = 18;
    public int? MaximumAge { get; init; }
    public string PrimaryStat { get; init; } = "intellect";
    public string? SecondaryStat { get; init; }
    public IReadOnlyList<string> BehaviorTags { get; init; } = [];
    public double PovertyMultiplier { get; init; } = 1.0;
    public double StressWeightPerPoint { get; init; }
    public string SettlementPreference { get; init; } = "Universal";
    public IReadOnlyList<string> PreferredOpportunityTags { get; init; } = [];
    public IReadOnlyList<string> PreferredCareerFamilies { get; init; } = [];

    public bool IsProfitCrime => ProfitMax > 0 || HasBehavior("profit");
    public bool IsPlanned => HasBehavior("planned");
    public bool IsSevere => HasBehavior("violent") || HasBehavior("severe") || HasBehavior("extreme");

    public bool HasBehavior(string tag) =>
        BehaviorTags.Contains(tag, StringComparer.OrdinalIgnoreCase);

    public bool IsAvailable(int year, int age) =>
        year >= StartYear
        && (EndYear is null || year <= EndYear.Value)
        && age >= MinimumAge
        && (MaximumAge is null || age <= MaximumAge.Value);
}
