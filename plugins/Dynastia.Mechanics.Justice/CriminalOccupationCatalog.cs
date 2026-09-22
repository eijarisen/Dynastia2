using System.Globalization;
using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Justice;

public sealed record CriminalMasteryRule(
    int Level,
    string DisplayName,
    int RequiredActiveHeistYears,
    double BaseDetectionChance);

public sealed record CriminalArchetypeRule(
    string ArchetypeId,
    string DisplayName,
    IReadOnlyList<string> DominantStats,
    decimal IncomeMultiplier);

public sealed class CriminalOccupationCatalog
{
    private const string RulesPath = "LocalSociety/criminal_occupation_rules.json";
    private const string MasteryPath = "LocalSociety/criminal_mastery_levels.csv";
    private const string ArchetypesPath = "LocalSociety/criminal_archetypes.csv";

    private readonly IReadOnlyList<CriminalMasteryRule> _mastery;
    private readonly IReadOnlyDictionary<string, CriminalArchetypeRule> _archetypes;

    private CriminalOccupationCatalog(
        CriminalOccupationRules rules,
        IReadOnlyList<CriminalMasteryRule> mastery,
        IReadOnlyDictionary<string, CriminalArchetypeRule> archetypes)
    {
        Rules = rules;
        _mastery = mastery;
        _archetypes = archetypes;
    }

    public CriminalOccupationRules Rules { get; }

    public static CriminalOccupationCatalog Load(IGameDataService data)
    {
        var rules = JsonSerializer.Deserialize<CriminalOccupationRules>(
            data.ReadText(RulesPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException($"{RulesPath}: invalid JSON.");

        var mastery = ParseMastery(data.ReadText(MasteryPath));
        var archetypes = ParseArchetypes(data.ReadText(ArchetypesPath));
        Validate(rules, mastery, archetypes);
        return new CriminalOccupationCatalog(rules, mastery, archetypes);
    }

    public CriminalMasteryRule ResolveMastery(int activeHeistYears) =>
        _mastery
            .Where(rule => activeHeistYears >= rule.RequiredActiveHeistYears)
            .OrderByDescending(rule => rule.RequiredActiveHeistYears)
            .First();

    public CriminalArchetypeRule ResolveArchetype(
        int appeal,
        int strength,
        int intellect)
    {
        if (appeal == 5 && strength == 5 && intellect == 5)
            return _archetypes["mastermind"];

        var max = Math.Max(appeal, Math.Max(strength, intellect));
        var dominant = new List<string>();
        if (appeal == max) dominant.Add("appeal");
        if (strength == max) dominant.Add("strength");
        if (intellect == max) dominant.Add("intellect");

        var id = dominant.Count switch
        {
            1 => dominant[0],
            2 when dominant.Contains("appeal") && dominant.Contains("strength") => "appeal_strength",
            2 when dominant.Contains("appeal") && dominant.Contains("intellect") => "appeal_intellect",
            2 => "strength_intellect",
            _ => "balanced"
        };

        return _archetypes[id];
    }

    private static IReadOnlyList<CriminalMasteryRule> ParseMastery(string text)
    {
        var lines = Lines(text);
        const string header = "Level,DisplayName,RequiredActiveHeistYears,BaseDetectionChance";
        if (lines.Count < 2 || !lines[0].Equals(header, StringComparison.Ordinal))
            throw new InvalidDataException($"{MasteryPath}: unexpected header.");

        return lines.Skip(1).Select((line, index) =>
        {
            var f = line.Split(',');
            if (f.Length != 4)
                throw new InvalidDataException($"{MasteryPath}: row {index + 2} must contain 4 fields.");
            return new CriminalMasteryRule(
                int.Parse(f[0], CultureInfo.InvariantCulture),
                f[1].Trim(),
                int.Parse(f[2], CultureInfo.InvariantCulture),
                double.Parse(f[3], CultureInfo.InvariantCulture));
        }).ToList();
    }

    private static IReadOnlyDictionary<string, CriminalArchetypeRule> ParseArchetypes(string text)
    {
        var lines = Lines(text);
        const string header = "ArchetypeId,DisplayName,DominantStats,IncomeMultiplier";
        if (lines.Count < 2 || !lines[0].Equals(header, StringComparison.Ordinal))
            throw new InvalidDataException($"{ArchetypesPath}: unexpected header.");

        return lines.Skip(1).Select((line, index) =>
        {
            var f = line.Split(',');
            if (f.Length != 4)
                throw new InvalidDataException($"{ArchetypesPath}: row {index + 2} must contain 4 fields.");
            return new CriminalArchetypeRule(
                f[0].Trim(),
                f[1].Trim(),
                f[2].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                decimal.Parse(f[3], CultureInfo.InvariantCulture));
        }).ToDictionary(rule => rule.ArchetypeId, StringComparer.OrdinalIgnoreCase);
    }

    private static List<string> Lines(string text) =>
        text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimStart('\uFEFF').Trim())
            .Where(line => line.Length > 0)
            .ToList();

    private static void Validate(
        CriminalOccupationRules rules,
        IReadOnlyList<CriminalMasteryRule> mastery,
        IReadOnlyDictionary<string, CriminalArchetypeRule> archetypes)
    {
        if (!rules.StartActionId.Equals("justice.commit_crime", StringComparison.OrdinalIgnoreCase)
            || !rules.StopActionId.Equals("justice.leave_life_of_crime", StringComparison.OrdinalIgnoreCase)
            || rules.MinimumAge != 18
            || !rules.RequiredMorals.Equals("Evil", StringComparison.OrdinalIgnoreCase)
            || rules.Heist.BaseIncome != 1000m)
        {
            throw new InvalidDataException($"{RulesPath}: unsupported criminal-occupation core rules.");
        }

        var expectedThresholds = new[] { 0, 2, 5, 9, 15 };
        var ordered = mastery.OrderBy(rule => rule.Level).ToArray();
        var detectionDecreases = ordered.Length == 5;
        for (var index = 1; index < ordered.Length; index++)
            detectionDecreases &= ordered[index - 1].BaseDetectionChance > ordered[index].BaseDetectionChance;

        if (ordered.Length != 5
            || !ordered.Select(rule => rule.RequiredActiveHeistYears).SequenceEqual(expectedThresholds)
            || !detectionDecreases)
        {
            throw new InvalidDataException($"{MasteryPath}: expected five decreasing-risk mastery levels at 0/2/5/9/15 years.");
        }

        foreach (var id in new[]
        {
            "appeal", "strength", "intellect", "appeal_strength", "appeal_intellect",
            "strength_intellect", "balanced", "mastermind"
        })
        {
            if (!archetypes.ContainsKey(id))
                throw new InvalidDataException($"{ArchetypesPath}: missing archetype '{id}'.");
        }
    }
}

public sealed class CriminalOccupationRules
{
    public string StartActionId { get; init; } = "justice.commit_crime";
    public string StopActionId { get; init; } = "justice.leave_life_of_crime";
    public string OccupationTag { get; init; } = "occupation.criminal";
    public int MinimumAge { get; init; } = 18;
    public string RequiredMorals { get; init; } = "Evil";
    public CriminalHeistRules Heist { get; init; } = new();
}

public sealed class CriminalHeistRules
{
    public decimal BaseIncome { get; init; } = 1000m;
    public int IncomeRollMinimum { get; init; } = 0;
    public int IncomeRollMaximumInclusive { get; init; } = 94;
    public decimal MastermindSentenceMultiplier { get; init; } = 0.8m;
}
