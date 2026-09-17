using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.RareEvents;

public sealed record RareEventSimpleEffect(
    string EventId,
    string EffectType,
    string Target,
    double MinimumValue,
    double MaximumValue,
    double Chance,
    string? Parameter);

public sealed class RareEventSimpleEffectCatalog
{
    private const string DataPath = "RareEvents/rare_event_simple_effects.csv";
    private readonly IReadOnlyDictionary<string, IReadOnlyList<RareEventSimpleEffect>> _byEvent;

    private RareEventSimpleEffectCatalog(IReadOnlyList<RareEventSimpleEffect> effects)
    {
        Effects = effects;
        _byEvent = effects.GroupBy(effect => effect.EventId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<RareEventSimpleEffect>)group.ToList(), StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<RareEventSimpleEffect> Effects { get; }
    public IReadOnlyList<RareEventSimpleEffect> GetEffects(string eventId) =>
        _byEvent.TryGetValue(eventId, out var effects) ? effects : [];

    public static RareEventSimpleEffectCatalog Load(IGameDataService data, RareEventCatalog events)
    {
        var lines = data.ReadText(DataPath).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        const string header = "EventId,EffectType,Target,MinimumValue,MaximumValue,Chance,Parameter";
        if (lines.Length < 2 || !lines[0].TrimStart('\uFEFF').Equals(header, StringComparison.Ordinal))
            throw new InvalidDataException($"{DataPath}: unexpected header or empty file.");

        var known = events.Events.Select(item => item.EventId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var list = new List<RareEventSimpleEffect>();
        var validEffects = new HashSet<string>(["WealthGain", "WealthLoss", "HealthDamage", "DeathChance"], StringComparer.OrdinalIgnoreCase);
        var validTargets = new HashSet<string>(["Subject", "Household"], StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i < lines.Length; i++)
        {
            var fields = lines[i].Split(',');
            if (fields.Length != 7) throw new InvalidDataException($"{DataPath} row {i + 1}: expected 7 fields.");
            if (!known.Contains(fields[0].Trim())) throw new InvalidDataException($"{DataPath} row {i + 1}: unknown event '{fields[0]}'.");
            if (!validEffects.Contains(fields[1].Trim())) throw new InvalidDataException($"{DataPath} row {i + 1}: unknown effect '{fields[1]}'.");
            if (!validTargets.Contains(fields[2].Trim())) throw new InvalidDataException($"{DataPath} row {i + 1}: unknown target '{fields[2]}'.");
            var min = Parse(fields[3], i, "MinimumValue");
            var max = Parse(fields[4], i, "MaximumValue");
            var chance = Parse(fields[5], i, "Chance");
            if (max < min || chance < 0 || chance > 1) throw new InvalidDataException($"{DataPath} row {i + 1}: invalid bounds/chance.");
            list.Add(new RareEventSimpleEffect(fields[0].Trim(), fields[1].Trim(), fields[2].Trim(), min, max, chance,
                string.IsNullOrWhiteSpace(fields[6]) ? null : fields[6].Trim()));
        }

        foreach (var item in events.Events.Where(item => item.HandlerId.StartsWith("generic.", StringComparison.OrdinalIgnoreCase)))
            if (!list.Any(effect => effect.EventId.Equals(item.EventId, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException($"{item.EventId}: generic handler has no simple effects.");

        return new RareEventSimpleEffectCatalog(list);
    }

    private static double Parse(string value, int row, string field) =>
        double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed : throw new InvalidDataException($"{DataPath} row {row + 1} {field}: invalid value '{value}'.");
}
