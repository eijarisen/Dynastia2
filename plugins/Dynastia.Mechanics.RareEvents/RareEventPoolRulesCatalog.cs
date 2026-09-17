using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.RareEvents;

public sealed class RareEventPoolRulesCatalog
{
    private const string DataPath = "RareEvents/rare_event_pool_rules.csv";
    private readonly IReadOnlyDictionary<string, double> _gates;

    private RareEventPoolRulesCatalog(IReadOnlyDictionary<string, double> gates) => _gates = gates;

    public double GetGateChance(string pool) =>
        _gates.TryGetValue(pool, out var chance)
            ? chance
            : throw new InvalidDataException($"{DataPath}: missing pool '{pool}'.");

    public static RareEventPoolRulesCatalog Load(IGameDataService data)
    {
        var lines = data.ReadText(DataPath).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        const string header = "Pool,AnnualGateChance,Notes";
        if (lines.Length < 2 || !lines[0].TrimStart('\uFEFF').Equals(header, StringComparison.Ordinal))
            throw new InvalidDataException($"{DataPath}: unexpected header or empty file.");

        var gates = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i < lines.Length; i++)
        {
            var fields = lines[i].Split(',', 3);
            if (fields.Length != 3 || !double.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var chance))
                throw new InvalidDataException($"{DataPath} row {i + 1}: invalid row.");
            if (chance < 0 || chance > 1)
                throw new InvalidDataException($"{DataPath} row {i + 1}: gate must be between 0 and 1.");
            if (!gates.TryAdd(fields[0].Trim(), chance))
                throw new InvalidDataException($"{DataPath}: duplicate pool '{fields[0].Trim()}'.");
        }

        foreach (var required in new[] { "Household", "Personal", "Special" })
            if (!gates.ContainsKey(required)) throw new InvalidDataException($"{DataPath}: missing {required} pool.");

        return new RareEventPoolRulesCatalog(gates);
    }
}
