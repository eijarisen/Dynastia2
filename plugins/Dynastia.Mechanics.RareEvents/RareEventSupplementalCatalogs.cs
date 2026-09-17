using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.RareEvents;

public sealed record RareEventEpidemicCondition(
    string ConditionId, int StartYear, int? EndYear, double BaseWeight, int MinimumAffected, int MaximumAffected)
{
    public bool IsAvailable(int year) => year >= StartYear && (EndYear is null || year <= EndYear.Value);
}

public sealed class RareEventEpidemicCatalog
{
    private const string Path = "RareEvents/rare_event_epidemic_conditions.csv";
    private RareEventEpidemicCatalog(IReadOnlyList<RareEventEpidemicCondition> entries) => Entries = entries;
    public IReadOnlyList<RareEventEpidemicCondition> Entries { get; }

    public static RareEventEpidemicCatalog Load(IGameDataService data)
    {
        var lines = data.ReadText(Path).Split(['\r','\n'], StringSplitOptions.RemoveEmptyEntries);
        const string header = "ConditionId,StartYear,EndYear,BaseWeight,MinimumAffected,MaximumAffected";
        if (lines.Length < 2 || !lines[0].TrimStart('\uFEFF').Equals(header, StringComparison.Ordinal))
            throw new InvalidDataException($"{Path}: unexpected header or empty file.");
        var list = new List<RareEventEpidemicCondition>();
        for (var i = 1; i < lines.Length; i++)
        {
            var f = lines[i].Split(',');
            if (f.Length != 6) throw new InvalidDataException($"{Path} row {i+1}: expected 6 fields.");
            int start = ParseInt(f[1], i), min = ParseInt(f[4], i), max = ParseInt(f[5], i);
            int? end = string.IsNullOrWhiteSpace(f[2]) ? null : ParseInt(f[2], i);
            double weight = ParseDouble(f[3], i);
            if (start < GameCalendarConfiguration.GameStartYear || end is int e && e < start || weight <= 0 || min < 1 || max < min)
                throw new InvalidDataException($"{Path} row {i+1}: invalid values.");
            list.Add(new RareEventEpidemicCondition(f[0].Trim(), start, end, weight, min, max));
        }
        return new RareEventEpidemicCatalog(list);
    }
    private static int ParseInt(string v, int i) => int.TryParse(v.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var p) ? p : throw new InvalidDataException($"{Path} row {i+1}: invalid integer.");
    private static double ParseDouble(string v, int i) => double.TryParse(v.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var p) ? p : throw new InvalidDataException($"{Path} row {i+1}: invalid number.");
}

public sealed class RareEventCareerFamilyWeightCatalog
{
    private const string Path = "RareEvents/rare_event_career_family_weights.csv";
    private readonly Dictionary<(string EventId, string Family), double> _weights;
    private RareEventCareerFamilyWeightCatalog(Dictionary<(string,string), double> weights) => _weights = weights;
    public double GetMultiplier(string eventId, string? careerFamily) =>
        string.IsNullOrWhiteSpace(careerFamily) ? 1.0 : _weights.TryGetValue((eventId.ToUpperInvariant(), careerFamily.ToUpperInvariant()), out var m) ? m : 1.0;

    public static RareEventCareerFamilyWeightCatalog Load(IGameDataService data, RareEventCatalog events, IReadOnlySet<string> knownCareerFamilies)
    {
        var lines = data.ReadText(Path).Split(['\r','\n'], StringSplitOptions.RemoveEmptyEntries);
        const string header = "EventId,CareerFamily,WeightMultiplier";
        if (lines.Length < 2 || !lines[0].TrimStart('\uFEFF').Equals(header, StringComparison.Ordinal)) throw new InvalidDataException($"{Path}: unexpected header.");
        var known = events.Events.Select(e => e.EventId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dict = new Dictionary<(string,string), double>();
        for (var i=1;i<lines.Length;i++)
        {
            var f=lines[i].Split(',');
            if (f.Length!=3 || !known.Contains(f[0].Trim()) || !knownCareerFamilies.Contains(f[1].Trim()) || !double.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var m) || m<=0)
                throw new InvalidDataException($"{Path} row {i+1}: invalid row or unknown CareerFamily.");
            var key=(f[0].Trim().ToUpperInvariant(), f[1].Trim().ToUpperInvariant());
            if (!dict.TryAdd(key,m)) throw new InvalidDataException($"{Path} row {i+1}: duplicate row.");
        }
        return new RareEventCareerFamilyWeightCatalog(dict);
    }
}

public sealed record RareEventVariant(string EventId, int StartYear, int? EndYear, string DisplayName)
{
    public bool Covers(int year) => year >= StartYear && (EndYear is null || year <= EndYear.Value);
}

public sealed class RareEventVariantCatalog
{
    private const string Path = "RareEvents/rare_event_variants.csv";
    private readonly IReadOnlyDictionary<string, IReadOnlyList<RareEventVariant>> _variants;
    private RareEventVariantCatalog(IReadOnlyList<RareEventVariant> variants) =>
        _variants = variants.GroupBy(v => v.EventId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<RareEventVariant>)g.OrderBy(v=>v.StartYear).ToList(), StringComparer.OrdinalIgnoreCase);

    public string ResolveName(RareEventDefinition definition, int year) =>
        _variants.TryGetValue(definition.EventId, out var rows)
            ? rows.FirstOrDefault(row => row.Covers(year))?.DisplayName ?? definition.Name
            : definition.Name;

    public static RareEventVariantCatalog Load(IGameDataService data, RareEventCatalog events)
    {
        var lines=data.ReadText(Path).Split(['\r','\n'], StringSplitOptions.RemoveEmptyEntries);
        const string header="EventId,StartYear,EndYear,DisplayName";
        if(lines.Length<2 || !lines[0].TrimStart('\uFEFF').Equals(header,StringComparison.Ordinal)) throw new InvalidDataException($"{Path}: unexpected header.");
        var known=events.Events.Select(e=>e.EventId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var list=new List<RareEventVariant>();
        for(var i=1;i<lines.Length;i++)
        {
            var f=lines[i].Split(','); if(f.Length!=4 || !known.Contains(f[0].Trim())) throw new InvalidDataException($"{Path} row {i+1}: invalid row.");
            int start=ParseInt(f[1],i); int? end=string.IsNullOrWhiteSpace(f[2])?null:ParseInt(f[2],i);
            if(end is int e && e<start) throw new InvalidDataException($"{Path} row {i+1}: invalid era.");
            list.Add(new RareEventVariant(f[0].Trim(),start,end,f[3].Trim()));
        }
        foreach(var group in list.GroupBy(v=>v.EventId,StringComparer.OrdinalIgnoreCase))
        {
            var ordered=group.OrderBy(v=>v.StartYear).ToList();
            for(var i=1;i<ordered.Count;i++) if(ordered[i-1].EndYear is null || ordered[i-1].EndYear!.Value>=ordered[i].StartYear)
                throw new InvalidDataException($"{Path}: overlapping variants for {group.Key}.");
        }
        return new RareEventVariantCatalog(list);
    }
    private static int ParseInt(string v,int i)=>int.TryParse(v.Trim(),NumberStyles.Integer,CultureInfo.InvariantCulture,out var p)?p:throw new InvalidDataException($"{Path} row {i+1}: invalid year.");
}
