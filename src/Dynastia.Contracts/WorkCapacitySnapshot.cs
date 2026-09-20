namespace Dynastia.Contracts;

public sealed record WorkCapacitySnapshot(
    double OutputMultiplier,
    bool CanWork,
    string? LimitingConditionId = null)
{
    public decimal Apply(decimal output) =>
        output <= 0m || OutputMultiplier <= 0
            ? 0m
            : output * (decimal)Math.Clamp(OutputMultiplier, 0, 1);
}
