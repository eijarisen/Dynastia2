using System.Text.Json;
using Dynastia.Contracts;

namespace Dynastia.Mechanics.TownLife;

internal sealed class TownProsperityRules
{
    private const string Path = "TownLife/prosperity_rules.json";

    private TownProsperityRules(
        int baseIndex,
        int minimumIndex,
        int maximumIndex,
        int initialDeviationMin,
        int initialDeviationMax,
        IReadOnlyList<WeightedDriftStep> weightedSteps,
        int meanReversionTarget,
        int meanReversionMaxAdditionalStep,
        int maximumOrdinaryAbsoluteChangePerYear,
        decimal deviationScalePerPoint,
        decimal hardMultiplierMin,
        decimal hardMultiplierMax,
        IReadOnlyDictionary<LocalEconomicStrength, StrengthSensitivity> sensitivities,
        IReadOnlyList<ProsperityLabel> labels)
    {
        BaseIndex = baseIndex;
        MinimumIndex = minimumIndex;
        MaximumIndex = maximumIndex;
        InitialDeviationMin = initialDeviationMin;
        InitialDeviationMax = initialDeviationMax;
        WeightedSteps = weightedSteps;
        MeanReversionTarget = meanReversionTarget;
        MeanReversionMaxAdditionalStep = meanReversionMaxAdditionalStep;
        MaximumOrdinaryAbsoluteChangePerYear = maximumOrdinaryAbsoluteChangePerYear;
        DeviationScalePerPoint = deviationScalePerPoint;
        HardMultiplierMin = hardMultiplierMin;
        HardMultiplierMax = hardMultiplierMax;
        Sensitivities = sensitivities;
        Labels = labels;
    }

    public int BaseIndex { get; }
    public int MinimumIndex { get; }
    public int MaximumIndex { get; }
    public int InitialDeviationMin { get; }
    public int InitialDeviationMax { get; }
    public IReadOnlyList<WeightedDriftStep> WeightedSteps { get; }
    public int MeanReversionTarget { get; }
    public int MeanReversionMaxAdditionalStep { get; }
    public int MaximumOrdinaryAbsoluteChangePerYear { get; }
    public decimal DeviationScalePerPoint { get; }
    public decimal HardMultiplierMin { get; }
    public decimal HardMultiplierMax { get; }
    public IReadOnlyDictionary<LocalEconomicStrength, StrengthSensitivity> Sensitivities { get; }
    public IReadOnlyList<ProsperityLabel> Labels { get; }

    public static TownProsperityRules Load(IGameDataService data)
    {
        var root = JsonSerializer.Deserialize<ProsperityRoot>(
            data.ReadText(Path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException($"{Path}: invalid JSON root.");

        if (root.MinimumIndex >= root.MaximumIndex)
            throw new InvalidDataException($"{Path}: minimumIndex must be below maximumIndex.");
        if (root.BaseIndex < root.MinimumIndex || root.BaseIndex > root.MaximumIndex)
            throw new InvalidDataException($"{Path}: baseIndex is outside the configured range.");
        if (root.LazyInitialization.InitialDeviationMin > root.LazyInitialization.InitialDeviationMax)
            throw new InvalidDataException($"{Path}: invalid lazy initialization deviation range.");
        if (root.Income.HardMultiplierMin <= 0
            || root.Income.HardMultiplierMax < root.Income.HardMultiplierMin
            || root.Income.DeviationScalePerPoint <= 0)
        {
            throw new InvalidDataException($"{Path}: invalid income multiplier tuning.");
        }

        var weightedSteps = root.AnnualDrift.WeightedSteps
            .Select(step => new WeightedDriftStep(step.Delta, step.Weight))
            .ToArray();
        if (weightedSteps.Length == 0 || weightedSteps.Any(step => step.Weight < 0)
            || weightedSteps.Sum(step => step.Weight) <= 0)
        {
            throw new InvalidDataException($"{Path}: annualDrift.weightedSteps must contain positive total weight.");
        }
        if (root.AnnualDrift.MeanReversion.MaxAdditionalStep < 0
            || root.AnnualDrift.MaximumOrdinaryAbsoluteChangePerYear <= 0)
        {
            throw new InvalidDataException($"{Path}: invalid annual drift limits.");
        }

        var sensitivities = new Dictionary<LocalEconomicStrength, StrengthSensitivity>
        {
            [LocalEconomicStrength.Strong] = Convert(root.Income.StrengthSensitivity.Strong, "strong"),
            [LocalEconomicStrength.Supported] = Convert(root.Income.StrengthSensitivity.Supported, "supported"),
            [LocalEconomicStrength.Normal] = Convert(root.Income.StrengthSensitivity.Normal, "normal"),
            [LocalEconomicStrength.Weak] = Convert(root.Income.StrengthSensitivity.Weak, "weak")
        };

        var labels = root.Labels
            .Select(label => new ProsperityLabel(label.Min, label.Max, label.Label))
            .OrderBy(label => label.Min)
            .ToArray();
        if (labels.Length == 0)
            throw new InvalidDataException($"{Path}: at least one prosperity label is required.");

        return new TownProsperityRules(
            root.BaseIndex,
            root.MinimumIndex,
            root.MaximumIndex,
            root.LazyInitialization.InitialDeviationMin,
            root.LazyInitialization.InitialDeviationMax,
            weightedSteps,
            root.AnnualDrift.MeanReversion.Target,
            root.AnnualDrift.MeanReversion.MaxAdditionalStep,
            root.AnnualDrift.MaximumOrdinaryAbsoluteChangePerYear,
            root.Income.DeviationScalePerPoint,
            root.Income.HardMultiplierMin,
            root.Income.HardMultiplierMax,
            sensitivities,
            labels);
    }

    public string LabelFor(int index) =>
        Labels.FirstOrDefault(label => index >= label.Min && index <= label.Max)?.Label
        ?? "Stable";

    public int DrawOrdinaryDrift(IGameRandom random)
    {
        var total = WeightedSteps.Sum(step => step.Weight);
        var roll = random.NextDouble() * total;
        var cumulative = 0.0;
        foreach (var step in WeightedSteps)
        {
            cumulative += step.Weight;
            if (roll < cumulative)
                return step.Delta;
        }

        return WeightedSteps[^1].Delta;
    }

    public double MeanReversionProbability(int baseIndex) =>
        Math.Min(Math.Abs(baseIndex - MeanReversionTarget) / 20.0, 0.75);

    private static StrengthSensitivity Convert(StrengthSensitivityDto dto, string name)
    {
        if (dto.Downturn <= 0 || dto.Boom <= 0)
            throw new InvalidDataException($"{Path}: {name} prosperity sensitivity must be positive.");
        return new StrengthSensitivity(dto.Downturn, dto.Boom);
    }

    internal sealed record StrengthSensitivity(decimal Downturn, decimal Boom);
    internal sealed record ProsperityLabel(int Min, int Max, string Label);
    internal sealed record WeightedDriftStep(int Delta, double Weight);

    private sealed class ProsperityRoot
    {
        public int BaseIndex { get; set; }
        public int MinimumIndex { get; set; }
        public int MaximumIndex { get; set; }
        public LazyInitializationDto LazyInitialization { get; set; } = new();
        public AnnualDriftDto AnnualDrift { get; set; } = new();
        public IncomeDto Income { get; set; } = new();
        public List<LabelDto> Labels { get; set; } = [];
    }

    private sealed class LazyInitializationDto
    {
        public int InitialDeviationMin { get; set; }
        public int InitialDeviationMax { get; set; }
    }

    private sealed class AnnualDriftDto
    {
        public List<WeightedStepDto> WeightedSteps { get; set; } = [];
        public MeanReversionDto MeanReversion { get; set; } = new();
        public int MaximumOrdinaryAbsoluteChangePerYear { get; set; }
    }

    private sealed class WeightedStepDto
    {
        public int Delta { get; set; }
        public double Weight { get; set; }
    }

    private sealed class MeanReversionDto
    {
        public int Target { get; set; } = 100;
        public int MaxAdditionalStep { get; set; } = 1;
    }

    private sealed class IncomeDto
    {
        public decimal DeviationScalePerPoint { get; set; }
        public decimal HardMultiplierMin { get; set; }
        public decimal HardMultiplierMax { get; set; }
        public SensitivityDto StrengthSensitivity { get; set; } = new();
    }

    private sealed class SensitivityDto
    {
        public StrengthSensitivityDto Strong { get; set; } = new();
        public StrengthSensitivityDto Supported { get; set; } = new();
        public StrengthSensitivityDto Normal { get; set; } = new();
        public StrengthSensitivityDto Weak { get; set; } = new();
    }

    private sealed class StrengthSensitivityDto
    {
        public decimal Downturn { get; set; }
        public decimal Boom { get; set; }
    }

    private sealed class LabelDto
    {
        public int Min { get; set; }
        public int Max { get; set; }
        public string Label { get; set; } = string.Empty;
    }
}
