using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.Status;

internal sealed class StatusRules
{
    private const string Path = "LocalSociety/status_rules.json";

    public double BaseRenown { get; private init; }
    public double BaseReputation { get; private init; }
    public double RenownMinimum { get; private init; }
    public double RenownMaximum { get; private init; }
    public double ReputationMinimum { get; private init; }
    public double ReputationMaximum { get; private init; }

    public IReadOnlyList<WealthBand> WealthBands { get; private init; } = [];
    public IReadOnlyDictionary<int, StatusDelta> Education { get; private init; } = new Dictionary<int, StatusDelta>();
    public IReadOnlyDictionary<int, StatusDelta> Career { get; private init; } = new Dictionary<int, StatusDelta>();
    public IReadOnlyDictionary<int, StatusDelta> CraftMastery { get; private init; } = new Dictionary<int, StatusDelta>();
    public StatusDelta ActiveFarmWorker { get; private init; } = new(0, 0);
    public StatusDelta CivicHead { get; private init; } = new(0, 0);

    public double InheritedRenownFraction { get; private init; }
    public double InheritedRenownCap { get; private init; }
    public double InheritedReputationFraction { get; private init; }
    public double InheritedReputationMinimum { get; private init; }
    public double InheritedReputationMaximum { get; private init; }

    public double MoveMultiplier { get; private init; }
    public int YearsToFullRecognition { get; private init; }

    public double ApplicationRenownFactor { get; private init; } = 0.00020;
    public double ApplicationReputationFactor { get; private init; } = 0.00005;
    public double ApplicationMinimum { get; private init; } = -0.005;
    public double ApplicationMaximum { get; private init; } = 0.025;
    public double PromotionRenownFactor { get; private init; } = 0.00035;
    public double PromotionReputationFactor { get; private init; } = 0.00010;
    public double PromotionMinimum { get; private init; } = -0.010;
    public double PromotionMaximum { get; private init; } = 0.045;

    public IReadOnlyList<LabelBand> RenownLabels { get; private init; } = [];
    public IReadOnlyList<LabelBand> ReputationLabels { get; private init; } = [];

    public static StatusRules Load(IGameDataService data)
    {
        ArgumentNullException.ThrowIfNull(data);
        using var document = JsonDocument.Parse(data.ReadText(Path));
        var root = document.RootElement;
        var ranges = root.GetProperty("ranges");
        var profile = root.GetProperty("profile");
        var inheritance = root.GetProperty("inheritance");
        var localRenown = root.GetProperty("localRenown");
        var career = root.GetProperty("career");
        var labels = root.GetProperty("labels");
        var applicationFormula = ParseCareerFormula(
            career.GetProperty("applicationBonusFormula").GetString() ?? string.Empty);
        var promotionFormula = ParseCareerFormula(
            career.GetProperty("promotionBonusFormula").GetString() ?? string.Empty);

        var renownRange = ranges.GetProperty("renown");
        var reputationRange = ranges.GetProperty("reputation");

        return new StatusRules
        {
            RenownMinimum = renownRange[0].GetDouble(),
            RenownMaximum = renownRange[1].GetDouble(),
            ReputationMinimum = reputationRange[0].GetDouble(),
            ReputationMaximum = reputationRange[1].GetDouble(),
            BaseRenown = ranges.GetProperty("baseRenown").GetDouble(),
            BaseReputation = ranges.GetProperty("baseReputation").GetDouble(),
            WealthBands = ParseWealthBands(profile.GetProperty("wealthNetWorth").GetProperty("bands")),
            Education = ParseLevelMap(profile.GetProperty("education")),
            Career = ParseLevelMap(profile.GetProperty("careerLevel")),
            CraftMastery = ParseLevelMap(profile.GetProperty("craftMastery")),
            ActiveFarmWorker = ParseDelta(profile.GetProperty("activeFarmWorker")),
            CivicHead = ParseDelta(profile.GetProperty("civicHead")),
            InheritedRenownFraction = inheritance.GetProperty("renownFractionOfHousehold").GetDouble(),
            InheritedRenownCap = inheritance.GetProperty("renownCap").GetDouble(),
            InheritedReputationFraction = inheritance.GetProperty("reputationFractionOfHousehold").GetDouble(),
            InheritedReputationMinimum = inheritance.GetProperty("reputationMin").GetDouble(),
            InheritedReputationMaximum = inheritance.GetProperty("reputationMax").GetDouble(),
            MoveMultiplier = localRenown.GetProperty("onMoveMultiplier").GetDouble(),
            YearsToFullRecognition = localRenown.GetProperty("yearsToFullRecognition").GetInt32(),
            RenownLabels = ParseLabels(labels.GetProperty("renown")),
            ReputationLabels = ParseLabels(labels.GetProperty("reputation")),
            ApplicationRenownFactor = applicationFormula.RenownFactor,
            ApplicationReputationFactor = applicationFormula.ReputationFactor,
            ApplicationMinimum = applicationFormula.Minimum,
            ApplicationMaximum = applicationFormula.Maximum,
            PromotionRenownFactor = promotionFormula.RenownFactor,
            PromotionReputationFactor = promotionFormula.ReputationFactor,
            PromotionMinimum = promotionFormula.Minimum,
            PromotionMaximum = promotionFormula.Maximum
        };
    }

    public StatusDelta WealthDelta(decimal netWorth)
    {
        foreach (var band in WealthBands)
        {
            if (band.Contains(netWorth))
                return band.Delta;
        }

        return new StatusDelta(0, 0);
    }

    public string RenownLabel(double value) => FindLabel(RenownLabels, value);
    public string ReputationLabel(double value) => FindLabel(ReputationLabels, value);

    private static string FindLabel(IReadOnlyList<LabelBand> bands, double value) =>
        bands.FirstOrDefault(band => value >= band.Minimum && value <= band.Maximum)?.Label
        ?? string.Empty;

    private static CareerFormula ParseCareerFormula(string formula)
    {
        var match = Regex.Match(
            formula,
            @"LocalRenown\*(?<renown>-?\d+(?:\.\d+)?)\s*\+\s*Reputation\*(?<reputation>-?\d+(?:\.\d+)?)\s*,\s*(?<min>-?\d+(?:\.\d+)?)\s*,\s*(?<max>-?\d+(?:\.\d+)?)",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (!match.Success)
            throw CatalogValidation.Error(Path, "a supported Status career bonus formula", value: formula);

        return new CareerFormula(
            double.Parse(match.Groups["renown"].Value, CultureInfo.InvariantCulture),
            double.Parse(match.Groups["reputation"].Value, CultureInfo.InvariantCulture),
            double.Parse(match.Groups["min"].Value, CultureInfo.InvariantCulture),
            double.Parse(match.Groups["max"].Value, CultureInfo.InvariantCulture));
    }

    private static IReadOnlyDictionary<int, StatusDelta> ParseLevelMap(JsonElement element)
    {
        var result = new Dictionary<int, StatusDelta>();
        foreach (var property in element.EnumerateObject())
        {
            if (int.TryParse(property.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var level))
                result[level] = ParseDelta(property.Value);
        }
        return result;
    }

    private static IReadOnlyList<WealthBand> ParseWealthBands(JsonElement element)
    {
        var result = new List<WealthBand>();
        foreach (var item in element.EnumerateArray())
        {
            decimal? min = item.TryGetProperty("min", out var minValue) ? minValue.GetDecimal() : null;
            decimal? max = item.TryGetProperty("max", out var maxValue) ? maxValue.GetDecimal() : null;
            result.Add(new WealthBand(min, max, ParseDelta(item)));
        }
        return result;
    }

    private static IReadOnlyList<LabelBand> ParseLabels(JsonElement element) =>
        element.EnumerateArray()
            .Select(item => new LabelBand(
                item.GetProperty("min").GetDouble(),
                item.GetProperty("max").GetDouble(),
                item.GetProperty("label").GetString() ?? string.Empty))
            .ToList();

    private static StatusDelta ParseDelta(JsonElement element) =>
        new(
            element.TryGetProperty("renown", out var renown) ? renown.GetDouble() : 0,
            element.TryGetProperty("reputation", out var reputation) ? reputation.GetDouble() : 0);

    internal sealed record StatusDelta(double Renown, double Reputation);
    internal sealed record WealthBand(decimal? Minimum, decimal? Maximum, StatusDelta Delta)
    {
        public bool Contains(decimal value) =>
            (!Minimum.HasValue || value >= Minimum.Value)
            && (!Maximum.HasValue || value <= Maximum.Value);
    }
    internal sealed record LabelBand(double Minimum, double Maximum, string Label);
    private sealed record CareerFormula(
        double RenownFactor,
        double ReputationFactor,
        double Minimum,
        double Maximum);
}
