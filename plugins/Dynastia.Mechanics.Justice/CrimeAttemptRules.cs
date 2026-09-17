using Dynastia.Contracts;

namespace Dynastia.Mechanics.Justice;

public sealed class CrimeAttemptRules
{
    public double BaseAttemptChance { get; init; } = 0.008;
    public double MinimumAttemptChance { get; init; } = 0.0004;
    public double MaximumAttemptChance { get; init; } = 0.04;
    public double PovertyMultiplier { get; init; } = 1.60;
    public double StressMultiplierPerPoint { get; init; } = 0.06;
    public double MaximumStressMultiplier { get; init; } = 1.60;
    public int MinimumAge { get; init; } = 18;

    public void Validate(string path = "Justice/crime_attempt_rules.json")
    {
        if (BaseAttemptChance <= 0)
            throw CatalogValidation.Error(path, "a number greater than 0", item: "root", field: "baseAttemptChance", value: BaseAttemptChance);
        if (MinimumAttemptChance < 0)
            throw CatalogValidation.Error(path, "a number of at least 0", item: "root", field: "minimumAttemptChance", value: MinimumAttemptChance);
        if (MaximumAttemptChance < BaseAttemptChance)
            throw CatalogValidation.Error(path, $"a number of at least baseAttemptChance ({BaseAttemptChance})", item: "root", field: "maximumAttemptChance", value: MaximumAttemptChance);
        if (MinimumAttemptChance > MaximumAttemptChance)
            throw CatalogValidation.Error(path, $"a number no greater than maximumAttemptChance ({MaximumAttemptChance})", item: "root", field: "minimumAttemptChance", value: MinimumAttemptChance);
        if (PovertyMultiplier <= 0)
            throw CatalogValidation.Error(path, "a number greater than 0", item: "root", field: "povertyMultiplier", value: PovertyMultiplier);
        if (StressMultiplierPerPoint < 0)
            throw CatalogValidation.Error(path, "a number of at least 0", item: "root", field: "stressMultiplierPerPoint", value: StressMultiplierPerPoint);
        if (MaximumStressMultiplier < 1)
            throw CatalogValidation.Error(path, "a number of at least 1", item: "root", field: "maximumStressMultiplier", value: MaximumStressMultiplier);
        if (MinimumAge < 18)
            throw CatalogValidation.Error(path, "an age of at least 18", item: "root", field: "minimumAge", value: MinimumAge);
    }
}
