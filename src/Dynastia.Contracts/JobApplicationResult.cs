namespace Dynastia.Contracts;

public sealed record JobApplicationResult(
    bool IsValid,
    bool WasAccepted,
    bool DeclinedForPay,
    double SuccessChance,
    CareerSnapshot Before,
    CareerSnapshot After);
