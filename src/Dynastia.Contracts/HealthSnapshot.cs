namespace Dynastia.Contracts;

public sealed record HealthSnapshot(
    double Current,
    double Maximum,
    IReadOnlyList<HealthConditionInfo> Conditions)
{
    public double Percentage =>
        Maximum <= 0
            ? 0
            : Math.Clamp((Current / Maximum) * 100.0, 0, 100);
}
