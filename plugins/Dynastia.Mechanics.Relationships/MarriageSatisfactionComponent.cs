namespace Dynastia.Mechanics.Relationships;

public sealed class MarriageSatisfactionComponent
{
    public Guid SpouseId { get; set; }

    public double Satisfaction { get; set; } =
        90;

    public int StartYear { get; set; }

    public List<string> CurrentIssues { get; } = [];
}
