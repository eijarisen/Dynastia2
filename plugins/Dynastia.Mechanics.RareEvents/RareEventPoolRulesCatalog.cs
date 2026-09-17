using Dynastia.Contracts;

namespace Dynastia.Mechanics.RareEvents;

public sealed class RareEventPoolRulesCatalog
{
    private const string DataPath = "RareEvents/rare_event_pool_rules.csv";
    private readonly IReadOnlyDictionary<string, double> _gates;

    private RareEventPoolRulesCatalog(IReadOnlyDictionary<string, double> gates) =>
        _gates = gates;

    public double GetGateChance(string pool) =>
        _gates.TryGetValue(pool, out var chance)
            ? chance
            : throw CatalogValidation.Error(
                DataPath,
                "a configured rare-event pool",
                item: pool,
                field: "Pool",
                value: pool);

    public static RareEventPoolRulesCatalog Load(IGameDataService data)
    {
        var lines = data.ReadText(DataPath)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        const string header = "Pool,AnnualGateChance,Notes";
        if (lines.Length < 2
            || !lines[0].TrimStart('\uFEFF').Equals(header, StringComparison.Ordinal))
        {
            throw CatalogValidation.UnexpectedHeader(
                DataPath,
                lines.Length == 0 ? null : lines[0].TrimStart('\uFEFF'),
                header);
        }

        var gates = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',', 3);
            var row = index + 1;
            if (fields.Length != 3)
                throw CatalogValidation.FieldCount(DataPath, row, fields.Length, 3);

            var pool = fields[0].Trim();
            if (string.IsNullOrWhiteSpace(pool))
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "a non-empty pool ID",
                    row,
                    field: "Pool",
                    value: pool);
            }

            var chance = CatalogValidation.ParseDouble(
                DataPath,
                row,
                "AnnualGateChance",
                fields[1]);
            if (chance < 0 || chance > 1)
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "a number from 0 through 1",
                    row,
                    pool,
                    "AnnualGateChance",
                    chance);
            }

            if (!gates.TryAdd(pool, chance))
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "a unique pool ID",
                    row,
                    pool,
                    "Pool",
                    pool);
            }
        }

        foreach (var required in new[] { "Household", "Personal", "Special" })
        {
            if (!gates.ContainsKey(required))
            {
                throw CatalogValidation.Error(
                    DataPath,
                    "required pools Household, Personal and Special",
                    item: required,
                    field: "Pool",
                    value: "missing");
            }
        }

        return new RareEventPoolRulesCatalog(gates);
    }
}
