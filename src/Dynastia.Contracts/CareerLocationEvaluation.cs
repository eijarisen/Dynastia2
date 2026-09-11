namespace Dynastia.Contracts;

public sealed record CareerLocationEvaluation(
    bool IsEligible,
    double WeightMultiplier,
    CareerOpportunityStrength Strength);
