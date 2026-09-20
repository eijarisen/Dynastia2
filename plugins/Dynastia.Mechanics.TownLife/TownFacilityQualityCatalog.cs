using System.Globalization;
using System.Text;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.TownLife;

internal sealed class TownFacilityQualityCatalog
{
    private const string BankPath = "TownLife/bank_offer_quality.csv";
    private const string MedicalPath = "TownLife/medical_quality.csv";

    private TownFacilityQualityCatalog(
        IReadOnlyDictionary<int, BankOfferQualityInfo> bank,
        IReadOnlyDictionary<int, MedicalQualityInfo> medical)
    {
        Bank = bank;
        Medical = medical;
    }

    public IReadOnlyDictionary<int, BankOfferQualityInfo> Bank { get; }
    public IReadOnlyDictionary<int, MedicalQualityInfo> Medical { get; }

    public static TownFacilityQualityCatalog Load(IGameDataService data)
    {
        var bank = ParseCsv(data.ReadText(BankPath), BankPath)
            .Select(row => new BankOfferQualityInfo(
                ParseTier(row, BankPath),
                Required(row, "DisplayName", BankPath),
                ParseDecimal(row, "PrincipalMultiplierMin", BankPath),
                ParseDecimal(row, "PrincipalMultiplierMax", BankPath),
                ParseDecimal(row, "InterestMultiplierMin", BankPath),
                ParseDecimal(row, "InterestMultiplierMax", BankPath),
                ParseDecimal(row, "DurationMultiplierMin", BankPath),
                ParseDecimal(row, "DurationMultiplierMax", BankPath)))
            .ToDictionary(row => row.Tier);

        var medical = ParseCsv(data.ReadText(MedicalPath), MedicalPath)
            .Select(row => new MedicalQualityInfo(
                ParseTier(row, MedicalPath),
                Required(row, "DisplayName", MedicalPath),
                ParseDouble(row, "TreatmentSuccessAdd", MedicalPath, min: 0, max: 0.95),
                ParseDecimal(row, "TreatmentCostMultiplier", MedicalPath)))
            .ToDictionary(row => row.Tier);

        ValidateTiers(bank.Keys, BankPath);
        ValidateTiers(medical.Keys, MedicalPath);

        foreach (var row in bank.Values)
        {
            ValidateRange(row.PrincipalMultiplierMin, row.PrincipalMultiplierMax, BankPath, row.Tier, "principal");
            ValidateRange(row.InterestMultiplierMin, row.InterestMultiplierMax, BankPath, row.Tier, "interest");
            ValidateRange(row.DurationMultiplierMin, row.DurationMultiplierMax, BankPath, row.Tier, "duration");
        }

        foreach (var row in medical.Values)
        {
            if (row.TreatmentCostMultiplier <= 0)
                throw new InvalidDataException($"{MedicalPath}: Tier {row.Tier} cost multiplier must be positive.");
        }

        return new TownFacilityQualityCatalog(bank, medical);
    }

    private static void ValidateTiers(IEnumerable<int> tiers, string path)
    {
        var ordered = tiers.OrderBy(value => value).ToArray();
        if (!ordered.SequenceEqual(new[] { 1, 2, 3, 4, 5 }))
            throw new InvalidDataException($"{path}: expected exactly tiers 1-5.");
    }

    private static void ValidateRange(decimal min, decimal max, string path, int tier, string label)
    {
        if (min <= 0 || max < min)
            throw new InvalidDataException($"{path}: Tier {tier} has invalid {label} multiplier range.");
    }

    private static int ParseTier(IReadOnlyDictionary<string, string> row, string path)
    {
        var text = Required(row, "Tier", path);
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            || value is < 1 or > 5)
        {
            throw new InvalidDataException($"{path}: invalid Tier '{text}'.");
        }

        return value;
    }

    private static decimal ParseDecimal(
        IReadOnlyDictionary<string, string> row,
        string field,
        string path)
    {
        var text = Required(row, field, path);
        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            throw new InvalidDataException($"{path}: invalid {field} '{text}'.");
        return value;
    }

    private static double ParseDouble(
        IReadOnlyDictionary<string, string> row,
        string field,
        string path,
        double min,
        double max)
    {
        var text = Required(row, field, path);
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || value < min
            || value > max)
        {
            throw new InvalidDataException($"{path}: invalid {field} '{text}'.");
        }
        return value;
    }

    private static string Required(
        IReadOnlyDictionary<string, string> row,
        string field,
        string path)
    {
        if (!row.TryGetValue(field, out var value) || string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"{path}: field '{field}' is required.");
        return value.Trim();
    }

    private static IReadOnlyList<Dictionary<string, string>> ParseCsv(string text, string path)
    {
        var lines = text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
        if (lines.Length == 0)
            throw new InvalidDataException($"{path}: CSV is empty.");

        var headers = ParseCsvLine(lines[0].TrimStart('\uFEFF'));
        var result = new List<Dictionary<string, string>>();
        for (var index = 1; index < lines.Length; index++)
        {
            var values = ParseCsvLine(lines[index]);
            if (values.Count != headers.Count)
                throw new InvalidDataException($"{path}: row {index + 1} has {values.Count} fields; expected {headers.Count}.");

            result.Add(headers
                .Select((header, column) => (header, value: values[column]))
                .ToDictionary(item => item.header, item => item.value, StringComparer.OrdinalIgnoreCase));
        }

        return result;
    }

    private static IReadOnlyList<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ',' && !quoted)
            {
                values.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        values.Add(current.ToString().Trim());
        return values;
    }
}
