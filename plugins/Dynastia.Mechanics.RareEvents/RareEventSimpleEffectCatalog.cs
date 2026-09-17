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
        _byEvent = effects
            .GroupBy(effect => effect.EventId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<RareEventSimpleEffect>)group.ToList(),
                StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<RareEventSimpleEffect> Effects { get; }

    public IReadOnlyList<RareEventSimpleEffect> GetEffects(string eventId) =>
        _byEvent.TryGetValue(eventId, out var effects) ? effects : [];

    public static RareEventSimpleEffectCatalog Load(
        IGameDataService data,
        RareEventCatalog events)
    {
        var lines = data.ReadText(DataPath)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        const string header =
            "EventId,EffectType,Target,MinimumValue,MaximumValue,Chance,Parameter";
        if (lines.Length < 2
            || !lines[0].TrimStart('\uFEFF').Equals(header, StringComparison.Ordinal))
        {
            throw CatalogValidation.UnexpectedHeader(
                DataPath,
                lines.Length == 0 ? null : lines[0].TrimStart('\uFEFF'),
                header);
        }

        var known = events.Events
            .Select(item => item.EventId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var list = new List<RareEventSimpleEffect>();
        var validEffects = new HashSet<string>(
            ["WealthGain", "WealthLoss", "HealthDamage", "DeathChance"],
            StringComparer.OrdinalIgnoreCase);
        var validTargets = new HashSet<string>(
            ["Subject", "Household"],
            StringComparer.OrdinalIgnoreCase);

        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            var row = index + 1;
            if (fields.Length != 7)
                throw CatalogValidation.FieldCount(DataPath, row, fields.Length, 7);

            var eventId = fields[0].Trim();
            var effectType = fields[1].Trim();
            var target = fields[2].Trim();

            if (!known.Contains(eventId))
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "an EventId present in rare_events.csv",
                    row,
                    field: "EventId",
                    value: eventId);
            }

            if (!validEffects.Contains(effectType))
            {
                throw CatalogValidation.Error(
                    DataPath,
                    $"one of: {string.Join(", ", validEffects.OrderBy(item => item))}",
                    row,
                    eventId,
                    "EffectType",
                    effectType);
            }

            if (!validTargets.Contains(target))
            {
                throw CatalogValidation.Error(
                    DataPath,
                    $"one of: {string.Join(", ", validTargets.OrderBy(item => item))}",
                    row,
                    eventId,
                    "Target",
                    target);
            }

            var minimum = CatalogValidation.ParseDouble(
                DataPath,
                row,
                "MinimumValue",
                fields[3]);
            var maximum = CatalogValidation.ParseDouble(
                DataPath,
                row,
                "MaximumValue",
                fields[4]);
            var chance = CatalogValidation.ParseDouble(
                DataPath,
                row,
                "Chance",
                fields[5]);

            if (maximum < minimum)
            {
                throw CatalogValidation.Error(
                    DataPath,
                    $"a value at least MinimumValue ({minimum})",
                    row,
                    eventId,
                    "MaximumValue",
                    maximum);
            }

            if (chance < 0 || chance > 1)
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "a number from 0 through 1",
                    row,
                    eventId,
                    "Chance",
                    chance);
            }

            list.Add(new RareEventSimpleEffect(
                eventId,
                effectType,
                target,
                minimum,
                maximum,
                chance,
                string.IsNullOrWhiteSpace(fields[6]) ? null : fields[6].Trim()));
        }

        foreach (var item in events.Events.Where(
            item => item.HandlerId.StartsWith("generic.", StringComparison.OrdinalIgnoreCase)))
        {
            if (!list.Any(effect => effect.EventId.Equals(
                    item.EventId,
                    StringComparison.OrdinalIgnoreCase)))
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "at least one simple effect for every generic rare event",
                    item: item.EventId,
                    field: "EventId",
                    value: item.EventId);
            }
        }

        return new RareEventSimpleEffectCatalog(list);
    }
}
