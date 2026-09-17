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

    public IContextWeightCatalog LoadGlobalCatalog(
        string relativePath,
        string itemId = "global")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);

        var text = _data.ReadText(relativePath);
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        const string header = "StartYear,EndYear,Dimension,Value,WeightMultiplier";
        if (lines.Length < 2
            || !lines[0].TrimStart('\uFEFF').Equals(header, StringComparison.Ordinal))
        {
            throw CatalogValidation.UnexpectedHeader(
                relativePath,
                lines.Length == 0 ? null : lines[0].TrimStart('\uFEFF'),
                header);
        }

        var normalized = new List<string>
        {
            "ItemId,StartYear,EndYear,Dimension,Value,WeightMultiplier"
        };
        normalized.AddRange(lines.Skip(1).Select(line => $"{itemId},{line}"));

        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { itemId };
        var rows = Parse(relativePath, string.Join(Environment.NewLine, normalized), known);
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
            throw CatalogValidation.UnexpectedHeader(
                path,
                lines.Length == 0 ? null : lines[0].TrimStart('\uFEFF'),
                header);

        var rows = new List<Row>();
        for (var index = 1; index < lines.Length; index++)
        {
            var fields = lines[index].Split(',');
            var rowNumber = index + 1;
            if (fields.Length != 6)
                throw CatalogValidation.FieldCount(path, rowNumber, fields.Length, 6);

            var itemId = fields[0].Trim();
            var dimension = fields[3].Trim();
            var value = fields[4].Trim();

            if (!known.Contains(itemId))
                throw CatalogValidation.Error(
                    path,
                    "an ItemId present in the owning catalog",
                    rowNumber,
                    field: "ItemId",
                    value: itemId);

            var startYear = ParseInt(fields[1], path, rowNumber, "StartYear");
            int? endYear = string.IsNullOrWhiteSpace(fields[2])
                ? null
                : ParseInt(fields[2], path, rowNumber, "EndYear");
            if (startYear < GameCalendarConfiguration.GameStartYear)
                throw CatalogValidation.Error(
                    path,
                    $"a year at or after {GameCalendarConfiguration.GameStartYear}",
                    rowNumber,
                    itemId,
                    "StartYear",
                    startYear);
            if (endYear is int end && end < startYear)
                throw CatalogValidation.Error(
                    path,
                    $"a year at or after StartYear ({startYear})",
                    rowNumber,
                    itemId,
                    "EndYear",
                    end);

            ValidateDimension(path, rowNumber, dimension, value);

            var multiplier = ParseDouble(fields[5], path, rowNumber, "WeightMultiplier");
            if (multiplier <= 0)
                throw CatalogValidation.Error(
                    path,
                    "a number greater than 0",
                    rowNumber,
                    itemId,
                    "WeightMultiplier",
                    multiplier);

            rows.Add(new Row(rowNumber, itemId, startYear, endYear, dimension, value, multiplier));
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
                    throw CatalogValidation.Error(
                        path,
                        $"a year range that does not overlap row {previous.SourceRow}",
                        current.SourceRow,
                        current.ItemId,
                        "StartYear",
                        current.StartYear);
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
                throw CatalogValidation.Error(
                    path,
                    "an empty value when Dimension is All",
                    row,
                    field: "Value",
                    value: value);
            return;
        }

        if (string.IsNullOrWhiteSpace(value))
            throw CatalogValidation.Error(
                path,
                $"a non-empty value for Dimension '{dimension}'",
                row,
                field: "Value",
                value: value);

        if (dimension.Equals("AgeBand", StringComparison.OrdinalIgnoreCase))
        {
            if (!AgeBands.Contains(value))
                throw CatalogValidation.Error(
                    path,
                    $"one of: {string.Join(", ", AgeBands.OrderBy(item => item))} (canonical AgeBand values)",
                    row,
                    field: "Value",
                    value: value);
            return;
        }

        if (dimension.Equals("Sex", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<Sex>(value, ignoreCase: true, out var sex)
                || !Enum.IsDefined(typeof(Sex), sex))
                throw CatalogValidation.Error(
                    path,
                    $"one of: {string.Join(", ", Enum.GetNames<Sex>())} (canonical Sex values)",
                    row,
                    field: "Value",
                    value: value);
            return;
        }

        if (dimension.Equals("Temperament", StringComparison.OrdinalIgnoreCase))
        {
            if (!Temperaments.Contains(value))
                throw CatalogValidation.Error(
                    path,
                    $"one of: {string.Join(", ", Temperaments.OrderBy(item => item))} (canonical Temperament values)",
                    row,
                    field: "Value",
                    value: value);
            return;
        }

        if (dimension.Equals("Morals", StringComparison.OrdinalIgnoreCase))
        {
            if (!Morals.Contains(value))
                throw CatalogValidation.Error(
                    path,
                    $"one of: {string.Join(", ", Morals.OrderBy(item => item))} (canonical Morals values)",
                    row,
                    field: "Value",
                    value: value);
            return;
        }

        if (dimension.Equals("SettlementClass", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<SettlementClass>(value, ignoreCase: true, out var settlementClass)
                || !Enum.IsDefined(typeof(SettlementClass), settlementClass))
                throw CatalogValidation.Error(
                    path,
                    $"one of: {string.Join(", ", Enum.GetNames<SettlementClass>())} (canonical SettlementClass values)",
                    row,
                    field: "Value",
                    value: value);
            return;
        }

        throw CatalogValidation.Error(
            path,
            "one of: All, AgeBand, Sex, Temperament, Morals, SettlementClass",
            row,
            field: "Dimension",
            value: dimension);
    }

    private static int ParseInt(string value, string path, int row, string field)
    {
        return CatalogValidation.ParseInt(path, row, field, value);
    }

    private static double ParseDouble(string value, string path, int row, string field)
    {
        return CatalogValidation.ParseDouble(path, row, field, value);
    }

    private sealed record Row(
        int SourceRow,
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

        public double GetDimensionMultiplier(
            string itemId,
            ContextWeightContext context,
            string dimension)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(dimension);

            if (!_rows.TryGetValue(itemId, out var rows))
                return 1.0;

            var multiplier = 1.0;
            foreach (var row in rows)
            {
                if (!row.Dimension.Equals(dimension, StringComparison.OrdinalIgnoreCase)
                    || !row.Covers(context.Year)
                    || !Matches(row, context))
                {
                    continue;
                }

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
