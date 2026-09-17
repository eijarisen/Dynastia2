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

    public void Validate()
    {
        if (BaseAttemptChance <= 0
            || MinimumAttemptChance < 0
            || MaximumAttemptChance < BaseAttemptChance
            || MinimumAttemptChance > MaximumAttemptChance)
            throw new InvalidDataException("Crime attempt chance configuration is invalid.");
        if (PovertyMultiplier <= 0)
            throw new InvalidDataException("Crime poverty multiplier must be positive.");
        if (StressMultiplierPerPoint < 0 || MaximumStressMultiplier < 1)
            throw new InvalidDataException("Crime stress scaling is invalid.");
        if (MinimumAge < 18)
            throw new InvalidDataException("Crime minimum age may not introduce juvenile justice in this batch.");
    }
}
