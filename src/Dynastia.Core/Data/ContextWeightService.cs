using System.Globalization;
using Dynastia.Contracts;

namespace Dynastia.Core.Data;

public sealed class ContextWeightService : IContextWeightService
{
    private static readonly HashSet<string> AgeBands =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Infant", "Child", "Adolescent", "YoungAdult",
            "Adult", "OlderAdult", "Elderly"
        };

    private static readonly HashSet<string> Temperaments =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Melancholic", "Phlegmatic", "Sanguine", "Choleric"
        };

    private static readonly HashSet<string> Morals =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Good", "Neutral", "Evil"
        };

    private readonly IGameDataService _data;

    public ContextWeightService(IGameDataService data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public IContextWeightCatalog LoadCatalog(
        string relativePath,
        IEnumerable<string> knownItemIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(knownItemIds);

        var known = knownItemIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rows = Parse(relativePath, _data.ReadText(relativePath), known);
        return new Catalog(rows, this);
    }

    public string GetAgeBand(int age) => age switch
    {
        < 0 => throw new ArgumentOutOfRangeException(nameof(age)),
        <= 4 => "Infant",
        <= 12 => "Child",
        <= 17 => "Adolescent",
        <= 34 => "YoungAdult",
        <= 54 => "Adult",
        <= 69 => "OlderAdult",
        _ => "Elderly"
    };

    private static IReadOnlyList<Row> Parse(
        string path,
        string text,
        IReadOnlySet<string> known)
    {
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        const string header = "ItemId,StartYear,EndYear,Dimension,Value,WeightMultiplier";
        if (lines.Length < 2 || !lines[0].TrimStart('\uFEFF').Equals(header, StringComparison.Ordinal))
            throw new InvalidDataException($"{path}: unexpected header or empty file.");

        var rows = new List<Row>();
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            var rowNumber = index + 1;
            if (fields.Length != 6)
                throw new InvalidDataException($"{path} row {rowNumber}: expected 6 fields.");

            var itemId = fields[0].Trim();
            var dimension = fields[3].Trim();
            var value = fields[4].Trim();

            if (!known.Contains(itemId))
                throw new InvalidDataException($"{path} row {rowNumber} ItemId: unknown item '{itemId}'.");

            var startYear = ParseInt(fields[1], path, rowNumber, "StartYear");
            var endYear = string.IsNullOrWhiteSpace(fields[2])
                ? null
                : ParseInt(fields[2], path, rowNumber, "EndYear");
            if (startYear < GameCalendarConfiguration.GameStartYear)
                throw new InvalidDataException($"{path} row {rowNumber} StartYear: may not be before {GameCalendarConfiguration.GameStartYear}.");
            if (endYear is int end && end < startYear)
                throw new InvalidDataException($"{path} row {rowNumber} EndYear: may not precede StartYear.");

            ValidateDimension(path, rowNumber, dimension, value);

            var multiplier = ParseDouble(fields[5], path, rowNumber, "WeightMultiplier");
            if (multiplier <= 0)
                throw new InvalidDataException($"{path} row {rowNumber} WeightMultiplier: must be greater than 0.");

            rows.Add(new Row(itemId, startYear, endYear, dimension, value, multiplier));
        }

        foreach (var group in rows.GroupBy(
                     row => (row.ItemId.ToUpperInvariant(), row.Dimension.ToUpperInvariant(), row.Value.ToUpperInvariant())))
        {
            var ordered = group.OrderBy(row => row.StartYear).ToList();
            for (var index = 1; index < ordered.Count; index++)
            {
                var previous = ordered[index - 1];
                var current = ordered[index];
                if (previous.EndYear is null || previous.EndYear.Value >= current.StartYear)
                {
                    throw new InvalidDataException(
                        $"{path}: overlapping rows for item '{current.ItemId}', dimension '{current.Dimension}', value '{current.Value}'.");
                }
            }
        }

        return rows;
    }

    private static void ValidateDimension(
        string path,
        int row,
        string dimension,
        string value)
    {
        if (dimension.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrEmpty(value))
                throw new InvalidDataException($"{path} row {row} Value: All requires an empty value.");
            return;
        }

        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"{path} row {row} Value: '{dimension}' requires a value.");

        if (dimension.Equals("AgeBand", StringComparison.OrdinalIgnoreCase))
        {
            if (!AgeBands.Contains(value))
                throw new InvalidDataException($"{path} row {row} Value: invalid AgeBand '{value}'.");
            return;
        }

        if (dimension.Equals("Sex", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<Sex>(value, ignoreCase: true, out _))
                throw new InvalidDataException($"{path} row {row} Value: invalid Sex '{value}'.");
            return;
        }

        if (dimension.Equals("Temperament", StringComparison.OrdinalIgnoreCase))
        {
            if (!Temperaments.Contains(value))
                throw new InvalidDataException($"{path} row {row} Value: invalid Temperament '{value}'.");
            return;
        }

        if (dimension.Equals("Morals", StringComparison.OrdinalIgnoreCase))
        {
            if (!Morals.Contains(value))
                throw new InvalidDataException($"{path} row {row} Value: invalid Morals '{value}'.");
            return;
        }

        if (dimension.Equals("SettlementClass", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<SettlementClass>(value, ignoreCase: true, out _))
                throw new InvalidDataException($"{path} row {row} Value: invalid SettlementClass '{value}'.");
            return;
        }

        throw new InvalidDataException($"{path} row {row} Dimension: unsupported dimension '{dimension}'.");
    }

    private static int ParseInt(string value, string path, int row, string field)
    {
        if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            throw new InvalidDataException($"{path} row {row} {field}: invalid integer '{value}'.");
        return parsed;
    }

    private static double ParseDouble(string value, string path, int row, string field)
    {
        if (!double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            throw new InvalidDataException($"{path} row {row} {field}: invalid number '{value}'.");
        return parsed;
    }

    private sealed record Row(
        string ItemId,
        int StartYear,
        int? EndYear,
        string Dimension,
        string Value,
        double WeightMultiplier)
    {
        public bool Covers(int year) =>
            year >= StartYear && (EndYear is null || year <= EndYear.Value);
    }

    private sealed class Catalog : IContextWeightCatalog
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<Row>> _rows;
        private readonly ContextWeightService _owner;

        public Catalog(IReadOnlyList<Row> rows, ContextWeightService owner)
        {
            _owner = owner;
            _rows = rows
                .GroupBy(row => row.ItemId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<Row>)group.ToList(),
                    StringComparer.OrdinalIgnoreCase);
        }

        public double GetMultiplier(string itemId, ContextWeightContext context)
        {
            if (!_rows.TryGetValue(itemId, out var rows))
                return 1.0;

            var multiplier = 1.0;
            foreach (var row in rows)
            {
                if (!row.Covers(context.Year) || !Matches(row, context))
                    continue;
                multiplier *= row.WeightMultiplier;
            }
            return multiplier;
        }

        private bool Matches(Row row, ContextWeightContext context)
        {
            if (row.Dimension.Equals("All", StringComparison.OrdinalIgnoreCase))
                return true;
            if (row.Dimension.Equals("AgeBand", StringComparison.OrdinalIgnoreCase))
                return row.Value.Equals(_owner.GetAgeBand(context.Age), StringComparison.OrdinalIgnoreCase);
            if (row.Dimension.Equals("Sex", StringComparison.OrdinalIgnoreCase))
                return context.Sex is Sex sex && sex.ToString().Equals(row.Value, StringComparison.OrdinalIgnoreCase);
            if (row.Dimension.Equals("Temperament", StringComparison.OrdinalIgnoreCase))
                return context.Temperament?.Equals(row.Value, StringComparison.OrdinalIgnoreCase) == true;
            if (row.Dimension.Equals("Morals", StringComparison.OrdinalIgnoreCase))
                return context.Morals?.Equals(row.Value, StringComparison.OrdinalIgnoreCase) == true;
            if (row.Dimension.Equals("SettlementClass", StringComparison.OrdinalIgnoreCase))
                return context.SettlementClass is SettlementClass settlementClass && settlementClass.ToString().Equals(row.Value, StringComparison.OrdinalIgnoreCase);
            return false;
        }
    }
}
