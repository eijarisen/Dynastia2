namespace Dynastia.Mechanics.Health;

public sealed class HealthComponent
{
    public double Current { get; set; } = 100;
    public double Maximum { get; set; } = 100;

    public List<HealthConditionState> Conditions { get; } = [];
}
