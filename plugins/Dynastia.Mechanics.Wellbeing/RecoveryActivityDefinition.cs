namespace Dynastia.Mechanics.Wellbeing;

internal sealed class RecoveryActivityDefinition
{
    public string Id { get; set; } =
        string.Empty;

    public string Text { get; set; } =
        string.Empty;

    public int StartYear { get; set; }

    public int? EndYear { get; set; }

    public int Weight { get; set; }
}
