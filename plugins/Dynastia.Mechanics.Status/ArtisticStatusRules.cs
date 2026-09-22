using System.Globalization;
using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Status;

internal sealed class ArtisticStatusRules
{
    private const string RulesPath = "LocalSociety/artistic_status_rules.json";

    private readonly IReadOnlyDictionary<int, ArtisticStatusDelta> _deltas;

    private ArtisticStatusRules(
        IReadOnlyDictionary<int, ArtisticStatusDelta> deltas,
        double renownCap,
        double reputationCap)
    {
        _deltas = deltas;
        RenownCap = renownCap;
        ReputationCap = reputationCap;
    }

    public double RenownCap { get; }
    public double ReputationCap { get; }

    public static ArtisticStatusRules Load(IGameDataService data)
    {
        using var document = JsonDocument.Parse(data.ReadText(RulesPath));
        var root = document.RootElement;

        if (!root.GetProperty("eventType").GetString()!.Equals(
                "artistic.work_created",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"{RulesPath}: unexpected eventType.");
        }

        var deltas = new Dictionary<int, ArtisticStatusDelta>();
        foreach (var property in root.GetProperty("deltasByMastery").EnumerateObject())
        {
            if (!int.TryParse(property.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var level))
                throw new InvalidDataException($"{RulesPath}: invalid Mastery level '{property.Name}'.");

            var delta = new ArtisticStatusDelta(
                property.Value.GetProperty("renown").GetDouble(),
                property.Value.GetProperty("reputation").GetDouble());
            if (!deltas.TryAdd(level, delta))
                throw new InvalidDataException($"{RulesPath}: duplicate Mastery level {level}.");
        }

        if (!deltas.Keys.OrderBy(value => value).SequenceEqual([1, 2, 3, 4, 5]))
            throw new InvalidDataException($"{RulesPath}: Mastery levels 1-5 are required.");

        var caps = root.GetProperty("perPersonPerCraftCaps");
        var renownCap = caps.GetProperty("renown").GetDouble();
        var reputationCap = caps.GetProperty("reputation").GetDouble();
        if (renownCap <= 0 || reputationCap <= 0)
            throw new InvalidDataException($"{RulesPath}: caps must be positive.");

        return new ArtisticStatusRules(deltas, renownCap, reputationCap);
    }

    public ArtisticStatusDelta Consume(
        StatusComponent component,
        string craftId,
        int masteryLevel)
    {
        if (string.IsNullOrWhiteSpace(craftId)
            || !_deltas.TryGetValue(masteryLevel, out var requested))
        {
            return new ArtisticStatusDelta(0, 0);
        }

        component.ArtisticRenownByCraft ??= new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        component.ArtisticReputationByCraft ??= new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        var currentRenown = component.ArtisticRenownByCraft.GetValueOrDefault(craftId, 0);
        var currentReputation = component.ArtisticReputationByCraft.GetValueOrDefault(craftId, 0);
        var renown = Math.Max(0, Math.Min(requested.Renown, RenownCap - currentRenown));
        var reputation = Math.Max(0, Math.Min(requested.Reputation, ReputationCap - currentReputation));

        component.ArtisticRenownByCraft[craftId] = currentRenown + renown;
        component.ArtisticReputationByCraft[craftId] = currentReputation + reputation;
        return new ArtisticStatusDelta(renown, reputation);
    }
}

internal sealed record ArtisticStatusDelta(double Renown, double Reputation);
